using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Yakiniku.Run.Inputs;
using Object = UnityEngine.Object;

namespace Yakiniku.Run.EditorTools
{
    /// <summary>
    /// ランゲームの動作確認シーン(Game_Run)と、そこで使う設定アセット・プレハブを自動生成する。
    ///
    /// 手作業でHierarchyを組むとレーン幅や接地Yの指定ミスで「見た目は合っているのに
    /// 当たり判定がずれる」事故が起きやすい。生成手順をコードにしておけば
    /// 数値の根拠がレビューできるし、壊したときに作り直せる。
    ///
    /// 実行方法:
    ///   Unityのメニュー  Yakiniku > Run > Game_Run シーンを生成
    ///   コマンドライン    Unity.exe -batchmode -quit -projectPath .
    ///                     -executeMethod Yakiniku.Run.EditorTools.RunSceneBuilder.BuildAll
    /// </summary>
    public static class RunSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Game_Run.unity";
        private const string LegacyScenePath = "Assets/Game_Run.unity";
        private const string SettingsDir = "Assets/Settings_Run";
        private const string PrefabDir = "Assets/Prefabs/Run";

        private const string RunSettingsPath = SettingsDir + "/RunSettings.asset";
        private const string GaugeSettingsPath = SettingsDir + "/GaugeSettings.asset";

        // 既存アセット（他メンバー作成。中身は変更せず、子として取り込むだけ）
        private const string ArtFloor = "Assets/Stage_Fixed.prefab";
        private const string ArtPlayer = "Assets/Player/PlayerModel.prefab";
        private const string ArtItem = "Assets/Item.prefab";
        private const string ArtObstacle = "Assets/Obstacle.prefab";
        private const string ArtJumpObstacle = "Assets/JumpObstacle.prefab";

        private const string ArtPenlight = "Assets/Particle System.prefab";
        private const string ArtGlobalVolume = "Assets/Global Volume.prefab";
        private const string BgmPath = "Assets/Resources/ぼららら.wav";
        private const string TmpFontPath =
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        private const string RunItemPath = PrefabDir + "/Run_Item.prefab";
        private const string RunObstaclePath = PrefabDir + "/Run_Obstacle.prefab";
        private const string RunJumpObstaclePath = PrefabDir + "/Run_JumpObstacle.prefab";
        private const string RunPenlightPath = PrefabDir + "/Run_PenlightStrip.prefab";
        private const string PenlightMatPath = SettingsDir + "/PenlightMat_Run.mat";

        // ---- 寸法の決めごと -------------------------------------------------
        // 床(Stage_Fixed/FloorMesh)は 10 x 1 x 50 のCubeで原点が中心にあるので、
        // 上面を y=0 に合わせるには床の原点を -0.5 に下げる。
        private const float FloorTopY = 0f;
        private const float FloorThickness = 1f;
        private const float TileLength = 50f;

        // プレイヤーの当たり判定は「胴体の箱」。足元が床の上面に来るように、
        // 基準点(PlayerRunnerが動かす点)から下に ModelYOffset だけずらした位置を足元とする。
        private const float BodyHeight = 1.6f;
        private const float BodyWidth = 0.8f;

        // モデル(PlayerModel)の原点は足元にあるので、基準点からこの分だけ下げて付ける。
        // 既存のGameシーンと同じ値。
        private const float ModelYOffset = -0.35f;
        private const float ModelScale = 2f;

        /// <summary>プレイヤーの基準点(= RunSettings.groundY)。足元が床の上面に来る高さ。</summary>
        private const float PlayerGroundY = FloorTopY - ModelYOffset; // 0.35

        // 床(Stage_Fixed)の幅は10mで、レーンは -4〜4 を使う。
        // ペンライトは床の外側に置きたいので、床の端(5m)より外に出す。
        private const float PenlightOffsetX = 6.5f;
        private const float PenlightStripWidth = 5f;
        private const int PenlightPerStrip = 450;

        [MenuItem("Yakiniku/Run/Game_Run シーンを生成", false, 10)]
        public static void BuildAll()
        {
            try
            {
                EnsureFolders();

                var runSettings = CreateRunSettings();
                var gaugeSettings = CreateGaugeSettings();

                var runItem = BuildItemPrefab();
                var runObstacle = BuildObstaclePrefab();
                var runJump = BuildJumpObstaclePrefab();
                var runPenlight = BuildPenlightPrefab();

                BuildScene(runSettings, gaugeSettings, runItem, runObstacle, runJump, runPenlight);
                RegisterSceneInBuildSettings();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log("[RunSceneBuilder] 生成完了: " + ScenePath);
            }
            catch (Exception e)
            {
                Debug.LogError("[RunSceneBuilder] 失敗: " + e);
                throw;
            }
        }

