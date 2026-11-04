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
    /// 実際に走らせて確認する。
    ///
    /// 譜面は自動生成で中身が変わるため、期待値を固定で持たず、
    /// 実行時に譜面を読んでそれに追従する「自動プレイヤー」として動く。
    ///
    /// 実行方法:
    ///   Unityのメニュー  Yakiniku > Run > Game_Run を自動プレイして確認
    ///   コマンドライン    Unity.exe -batchmode -projectPath &lt;パス&gt;
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
        /// Playモード中に走って判定する。
        /// 入力はキーボードを叩けないので PlayerRunner の公開APIを直接呼ぶ。
        /// </summary>
        private sealed class Driver : MonoBehaviour
        {
            private const float JumpLeadDistance = 3.5f;  // 壁の何m手前で飛ぶか
            private const float TimeoutSeconds = 180f;
            private const float SpeedUp = 8f;             // 早送り倍率

            private readonly List<string> results = new List<string>();
            private readonly List<string> failures = new List<string>();

            private PlayerRunner player;
            private BeatGaugeController gauge;
            private GroundTiler ground;
            private RunGoalWatcher goal;
            private RunScore score;
            private RunRival rival;

            // 譜面。追従するために自分でも読む
            private readonly List<NoteData> items = new List<NoteData>();
            private readonly List<NoteData> walls = new List<NoteData>();
            private int nextItem;
            private int nextWall;

            private int startHealth;
            private float startZ;
            private int itemsCollected;
            private float maxZ;
            private float maxMultiplier;
            private bool sawSuperFever;
            private bool laneReachedEdge;
            private Vector3 obstacleSize = Vector3.zero;
            private int penlightLeft = -1;
            private int penlightRight = -1;
            private float elapsed;
            private bool finished;

            private void Start()
            {
                // バッチモードでも音は再生される。自動テストは裏で何度も走らせるので、
                // 作業中に突然BGMが鳴り出すのを防ぐため消音する
                AudioListener.volume = 0f;

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

                LoadChart();

                startZ = player.DistanceZ;
                startHealth = player.Health;
                player.OnItemCollected += _ => itemsCollected++;

                Time.timeScale = SpeedUp;
            }

            /// <summary>譜面を読んで、アイテムと壁の位置を覚える。</summary>
            private void LoadChart()
            {
                string fileName = string.IsNullOrEmpty(GameData.selectedJsonName)
                    ? "StageData_Normal"
                    : GameData.selectedJsonName;

                var json = Resources.Load<TextAsset>(fileName);
                if (json == null)
                {
                    Fail($"譜面 {fileName} が読めない");
                    return;
                }

                var data = JsonUtility.FromJson<StageData>(json.text);
                if (data?.notes == null)
                {
                    Fail($"譜面 {fileName} の解析に失敗");
                    return;
                }

                foreach (var note in data.notes)
                {
                    if (note.type == "Item") items.Add(note);
                    else if (note.type == "JumpObstacle") walls.Add(note);
                }

                items.Sort((a, b) => a.spawnZ.CompareTo(b.spawnZ));
                walls.Sort((a, b) => a.spawnZ.CompareTo(b.spawnZ));
            }

            private void Update()
            {
                if (finished) return;

                elapsed += Time.unscaledDeltaTime;
                if (elapsed > TimeoutSeconds)
                {
                    Fail($"{TimeoutSeconds}秒たってもゴールしない"
                         + $"（Z={player.DistanceZ:F1} HP={player.Health}）");
                    Evaluate();
                    Finish();
                    return;
                }

                float z = player.DistanceZ;
                maxZ = Mathf.Max(maxZ, z);

                FollowChart(z);
                Observe(z);

                if (goal != null && goal.IsFinished)
                {
                    Evaluate();
                    Finish();
                }
            }

            /// <summary>
            /// 譜面に追従する。次のアイテムのレーンへ寄せ、壁の手前で飛ぶ。
            /// 障害物は進路の隣に置かれているので、アイテムを追っていれば自然に避けられる。
            /// </summary>
            private void FollowChart(float z)
            {
                while (nextItem < items.Count && items[nextItem].spawnZ < z) nextItem++;
                while (nextWall < walls.Count && walls[nextWall].spawnZ < z - 1f) nextWall++;

                if (nextItem < items.Count)
                {
                    int target = player.Settings.ClampLane(items[nextItem].lane);
                    if (player.CurrentLane != target)
                    {
                        player.TryChangeLane(target > player.CurrentLane ? 1 : -1);
                        if (target == player.Settings.MinLane) laneReachedEdge = true;
                    }
                }

                if (nextWall < walls.Count && z > walls[nextWall].spawnZ - JumpLeadDistance)
                {
                    player.TryJump();
                }
            }

            private void Observe(float z)
            {
                if (score != null) maxMultiplier = Mathf.Max(maxMultiplier, score.Multiplier);
                if (gauge != null && gauge.IsSuperFever) sawSuperFever = true;
                if (obstacleSize == Vector3.zero) MeasureObstacle();
                if (penlightLeft < 0 && z > 20f) MeasurePenlights();
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

            private void Evaluate()
            {
                float z = player.DistanceZ;

                Check(z > startZ + 500f, $"譜面の長さぶん前に進む（{startZ:F1} → {z:F1}）");
                Check(laneReachedEdge, "譜面に合わせて端のレーンまで移動できる");
                Check(player.IsGrounded || player.Health <= 0, "ジャンプ後に着地している");

                float collectRate = items.Count > 0 ? itemsCollected / (float)items.Count : 0f;
                Check(collectRate > 0.8f,
                    $"譜面のアイテムをほぼ取れる（{itemsCollected}/{items.Count} = {collectRate:P0}）");

                Check(player.IsAlive, $"最後まで生き残る（HP {startHealth} → {player.Health}）");
                Check(goal != null && goal.IsFinished,
                    $"譜面を走り切ってゴール判定が出る（到達 Z={maxZ:F1}）");

                Check(obstacleSize != Vector3.zero && obstacleSize.y > obstacleSize.z,
                    "障害物が倒れずに立っている"
                    + $"（幅{obstacleSize.x:F2} 高さ{obstacleSize.y:F2} 奥行き{obstacleSize.z:F2}）");

                Check(penlightLeft > 0 && penlightRight > 0,
                    $"ペンライトが床の両サイドに居る（左{penlightLeft}列 / 右{penlightRight}列）");

                CheckBloomReady();
                CheckPenlightColors();
                CheckTextGlyphs();
                CheckGround(z);
                CheckScoreAndRival();
            }

            private void CheckGround(float z)
            {
                if (ground == null)
                {
                    Fail("GroundTiler がシーンに無い");
                    return;
                }

                float farthest = float.MinValue;
                foreach (Transform tile in ground.transform)
                {
                    farthest = Mathf.Max(farthest, tile.position.z);
                }

                Check(ground.transform.childCount >= 3 && farthest > z,
                    $"床が途切れずに前方へ続く（タイル{ground.transform.childCount}枚"
                    + $"・最前 Z={farthest:F1}）");
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

                    foreach (char c in missing) missingAll.Add($"{label.name}:'{c}'");
                }

                Check(checkedCount > 0 && missingAll.Count == 0,
                    missingAll.Count == 0
                        ? $"HUDの文字が全てフォントにある（{checkedCount}個のテキストを確認）"
                        : "フォントに無い文字がある: " + string.Join(" ", missingAll));
            }

            /// <summary>スコア・倍率・ライバルの確認。</summary>
            private void CheckScoreAndRival()
            {
                if (score == null || rival == null)
                {
                    Fail("RunScore / RunRival がシーンに無い");
                    return;
                }

                Check(score.Score > 0, $"アイテム取得でスコアが入る（{score.Score:N0}点）");
                Check(maxMultiplier > 1f, $"スコア倍率が上がる（最大 x{maxMultiplier:F2}）");
                Check(score.MaxCombo > 50, $"コンボが繋がる（最大{score.MaxCombo}）");

                // ここが今回の本題。譜面のアイテム数でライバルを倒し切れるか
                Check(rival.Defeats > 0,
                    $"譜面のアイテム数でライバルを撃破できる（撃破{rival.Defeats}回）");

                Check(sawSuperFever,
                    sawSuperFever
                        ? "走行中にスーパーフィーバーが発動する"
                        : "走行中にスーパーフィーバーが発動しない（きっかけが揃っていない）");

                CheckSuperFeverPromotion();
            }

            /// <summary>
            /// スーパーフィーバーへの昇格条件を直接確かめる。
            /// 同じきっかけを重ねても昇格しないこと、もう一方で昇格することの両方を見る。
            /// </summary>
            private void CheckSuperFeverPromotion()
            {
                if (gauge == null) return;

                var settings = AssetDatabase
                    .LoadAssetAtPath<ScoreSettings>("Assets/Settings_Run/ScoreSettings.asset");
                if (settings == null)
                {
                    Fail("ScoreSettings が読み込めない");
                    return;
                }

                float noDefeat = settings.CalculateMultiplier(0, 0f, 0, false);
                float withDefeats = settings.CalculateMultiplier(0, 0f, 3, false);
                float withGauge = settings.CalculateMultiplier(0, 1f, 0, false);
                float fever = settings.CalculateMultiplier(0, 0f, 0, true);
                float superFever = settings.CalculateMultiplier(0, 0f, 0, true, true);

                Check(withDefeats > noDefeat,
                    $"ライバル撃破で倍率が上がる（撃破0: x{noDefeat:F2} → 撃破3: x{withDefeats:F2}）");
                Check(withGauge > noDefeat,
                    $"ゲージ満タンで倍率が上がる（x{noDefeat:F2} → x{withGauge:F2}）");
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
