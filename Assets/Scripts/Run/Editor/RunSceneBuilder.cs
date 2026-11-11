using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Yakiniku.Run.Inputs;
using static Yakiniku.Run.EditorTools.BuilderUtil;
using Object = UnityEngine.Object;

namespace Yakiniku.Run.EditorTools
{
    /// <summary>
    /// ランゲームのシーン(Game_Run)と、そこで使う設定アセット・プレハブ・材質を自動生成する。
    ///
    /// 手作業でHierarchyを組むとレーン幅や接地Yの指定ミスで「見た目は合っているのに
    /// 当たり判定がずれる」事故が起きやすい。生成手順をコードにしておけば
    /// 数値の根拠がレビューできるし、壊したときに作り直せる。
    ///
    /// 実行方法:
    ///   Yakiniku > Run > 全部生成            … ゲーム・難易度選択・リザルトをまとめて
    ///   Yakiniku > Run > Game_Run シーンを生成 … ゲームだけ
    ///   コマンドライン: -executeMethod Yakiniku.Run.EditorTools.RunSceneBuilder.BuildEverything
    /// </summary>
    public static class RunSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Game_Run.unity";
        private const string LegacyScenePath = "Assets/Game_Run.unity";

        public const string RunSettingsPath = SettingsDir + "/RunSettings.asset";
        public const string GaugeSettingsPath = SettingsDir + "/GaugeSettings.asset";
        public const string ScoreSettingsPath = SettingsDir + "/ScoreSettings.asset";
        public const string VolumeProfilePath = SettingsDir + "/GameRunVolume.asset";

        // どのシーンからでも読めるよう Resources に置く
        public const string DifficultySettingsPath = ResourcesDir + "/DifficultySettings.asset";
        public const string RankSettingsPath = ResourcesDir + "/RankSettings.asset";
        public const string SeSettingsPath = ResourcesDir + "/SeSettings.asset";

        // 既存アセット（他メンバー作成。中身は変更せず、子として取り込むだけ）
        private const string ArtFloor = "Assets/Stage_Fixed.prefab";
        private const string ArtPlayer = "Assets/Player/PlayerModel.prefab";
        private const string ArtItem = "Assets/Item.prefab";
        private const string ArtObstacle = "Assets/Obstacle.prefab";
        private const string ArtJumpObstacle = "Assets/JumpObstacle.prefab";
        private const string ArtPenlight = "Assets/Particle System.prefab";
        private const string ArtGlobalVolume = "Assets/Global Volume.prefab";
        private const string ArtRival = "Assets/brochiManac_3D/A003_1_MINIUS/Prefab/目線が通常Ver/MINIus.prefab";
        private const string BgmPath = "Assets/Resources/ぼららら.wav";

        private const string JpFontFilePath = "Assets/Fonts/NotoSansJP-VF.ttf";
        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        private const string RunItemPath = PrefabDir + "/Run_Item.prefab";
        private const string RunObstaclePath = PrefabDir + "/Run_Obstacle.prefab";
        private const string RunJumpObstaclePath = PrefabDir + "/Run_JumpObstacle.prefab";
        private const string RunPenlightPath = PrefabDir + "/Run_PenlightStrip.prefab";
        private const string RunLaneTrackPath = PrefabDir + "/Run_LaneTrack.prefab";
        private const string RunOrbPath = PrefabDir + "/Run_BeatOrb.prefab";

        private const string PenlightMatPath = SettingsDir + "/PenlightMat_Run.mat";
        private const string LaneSurfaceMatPath = SettingsDir + "/LaneSurface.mat";
        private const string LaneLineMatPath = SettingsDir + "/LaneLine.mat";
        private const string OrbMatPath = SettingsDir + "/BeatOrb.mat";

        // ---- 寸法の決めごと -------------------------------------------------
        // 床(Stage_Fixed/FloorMesh)は 10 x 1 x 50 のCubeで原点が中心にあるので、
        // 上面を y=0 に合わせるには床の原点を -0.5 に下げる。
        private const float FloorTopY = 0f;
        private const float FloorThickness = 1f;
        private const float TileLength = 50f;
        private const float LaneWidth = 2f;

        // プレイヤーの当たり判定は「胴体の箱」。
        private const float BodyHeight = 1.6f;
        private const float BodyWidth = 0.8f;

        // モデル(PlayerModel)の原点は足元にあるので、基準点からこの分だけ下げて付ける。
        private const float ModelYOffset = -0.35f;
        private const float ModelScale = 2f;
        private const float PlayerGroundY = FloorTopY - ModelYOffset; // 0.35

        // ペンライトは床の外側に置く（床の端は5m）
        private const float PenlightOffsetX = 6.5f;
        private const float PenlightStripWidth = 5f;
        private const int PenlightPerStrip = 450;

        /// <summary>
        /// ペンライトの明るさ（材質側の倍率）。粒ごとの色は8bit(Color32)で1.0を超えられない。
        ///
        /// 【上げすぎると白くなる】倍率は粒の色の全成分に掛かるので、2.6 にすると
        /// ピンク(1, 0.4, 0.8) が (2.6, 1.04, 2.08) になり、画面上はほぼ白に飽和した。
        /// 一番強い成分だけが1を超える程度(1.15)に抑え、色そのものを鮮やかにしてある。
        /// 光のにじみはレーンの境界線(HDR)が担う。
        /// </summary>
        private const float PenlightBrightness = 1.15f;

        /// <summary>
        /// ステージの背景色（本家は暗いライブ会場）。明るい空だと HUD の白文字が読めず、
        /// ペンライトやレーンの光も映えない。遠景はこの色のフォグに溶かす。
        /// </summary>
        private static readonly Color StageBackground = new Color(0.07f, 0.055f, 0.16f);

        // ライバルはノーツの先読み(60m)より奥に、遠くでも見える大きさで立たせる
        private const float RivalDistance = 64f;
        private const float RivalHeight = 4.5f;

        // =====================================================================
        // 入口
        // =====================================================================

