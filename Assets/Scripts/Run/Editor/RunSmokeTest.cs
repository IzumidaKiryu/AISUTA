using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Yakiniku.Run.EditorTools
{
    /// <summary>
    /// Game_Run シーンを実際にPlayして、README の動作確認チェックリストを機械的に確認する。
    ///
    /// 「参照が全部埋まっている」ことと「遊べる」ことは別なので、
    /// 前進・レーン移動・ジャンプ・アイテム取得・被弾・床のループを
    /// 実際に走らせて確認する。譜面(StageData_Normal)の内容に合わせた期待値を持っている。
    ///
    /// 実行方法:
    ///   Unityのメニュー  Yakiniku > Run > Game_Run を自動プレイして確認
    ///   コマンドライン    Unity.exe -batchmode -projectPath <パス>
    ///                     -executeMethod Yakiniku.Run.EditorTools.RunSmokeTest.Run
    ///                     （-quit は付けない。確認が終わったら自分で終了する）
    /// </summary>
    public static class RunSmokeTest
    {
        private const string ScenePath = "Assets/Scenes/Game_Run.unity";
        private const string SessionKey = "Yakiniku.Run.SmokeTest";

        [MenuItem("Yakiniku/Run/Game_Run を自動プレイして確認", false, 20)]
        public static void Run()
        {
            SessionState.SetBool(SessionKey, true);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        /// <summary>
        /// Playモードに入るとドメインリロードが走って静的な購読が消えるので、
        /// SessionState に残したフラグを見て読み込み直後に貼り直す。
        /// </summary>
        [InitializeOnLoadMethod]
        private static void Hook()
        {
            if (!SessionState.GetBool(SessionKey, false)) return;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode) return;

            var go = new GameObject("~RunSmokeDriver");
            go.AddComponent<Driver>();
        }

        /// <summary>
        /// Playモード中に走って判定するだけの入れ物。
        /// 入力はキーボードを叩けないので PlayerRunner の公開APIを直接呼ぶ。
        /// </summary>
        private sealed class Driver : MonoBehaviour
        {
            // 譜面(StageData_Normal.json)のうち、レーン0をまっすぐ走ると関係するもの
            private const float ItemZ = 20f;          // lane 0 のアイテム
            private const float ObstacleZ = 60f;      // lane 0 の障害物
            private const float JumpWallZ = 100f;     // 全レーンを塞ぐ低い壁
            private const float SecondWallZ = 170f;   // 同じく低い壁（最後のノーツ）

            private const float JumpLeadDistance = 3.5f; // 壁の何m手前で飛ぶか
            private const float WallClearedDistance = 6f; // 壁の何m先で「越えた」と判定するか
            private const float TimeoutSeconds = 60f;

            private readonly List<string> results = new List<string>();
            private readonly List<string> failures = new List<string>();

            private PlayerRunner player;
            private BeatGaugeController gauge;
            private GroundTiler ground;
            private RunGoalWatcher goal;
            private RunScore score;
            private RunRival rival;

            // スコア倍率とライバルの推移
            private float startMultiplier = -1f;
            private float maxMultiplier;
            private int maxDefeats;
            private int minHearts = int.MaxValue;
            private int scoreAtFirstItem = -1;

            // ペンライトが床の両サイドに居るかの確認用
            private int penlightLeft = -1;
            private int penlightRight = -1;
            private float maxZ;

            private float startZ;
            private int startHealth;

            private int itemsCollected;
            private float gaugeAfterFirstItem = -1f;

            // 壁ごとに「手前のHP」と「越えた直後のHP」を記録して比較する。
            // 壁と壁の間にも障害物(Z=150)があるので、最後のHPとは比べられない。
            private readonly int[] healthBeforeWall = { -1, -1 };
            private readonly int[] healthAfterWall = { -1, -1 };
            private readonly bool[] jumped = { false, false };

            // 見た目が倒れていないかの確認用（Z-upモデルの補正回転が消えると倒れる）
            private Vector3 obstacleSize = Vector3.zero;

            private bool laneTestStarted;
            private bool laneTestDone;
            private bool laneReached;
            private bool laneReturned;
            private float laneTestStartedZ;
            private float elapsed;
            private bool finished;

            private void Start()
            {
                player = FindFirstObjectByType<PlayerRunner>();
                gauge = FindFirstObjectByType<BeatGaugeController>();
                ground = FindFirstObjectByType<GroundTiler>();
                goal = FindFirstObjectByType<RunGoalWatcher>();
                score = FindFirstObjectByType<RunScore>();
                rival = FindFirstObjectByType<RunRival>();

                if (player == null)
                {
                    Fail("PlayerRunner がシーンに無い");
                    Finish();
                    return;
                }

                startZ = player.DistanceZ;
                startHealth = player.Health;

                player.OnItemCollected += _ => itemsCollected++;

                // バッチモードでも音は再生される。自動テストは裏で何度も走らせるので、
                // 作業中に突然BGMが鳴り出すのを防ぐため消音する
                AudioListener.volume = 0f;

                // 17秒ぶんの譜面を待つのは長いので早送りする
                Time.timeScale = 4f;
            }

            private void Update()
            {
                if (finished) return;

                elapsed += Time.unscaledDeltaTime;
                if (elapsed > TimeoutSeconds)
                {
                    Fail($"{TimeoutSeconds}秒たってもゴールしない（Z={player.DistanceZ:F1}）");
                    Finish();
                    return;
                }

                float z = player.DistanceZ;

                // --- レーン移動: 最初のノーツ(Z=20)より手前で左端まで寄せて、中央に戻す ---
                // 譜面のノーツはレーン0に置かれているので、確認が済んだら必ず0へ戻す
                if (!laneTestStarted && z > 3f)
                {
                    laneTestStarted = true;
                    laneTestStartedZ = z;
                    player.TryChangeLane(-10); // 端で止まるはず
                }
                else if (laneTestStarted && !laneTestDone)
                {
                    float edgeX = player.Settings.LaneToX(player.Settings.MinLane);

                    if (player.CurrentLane != player.Settings.MinLane)
                    {
                        Fail($"左端まで寄せたのにレーンが {player.CurrentLane}（期待 {player.Settings.MinLane}）");
                        laneTestDone = true;
                    }
                    else if (Mathf.Abs(player.transform.position.x - edgeX) < 0.05f)
                    {
                        laneReached = true;
                        laneTestDone = true;
                        player.TryChangeLane(-player.Settings.MinLane); // -2 → 0 に戻す
                    }
                    else if (z > laneTestStartedZ + 12f)
                    {
                        Fail($"左端に寄るのが遅すぎる（x={player.transform.position.x:F2} 期待{edgeX:F2}）");
                        laneTestDone = true;
                    }
                }
                else if (laneTestDone && !laneReturned && player.CurrentLane == 0
                         && Mathf.Abs(player.transform.position.x) < 0.05f)
                {
                    laneReturned = true;
                }

                // --- アイテム取得直後のゲージを記録 ---
                if (gaugeAfterFirstItem < 0f && itemsCollected > 0 && gauge != null)
                {
                    gaugeAfterFirstItem = gauge.Value;
                }

                // --- 障害物の見た目が立っているかを1回だけ測る ---
                if (obstacleSize == Vector3.zero) MeasureObstacle();

                // --- ペンライトが床の両サイドに居るかを1回だけ測る ---
                if (penlightLeft < 0 && z > 20f) MeasurePenlights();

                maxZ = Mathf.Max(maxZ, z);

                // --- スコア倍率とライバルの推移を追う ---
                if (score != null)
                {
                    if (startMultiplier < 0f) startMultiplier = score.Multiplier;
                    maxMultiplier = Mathf.Max(maxMultiplier, score.Multiplier);
                    if (scoreAtFirstItem < 0 && score.Score > 0) scoreAtFirstItem = score.Score;
                }

                if (rival != null)
                {
                    maxDefeats = Mathf.Max(maxDefeats, rival.Defeats);
                    minHearts = Mathf.Min(minHearts, rival.Hearts);
                }

                // --- 低い壁はジャンプで越える ---
                TryWall(0, JumpWallZ, z);
                TryWall(1, SecondWallZ, z);

                // ゴール判定が出たら（リザルトへ遷移する前に）評価する
                if (goal != null && goal.IsFinished)
                {
                    Evaluate();
                    Finish();
                }
            }

            /// <summary>床の左右にペンライトの列が居るか数える。</summary>
            private void MeasurePenlights()
            {
                penlightLeft = 0;
                penlightRight = 0;

                foreach (var strip in FindObjectsByType<PenlightStrip>(FindObjectsSortMode.None))
                {
                    float x = strip.transform.position.x;
                    if (x < 0f) penlightLeft++;
                    else if (x > 0f) penlightRight++;
                }
            }

            /// <summary>
            /// 生成された障害物の見た目の外形を測る。
            /// Z-upで作られたモデルの補正回転が失われると「倒れた状態」になるので、
            /// 高さ(Y)が奥行き(Z)より大きいことを後で確認する。
            /// </summary>
            private void MeasureObstacle()
            {
                foreach (var obstacle in FindObjectsByType<Obstacle>(FindObjectsSortMode.None))
                {
                    if (!obstacle.name.StartsWith("Run_Obstacle")) continue;

                    var renderers = obstacle.GetComponentsInChildren<Renderer>();
                    if (renderers.Length == 0) continue;

                    var bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

                    obstacleSize = bounds.size;
                    return;
                }
            }

            /// <summary>壁の手前で飛び、越えた直後のHPを記録する。</summary>
            private void TryWall(int index, float wallZ, float z)
            {
                if (!jumped[index] && z > wallZ - JumpLeadDistance)
                {
                    jumped[index] = true;
                    healthBeforeWall[index] = player.Health;
                    if (!player.TryJump()) Fail($"{index + 1}枚目の壁の手前でジャンプできなかった");
                }

                if (jumped[index] && healthAfterWall[index] < 0 && z > wallZ + WallClearedDistance)
                {
                    healthAfterWall[index] = player.Health;
                }
            }

            private void Evaluate()
            {
                float z = player.DistanceZ;

                Check(z > startZ + 150f, $"自動で前に進む（{startZ:F1} → {z:F1}）");
                Check(laneReached, "レーン移動が端(-2)まで届いて、そこで止まる");
                Check(laneReturned, "レーンを中央(0)に戻せる");
                Check(itemsCollected >= 2, $"アイテムを取得できる（{itemsCollected}個）");
                Check(gaugeAfterFirstItem > 0f, $"取得でゲージが増える（{gaugeAfterFirstItem:F3}）");
                Check(player.Health < startHealth, $"障害物に当たるとHPが減る（{startHealth} → {player.Health}）");

                for (int i = 0; i < 2; i++)
                {
                    Check(healthAfterWall[i] >= 0 && healthAfterWall[i] == healthBeforeWall[i],
                        $"{i + 1}枚目の低い壁をジャンプで越えられる"
                        + $"（手前 HP{healthBeforeWall[i]} → 通過後 HP{healthAfterWall[i]}）");
                }

                Check(player.IsAlive, $"ステージ終端まで生き残る（HP{player.Health}）");
                Check(player.IsGrounded, "ジャンプ後に着地している");

                Check(obstacleSize != Vector3.zero && obstacleSize.y > obstacleSize.z,
                    "障害物が倒れずに立っている"
                    + $"（幅{obstacleSize.x:F2} 高さ{obstacleSize.y:F2} 奥行き{obstacleSize.z:F2}）");

                Check(penlightLeft > 0 && penlightRight > 0,
                    $"ペンライトが床の両サイドに居る（左{penlightLeft}列 / 右{penlightRight}列）");

                CheckBloomReady();
                CheckPenlightColors();
                CheckTextGlyphs();

                Check(goal != null && goal.IsFinished,
                    $"譜面を走り切ってゴール判定が出る（到達 Z={maxZ:F1}）");

                CheckScoreAndRival();

                if (ground != null)
                {
                    float farthest = float.MinValue;
                    foreach (Transform tile in ground.transform)
                    {
                        farthest = Mathf.Max(farthest, tile.position.z);
                    }

                    Check(ground.transform.childCount >= 3 && farthest > z,
                        $"床が途切れずに前方へ続く（タイル{ground.transform.childCount}枚・最前 Z={farthest:F1}）");
                }
                else
                {
                    Fail("GroundTiler がシーンに無い");
                }
            }

            /// <summary>
            /// Bloom が掛かる状態か。Volume を置いてもカメラ側のポストプロセスが
            /// オフだと一切効かず、ペンライトがただの明るいベタ塗りになる。
            /// </summary>
            private void CheckBloomReady()
            {
                var cam = Camera.main;
                var data = cam != null ? cam.GetUniversalAdditionalCameraData() : null;
                bool postOn = data != null && data.renderPostProcessing;
                bool hasVolume = FindFirstObjectByType<Volume>() != null;

                Check(postOn && hasVolume,
                    $"Bloomが掛かる状態（カメラのポストプロセス={postOn} / Volume={hasVolume}）");
            }

            /// <summary>
            /// ペンライトの色が白に寄っていないか。
            /// 2色のランダム補間にすると中間が白くなり、大半が白い棒になる。
            /// 各色についてRGBの最大と最小の差を見て、彩度が残っているかを確かめる。
            /// </summary>
            private void CheckPenlightColors()
            {
                var strip = FindFirstObjectByType<PenlightStrip>();
                var ps = strip != null ? strip.GetComponentInChildren<ParticleSystem>(true) : null;
                if (ps == null)
                {
                    Fail("ペンライトのParticleSystemが見つからない");
                    return;
                }

                var startColor = ps.main.startColor;
                float worst = 1f;
                int sampled = 0;

                // 0〜1を刻んで実際に引かれうる色を見る
                for (int i = 0; i <= 10; i++)
                {
                    Color c = startColor.Evaluate(0f, i / 10f);
                    float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                    float min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                    if (max <= 0f) continue;

                    worst = Mathf.Min(worst, (max - min) / max);
                    sampled++;
                }

                Check(sampled > 0 && worst > 0.4f,
                    $"ペンライトの色が白に寄っていない（最も白い色の彩度 {worst:F2}）");
            }

            /// <summary>
            /// HUDの文字が全部フォントに存在するか。
            /// 足りない字は豆腐(□)として描かれるが、見た目では気づきにくいので機械的に見る。
            /// </summary>
            private void CheckTextGlyphs()
            {
                var missingAll = new List<string>();
                int checkedCount = 0;

                foreach (var label in FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None))
                {
                    if (string.IsNullOrEmpty(label.text) || label.font == null) continue;

                    checkedCount++;
                    if (label.font.HasCharacters(label.text, out var missing) || missing == null) continue;

                    foreach (char c in missing)
                    {
                        missingAll.Add($"{label.name}:'{c}'");
                    }
                }

                Check(checkedCount > 0 && missingAll.Count == 0,
                    missingAll.Count == 0
                        ? $"HUDの文字が全てフォントにある（{checkedCount}個のテキストを確認）"
                        : "フォントに無い文字がある: " + string.Join(" ", missingAll));
            }

            /// <summary>
            /// スコア・倍率・ライバルの確認。
            ///
            /// 今の譜面はアイテムが8個しか無く、レーン0で拾えるのは2個なので、
            /// ライバル(体力50)を自然に倒し切ることはできない。
            /// そこで走り終えてから直接ダメージを与え、
            /// 「削り切る → ハートが1つ減る → 全部割れたらフィーバー」の流れを確かめる。
            /// </summary>
            private void CheckScoreAndRival()
            {
                if (score == null || rival == null)
                {
                    Fail("RunScore / RunRival がシーンに無い");
                    return;
                }

                Check(score.Score > 0, $"アイテム取得でスコアが入る（{score.Score}点）");
                Check(maxMultiplier > 1f,
                    $"コンボとゲージでスコア倍率が上がる（最大 x{maxMultiplier:F2}）");

                // --- ここから直接ライバルを削る ---
                int heartsBefore = rival.Hearts;
                int defeatsBefore = rival.Defeats;

                // 走行中に既に何発か入っているので、残り体力ぶんだけ叩いて
                // ちょうど1本だけ削り切る
                int hitsToBreak = rival.Hp;
                for (int i = 0; i < hitsToBreak; i++) rival.ApplyHit();

                Check(rival.Defeats == defeatsBefore + 1,
                    $"体力バーを削り切ると撃破数が増える（{defeatsBefore} → {rival.Defeats}）");
                Check(rival.Hearts == heartsBefore - 1,
                    $"撃破するとハートが1つ減る（{heartsBefore} → {rival.Hearts}）");
                Check(rival.Hp == rival.MaxHp,
                    $"ハートが残っていれば体力バーが回復する（{rival.Hp}/{rival.MaxHp}）");

                // 残りのハートを全部割る
                while (rival.Hearts > 0 && rival.Defeats < rival.MaxHearts * 2)
                {
                    rival.ApplyHit();
                }

                Check(gauge != null && gauge.IsFever,
                    $"ハートを全部割るとフィーバーが始まる（フィーバー={gauge?.IsFever}）");

                CheckSuperFever();

                // --- 倍率の計算式そのものを確かめる ---
                var settings = UnityEditor.AssetDatabase
                    .LoadAssetAtPath<ScoreSettings>("Assets/Settings_Run/ScoreSettings.asset");

                if (settings == null)
                {
                    Fail("ScoreSettings が読み込めない");
                    return;
                }

                float noDefeat = settings.CalculateMultiplier(0, 0f, 0, false);
                float withDefeats = settings.CalculateMultiplier(0, 0f, 3, false);
                float withGauge = settings.CalculateMultiplier(0, 1f, 0, false);
                float withFever = settings.CalculateMultiplier(0, 0f, 0, true);

                Check(withDefeats > noDefeat,
                    $"ライバル撃破で倍率が上がる（撃破0: x{noDefeat:F2} → 撃破3: x{withDefeats:F2}）");
                Check(withGauge > noDefeat,
                    $"ゲージ満タンで倍率が上がる（x{noDefeat:F2} → x{withGauge:F2}）");
                Check(withFever > noDefeat,
                    $"フィーバーで倍率が上がる（x{noDefeat:F2} → x{withFever:F2}）");
            }

            /// <summary>
            /// スーパーフィーバーへの昇格を確かめる。
            ///
            /// ここに来た時点で、ライバルのハート全割りによるフィーバーが掛かっている。
            /// 同じきっかけを重ねても昇格しないこと、もう一方のきっかけで昇格することの
            /// 両方を見る。「演出でゲージを満タンにしただけ」で勝手に昇格しないことも確認する。
            /// </summary>
            private void CheckSuperFever()
            {
                if (gauge == null)
                {
                    Fail("BeatGaugeController がシーンに無い");
                    return;
                }

                Check(!gauge.IsSuperFever,
                    $"きっかけ1つではスーパーフィーバーにならない（発動源={gauge.Sources}）");

                // 同じきっかけをもう一度。これで上がってしまうと連取で暴発する
                gauge.TriggerFever(FeverSource.Rival);
                Check(!gauge.IsSuperFever, "同じきっかけを重ねても昇格しない");

                // もう一方のきっかけ
                gauge.TriggerFever(FeverSource.Gauge);
                Check(gauge.IsSuperFever,
                    $"2つのきっかけが揃うとスーパーフィーバーになる（発動源={gauge.Sources}）");

                var settings = UnityEditor.AssetDatabase
                    .LoadAssetAtPath<ScoreSettings>("Assets/Settings_Run/ScoreSettings.asset");
                if (settings == null) return;

                float fever = settings.CalculateMultiplier(0, 0f, 0, true, false);
                float superFever = settings.CalculateMultiplier(0, 0f, 0, true, true);

                Check(superFever > fever,
                    $"スーパーフィーバーは通常より倍率が高い（x{fever:F2} → x{superFever:F2}）");
            }

            private void Check(bool ok, string label)
            {
                results.Add((ok ? "  OK   " : "  NG   ") + label);
                if (!ok) failures.Add(label);
            }

            private void Fail(string label)
            {
                results.Add("  NG   " + label);
                failures.Add(label);
            }

            private void Finish()
            {
                if (finished) return;
                finished = true;

                var sb = new StringBuilder();
                sb.AppendLine("===== Game_Run 自動プレイ確認 =====");
                foreach (var line in results) sb.AppendLine(line);
                sb.AppendLine(failures.Count == 0
                    ? "===== 全項目OK ====="
                    : $"===== {failures.Count}件NG =====");

                Debug.Log(sb.ToString());

                Time.timeScale = 1f;
                SessionState.SetBool(SessionKey, false);

                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
                }
                else
                {
                    EditorApplication.ExitPlaymode();
                }
            }
        }
    }
}
