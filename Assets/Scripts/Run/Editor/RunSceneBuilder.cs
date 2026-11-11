using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;
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
        private const string ScoreSettingsPath = SettingsDir + "/ScoreSettings.asset";
        private const string VolumeProfilePath = SettingsDir + "/GameRunVolume.asset";

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

        // UIに日本語を出すためのフォント。TMPの既定 LiberationSans には日本語グリフが無い
        private const string JpFontFilePath = "Assets/Fonts/NotoSansJP-VF.ttf";
        private const string JpFontAssetPath = "Assets/Fonts/NotoSansJP SDF.asset";
        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

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

        /// <summary>
        /// ペンライトの輝度。Bloom の threshold(1.0) を超えないと光らないので、
        /// 少し上に置く。上げすぎると芯が白く飛んで色が分からなくなる。
        /// </summary>
        private const float PenlightLuminance = 2.0f;

        /// <summary>UIで使う日本語フォント。BuildAll で用意する。</summary>
        private static TMP_FontAsset japaneseFont;

        [MenuItem("Yakiniku/Run/Game_Run シーンを生成", false, 10)]
        public static void BuildAll()
        {
            try
            {
                EnsureFolders();

                // フォントを先に作る。TMP_FontAsset.CreateFontAsset はアセットデータベースを
                // 作り直すので、先に取得しておいた参照が無効になる（= 破棄済みオブジェクト）。
                // その状態で SerializedProperty に入れると黙って null が書き込まれる。
                japaneseFont = CreateJapaneseFontAsset();
                RegisterFontFallback(japaneseFont);

                CreateRunSettings();
                CreateGaugeSettings();
                CreateScoreSettings();
                CreateVolumeProfile();

                BuildItemPrefab();
                BuildObstaclePrefab();
                BuildJumpObstaclePrefab();
                BuildPenlightPrefab();

                AssetDatabase.SaveAssets();

                BuildScene();
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
            settings.superFeverDuration = 12f;

            EditorUtility.SetDirty(settings);
            return settings;
        }

        /// <summary>
        /// 日本語を表示できる TMP Font Asset を作る。
        ///
        /// 日本語は常用漢字だけでも数千字あるので、全字をアトラスに焼くと巨大になる。
        /// Atlas Population Mode を Dynamic にして、実際に表示された字だけを
        /// 実行時にアトラスへ足していく方式にしている。
        ///
        /// フォントは Noto Sans JP（SIL Open Font License 1.1）。
        /// Meiryo や Yu Gothic はビルドに同梱できないライセンスなので使わないこと。
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

            var asset = TMP_FontAsset.CreateFontAsset(
                source,
                samplingPointSize: 90,
                atlasPadding: 9,
                renderMode: GlyphRenderMode.SDFAA,
                atlasWidth: 1024,
                atlasHeight: 1024,
                atlasPopulationMode: AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: true);

            if (asset == null) throw new Exception("TMP Font Asset を作成できません");

            asset.name = "NotoSansJP SDF";
            AssetDatabase.CreateAsset(asset, JpFontAssetPath);

            // アトラステクスチャとマテリアルはサブアセットとして一緒に保存する。
            // ここを省くと参照切れのフォントアセットが出来上がる
            if (asset.atlasTextures != null && asset.atlasTextures.Length > 0
                && asset.atlasTextures[0] != null)
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

            Debug.Log("[RunSceneBuilder] 日本語フォントアセットを作成: " + JpFontAssetPath);
            return asset;
        }

        /// <summary>
        /// TMP の既定フォールバックに日本語フォントを登録する。
        /// こうしておくと、既存のテキスト（Title / Select / Result など他メンバーが作った分も含む）
        /// が LiberationSans のままでも、日本語の字だけ自動でこちらから引かれる。
        /// テキストを1つずつ差し替える必要がなくなる。
        /// </summary>
        private static void RegisterFontFallback(TMP_FontAsset font)
        {
            if (font == null) return;

            var settings = AssetDatabase.LoadAssetAtPath<ScriptableObject>(TmpSettingsPath);
            if (settings == null)
            {
                Debug.LogWarning("[RunSceneBuilder] TMP Settings が見つかりません: " + TmpSettingsPath);
                return;
            }

            var so = new SerializedObject(settings);
            var list = so.FindProperty("m_fallbackFontAssets");
            if (list == null)
            {
                Debug.LogWarning("[RunSceneBuilder] TMP Settings のフォールバック欄が見つかりません");
                return;
            }

            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == font) return; // 登録済み
            }

            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = font;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);

            Debug.Log("[RunSceneBuilder] TMP のフォールバックに日本語フォントを登録しました");
        }

        /// <summary>パスからアセットを取り直す。見つからなければ例外。</summary>
        private static T Reload<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new Exception($"アセットを読み込めません: {path}");
            return asset;
        }

        private static ScoreSettings CreateScoreSettings()
        {
            var settings = LoadOrCreate<ScoreSettings>(ScoreSettingsPath);

            settings.comboRate = 0.02f;
            settings.comboBonusMax = 2f;
            settings.gaugeBonusMax = 1f;
            settings.rivalDefeatBonus = 0.5f;
            settings.feverMultiplier = 2f;
            settings.superFeverMultiplier = 4f;
            settings.maxMultiplier = 10f;
            settings.rivalMaxHp = 50;
            settings.rivalHearts = 3;
            settings.rivalDamagePerHit = 1;
            settings.rivalRespawns = true;

            EditorUtility.SetDirty(settings);
            return settings;
        }

        /// <summary>
        /// Game_Run 専用の Volume Profile を作る。
        ///
        /// 既存の `Global Volume Profile 1` は Bloom の threshold が 0.2 で、
        /// 輝度0.2を超えるもの（床・空・キャラ）が全部光ってしまい、
        /// 画面全体が白く飛んで何も見えなくなる。
        ///
        /// ペンライトはHDR(1.9前後)で描いているので、threshold を 1.0 付近にすれば
        /// **ペンライトだけが光り、通常の物体は光らない**。これが狙い。
        /// 共有プロファイルを書き換えると Title や旧 Game の見た目も変わるので、
        /// こちらは別アセットにしてある。
        /// </summary>
        private static VolumeProfile CreateVolumeProfile()
        {
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (existing != null) return existing;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "GameRunVolume";
            AssetDatabase.CreateAsset(profile, VolumeProfilePath);

            var bloom = profile.Add<Bloom>(true);
            bloom.name = "Bloom";

            // 1.0 未満の明るさ（＝普通の物体）は光らせない
            bloom.threshold.Override(1.0f);

            // 閾値1.0のおかげで床や空は一切光らない。つまり強度を上げても
            // にじむのはペンライトだけなので、大きめに振れる。
            // （閾値0.2のときに強度9まで上げたら画面全体が白飛びした）
            bloom.intensity.Override(4f);

            // にじみを少し締める。大きいと光が広がって背景を覆う
            bloom.scatter.Override(0.6f);

            // 極端に明るい点が画面を覆い尽くさないように頭を押さえる
            bloom.clamp.Override(30f);

            AssetDatabase.AddObjectToAsset(bloom, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            Debug.Log("[RunSceneBuilder] Volume Profile を作成: " + VolumeProfilePath);
            return profile;
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
            // 彩度のある色を、輝度を揃えてHDRに持ち上げる。
            //
            // 【Bloomの閾値は最大チャンネルではなく「輝度」で判定される】
            // 輝度 = 0.2126R + 0.7152G + 0.0722B で、緑の重みが圧倒的に大きい。
            // そのため同じ「2.8」でも、緑を含む色は光り、ピンクや紫は光らない。
            // 実際、各成分を揃えただけの指定ではピンク(輝度0.82)と紫(0.69)が
            // 閾値1.0に届かず、5色中2色が光らなかった。
            // ここで輝度を揃えておけば、どの色も同じように光る。
            var baseColors = new[]
            {
                new Color(1.00f, 0.07f, 0.40f), // ピンク
                new Color(0.07f, 0.60f, 1.00f), // 水色
                new Color(0.25f, 1.00f, 0.15f), // 黄緑
                new Color(1.00f, 0.45f, 0.07f), // 橙
                new Color(0.45f, 0.10f, 1.00f), // 紫
            };

            var colors = new Color[baseColors.Length];
            for (int i = 0; i < baseColors.Length; i++)
            {
                colors[i] = ScaleToLuminance(baseColors[i], PenlightLuminance);
            }

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

        private static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // 【アセットの取得はシーンを作った後に行うこと】
            // NewScene はシーン切り替えの際に、どこからも参照されていないアセットを
            // メモリから破棄する。先に読んでおいたローカル変数は破棄済みオブジェクトになり、
            // SerializedProperty に入れても null が書き込まれる
            // （「シーンは出来たのに参照だけ空」という壊れ方をする）。
            var runSettings = Reload<RunSettings>(RunSettingsPath);
            var gaugeSettings = Reload<GaugeSettings>(GaugeSettingsPath);
            var itemPrefab = Reload<GameObject>(RunItemPath);
            var obstaclePrefab = Reload<GameObject>(RunObstaclePath);
            var jumpObstaclePrefab = Reload<GameObject>(RunJumpObstaclePath);
            var penlightPrefab = Reload<GameObject>(RunPenlightPath);

            japaneseFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(JpFontAssetPath);

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

            // --- ライバル ---
            var scoreSettings = Reload<ScoreSettings>(ScoreSettingsPath);

            var rivalGo = new GameObject("Rival");
            var rival = rivalGo.AddComponent<RunRival>();
            SetField(rival, "settings", scoreSettings);
            SetField(rival, "gauge", gauge); // ハート全割りでフィーバーを頼む相手
            SetField(rival, "rivalName", "ライバル");

            // --- スコアと倍率 ---
            var scoreGo = new GameObject("Score");
            var score = scoreGo.AddComponent<RunScore>();
            SetField(score, "settings", scoreSettings);
            SetField(score, "player", runner);
            SetField(score, "gauge", gauge);
            SetField(score, "rival", rival);

            // --- HUD ---
            BuildHud(gauge, score, rival, runner);

            // --- ゴール判定 ---
            var goalGo = new GameObject("GoalWatcher");
            var goal = goalGo.AddComponent<RunGoalWatcher>();
            SetField(goal, "condition", (int)RunClearCondition.ChartEnd);
            SetField(goal, "player", runner);
            SetField(goal, "spawner", spawner);
            SetField(goal, "score", score);
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

            // 共有のプロファイル(Global Volume Profile 1)は Title や旧 Game でも使われている。
            // そちらの見た目を変えずに調整したいので、Game_Run 専用のものに差し替える
            var volumeComponent = volume.GetComponent<Volume>();
            if (volumeComponent != null)
            {
                volumeComponent.sharedProfile = Reload<VolumeProfile>(VolumeProfilePath);
            }

            // フィーバー中に画面全体の Bloom を上げて派手にする
            var feverEffect = volume.AddComponent<FeverScreenEffect>();
            SetField(feverEffect, "gauge", gauge);
            SetField(feverEffect, "volume", volume.GetComponent<Volume>());
            // 上げすぎると画面が白飛びして譜面が見えなくなる。
            // 通常1.0に対して、フィーバーで2倍、スーパーで3.5倍くらいが限度
            SetField(feverEffect, "feverIntensity", 7f);
            SetField(feverEffect, "superFeverIntensity", 10f);
            SetField(feverEffect, "superPulseSpeed", 8f);
            SetField(feverEffect, "superPulseAmount", 2f);
            SetField(feverEffect, "blendSpeed", 6f);

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

        /// <summary>
        /// HUDを組む。中身は全て仮の見た目で、後から差し替える前提。
        ///
        /// 配置は参考画面に寄せてある。
        ///   画面最上部 … キラメキビートゲージ（オリジナル要素なので一番上）
        ///   そのすぐ下 … ライバルの体力バー（名前とハート付き）
        ///   左上       … COMBO / SCORE / 倍率
        ///   左下       … LIFE（プレイヤーのハート）
        ///
        /// 差し替えるときは `Canvas/HUD` 配下を好きに作り直してよい。
        /// ロジック側が知っているのは BeatGaugeView と RunHudView の参照だけなので、
        /// そこを繋ぎ直せば見た目は自由に変えられる。
        /// </summary>
        private static void BuildHud(
            BeatGaugeController controller, RunScore score, RunRival rival, PlayerRunner player)
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

            CreateText(hudGo, "GaugeLabel", "キラメキビート", 22f,
                new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(760f, 26f),
                TextAlignmentOptions.Center);

            // --- ライバルの体力バー（ゲージのすぐ下） ---
            var rivalBarGo = DefaultControls.CreateSlider(GetUIResources());
            rivalBarGo.name = "RivalHpBar";
            rivalBarGo.transform.SetParent(hudGo.transform, false);

            var rivalRect = rivalBarGo.GetComponent<RectTransform>();
            rivalRect.anchorMin = new Vector2(0.5f, 1f);
            rivalRect.anchorMax = new Vector2(0.5f, 1f);
            rivalRect.pivot = new Vector2(0.5f, 1f);
            rivalRect.anchoredPosition = new Vector2(0f, -94f);
            rivalRect.sizeDelta = new Vector2(620f, 20f);

            var rivalBar = rivalBarGo.GetComponent<Slider>();
            rivalBar.minValue = 0f;
            rivalBar.maxValue = 1f;
            rivalBar.wholeNumbers = false;
            rivalBar.value = 1f;
            rivalBar.interactable = false;

            var rivalHandle = rivalBarGo.transform.Find("Handle Slide Area");
            if (rivalHandle != null) rivalHandle.gameObject.SetActive(false);

            Image rivalFill = null;
            var rivalFillTransform = rivalBarGo.transform.Find("Fill Area/Fill");
            if (rivalFillTransform != null) rivalFill = rivalFillTransform.GetComponent<Image>();

            var rivalNameText = CreateText(hudGo, "RivalNameText", "ライバル", 20f,
                new Vector2(0.5f, 1f), new Vector2(-312f, -90f), new Vector2(260f, 26f),
                TextAlignmentOptions.Left);

            var rivalHeartsText = CreateText(hudGo, "RivalHeartsText", "♥♥♥", 24f,
                new Vector2(0.5f, 1f), new Vector2(312f, -90f), new Vector2(160f, 26f),
                TextAlignmentOptions.Right);

            // --- 左上のスコア類 ---
            var comboText = CreateText(hudGo, "ComboText", "0", 64f,
                new Vector2(0f, 1f), new Vector2(32f, -130f), new Vector2(420f, 72f),
                TextAlignmentOptions.TopLeft);

            CreateText(hudGo, "ComboLabel", "COMBO", 22f,
                new Vector2(0f, 1f), new Vector2(34f, -200f), new Vector2(420f, 28f),
                TextAlignmentOptions.TopLeft);

            var multiplierText = CreateText(hudGo, "MultiplierText", "x1.00", 32f,
                new Vector2(0f, 1f), new Vector2(34f, -234f), new Vector2(420f, 40f),
                TextAlignmentOptions.TopLeft);

            CreateText(hudGo, "ScoreLabel", "SCORE", 22f,
                new Vector2(0f, 1f), new Vector2(34f, -284f), new Vector2(420f, 28f),
                TextAlignmentOptions.TopLeft);

            var scoreText = CreateText(hudGo, "ScoreText", "0", 40f,
                new Vector2(0f, 1f), new Vector2(32f, -312f), new Vector2(420f, 50f),
                TextAlignmentOptions.TopLeft);

            // --- 左下のライフ ---
            CreateText(hudGo, "LifeLabel", "LIFE", 22f,
                new Vector2(0f, 0f), new Vector2(34f, 72f), new Vector2(240f, 28f),
                TextAlignmentOptions.BottomLeft);

            var lifeText = CreateText(hudGo, "LifeText", "♥♥♥", 36f,
                new Vector2(0f, 0f), new Vector2(32f, 28f), new Vector2(240f, 44f),
                TextAlignmentOptions.BottomLeft);

            // フィーバー中だけ出す。画面中央やや上に大きく
            var feverText = CreateText(hudGo, "FeverText", "FEVER!", 56f,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 220f), new Vector2(900f, 80f),
                TextAlignmentOptions.Center);
            feverText.gameObject.SetActive(false);

            var hudView = hudGo.AddComponent<RunHudView>();
            SetField(hudView, "gauge", controller);
            SetField(hudView, "feverText", feverText);
            SetField(hudView, "score", score);
            SetField(hudView, "rival", rival);
            SetField(hudView, "player", player);
            SetField(hudView, "rivalHpBar", rivalBar);
            SetField(hudView, "rivalHpFill", rivalFill);
            SetField(hudView, "rivalNameText", rivalNameText);
            SetField(hudView, "rivalHeartsText", rivalHeartsText);
            SetField(hudView, "scoreText", scoreText);
            SetField(hudView, "comboText", comboText);
            SetField(hudView, "multiplierText", multiplierText);
            SetField(hudView, "lifeText", lifeText);
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

            // 日本語フォントを優先して割り当てる。無ければ既定（英数字のみ）に落とす
            var font = japaneseFont
                       ?? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpFontPath);
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

        /// <summary>
        /// 色相はそのままに、輝度が指定値になるよう明るさを揃える。
        /// Bloom は輝度で判定するので、色ごとの光り方を揃えるにはここを合わせる必要がある。
        /// </summary>
        private static Color ScaleToLuminance(Color color, float targetLuminance)
        {
            float luminance = 0.2126f * color.r + 0.7152f * color.g + 0.0722f * color.b;
            if (luminance <= 0.0001f) return color;

            float scale = targetLuminance / luminance;
            return new Color(color.r * scale, color.g * scale, color.b * scale, color.a);
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
                    // 破棄済みのオブジェクトを渡すと黙って null が書き込まれ、
                    // 「シーンは出来たのに参照が空」という分かりにくい壊れ方をする。
                    // 意図的に空にしたい場合だけ null リテラルを渡すこと
                    if (value != null && (Object)value == null)
                    {
                        throw new Exception(
                            $"{target.GetType().Name}.{field} に渡された参照が無効です。"
                            + "アセットを作り直した後は、パスから取り直してから渡すこと");
                    }

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