        [MenuItem("Yakiniku/Run/全部生成", false, 9)]
        public static void BuildEverything()
        {
            BuildAll();
            SelectSceneBuilder.Build();
            ResultSceneBuilder.Build();
            AssetDatabase.SaveAssets();
            Debug.Log("[RunSceneBuilder] 全シーンの生成完了");
        }

        [MenuItem("Yakiniku/Run/Game_Run シーンを生成", false, 10)]
        public static void BuildAll()
        {
            try
            {
                BuildSharedAssets();
                BuildScene();
                RegisterSceneInBuildSettings(ScenePath, LegacyScenePath);

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

        /// <summary>
        /// シーンより先に作っておく必要があるアセット一式。
        /// 難易度選択・リザルトのビルダーもこれを前提にしている。
        /// </summary>
        public static void BuildSharedAssets()
        {
            EnsureFolder(SettingsDir);
            EnsureFolder(PrefabDir);
            EnsureFolder(ResourcesDir);
            EnsureFolder("Assets/Scenes");

            // フォントを先に作る。TMP_FontAsset.CreateFontAsset はアセットデータベースを
            // 作り直すので、先に取得しておいた参照が無効になる
            var font = CreateJapaneseFontAsset();
            RegisterFontFallback(font);

            CreateRunSettings();
            CreateGaugeSettings();
            CreateScoreSettings();
            CreateVolumeProfile();
            CreateDifficultySettings();
            CreateRankSettings();
            LoadOrCreate<SeSettings>(SeSettingsPath);

            CreateMaterials();

            BuildItemPrefab();
            BuildObstaclePrefab();
            BuildJumpObstaclePrefab();
            BuildPenlightPrefab();
            BuildLaneTrackPrefab();
            BuildOrbPrefab();

            AssetDatabase.SaveAssets();
        }

        // =====================================================================
        // フォント
        // =====================================================================

        /// <summary>
        /// 日本語を表示できる TMP Font Asset。Atlas Population Mode を Dynamic にして、
        /// 実際に表示された字だけを実行時にアトラスへ足していく方式にしている。
        /// フォントは Noto Sans JP（SIL OFL 1.1）。Meiryo や Yu Gothic はビルドに同梱できない。
        /// </summary>
        private static TMP_FontAsset CreateJapaneseFontAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(JpFontAssetPath);
            if (existing != null) return existing;

            var source = AssetDatabase.LoadAssetAtPath<Font>(JpFontFilePath);
            if (source == null)
            {
                Debug.LogWarning($"[RunSceneBuilder] 日本語フォントが見つかりません: {JpFontFilePath}");
                return null;
            }

            var asset = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA,
                1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (asset == null) throw new Exception("TMP Font Asset を作成できません");

            asset.name = "NotoSansJP SDF";
            AssetDatabase.CreateAsset(asset, JpFontAssetPath);

            // アトラスとマテリアルはサブアセットとして一緒に保存する。省くと参照切れになる
            if (asset.atlasTextures != null && asset.atlasTextures.Length > 0 && asset.atlasTextures[0] != null)
            {
                asset.atlasTextures[0].name = asset.name + " Atlas";
                AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
            }

            if (asset.material != null)
            {
                asset.material.name = asset.name + " Material";
                AssetDatabase.AddObjectToAsset(asset.material, asset);
            }

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        /// <summary>
        /// TMP の既定フォールバックに日本語フォントを登録する。既存のテキストが
        /// LiberationSans のままでも、日本語の字だけ自動でこちらから引かれる。
        /// </summary>
        private static void RegisterFontFallback(TMP_FontAsset font)
        {
            if (font == null) return;

            var settings = AssetDatabase.LoadAssetAtPath<ScriptableObject>(TmpSettingsPath);
            if (settings == null) return;

            var so = new SerializedObject(settings);
            var list = so.FindProperty("m_fallbackFontAssets");
            if (list == null) return;

            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == font) return;
            }

            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = font;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }

        // =====================================================================
        // 設定アセット
        // =====================================================================

        private static void CreateRunSettings()
        {
            var s = LoadOrCreate<RunSettings>(RunSettingsPath);
            s.laneCount = 5;
            s.laneWidth = LaneWidth;
            s.forwardSpeed = 10f;
            s.laneChangeSpeed = 18f;
            s.jumpHeight = 1.6f;
            s.gravity = -25f;
            s.groundY = PlayerGroundY;
            s.maxHealth = 5;           // 本家は LIFE がハート5つ
            s.invincibleTime = 1.5f;
            EditorUtility.SetDirty(s);
        }

        private static void CreateGaugeSettings()
        {
            var s = LoadOrCreate<GaugeSettings>(GaugeSettingsPath);

            // ゲージの溜まり方は RunBalance が譜面から決めるので、ここは既定値。
            // 時間で勝手に溜まったり減ったりすると回数の計算が合わなくなるので、ノーツだけで動かす
            s.gainPerItem = 0.02f;
            s.gainPerSecond = 0f;
            s.decayPerSecond = 0f;
            s.comboGainRate = 0f;
            s.maxComboMultiplier = 1f;
            s.damagePenalty = 0.1f;

            s.feverDuration = 8f;
            s.drainDuringFever = true;
            s.superFeverDuration = 12f;
            s.feversPerSong = 3;
            EditorUtility.SetDirty(s);
        }

        private static void CreateScoreSettings()
        {
            var s = LoadOrCreate<ScoreSettings>(ScoreSettingsPath);
            s.comboRate = 0.02f;
            s.comboBonusMax = 2f;
            s.gaugeBonusMax = 1f;
            s.rivalDefeatBonus = 0.5f;
            s.feverMultiplier = 1.5f;
            s.encoreMultiplier = 2f;
            s.superFeverMultiplier = 4f;
            s.maxMultiplier = 10f;
            s.rivalHearts = 5;
            s.rivalMaxHp = 50;
            s.rivalDamagePerHit = 1;
            EditorUtility.SetDirty(s);
        }