        // =====================================================================
        // フォルダ・設定アセット
        // =====================================================================

        private static void EnsureFolders()
        {
            EnsureFolder(SettingsDir);
            EnsureFolder("Assets/Prefabs");
            EnsureFolder(PrefabDir);
            EnsureFolder("Assets/Scenes");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            string leaf = path.Substring(slash + 1);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static RunSettings CreateRunSettings()
        {
            var settings = LoadOrCreate<RunSettings>(RunSettingsPath);

            settings.laneCount = 5;
            settings.laneWidth = 2f;
            settings.forwardSpeed = 10f;
            settings.laneChangeSpeed = 18f;
            settings.jumpHeight = 1.6f;
            settings.gravity = -25f;
            settings.groundY = PlayerGroundY;
            settings.maxHealth = 3;
            settings.invincibleTime = 1.5f;

            EditorUtility.SetDirty(settings);
            return settings;
        }

        private static GaugeSettings CreateGaugeSettings()
        {
            var settings = LoadOrCreate<GaugeSettings>(GaugeSettingsPath);

            settings.gainPerItem = 0.06f;
            settings.gainPerSecond = 0.02f;
            settings.comboGainRate = 0.05f;
            settings.maxComboMultiplier = 2f;
            settings.decayPerSecond = 0.01f;
            settings.damagePenalty = 0.15f;
            settings.feverDuration = 8f;
            settings.drainDuringFever = true;

            EditorUtility.SetDirty(settings);
            return settings;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        // =====================================================================
        // ノーツ用プレハブ
        //
        // 見た目は既存アセット（他メンバー担当）をそのまま子として入れ、
        // 当たり判定とゲームロジックは自分で作った親側に持たせる。
        // こうするとモデルの差し替えやスケール調整で当たり判定が壊れない。
        // 既存アセットが持っているColliderは二重判定になるので無効化する。
        // =====================================================================

        private static GameObject BuildItemPrefab()
        {
            var root = new GameObject("Run_Item");
            try
            {
                AttachArt(root, ArtItem);

                // 半径0.7。レーン幅2mなので隣のレーンまでは届かない大きさ
                var col = root.AddComponent<SphereCollider>();
                col.radius = 0.7f;
                col.isTrigger = true;

                var item = root.AddComponent<BeatItem>();
                SetField(item, "gaugeGain", 0.06f);
                SetField(item, "score", 10);

                return SavePrefab(root, RunItemPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildObstaclePrefab()
        {
            var root = new GameObject("Run_Obstacle");
            try
            {
                AttachArt(root, ArtObstacle);

                // 高さ2mのカプセル。ジャンプでは越えられない＝横に避ける障害物
                var col = root.AddComponent<CapsuleCollider>();
                col.radius = 0.5f;
                col.height = 2f;
                col.direction = 1; // Y軸
                col.isTrigger = true;

                var obstacle = root.AddComponent<Obstacle>();
                SetField(obstacle, "damage", 1);
                SetField(obstacle, "despawnOnHit", true);

                return SavePrefab(root, RunObstaclePath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildJumpObstaclePrefab()
        {
            var root = new GameObject("Run_JumpObstacle");
            try
            {
                AttachArt(root, ArtJumpObstacle);

                // 全レーンを塞ぐ低い壁。見た目(高さ1m)より判定を0.8mに抑えて
                // 「ギリギリ越えた」が成功になるようにしている
                var col = root.AddComponent<BoxCollider>();
                col.size = new Vector3(10f, 0.8f, 1f);
                col.center = new Vector3(0f, -0.1f, 0f);
                col.isTrigger = true;

                var obstacle = root.AddComponent<Obstacle>();
                SetField(obstacle, "damage", 1);
                SetField(obstacle, "despawnOnHit", false); // 壁は残す（無敵時間で多重ヒットを防ぐ）

                return SavePrefab(root, RunJumpObstaclePath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// 床タイル1枚の片側に並ぶペンライト1列。
        ///
        /// 既存の `Particle System.prefab`（ペンライトの見た目）を子として抱え、
        /// 並べ方だけこちら側で上書きする。元のプレハブは触らない。
        /// 変更点は3つだけ:
        ///   ・Simulation Space を World に（タイルを前に回したとき粒が付いてこないように）
        ///   ・Box emitter を「タイル1枚ぶんの細い帯」に（元は100m四方で右に偏っていた）
        ///   ・1回のバースト数を現実的な数に（元は50000個）
        /// </summary>
        private static GameObject BuildPenlightPrefab()
        {
            var root = new GameObject("Run_PenlightStrip");
            try
            {
                var art = InstantiateArt(ArtPenlight);
                art.transform.SetParent(root.transform, false);
                art.transform.localPosition = Vector3.zero;

                var ps = art.GetComponentInChildren<ParticleSystem>(true);
                if (ps == null) throw new Exception("ペンライトのParticleSystemが見つかりません: " + ArtPenlight);

                var main = ps.main;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.playOnAwake = true;
                main.loop = false;
                main.startSpeed = 0f; // 観客は動かない。置物として撒く
                main.maxParticles = PenlightPerStrip * 2;

                // タイル1枚が視界から消えるまで持てばよい。回収時に Clear するので溜まらない
                main.startLifetime = 60f;

                main.startColor = CreatePenlightColors();

                var emission = ps.emission;
                emission.enabled = true;
                emission.rateOverTime = 0f; // 時間で増やさない
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)PenlightPerStrip) });

                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.position = Vector3.zero;
                shape.rotation = Vector3.zero;
                shape.scale = new Vector3(PenlightStripWidth, 0f, TileLength);

                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (renderer != null)
                {
                    // 元のメッシュ・サイズはそのまま使い、マテリアルだけ差し替える
                    renderer.sharedMaterial = CreatePenlightMaterial(renderer.sharedMaterial);
                }

                var strip = root.AddComponent<PenlightStrip>();
                SetField(strip, "ps", ps);

                return SavePrefab(root, RunPenlightPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// ペンライトの発光色。粒ごとに1色を選ばせる。
        ///
        /// 【2色のランダム補間にしてはいけない】
        /// MinMaxGradient の TwoColors は2色の「間」をランダムに取るので、
        /// 補色に近い2色（ピンクと水色など）を指定すると中間がほぼ白になり、
        /// 結果として大半のペンライトが白くなる。
        /// Gradient を Fixed（段階）にして RandomColor で引けば、各粒は必ず
        /// 指定した色のどれかになり、彩度が残る。
        ///
        /// 明るい色にしてあるのは Bloom で光らせるため。
        /// Global Volume Profile 1 の Bloom は threshold 0.2 なので、
        /// 1.0 未満の成分があっても十分にじむ。
        /// </summary>
        private static ParticleSystem.MinMaxGradient CreatePenlightColors()
        {
            var colors = new[]
            {
                new Color(1.9f, 0.15f, 0.75f), // ピンク
                new Color(0.15f, 1.2f, 1.9f),  // 水色
                new Color(0.55f, 1.9f, 0.3f),  // 黄緑
                new Color(1.9f, 0.9f, 0.15f),  // 橙
                new Color(0.9f, 0.25f, 1.9f),  // 紫
            };

            var keys = new GradientColorKey[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                // 等間隔に並べる。Fixed なので境目で切り替わる
                keys[i] = new GradientColorKey(colors[i], i / (float)(colors.Length - 1));
            }

            var gradient = new Gradient { mode = GradientMode.Fixed };
            gradient.SetKeys(keys, new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f),
            });

            return new ParticleSystem.MinMaxGradient(gradient)
            {
                mode = ParticleSystemGradientMode.RandomColor,
            };
        }

        /// <summary>
        /// ペンライト用マテリアルを作る。既存 PenlightMat は Game シーンでも使われているので、
        /// そちらを書き換えずに複製して調整する。
        /// </summary>
        private static Material CreatePenlightMaterial(Material source)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(PenlightMatPath);
            if (existing != null) return existing;

            if (source == null) throw new Exception("元のペンライトマテリアルが取得できません");

            var copy = new Material(source) { name = "PenlightMat_Run" };

            // 発光は粒ごとの色(HDR)で出すので、マテリアル側の一律な加算は切る。
            // ここに灰色(0.749)が入っていると全体にベタっと乗って立体感が消える
            if (copy.HasProperty("_EmissionColor")) copy.SetColor("_EmissionColor", Color.black);
            if (copy.HasProperty("_Color")) copy.SetColor("_Color", Color.white);

            AssetDatabase.CreateAsset(copy, PenlightMatPath);
            return copy;
        }

        /// <summary>既存の見た目プレハブを子として付け、そちらのColliderは無効化する。</summary>
        private static void AttachArt(GameObject root, string artPath)
        {
            var art = InstantiateArt(artPath);
            art.transform.SetParent(root.transform, false);

            // 位置はプレハブに手置きの値(0.99999, 0.7, 1)が残っているので0に揃えるが、
            // 【回転は絶対に触らない】。Obstacle.prefab はZ-upで作られたモデルを
            // 立たせるために root に -90°(X) の補正回転を持っている。
            // ここで identity にすると障害物が倒れた状態になる
            // （既存 StageManager が Quaternion.identity で生成して倒れているのと同じ原因）。
            art.transform.localPosition = Vector3.zero;

            foreach (var col in art.GetComponentsInChildren<Collider>(true))
            {
                col.enabled = false;
            }
        }

        private static GameObject InstantiateArt(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) throw new Exception("プレハブが見つかりません: " + path);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            if (instance == null) throw new Exception("プレハブを配置できません: " + path);
            return instance;
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path, out bool ok);
            if (!ok || saved == null) throw new Exception("プレハブを保存できません: " + path);
            return saved;
        }

        // =====================================================================
        // シーン
        // =====================================================================

        private static void BuildScene(
            RunSettings runSettings,
            GaugeSettings gaugeSettings,
            GameObject itemPrefab,
            GameObject obstaclePrefab,
            GameObject jumpObstaclePrefab,
            GameObject penlightPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // --- プレイヤー ---
            var player = new GameObject("Player");
            player.transform.position = new Vector3(0f, PlayerGroundY, 0f);

            var body = player.AddComponent<BoxCollider>();
            body.size = new Vector3(BodyWidth, BodyHeight, BodyWidth);
            body.center = new Vector3(0f, BodyHeight * 0.5f + ModelYOffset, 0f);
            body.isTrigger = false; // ここをトリガーにすると障害物の判定が飛ばない

            var rb = player.AddComponent<Rigidbody>();
            rb.isKinematic = true; // PlayerRunnerがAwakeでも設定するが、Inspectorで見て分かるように明示
            rb.useGravity = false;

            var model = InstantiateArt(ArtPlayer);
            model.name = "PlayerModel";
            model.transform.SetParent(player.transform, false);
            model.transform.localPosition = new Vector3(0f, ModelYOffset, 0f);
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * ModelScale;
            FixPlayerModelChildren(model);

            // --- 入力 ---
            var inputGo = new GameObject("InputSource");
            var input = inputGo.AddComponent<KeyboardRunInput>();

            var runner = player.AddComponent<PlayerRunner>();
            SetField(runner, "settings", runSettings);
            SetField(runner, "inputSource", input);

            // --- 床 ---
            var groundGo = new GameObject("Ground");
            var tiler = groundGo.AddComponent<GroundTiler>();
            var floorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArtFloor);
            if (floorPrefab == null) throw new Exception("床プレハブが見つかりません: " + ArtFloor);

            SetField(tiler, "player", runner);
            SetField(tiler, "groundPrefab", floorPrefab);
            SetField(tiler, "tileLength", TileLength);
            SetField(tiler, "pivotAtCenter", true);
            SetField(tiler, "tileCount", 3);
            SetField(tiler, "recycleBehindDistance", 10f);
            SetField(tiler, "groundY", FloorTopY - FloorThickness * 0.5f);

            // ペンライトは床と同じ仕組みでループさせる。別管理にすると床とズレていく
            SetArrayField(tiler, "sideProps", new Object[] { penlightPrefab });
            SetField(tiler, "sidePropOffsetX", PenlightOffsetX);
            SetField(tiler, "sidePropY", FloorTopY);

            // --- 譜面と生成 ---
            var spawnerGo = new GameObject("Spawner");
            var provider = spawnerGo.AddComponent<JsonNoteProvider>();
            SetField(provider, "resourcesFileName", "StageData_Normal");
            SetField(provider, "useSelectedDifficulty", true);

            var spawner = spawnerGo.AddComponent<RunSpawner>();
            SetField(spawner, "settings", runSettings);
            SetField(spawner, "player", runner);
            SetField(spawner, "noteProvider", provider);
            SetField(spawner, "spawnAheadDistance", 60f);
            SetField(spawner, "despawnBehindDistance", 15f);
            SetField(spawner, "poolDefaultCapacity", 16);
            SetField(spawner, "poolMaxSize", 128);
            SetNotePrefabs(spawner, new[]
            {
                // type文字列は譜面JSONと完全一致させる
                new NoteEntry("Item", itemPrefab, 1.0f, false),
                new NoteEntry("Obstacle", obstaclePrefab, 1.0f, false),
                // 低い壁は全レーンを塞ぐので、譜面のlane指定を無視して中央に出す
                new NoteEntry("JumpObstacle", jumpObstaclePrefab, 0.5f, true),
            });

            // --- ゲージ ---
            var gaugeGo = new GameObject("GaugeController");
            var gauge = gaugeGo.AddComponent<BeatGaugeController>();
            SetField(gauge, "settings", gaugeSettings);
            SetField(gauge, "player", runner);

            // --- BGM ---
            // GameManager の bgmSource には入れない。入れると GameManager 側の
            // 「曲の長さでクリア」コルーチンが走り、RunGoalWatcher と二重に終了判定が出る
            var bgmGo = new GameObject("BGM");
            var bgm = bgmGo.AddComponent<AudioSource>();
            bgm.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(BgmPath);
            bgm.playOnAwake = true;
            bgm.loop = false;

            // --- HUD（スコア・コンボ・HP・ゲージ） ---
            var hud = BuildHud(gauge);

            // --- 既存 GameManager（スコア計算とUI更新を任せる） ---
            var managerGo = new GameObject("GameManager");
            var manager = managerGo.AddComponent<GameManager>();
            manager.scoreText = hud.Score;
            manager.comboText = hud.Combo;
            manager.playerHpText = hud.PlayerHp;
            manager.rivalHpText = hud.RivalHp;
            manager.bgmSource = null; // 上記の理由で意図的に空

            // --- ランゲーム → GameManager の繋ぎ ---
            var bridgeGo = new GameObject("RunGameBridge");
            var bridge = bridgeGo.AddComponent<RunGameBridge>();
            SetField(bridge, "player", runner);
            SetField(bridge, "gauge", gauge);

            // --- ゴール判定 ---
            var goalGo = new GameObject("GoalWatcher");
            var goal = goalGo.AddComponent<RunGoalWatcher>();
            SetField(goal, "condition", (int)RunClearCondition.ChartEnd);
            SetField(goal, "player", runner);
            SetField(goal, "spawner", spawner);
            SetField(goal, "bgmSource", bgm);
            SetField(goal, "goalMarginZ", 15f);
            SetField(goal, "resultSceneName", "Result");
            SetField(goal, "transitionDelay", 1.5f);

            // --- カメラ ---
            var camera = Camera.main;
            if (camera != null)
            {
                camera.transform.position = new Vector3(0f, 6f, -9f);
                camera.transform.rotation = Quaternion.Euler(20f, 0f, 0f);

                var follow = camera.gameObject.GetComponent<CameraController>();
                if (follow == null) follow = camera.gameObject.AddComponent<CameraController>();
                follow.player = player.transform;

                // これを入れないと Volume の Bloom が一切掛からない。
                // 新規シーンのカメラは既定でポストプロセスがオフになっている
                var cameraData = camera.GetUniversalAdditionalCameraData();
                if (cameraData != null) cameraData.renderPostProcessing = true;
            }

            // --- ポストプロセス ---
            // ペンライトは Bloom でにじませて光らせる前提なので、Volume が無いと
            // ただの明るいベタ塗り（＝白い棒）になる。既存シーンと同じプレハブを使う
            var volume = InstantiateArt(ArtGlobalVolume);
            volume.name = "Global Volume";
            volume.transform.position = Vector3.zero;

            EditorSceneManager.SaveScene(scene, ScenePath);

            // 同名の空シーンが Assets 直下に残っていると、どちらを開くのか分からなくなる
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(LegacyScenePath) != null)
            {
                AssetDatabase.DeleteAsset(LegacyScenePath);
                Debug.Log("[RunSceneBuilder] 空だった " + LegacyScenePath + " を削除しました");
            }
        }

        /// <summary>
        /// PlayerModel.prefab は中身(Lily / Hair 4)のlocalPositionが
        /// (10019, -37621, -37764) という値で保存されており、そのまま配置するとモデルが
        /// 37km先に飛ぶ。既存のGameシーンでも0に上書きして使っているので、同じ扱いにする。
        /// </summary>
        private static void FixPlayerModelChildren(GameObject model)
        {
            foreach (Transform child in model.transform)
            {
                if (child.localPosition.sqrMagnitude > 1f)
                {
                    child.localPosition = Vector3.zero;
                }
            }
        }

        /// <summary>組み上げたHUDのうち、GameManager に渡す必要があるテキスト。</summary>
        private readonly struct HudRefs
        {
            public readonly TextMeshProUGUI Score;
            public readonly TextMeshProUGUI Combo;
            public readonly TextMeshProUGUI PlayerHp;
            public readonly TextMeshProUGUI RivalHp;

            public HudRefs(TextMeshProUGUI score, TextMeshProUGUI combo,
                           TextMeshProUGUI playerHp, TextMeshProUGUI rivalHp)
            {
                Score = score;
                Combo = combo;
                PlayerHp = playerHp;
                RivalHp = rivalHp;
            }
        }

        /// <summary>
        /// HUDを組む。中身は全て仮の見た目で、後から差し替える前提。
        ///
        /// 差し替えるときは `Canvas/HUD` 配下を好きに作り直してよい。
        /// ゲームロジック側が知っているのは
        ///   ・BeatGaugeView の Slider と Fill Image
        ///   ・GameManager の4つの TextMeshProUGUI
        /// だけなので、この参照さえ繋ぎ直せば見た目は自由に変えられる。
        /// </summary>
        private static HudRefs BuildHud(BeatGaugeController controller)
        {
            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            // 差し替え単位をはっきりさせるため、HUDは専用の入れ物の下にまとめる
            var hudGo = new GameObject("HUD", typeof(RectTransform));
            hudGo.transform.SetParent(canvasGo.transform, false);
            var hudRect = hudGo.GetComponent<RectTransform>();
            hudRect.anchorMin = Vector2.zero;
            hudRect.anchorMax = Vector2.one;
            hudRect.offsetMin = Vector2.zero;
            hudRect.offsetMax = Vector2.zero;

            var sliderGo = DefaultControls.CreateSlider(GetUIResources());
            sliderGo.name = "BeatGaugeSlider";
            sliderGo.transform.SetParent(hudGo.transform, false);

            var rect = sliderGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -56f);
            rect.sizeDelta = new Vector2(760f, 30f);

            var slider = sliderGo.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.value = 0f;
            slider.interactable = false;

            // つまみは見せない（ゲージなので操作させない）
            var handleArea = sliderGo.transform.Find("Handle Slide Area");
            if (handleArea != null) handleArea.gameObject.SetActive(false);

            Image fill = null;
            var fillTransform = sliderGo.transform.Find("Fill Area/Fill");
            if (fillTransform != null) fill = fillTransform.GetComponent<Image>();

            var view = sliderGo.AddComponent<BeatGaugeView>();
            SetField(view, "controller", controller);
            SetField(view, "slider", slider);
            SetField(view, "fillImage", fill);
            SetField(view, "smoothTime", 0.08f);
            SetField(view, "normalColor", new Color(0.35f, 0.8f, 1f));
            SetField(view, "feverColor", new Color(1f, 0.85f, 0.25f));
            SetField(view, "feverPulseSpeed", 6f);

            // ここから下は全て仮の見た目。
            // 文字列を日本語にすると豆腐(□)になる。TMPの既定フォント
            // LiberationSans SDF に日本語グリフが無いため。日本語を出したい場合は
            // 日本語フォントから TMP Font Asset を作って差し替えること
            CreateText(hudGo, "GaugeLabel", "KIRAMEKI BEAT", 24f,
                new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(760f, 30f),
                TextAlignmentOptions.Center);

            var score = CreateText(hudGo, "ScoreText", "Score: 0", 36f,
                new Vector2(0f, 1f), new Vector2(32f, -32f), new Vector2(420f, 46f),
                TextAlignmentOptions.TopLeft);

            var combo = CreateText(hudGo, "ComboText", "Combo: 0", 36f,
                new Vector2(0f, 1f), new Vector2(32f, -84f), new Vector2(420f, 46f),
                TextAlignmentOptions.TopLeft);

            var rivalHp = CreateText(hudGo, "RivalHPText", "Rival HP: 50", 36f,
                new Vector2(1f, 1f), new Vector2(-32f, -32f), new Vector2(420f, 46f),
                TextAlignmentOptions.TopRight);

            var playerHp = CreateText(hudGo, "PlayerHPText", "Player HP: 3", 36f,
                new Vector2(0f, 0f), new Vector2(32f, 32f), new Vector2(420f, 46f),
                TextAlignmentOptions.BottomLeft);

            return new HudRefs(score, combo, playerHp, rivalHp);
        }

        /// <summary>仮UI用のテキストを1つ作る。</summary>
        private static TextMeshProUGUI CreateText(
            GameObject parent, string name, string text, float fontSize,
            Vector2 anchor, Vector2 anchoredPosition, Vector2 size,
            TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = Color.white;
            label.raycastTarget = false;

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpFontPath);
            if (font != null) label.font = font;

            return label;
        }

        private static DefaultControls.Resources GetUIResources()
        {
            return new DefaultControls.Resources
            {
                standard = Builtin("UI/Skin/UISprite.psd"),
                background = Builtin("UI/Skin/Background.psd"),
                inputField = Builtin("UI/Skin/InputFieldBackground.psd"),
                knob = Builtin("UI/Skin/Knob.psd"),
                checkmark = Builtin("UI/Skin/Checkmark.psd"),
                dropdown = Builtin("UI/Skin/DropdownArrow.psd"),
                mask = Builtin("UI/Skin/UIMask.psd"),
            };
        }

        private static Sprite Builtin(string path)
        {
            return AssetDatabase.GetBuiltinExtraResource<Sprite>(path);
        }

        private static void RegisterSceneInBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == LegacyScenePath);

