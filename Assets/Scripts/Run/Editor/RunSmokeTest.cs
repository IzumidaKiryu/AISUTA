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
    /// Game_Run を実際にPlayして、動作確認チェックリストを機械的に確認する。
    ///
    /// 譜面は自動生成で中身が変わるため、期待値を固定で持たず、
    /// 実行時に譜面を読んでそれに追従する「自動プレイヤー」として動く（ノーミスで走る）。
    ///
    /// 実行方法:
    ///   Unityのメニュー  Yakiniku > Run > Game_Run を自動プレイして確認
    ///   コマンドライン    Unity.exe -batchmode -projectPath . -executeMethod
    ///                     Yakiniku.Run.EditorTools.RunSmokeTest.Run
    ///                     （-quit は付けない。確認が終わったら自分で終了する）
    ///   難易度を変える   環境変数 YAKINIKU_DIFFICULTY=Easy / Normal / Hard / Universe
    ///
    /// ノーミスで走るので、出力される [CALIBRATE] 行のスコアがそのまま
    /// DifficultySettings の perfectScore（ランク計算の基準）の目安になる。
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

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            if (!SessionState.GetBool(SessionKey, false)) return;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        /// <summary>
        /// 難易度は譜面の読み込み（RunSpawner.Awake）より前に決めておく必要があるので、
        /// シーンが読まれる前に差し込む。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void BeforeScene()
        {
            if (!SessionState.GetBool(SessionKey, false)) return;

            string name = System.Environment.GetEnvironmentVariable("YAKINIKU_DIFFICULTY");
            if (string.IsNullOrEmpty(name) || !System.Enum.TryParse(name, out RunDifficulty difficulty))
            {
                difficulty = RunDifficulty.Normal;
            }

            RunSession.Difficulty = difficulty;
            var entry = DifficultySettings.Load()?.Get(difficulty);
            if (entry != null) GameData.selectedJsonName = entry.chartFile;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode) return;
            new GameObject("~RunSmokeDriver").AddComponent<Driver>();
        }

        private sealed class Driver : MonoBehaviour
        {
            private const float JumpLeadDistance = 3.5f;
            private const float TimeoutSeconds = 240f;
            private const float SpeedUp = 8f;

            private readonly List<string> results = new List<string>();
            private readonly List<string> failures = new List<string>();

            private PlayerRunner player;
            private BeatGaugeController gauge;
            private GroundTiler ground;
            private RunGoalWatcher goal;
            private RunScore score;
            private RunRival rival;
            private RunIntroSequence intro;
            private RunFinishSequence finish;
            private BeatOrbLauncher launcher;
            private RunBalance balance;
            private StageTheme theme;

            private readonly List<NoteData> items = new List<NoteData>();
            private readonly List<NoteData> walls = new List<NoteData>();
            private int nextItem;
            private int nextWall;

            private Dictionary<string, int> savedProgress;

            private int startHealth;
            // 演出中に動いた最大の位置。演出明けのフレームで測ると、バッチモードは
            // 1フレームが重く8倍速で数十m進んでしまうので、演出中ずっと見張る
            private float maxZDuringIntro;
            private bool introObserved;
            private float maxZ;
            private float maxMultiplier;
            private bool laneReachedEdge;
            private int feverCount;
            private bool sawSuper;
            private float defeatRatio = -1f;
            private float penlightMinAngle = float.MaxValue;
            private float penlightMaxAngle = float.MinValue;
            private Vector3 obstacleSize = Vector3.zero;
            private int penlightLeft = -1;
            private int penlightRight = -1;
            private float elapsed;
            private bool finished;
            private ParticleSystem.Particle[] particleBuffer;

            private void Start()
            {
                AudioListener.volume = 0f; // バッチモードでも音は鳴る。裏で走らせるので消音

                // テストの結果で手元のハイスコアや解放状態が書き換わらないよう、退避しておく
                savedProgress = RunProgress.Snapshot();

                player = FindFirstObjectByType<PlayerRunner>();
                gauge = FindFirstObjectByType<BeatGaugeController>();
                ground = FindFirstObjectByType<GroundTiler>();
                goal = FindFirstObjectByType<RunGoalWatcher>();
                score = FindFirstObjectByType<RunScore>();
                rival = FindFirstObjectByType<RunRival>();
                intro = FindFirstObjectByType<RunIntroSequence>();
                finish = FindFirstObjectByType<RunFinishSequence>();
                launcher = FindFirstObjectByType<BeatOrbLauncher>();
                balance = FindFirstObjectByType<RunBalance>();
                theme = FindFirstObjectByType<StageTheme>();

                if (player == null)
                {
                    Fail("PlayerRunner がシーンに無い");
                    Finish();
                    return;
                }

                if (finish != null) finish.SetGoToResult(false);

                if (gauge != null)
                {
                    gauge.OnFeverStart += () => feverCount++;
                    gauge.OnSuperFeverStart += () => sawSuper = true;
                }

                if (rival != null && score != null)
                {
                    rival.OnDefeated += () =>
                        defeatRatio = items.Count > 0 ? score.ItemsCollected / (float)items.Count : -1f;
                }

                LoadChart();
                startHealth = player.Health;
                Time.timeScale = SpeedUp;
            }

            private void LoadChart()
            {
                string fileName = string.IsNullOrEmpty(GameData.selectedJsonName)
                    ? "StageData_Normal"
                    : GameData.selectedJsonName;

                var json = Resources.Load<TextAsset>(fileName);
                var data = json != null ? JsonUtility.FromJson<StageData>(json.text) : null;
                if (data?.notes == null)
                {
                    Fail($"譜面 {fileName} が読めない");
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
                    Fail($"{TimeoutSeconds}秒たってもゴールしない（Z={player.DistanceZ:F1} HP={player.Health}）");
                    Evaluate();
                    Finish();
                    return;
                }

                // 開始演出の間は操作しない。その間プレイヤーが止まっているかだけ見張る
                if (intro != null && intro.IsPlaying)
                {
                    introObserved = true;
                    maxZDuringIntro = Mathf.Max(maxZDuringIntro, player.DistanceZ);
                    return;
                }

                float z = player.DistanceZ;
                maxZ = Mathf.Max(maxZ, z);

                FollowChart(z);
                Observe(z);

                // 終了判定が出て、結果がまとまったら評価する
                if (goal != null && goal.IsFinished && finish != null && finish.Result != null)
                {
                    Evaluate();
                    Finish();
                }
            }

            /// <summary>次のアイテムのレーンへ寄せ、壁の手前で飛ぶ。</summary>
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
                if (obstacleSize == Vector3.zero) MeasureObstacle();
                if (penlightLeft < 0 && z > 20f) MeasurePenlights();
                if (z > 20f) MeasurePenlightSway();
            }

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

            /// <summary>ペンライト1本の傾きを追う。左右に振れていれば最小と最大が離れる。</summary>
            private void MeasurePenlightSway()
            {
                var strip = FindFirstObjectByType<PenlightStrip>();
                var ps = strip != null ? strip.GetComponentInChildren<ParticleSystem>(true) : null;
                if (ps == null) return;

                if (particleBuffer == null) particleBuffer = new ParticleSystem.Particle[ps.main.maxParticles];
                int count = ps.GetParticles(particleBuffer);
                if (count == 0) return;

                float angle = Mathf.DeltaAngle(0f, particleBuffer[0].rotation3D.z);
                penlightMinAngle = Mathf.Min(penlightMinAngle, angle);
                penlightMaxAngle = Mathf.Max(penlightMaxAngle, angle);
            }

            private void Evaluate()
            {
                var result = finish != null ? finish.Result : null;
                float z = player.DistanceZ;

                // --- 流れ ---
                Check(introObserved && maxZDuringIntro < 1f,
                    $"開始演出の間は走り出さない（演出中の最大 Z={maxZDuringIntro:F1}）");
                Check(z > 500f, $"譜面の長さぶん前に進む（Z={z:F1}）");
                Check(goal != null && goal.IsFinished, $"譜面を走り切って終了判定が出る（到達 Z={maxZ:F1}）");
                Check(laneReachedEdge, "譜面に合わせて端のレーンまで移動できる");
                Check(player.IsGrounded || player.Health <= 0, "ジャンプ後に着地している");

                // --- プレイヤー ---
                Check(startHealth == 5, $"LIFE がハート5つ（{startHealth}）");
                Check(player.IsAlive, $"最後まで生き残る（HP {startHealth} → {player.Health}）");

                int collected = score != null ? score.ItemsCollected : 0;
                float rate = items.Count > 0 ? collected / (float)items.Count : 0f;
                Check(rate > 0.8f, $"譜面のアイテムをほぼ取れる（{collected}/{items.Count} = {rate:P0}）");

                // --- 見た目 ---
                Check(obstacleSize != Vector3.zero && obstacleSize.y > obstacleSize.z,
                    $"障害物が倒れずに立っている（高さ{obstacleSize.y:F2} 奥行き{obstacleSize.z:F2}）");
                Check(penlightLeft > 0 && penlightRight > 0,
                    $"ペンライトが床の両サイドに居る（左{penlightLeft}列 / 右{penlightRight}列）");
                float swing = penlightMaxAngle - penlightMinAngle;
                Check(swing > 15f, $"ペンライトが左右に揺れている（振れ幅 {swing:F0}°）");

                int tracks = FindObjectsByType<LaneTrack>(FindObjectsSortMode.None).Length;
                Check(tracks >= 3, $"レーン（路面と境界線）が敷かれている（{tracks}枚）");

                CheckBloomReady();
                CheckTextGlyphs();
                CheckGround(z);

                // --- ライバル ---
                int landed = launcher != null ? launcher.Landed : 0;
                Check(landed > collected * 0.9f, $"取ったノーツの白い球がライバルに届く（{landed}/{collected}発）");
                Check(rival != null && rival.IsDefeated,
                    $"ライバルを撃破できる（ハート {rival?.HeartsBroken}/{rival?.MaxHearts}）");

                var entry = DifficultySettings.Load()?.Get(RunSession.Difficulty);
                float target = entry != null ? entry.rivalDefeatAt : 0.9f;
                Check(defeatRatio > 0f && Mathf.Abs(defeatRatio - target) <= 0.05f,
                    $"狙った地点でライバルを撃破する（狙い {target:P0} → 実際 {defeatRatio:P0}" +
                    $"、体力 {balance?.RivalHpPerHeart}×{rival?.MaxHearts}）");

                // --- フィーバー ---
                Check(feverCount >= 2 && feverCount <= 4, $"ゲージのフィーバーが1曲に2〜4回（{feverCount}回）");
                Check(gauge != null && gauge.IsEncore, "撃破後は曲の終わりまで ENCORE が続く");
                Check(theme != null && (theme.CurrentState == StageTheme.State.Encore || theme.CurrentState == StageTheme.State.Super),
                    $"ENCORE 中はステージが金色基調（状態 {theme?.CurrentState}）");
                Check(sawSuper, sawSuper
                    ? "フルコンボならスーパーフィーバーが発動する"
                    : "スーパーフィーバーが発動しなかった（ENCORE とフィーバーが重ならない）");

                // --- スコア ---
                Check(score != null && score.Score > 0, $"スコアが入る（{score?.Score:N0}点）");
                Check(maxMultiplier > 1f, $"スコア倍率が上がる（最大 x{maxMultiplier:F2}）");
                Check(score != null && score.MaxCombo >= items.Count * 0.8f, $"コンボが繋がる（最大{score?.MaxCombo}）");

                if (result != null && entry != null)
                {
                    float ratio = result.score / (float)entry.perfectScore;
                    Debug.Log($"[CALIBRATE] difficulty={result.difficulty} score={result.score} " +
                              $"perfectScore(設定)={entry.perfectScore} 比率={ratio:F3}");

                    Check(result.rank == RunRank.SSPlus,
                        $"ノーミスなら SS+（{RankSettings.Load()?.Label(result.rank)}、基準の {ratio:P0}）");
                    Check(result.IsWin, $"ライバルを倒して生き残れば WIN（{(result.IsWin ? "WIN" : "LOSE")}）");
                    Check(RunProgress.HighScore(result.difficulty) == result.score, "結果がハイスコアとして保存される");
                }
                else
                {
                    Fail("リザルトに渡す結果がまとまっていない");
                }

                CheckMultiplierFormula();
            }

            private void CheckGround(float z)
            {
                if (ground == null)
                {
                    Fail("GroundTiler がシーンに無い");
                    return;
                }

                float farthest = float.MinValue;
                foreach (Transform tile in ground.transform) farthest = Mathf.Max(farthest, tile.position.z);
                Check(farthest > z, $"床が途切れずに前方へ続く（最前 Z={farthest:F1}）");
            }

            private void CheckBloomReady()
            {
                var cam = Camera.main;
                var data = cam != null ? cam.GetUniversalAdditionalCameraData() : null;
                bool postOn = data != null && data.renderPostProcessing;
                bool hasVolume = FindFirstObjectByType<Volume>() != null;
                Check(postOn && hasVolume, $"Bloomが掛かる状態（ポストプロセス={postOn} / Volume={hasVolume}）");
            }

            private void CheckTextGlyphs()
            {
                var missingAll = new List<string>();
                int checkedCount = 0;

                foreach (var label in FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (string.IsNullOrEmpty(label.text) || label.font == null) continue;

                    checkedCount++;
                    // tryAddCharacter を true にしないと「アトラスに読み込み済みの字」しか見ないので、
                    // まだ一度も描画していない字が「無い」と誤判定される。フォントファイル自体を見る
                    if (label.font.HasCharacters(label.text, out uint[] missing, true, true) || missing == null) continue;
                    foreach (uint code in missing) missingAll.Add($"{label.name}:'{char.ConvertFromUtf32((int)code)}'");
                }

                Check(checkedCount > 0 && missingAll.Count == 0,
                    missingAll.Count == 0
                        ? $"画面の文字が全てフォントにある（{checkedCount}個）"
                        : "フォントに無い文字がある: " + string.Join(" ", missingAll));
            }

            private void CheckMultiplierFormula()
            {
                var settings = AssetDatabase.LoadAssetAtPath<ScoreSettings>("Assets/Settings_Run/ScoreSettings.asset");
                if (settings == null)
                {
                    Fail("ScoreSettings が読み込めない");
                    return;
                }

                float none = settings.CalculateMultiplier(0, 0f, 0, false, false);
                float fever = settings.CalculateMultiplier(0, 0f, 0, true, false);
                float encore = settings.CalculateMultiplier(0, 0f, 0, false, true);
                float super = settings.CalculateMultiplier(0, 0f, 0, true, true);

                Check(none < fever && fever < encore && encore < super,
                    $"倍率が 通常 < フィーバー < ENCORE < スーパー（x{none:F1} / x{fever:F1} / x{encore:F1} / x{super:F1}）");
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

                // 手元の保存内容を元に戻す
                RunProgress.Restore(savedProgress);

                var sb = new StringBuilder();
                sb.AppendLine($"===== Game_Run 自動プレイ確認（{RunSession.Difficulty}） =====");
                foreach (var line in results) sb.AppendLine(line);
                sb.AppendLine(failures.Count == 0 ? "===== 全項目OK =====" : $"===== {failures.Count}件NG =====");
                Debug.Log(sb.ToString());

                Time.timeScale = 1f;
                SessionState.SetBool(SessionKey, false);

                if (Application.isBatchMode) EditorApplication.Exit(failures.Count == 0 ? 0 : 1);
                else EditorApplication.ExitPlaymode();
            }
        }
    }
}