        /// <summary>
        /// Game_Run 専用の Volume Profile。threshold を 1.0 にして、
        /// HDRで描いたペンライト・レーン線・白い球だけが光り、床や空やキャラは光らないようにする。
        /// 共有の Global Volume Profile 1 は Title や旧 Game でも使われているので書き換えない。
        /// </summary>
        private static void CreateVolumeProfile()
        {
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath) != null) return;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "GameRunVolume";
            AssetDatabase.CreateAsset(profile, VolumeProfilePath);

            var bloom = profile.Add<Bloom>(true);
            bloom.name = "Bloom";
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(4f);
            bloom.scatter.Override(0.6f);
            bloom.clamp.Override(30f);

            AssetDatabase.AddObjectToAsset(bloom, profile);
            EditorUtility.SetDirty(profile);
        }

        /// <summary>
        /// 難易度ごとの設定。perfectScore は自動プレイ（ノーミス）で測った値。
        /// 譜面やスコア計算を変えたら、自動プレイテストの出力を見て直すこと。
        /// </summary>
        private static void CreateDifficultySettings()
        {
            var settings = LoadOrCreate<DifficultySettings>(DifficultySettingsPath);

            var rows = new[]
            {
                (RunDifficulty.Easy,     "Easy",     "StageData_Easy",     0.75f, 7500,  Hex("#5FC8FF"), false),
                (RunDifficulty.Normal,   "Normal",   "StageData_Normal",   0.85f, 14450, Hex("#7AD47A"), false),
                (RunDifficulty.Hard,     "Hard",     "StageData_Hard",     0.90f, 28900, Hex("#FF8A5B"), false),
                (RunDifficulty.Universe, "Universe", "StageData_Universe", 0.90f, 28450, Hex("#B56BFF"), true),
            };

            SetObjectArray(settings, "entries", rows.Length, (e, i) =>
            {
                var r = rows[i];
                SetRelative(e, "difficulty", (int)r.Item1);
                SetRelative(e, "displayName", r.Item2);
                SetRelative(e, "chartFile", r.Item3);
                SetRelative(e, "rivalDefeatAt", r.Item4);
                SetRelative(e, "perfectScore", r.Item5);
                SetRelative(e, "themeColor", r.Item6);
                SetRelative(e, "locked", r.Item7);
                SetRelative(e, "unlockBy", (int)RunDifficulty.Hard);
                SetRelative(e, "unlockRank", (int)RunRank.SS);
            });

            EditorUtility.SetDirty(settings);
        }

        private static void CreateRankSettings()
        {
            var settings = LoadOrCreate<RankSettings>(RankSettingsPath);

            // 上から順に判定する。ノーミス時のスコアに対する割合
            var rows = new[]
            {
                (RunRank.SSPlus, "SS+", 0.95f, Hex("#FF6FB5")),
                (RunRank.SS,     "SS",  0.85f, Hex("#B56BFF")),
                (RunRank.S,      "S",   0.70f, Hex("#FFB531")),
                (RunRank.A,      "A",   0.55f, Hex("#FF7A59")),
                (RunRank.B,      "B",   0.40f, Hex("#4FB8FF")),
                (RunRank.C,      "C",   0.00f, Hex("#8C8CA8")),
            };

            SetObjectArray(settings, "tiers", rows.Length, (e, i) =>
            {
                var r = rows[i];
                SetRelative(e, "rank", (int)r.Item1);
                SetRelative(e, "label", r.Item2);
                SetRelative(e, "threshold", r.Item3);
                SetRelative(e, "color", r.Item4);
            });

            EditorUtility.SetDirty(settings);
        }

        // =====================================================================
        // 材質
        // =====================================================================

        private static void CreateMaterials()
        {
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null) throw new Exception("URP の Unlit シェーダが見つかりません");

            // レーン面は暗い色。光らない（輝度1.0未満）
            CreateOrUpdateMaterial(LaneSurfaceMatPath, unlit, new Color(0.07f, 0.08f, 0.17f));

            // レーン線と白い球は HDR で光らせる。Unlit の _BaseColor に1超えを入れると Bloom に乗る
            CreateOrUpdateMaterial(LaneLineMatPath, unlit, new Color(0.55f, 1.98f, 2.2f));
            CreateOrUpdateMaterial(OrbMatPath, unlit, new Color(3f, 3f, 3.2f));
        }

        private static void CreateOrUpdateMaterial(string path, Shader shader, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            EditorUtility.SetDirty(material);
        }

        // =====================================================================
        // プレハブ
        //
        // 見た目は既存アセットをそのまま子として入れ、当たり判定とゲームロジックは
        // 自分で作った親側に持たせる。モデルの差し替えやスケール調整で当たり判定が壊れない。
        // =====================================================================

        private static void BuildItemPrefab()
        {
            var root = new GameObject("Run_Item");
            try
            {
                AttachArt(root, ArtItem);

                var col = root.AddComponent<SphereCollider>();
                col.radius = 0.7f; // レーン幅2mなので隣のレーンまでは届かない
                col.isTrigger = true;

                var item = root.AddComponent<BeatItem>();
                SetField(item, "gaugeGain", 0f); // 0以下 → RunBalance が決めた値を使う
                SetField(item, "score", 10);

                SavePrefab(root, RunItemPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildObstaclePrefab()
        {
            var root = new GameObject("Run_Obstacle");
            try
            {
                AttachArt(root, ArtObstacle);

                // 高さ2mのカプセル。ジャンプでは越えられない＝横に避ける障害物
                var col = root.AddComponent<CapsuleCollider>();
                col.radius = 0.5f;
                col.height = 2f;
                col.direction = 1;
                col.isTrigger = true;

                var obstacle = root.AddComponent<Obstacle>();
                SetField(obstacle, "damage", 1);
                SetField(obstacle, "despawnOnHit", true);

                SavePrefab(root, RunObstaclePath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildJumpObstaclePrefab()
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
                SetField(obstacle, "despawnOnHit", false);

                SavePrefab(root, RunJumpObstaclePath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// 床タイル1枚の片側に並ぶペンライト1列。既存の `Particle System.prefab` を子として抱え、
        /// 並べ方だけ上書きする（元のプレハブは触らない）。
        ///   ・Simulation Space を World に（タイルを前に回したとき粒が付いてこないように）
        ///   ・Box emitter を「タイル1枚ぶんの細い帯」に
        ///   ・1回のバースト数を現実的な数に（元は50000個）
        ///   ・粒ごとに回せるようにする（PenlightSway が曲に合わせて左右に振る）
        /// </summary>
        private static void BuildPenlightPrefab()
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
                main.startSpeed = 0f;
                main.maxParticles = PenlightPerStrip * 2;
                main.startLifetime = 60f;
                main.startRotation3D = true; // 左右に振るため、粒ごとの回転を有効にする
                main.startRotationX = 0f;
                main.startRotationY = 0f;
                main.startRotationZ = 0f;

                // 初期色。実際の色は PenlightSway が毎フレームステージの状態に合わせて書く
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.2f, 0.9f, 1f));

                var emission = ps.emission;
                emission.enabled = true;
                emission.rateOverTime = 0f;
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
                    renderer.sharedMaterial = CreatePenlightMaterial(renderer.sharedMaterial);
                    // ワールド基準で回す（カメラ基準だと左右に振っているように見えない）
                    renderer.alignment = ParticleSystemRenderSpace.World;
                }

                var strip = root.AddComponent<PenlightStrip>();
                SetField(strip, "ps", ps);

                var sway = root.AddComponent<PenlightSway>();
                SetField(sway, "ps", ps);
                SetField(sway, "amplitude", 22f);
                SetField(sway, "beatsPerSway", 2f);

                SavePrefab(root, RunPenlightPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// ペンライト用マテリアル。既存 PenlightMat は旧 Game シーンでも使われているので複製して調整する。
        ///
        /// 粒ごとの色は8bit(Color32)で1.0を超えられないため、HDRの明るさはここ(_BaseColor)に持たせる。
        /// 粒の色(色相) × この明るさ が最終的な色になり、Bloom の閾値(1.0)を超えて光る。
        /// </summary>
        private static Material CreatePenlightMaterial(Material source)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(PenlightMatPath);
            if (material == null)
            {
                if (source == null) throw new Exception("元のペンライトマテリアルが取得できません");
                material = new Material(source) { name = "PenlightMat_Run" };
                AssetDatabase.CreateAsset(material, PenlightMatPath);
            }

            var bright = new Color(PenlightBrightness, PenlightBrightness, PenlightBrightness, 1f);
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", bright);
            if (material.HasProperty("_Color")) material.SetColor("_Color", bright);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// 床タイル1枚ぶんのレーン（暗い路面5本と、光る境界線6本）。
        /// 床のアセットの上に1枚重ねる。色は StageTheme が状態に合わせて変える。
        /// </summary>
        private static void BuildLaneTrackPrefab()
        {
            var root = new GameObject("Run_LaneTrack");
            try
            {
                var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                var surface = Reload<Material>(LaneSurfaceMatPath);
                var line = Reload<Material>(LaneLineMatPath);

                var lanes = new Renderer[StageTheme.LaneCount];
                for (int i = 0; i < lanes.Length; i++)
                {
                    float x = (i - (lanes.Length - 1) * 0.5f) * LaneWidth;
                    lanes[i] = CreateQuad(root.transform, "Lane" + i, quad, surface,
                        new Vector3(x, 0f, 0f), new Vector2(LaneWidth, TileLength));
                }

                var lines = new Renderer[StageTheme.LineCount];
                for (int i = 0; i < lines.Length; i++)
                {
                    float x = (i - (lines.Length - 1) * 0.5f) * LaneWidth;
                    // 端の2本は太く。レーンの外枠がはっきりする
                    float width = i == 0 || i == lines.Length - 1 ? 0.22f : 0.1f;
                    lines[i] = CreateQuad(root.transform, "Line" + i, quad, line,
                        new Vector3(x, 0.01f, 0f), new Vector2(width, TileLength));
                }

                var track = root.AddComponent<LaneTrack>();
                SetArrayField(track, "lanes", lanes);
                SetArrayField(track, "lines", lines);

                SavePrefab(root, RunLaneTrackPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Renderer CreateQuad(Transform parent, string name, Mesh mesh, Material material,
            Vector3 position, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // 上向きに寝かせる
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        /// <summary>ライバルへ飛んでいく白い球。光る球と、短い尾。</summary>
        private static void BuildOrbPrefab()
        {
            var root = new GameObject("Run_BeatOrb");
            try
            {
                var material = Reload<Material>(OrbMatPath);

                var sphere = new GameObject("Sphere");
                sphere.transform.SetParent(root.transform, false);
                sphere.transform.localScale = Vector3.one * 0.45f;
                sphere.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
                var renderer = sphere.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;

                var trail = root.AddComponent<TrailRenderer>();
                trail.sharedMaterial = material;
                trail.time = 0.18f;
                trail.startWidth = 0.4f;
                trail.endWidth = 0f;
                trail.minVertexDistance = 0.2f;
                trail.shadowCastingMode = ShadowCastingMode.Off;

                SavePrefab(root, RunOrbPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>既存の見た目プレハブを子として付け、そちらのColliderは無効化する。</summary>
        private static void AttachArt(GameObject root, string artPath)
        {
            var art = InstantiateArt(artPath);
            art.transform.SetParent(root.transform, false);

            // 【回転は絶対に触らない】Obstacle.prefab はZ-upで作られたモデルを
            // 立たせるために -90°(X) の補正回転を持っている。identity にすると倒れる
            art.transform.localPosition = Vector3.zero;

            foreach (var col in art.GetComponentsInChildren<Collider>(true)) col.enabled = false;
        }

        // =====================================================================
        // シーン
        // =====================================================================

        private static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // 【アセットの取得はシーンを作った後に行うこと】NewScene は未参照アセットを
            // メモリから破棄するので、先に読んでおいた参照は破棄済みオブジェクトになる
            var runSettings = Reload<RunSettings>(RunSettingsPath);
            var gaugeSettings = Reload<GaugeSettings>(GaugeSettingsPath);
            var scoreSettings = Reload<ScoreSettings>(ScoreSettingsPath);

            // --- プレイヤー ---
            var player = new GameObject("Player");
            player.transform.position = new Vector3(0f, PlayerGroundY, 0f);

            var body = player.AddComponent<BoxCollider>();
            body.size = new Vector3(BodyWidth, BodyHeight, BodyWidth);
            body.center = new Vector3(0f, BodyHeight * 0.5f + ModelYOffset, 0f);
            body.isTrigger = false;

            var rb = player.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var model = InstantiateArt(ArtPlayer);
            model.name = "PlayerModel";
            model.transform.SetParent(player.transform, false);
            model.transform.localPosition = new Vector3(0f, ModelYOffset, 0f);
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * ModelScale;
            FixPlayerModelChildren(model);

            var inputGo = new GameObject("InputSource");
            var input = inputGo.AddComponent<KeyboardRunInput>();

            var runner = player.AddComponent<PlayerRunner>();
            SetField(runner, "settings", runSettings);
            SetField(runner, "inputSource", input);

            // --- 床・レーン・ペンライト ---
            var groundGo = new GameObject("Ground");
            var tiler = groundGo.AddComponent<GroundTiler>();
            SetField(tiler, "player", runner);
            SetField(tiler, "groundPrefab", Reload<GameObject>(ArtFloor));
            SetField(tiler, "tileLength", TileLength);
            SetField(tiler, "pivotAtCenter", true);
            SetField(tiler, "tileCount", 3);
            SetField(tiler, "recycleBehindDistance", 10f);
            SetField(tiler, "groundY", FloorTopY - FloorThickness * 0.5f);
            SetArrayField(tiler, "sideProps", new Object[] { Reload<GameObject>(RunPenlightPath) });
            SetField(tiler, "sidePropOffsetX", PenlightOffsetX);
            SetField(tiler, "sidePropY", FloorTopY);
            SetArrayField(tiler, "centerProps", new Object[] { Reload<GameObject>(RunLaneTrackPath) });
            SetField(tiler, "centerPropY", FloorTopY + 0.02f);

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
            var notePrefabs = new[]
            {
                ("Item", Reload<GameObject>(RunItemPath), 1.0f, false),
                ("Obstacle", Reload<GameObject>(RunObstaclePath), 1.0f, false),
                ("JumpObstacle", Reload<GameObject>(RunJumpObstaclePath), 0.5f, true),
            };
            SetObjectArray(spawner, "notePrefabs", notePrefabs.Length, (e, i) =>
            {
                SetRelative(e, "type", notePrefabs[i].Item1);
                SetRelative(e, "prefab", notePrefabs[i].Item2);
                SetRelative(e, "spawnY", notePrefabs[i].Item3);
                SetRelative(e, "forceCenterLane", notePrefabs[i].Item4);
            });

            // --- ゲージ（フィーバー／ENCORE の状態も持つ） ---
            var gaugeGo = new GameObject("GaugeController");
            var gauge = gaugeGo.AddComponent<BeatGaugeController>();
            SetField(gauge, "settings", gaugeSettings);
            SetField(gauge, "player", runner);

            // --- ライバル（奥に立つ姿と、体力・ハート） ---
            var rivalRoot = new GameObject("Rival");
            var rival = rivalRoot.AddComponent<RunRival>();
            SetField(rival, "settings", scoreSettings);
            SetField(rival, "gauge", gauge);
            SetField(rival, "rivalName", "ライバル");

            var rivalModel = BuildRivalModel(rivalRoot.transform);

            var hitPoint = new GameObject("HitPoint").transform;
            hitPoint.SetParent(rivalRoot.transform, false);
            hitPoint.localPosition = new Vector3(0f, RivalHeight * 0.6f, 0f);

            var rivalView = rivalRoot.AddComponent<RivalView>();
            SetField(rivalView, "player", runner);
            SetField(rivalView, "rival", rival);
            SetField(rivalView, "model", rivalModel);
            SetField(rivalView, "distanceAhead", RivalDistance);
            SetField(rivalView, "groundY", FloorTopY);
            rivalRoot.transform.position = new Vector3(0f, FloorTopY, RivalDistance);

            // --- スコア ---
            var scoreGo = new GameObject("Score");
            var score = scoreGo.AddComponent<RunScore>();
            SetField(score, "settings", scoreSettings);
            SetField(score, "player", runner);
            SetField(score, "gauge", gauge);
            SetField(score, "rival", rival);

            // --- 譜面に合わせたバランス調整 ---
            var balance = new GameObject("Balance").AddComponent<RunBalance>();
            SetField(balance, "spawner", spawner);
            SetField(balance, "rival", rival);
            SetField(balance, "gauge", gauge);
            SetField(balance, "runSettings", runSettings);
            SetField(balance, "gaugeSettings", gaugeSettings);
            SetField(balance, "scoreSettings", scoreSettings);

            // --- 白い球 ---
            var launcher = new GameObject("OrbLauncher").AddComponent<BeatOrbLauncher>();
            SetField(launcher, "player", runner);
            SetField(launcher, "rival", rival);
            SetField(launcher, "target", hitPoint);
            SetField(launcher, "orbPrefab", Reload<GameObject>(RunOrbPath));

            // --- BGM（開始演出の FIGHT で鳴らし始める） ---
            var bgm = new GameObject("BGM").AddComponent<AudioSource>();
            bgm.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(BgmPath);
            bgm.playOnAwake = false;
            bgm.loop = false;

            // --- カメラ ---
            var camera = Camera.main;
            if (camera != null)
            {
                camera.transform.position = new Vector3(0f, 6f, -9f);
                camera.transform.rotation = Quaternion.Euler(20f, 0f, 0f);
                camera.farClipPlane = 300f;

                // 空を描かず単色で塗る。キャラの環境光は空(RenderSettings.skybox)から取るので暗くならない
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = StageBackground;

                // Unity のオブジェクトに ?? は使わない（破棄済みを null と判定しないことがある）
                var follow = camera.gameObject.GetComponent<CameraController>();
                if (follow == null) follow = camera.gameObject.AddComponent<CameraController>();
                follow.player = player.transform;

                // これが無いと Volume の Bloom が一切掛からない
                var cameraData = camera.GetUniversalAdditionalCameraData();
                if (cameraData != null) cameraData.renderPostProcessing = true;
            }

            // 遠くのレーンとペンライトを背景色に溶かす。奥のライバル(約73m)が
            // 見えなくならないよう、終わりは十分遠くにする
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = StageBackground;
            RenderSettings.fogStartDistance = 45f;
            RenderSettings.fogEndDistance = 170f;

            // --- ポストプロセス（Game_Run 専用のプロファイル） ---
            var volumeGo = InstantiateArt(ArtGlobalVolume);
            volumeGo.name = "Global Volume";
            volumeGo.transform.position = Vector3.zero;
            var volume = volumeGo.GetComponent<Volume>();
            if (volume != null) volume.sharedProfile = Reload<VolumeProfile>(VolumeProfilePath);

            // --- ステージの色（通常／フィーバー／ENCORE／スーパー） ---
            var theme = new GameObject("StageTheme").AddComponent<StageTheme>();
            SetField(theme, "gauge", gauge);
            SetField(theme, "volume", volume);
            SetField(theme, "laneMaterial", Reload<Material>(LaneSurfaceMatPath));
            SetField(theme, "lineMaterial", Reload<Material>(LaneLineMatPath));
            ApplyPalettes(theme);

            // --- 効果音 ---
            var se = new GameObject("SeHooks").AddComponent<RunSeHooks>();
            SetField(se, "player", runner);
            SetField(se, "rival", rival);
            SetField(se, "gauge", gauge);

            // --- 終了判定（遷移は RunFinishSequence が行うので、ここでは遷移しない） ---
            var goal = new GameObject("GoalWatcher").AddComponent<RunGoalWatcher>();
            SetField(goal, "condition", (int)RunClearCondition.ChartEnd);
            SetField(goal, "player", runner);
            SetField(goal, "spawner", spawner);
            SetField(goal, "score", score);
            SetField(goal, "bgmSource", bgm);
            SetField(goal, "goalMarginZ", 15f);
            SetField(goal, "resultSceneName", string.Empty);
            SetField(goal, "transitionDelay", 0f);

            // --- UI ---
            var canvas = CreateCanvas("Canvas");
            BuildHud(canvas.transform, gauge, score, rival, runner);
            BuildIntro(canvas.transform, runner, rival, bgm);
            BuildFinish(canvas.transform, goal, runner, score, rival, spawner);
            EnsureEventSystem();

            EditorSceneManager.SaveScene(scene, ScenePath);

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(LegacyScenePath) != null)
            {
                AssetDatabase.DeleteAsset(LegacyScenePath);
            }
        }

        /// <summary>
        /// ライバルのモデル（MINIus）。遠くでも見えるよう高さを揃え、カメラの方を向かせる。
        /// モデルごとに原点や大きさがまちまちなので、描画範囲を測って足元を地面に合わせる。
        /// </summary>
        private static Transform BuildRivalModel(Transform parent)
        {
            var holder = new GameObject("Model").transform;
            holder.SetParent(parent, false);

            var model = InstantiateArt(ArtRival);
            model.transform.SetParent(holder, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // カメラ（手前）を向く

            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

                if (bounds.size.y > 0.01f)
                {
                    float scale = RivalHeight / bounds.size.y;
                    model.transform.localScale *= scale;

                    // 縮尺を変えた後の足元を y=0 に合わせる
                    float bottom = (bounds.min.y - holder.position.y) * scale;
                    model.transform.localPosition = new Vector3(0f, -bottom, 0f);
                }
            }

            return holder;
        }

        /// <summary>
        /// PlayerModel.prefab は中身(Lily / Hair 4)のlocalPositionが (10019, -37621, -37764) で
        /// 保存されており、そのまま配置するとモデルが37km先に飛ぶ。既存Gameシーンと同じく0に直す。
        /// </summary>
        private static void FixPlayerModelChildren(GameObject model)
        {
            foreach (Transform child in model.transform)
            {
                if (child.localPosition.sqrMagnitude > 1f) child.localPosition = Vector3.zero;
            }
        }

        /// <summary>
        /// ステージの配色。明るさは状態によらず同じにして、色相だけを変える
        /// （本家も ENCORE で金色基調になるだけで眩しくはならない）。
        /// </summary>
        private static void ApplyPalettes(StageTheme theme)
        {
            const float glow = 2.2f;

            SetPalette(theme, "normal",
                line: new Color(0.25f, 0.9f, 1f) * glow,
                laneA: new Color(0.06f, 0.07f, 0.16f),
                laneB: new Color(0.09f, 0.10f, 0.21f),
                bloomTint: new Color(0.85f, 0.95f, 1f),
                // 本家どおり水色が主役。差し色にピンクを少しだけ。
                // 「一番強い成分が1、残りは低め」にする。材質側で少し明るくするので、
                // 弱い成分が高いと白っぽく飽和する
                penlights: new[]
                {
                    new Color(0.00f, 0.90f, 1.00f), // 水色
                    new Color(0.10f, 1.00f, 0.80f), // 青緑
                    new Color(0.35f, 0.75f, 1.00f), // 空色
                    new Color(0.00f, 0.90f, 1.00f), // 水色（多めに出す）
                    new Color(1.00f, 0.30f, 0.70f), // ピンク（差し色）
                });

            SetPalette(theme, "fever",
                line: new Color(1f, 0.35f, 0.85f) * glow,
                laneA: new Color(0.14f, 0.05f, 0.16f),
                laneB: new Color(0.18f, 0.07f, 0.20f),
                bloomTint: new Color(1f, 0.85f, 0.95f),
                penlights: new[]
                {
                    new Color(1.00f, 0.25f, 0.65f), // ピンク
                    new Color(1.00f, 0.15f, 0.95f), // マゼンタ
                    new Color(0.80f, 0.35f, 1.00f), // 紫
                    new Color(0.00f, 0.90f, 1.00f), // 水色
                });

            SetPalette(theme, "encore",
                line: new Color(1f, 0.78f, 0.25f) * glow,
                laneA: new Color(0.55f, 0.38f, 0.06f),
                laneB: new Color(0.62f, 0.45f, 0.10f),
                bloomTint: new Color(1f, 0.88f, 0.65f),
                // 本家どおり客席ごと橙〜金に
                penlights: new[]
                {
                    new Color(1.00f, 0.50f, 0.00f), // 橙
                    new Color(1.00f, 0.72f, 0.05f), // 琥珀
                    new Color(1.00f, 0.92f, 0.20f), // 黄
                    new Color(1.00f, 0.60f, 0.15f), // 金
                });
        }

        private static void SetPalette(StageTheme theme, string field, Color line, Color laneA, Color laneB,
            Color bloomTint, Color[] penlights)
        {
            var so = new SerializedObject(theme);
            so.FindProperty(field + ".line").colorValue = line;
            so.FindProperty(field + ".laneA").colorValue = laneA;
            so.FindProperty(field + ".laneB").colorValue = laneB;
            so.FindProperty(field + ".bloomTint").colorValue = bloomTint;

            var array = so.FindProperty(field + ".penlights");
            array.arraySize = penlights.Length;
            for (int i = 0; i < penlights.Length; i++) array.GetArrayElementAtIndex(i).colorValue = penlights[i];

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // =====================================================================
        // HUD
        //
        // 配置は本家に寄せてある。
        //   最上部     … キラメキビートゲージ（オリジナル要素なので一番上）
        //   そのすぐ下 … ライバルの体力バー（名前とハート付きの枠）
        //   左上       … COMBO（大）／倍率／SCORE／LIFE
        //   中央       … FEVER / ENCORE TIME / SUPER FEVER!! のバナー
        // 全て仮の見た目。Image にスプライトを入れれば差し替わる。
        // =====================================================================

        private static readonly Color Ink = new Color(0.22f, 0.16f, 0.42f);
        private static readonly Color FramePurple = new Color(0.36f, 0.25f, 0.62f, 0.92f);

        private static void BuildHud(Transform canvas, BeatGaugeController gauge, RunScore score,
            RunRival rival, PlayerRunner player)
        {
            var hud = Stretch(CreateRect(canvas, "HUD"));
            var top = new Vector2(0.5f, 1f);
            var topLeft = new Vector2(0f, 1f);

            // --- 上部の枠（本家の紫の枠）。上段にキラメキビート、下段にライバルの体力 ---
            var frame = CreatePanel(hud, "TopFrame", FramePurple, top, new Vector2(0f, -10f), new Vector2(900f, 112f));

            CreateText(frame.transform, "GaugeLabel", "キラメキビート", 22f, new Color(1f, 0.85f, 0.35f),
                new Vector2(0f, 1f), new Vector2(22f, -12f), new Vector2(200f, 30f),
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

            var (gaugeSlider, gaugeFill) = CreateBar(frame.transform, "BeatGaugeSlider",
                new Vector2(1f, 1f), new Vector2(-24f, -16f), new Vector2(640f, 22f), new Color(1f, 1f, 1f, 0.25f));

            var gaugeView = gaugeSlider.gameObject.AddComponent<BeatGaugeView>();
            SetField(gaugeView, "controller", gauge);
            SetField(gaugeView, "slider", gaugeSlider);
            SetField(gaugeView, "fillImage", gaugeFill);
            SetField(gaugeView, "normalColor", new Color(0.35f, 0.8f, 1f));
            SetField(gaugeView, "feverColor", new Color(1f, 0.45f, 0.85f));

            var rivalName = CreateText(frame.transform, "RivalNameText", "ライバル", 24f, Color.white,
                new Vector2(0f, 0f), new Vector2(22f, 14f), new Vector2(200f, 36f),
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

            var (rivalBar, rivalFill) = CreateBar(frame.transform, "RivalHpBar",
                new Vector2(0.5f, 0f), new Vector2(10f, 22f), new Vector2(440f, 20f), new Color(1f, 1f, 1f, 0.25f));

            var hearts = CreateText(frame.transform, "RivalHeartsText", "♥♥♥♥♥", 26f, new Color(1f, 0.45f, 0.65f),
                new Vector2(1f, 0f), new Vector2(-24f, 14f), new Vector2(170f, 36f), TextAlignmentOptions.MidlineRight);

            // --- 左上のスコア類 ---
            var combo = CreateText(hud, "ComboText", "0", 88f, Color.white,
                topLeft, new Vector2(40f, -120f), new Vector2(360f, 96f), TextAlignmentOptions.TopLeft, FontStyles.Bold);
            CreateText(hud, "ComboLabel", "COMBO", 24f, Color.white,
                topLeft, new Vector2(46f, -212f), new Vector2(360f, 30f), TextAlignmentOptions.TopLeft, FontStyles.Bold);
            var multiplier = CreateText(hud, "MultiplierText", "x1.00", 30f, new Color(1f, 0.85f, 0.35f),
                topLeft, new Vector2(46f, -246f), new Vector2(360f, 36f), TextAlignmentOptions.TopLeft, FontStyles.Bold);

            var scorePill = CreatePanel(hud, "ScorePill", new Color(1f, 1f, 1f, 0.88f),
                topLeft, new Vector2(30f, -294f), new Vector2(340f, 74f));
            CreateText(scorePill.transform, "ScoreLabel", "SCORE", 18f, new Color(0.85f, 0.35f, 0.6f),
                topLeft, new Vector2(16f, -6f), new Vector2(200f, 24f), TextAlignmentOptions.TopLeft, FontStyles.Bold);
            var scoreText = CreateText(scorePill.transform, "ScoreText", "0", 36f, Ink,
                new Vector2(1f, 0f), new Vector2(-16f, 6f), new Vector2(300f, 44f), TextAlignmentOptions.BottomRight, FontStyles.Bold);

            var life = CreateText(hud, "LifeText", "♥♥♥♥♥", 34f, new Color(1f, 0.35f, 0.55f),
                topLeft, new Vector2(40f, -378f), new Vector2(300f, 40f), TextAlignmentOptions.TopLeft);
            CreateText(hud, "LifeLabel", "LIFE", 18f, Color.white,
                topLeft, new Vector2(42f, -418f), new Vector2(120f, 24f), TextAlignmentOptions.TopLeft, FontStyles.Bold);

            // --- バナー。文字の後ろに暗い帯を敷く（金色のレーンの上だと金色の文字が溶けるため） ---
            var ribbon = CreatePanel(hud, "Banner", new Color(0.08f, 0.04f, 0.2f, 0.7f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(2400f, 150f));
            var bannerGroup = ribbon.gameObject.AddComponent<CanvasGroup>();
            var banner = CreateText(ribbon.transform, "BannerText", "FEVER!", 96f, Color.white,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 130f),
                TextAlignmentOptions.Center, FontStyles.Bold | FontStyles.Italic);
            ribbon.gameObject.SetActive(false);

            var view = hud.gameObject.AddComponent<RunHudView>();
            SetField(view, "score", score);
            SetField(view, "rival", rival);
            SetField(view, "player", player);
            SetField(view, "gauge", gauge);
            SetField(view, "rivalHpBar", rivalBar);
            SetField(view, "rivalHpFill", rivalFill);
            SetField(view, "rivalNameText", rivalName);
            SetField(view, "rivalHeartsText", hearts);
            SetField(view, "scoreText", scoreText);
            SetField(view, "comboText", combo);
            SetField(view, "multiplierText", multiplier);
            SetField(view, "lifeText", life);
            SetField(view, "bannerText", banner);
            SetField(view, "bannerGroup", bannerGroup);
        }

        /// <summary>
        /// 開始演出（VS カットイン → Let's FIGHT!）。立ち絵は仮の色板なので、
        /// PlayerPortrait / RivalPortrait の Image にスプライトを入れれば差し替わる。
        /// </summary>
        private static RunIntroSequence BuildIntro(Transform canvas, PlayerRunner player, RunRival rival, AudioSource bgm)
        {
            var root = Stretch(CreateRect(canvas, "Intro"));
            var center = new Vector2(0.5f, 0.5f);

            var versus = Stretch(CreateRect(root, "Versus"));
            var group = versus.gameObject.AddComponent<CanvasGroup>();
            CreateFullscreen(versus, "Dim", new Color(0.1f, 0.05f, 0.2f, 0.45f));

            var (playerCard, playerPortrait, playerName, playerLine) =
                BuildVersusCard(versus, "PlayerCard", new Vector2(-470f, 10f), Hex("#5FC8FF"), Hex("#2D6FD1"));
            var (rivalCard, rivalPortrait, rivalName, rivalLine) =
                BuildVersusCard(versus, "RivalCard", new Vector2(470f, 10f), Hex("#FF8FC8"), Hex("#C93D8B"));

            var vs = CreateText(versus, "VS", "VS", 200f, new Color(1f, 0.82f, 0.25f),
                center, new Vector2(0f, 40f), new Vector2(420f, 240f), TextAlignmentOptions.Center,
                FontStyles.Bold | FontStyles.Italic);

            var fight = CreateText(root, "FightText", "Let's FIGHT!", 140f, Color.white,
                center, new Vector2(0f, 60f), new Vector2(1600f, 200f), TextAlignmentOptions.Center,
                FontStyles.Bold | FontStyles.Italic);

            var intro = root.gameObject.AddComponent<RunIntroSequence>();
            SetField(intro, "player", player);
            SetField(intro, "rival", rival);
            SetField(intro, "bgm", bgm);
            SetField(intro, "versusGroup", group);
            SetField(intro, "playerCard", playerCard);
            SetField(intro, "rivalCard", rivalCard);
            SetField(intro, "vsMark", vs.rectTransform);
            SetField(intro, "playerNameText", playerName);
            SetField(intro, "rivalNameText", rivalName);
            SetField(intro, "playerLineText", playerLine);
            SetField(intro, "rivalLineText", rivalLine);
            SetField(intro, "playerPortrait", playerPortrait);
            SetField(intro, "rivalPortrait", rivalPortrait);
            SetField(intro, "fightText", fight);
            return intro;
        }

        private static (RectTransform card, Image portrait, TextMeshProUGUI name, TextMeshProUGUI line)
            BuildVersusCard(Transform parent, string name, Vector2 position, Color light, Color dark)
        {
            var card = CreatePanel(parent, name, light, new Vector2(0.5f, 0.5f), position, new Vector2(820f, 520f));

            // 立ち絵（仮）。スプライトを入れれば差し替わる
            var portrait = CreatePanel(card.transform, name.Replace("Card", "Portrait"), dark,
                new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(360f, 340f));
            CreateText(portrait.transform, "Placeholder", "立ち絵（仮）", 26f, new Color(1f, 1f, 1f, 0.7f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(340f, 60f));

            var nameText = CreateText(card.transform, "Name", "名前", 40f, Color.white,
                new Vector2(0.5f, 0f), new Vector2(0f, 118f), new Vector2(760f, 56f),
                TextAlignmentOptions.Center, FontStyles.Bold);

            var bubble = CreatePanel(card.transform, "Bubble", Color.white,
                new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(740f, 86f));
            var lineText = CreateText(bubble.transform, "Line", "セリフ", 28f, Ink,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 70f));

            return (card.rectTransform, portrait, nameText, lineText);
        }

        private static void BuildFinish(Transform canvas, RunGoalWatcher goal, PlayerRunner player,
            RunScore score, RunRival rival, RunSpawner spawner)
        {
            var finishText = CreateText(canvas, "FinishText", "FINISH", 170f, Color.white,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1600f, 220f),
                TextAlignmentOptions.Center, FontStyles.Bold | FontStyles.Italic);

            var finish = new GameObject("FinishSequence").AddComponent<RunFinishSequence>();
            SetField(finish, "goal", goal);
            SetField(finish, "player", player);
            SetField(finish, "score", score);
            SetField(finish, "rival", rival);
            SetField(finish, "spawner", spawner);
            SetField(finish, "finishText", finishText);
        }
    }
}