            if (!scenes.Exists(s => s.path == ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // =====================================================================
        // SerializeField(private) への書き込み
        // =====================================================================

        private readonly struct NoteEntry
        {
            public readonly string Type;
            public readonly GameObject Prefab;
            public readonly float SpawnY;
            public readonly bool ForceCenterLane;

            public NoteEntry(string type, GameObject prefab, float spawnY, bool forceCenterLane)
            {
                Type = type;
                Prefab = prefab;
                SpawnY = spawnY;
                ForceCenterLane = forceCenterLane;
            }
        }

        private static void SetNotePrefabs(RunSpawner spawner, NoteEntry[] list)
        {
            var so = new SerializedObject(spawner);
            var array = so.FindProperty("notePrefabs");
            if (array == null) throw new Exception("RunSpawner.notePrefabs が見つかりません");

            array.arraySize = list.Length;
            for (int i = 0; i < list.Length; i++)
            {
                var element = array.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("type").stringValue = list[i].Type;
                element.FindPropertyRelative("prefab").objectReferenceValue = list[i].Prefab;
                element.FindPropertyRelative("spawnY").floatValue = list[i].SpawnY;
                element.FindPropertyRelative("forceCenterLane").boolValue = list[i].ForceCenterLane;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>オブジェクト参照の配列フィールドに書き込む。</summary>
        private static void SetArrayField(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var array = so.FindProperty(field);
            if (array == null) throw new Exception($"{target.GetType().Name}.{field} が見つかりません");

            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetField(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                throw new Exception($"{target.GetType().Name}.{field} が見つかりません（スクリプト側の名前が変わった？）");
            }

            switch (prop.propertyType)
            {
                case SerializedPropertyType.ObjectReference:
                    prop.objectReferenceValue = (Object)value;
                    break;
                case SerializedPropertyType.Float:
                    prop.floatValue = Convert.ToSingle(value);
                    break;
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Enum:
                    prop.intValue = Convert.ToInt32(value);
                    break;
                case SerializedPropertyType.Boolean:
                    prop.boolValue = Convert.ToBoolean(value);
                    break;
                case SerializedPropertyType.String:
                    prop.stringValue = (string)value;
                    break;
                case SerializedPropertyType.Color:
                    prop.colorValue = (Color)value;
                    break;
                default:
                    throw new Exception($"未対応の型です: {target.GetType().Name}.{field} ({prop.propertyType})");
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
