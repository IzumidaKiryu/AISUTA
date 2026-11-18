using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Yakiniku.Run.EditorTools
{
    /// <summary>
    /// シーン生成スクリプトの共通処理。ゲーム／難易度選択／リザルトの各ビルダーが使う。
    ///
    /// UI を組む関数は「位置・大きさ・色」を引数で全部受け取る形にしてあり、
    /// 生成されたものはただの Image / TextMeshProUGUI なので、後から Inspector で
    /// スプライトを差し替えれば見た目は自由に変えられる。
    /// </summary>
    public static class BuilderUtil
    {
        public const string SettingsDir = "Assets/Settings_Run";
        public const string PrefabDir = "Assets/Prefabs/Run";
        public const string ResourcesDir = "Assets/Resources";

        public const string JpFontAssetPath = "Assets/Fonts/NotoSansJP SDF.asset";
        public const string TmpFontPath =
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        // =====================================================================
        // アセット
        // =====================================================================

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }

        /// <summary>
        /// パスからアセットを取り直す。見つからなければ例外。
        ///
        /// 【NewScene の後に呼ぶこと】NewScene はどこからも参照されていないアセットを
        /// メモリから破棄するので、先に読んでおいた参照は使えなくなる。
        /// </summary>
        public static T Reload<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new Exception($"アセットを読み込めません: {path}");
            return asset;
        }

        public static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        public static GameObject InstantiateArt(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) throw new Exception("プレハブが見つかりません: " + path);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            if (instance == null) throw new Exception("プレハブを配置できません: " + path);
            return instance;
        }

        public static GameObject SavePrefab(GameObject root, string path)
        {
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path, out bool ok);
            if (!ok || saved == null) throw new Exception("プレハブを保存できません: " + path);
            return saved;
        }

        public static void RegisterSceneInBuildSettings(string scenePath, params string[] removePaths)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var remove in removePaths) scenes.RemoveAll(s => s.path == remove);

            if (!scenes.Exists(s => s.path == scenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // =====================================================================
        // SerializeField(private) への書き込み
        // =====================================================================

        public static void SetField(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                throw new Exception($"{target.GetType().Name}.{field} が見つかりません（スクリプト側の名前が変わった？）");
            }

            Assign(prop, value, $"{target.GetType().Name}.{field}");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetArrayField(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var array = so.FindProperty(field);
            if (array == null) throw new Exception($"{target.GetType().Name}.{field} が見つかりません");

            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                Assign(array.GetArrayElementAtIndex(i), values[i], $"{field}[{i}]");
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 中身がクラスの配列（[Serializable] な入れ物の配列）を書く。
        /// 各要素の中身は <paramref name="fill"/> で <see cref="SetRelative"/> を使って埋める。
        /// </summary>
        public static void SetObjectArray(Object target, string field, int count,
            Action<SerializedProperty, int> fill)
        {
            var so = new SerializedObject(target);
            var array = so.FindProperty(field);
            if (array == null) throw new Exception($"{target.GetType().Name}.{field} が見つかりません");

            array.arraySize = count;
            for (int i = 0; i < count; i++) fill(array.GetArrayElementAtIndex(i), i);

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetRelative(SerializedProperty element, string field, object value)
        {
            var prop = element.FindPropertyRelative(field);
            if (prop == null) throw new Exception($"{element.propertyPath}.{field} が見つかりません");
            Assign(prop, value, $"{element.propertyPath}.{field}");
        }

        private static void Assign(SerializedProperty prop, object value, string label)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.ObjectReference:
                    // 破棄済みのオブジェクトを渡すと黙って null が書き込まれ、
                    // 「シーンは出来たのに参照が空」という分かりにくい壊れ方をする
                    if (value != null && (Object)value == null)
                    {
                        throw new Exception($"{label} に渡された参照が無効です。" +
                                            "アセットを作り直した後は、パスから取り直してから渡すこと");
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
                    throw new Exception($"未対応の型です: {label} ({prop.propertyType})");
            }
        }

        // =====================================================================
        // UI
        // =====================================================================

        /// <summary>縦持ち前提の基準解像度。レイアウトはこの 1080x1920 の中に収めて組む。</summary>
        public static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);

        /// <summary>
        /// 画面上端の「上部エリア」の高さ。本家の画面ではここに配信アプリのコメント欄が重なっているので、
        /// 各画面の本体（HUD・帯・カード等）はこの下から置き、ここには画面ごとの情報を入れる。
        ///   ゲーム中         … 実況ログ欄（<see cref="RunEventFeed"/>）
        ///   難易度選択・リザルト … プレイヤー情報の帯（<see cref="BuildPlayerStatus"/>）
        /// </summary>
        public const float TopAreaHeight = 440f;

        /// <summary>
        /// 画面全体の Canvas。縦持ち(1080x1920)を基準にする。
        ///
        /// 【Expand で合わせる】基準の 1080x1920 が必ず画面内に丸ごと収まるよう拡大率を決める。
        /// 9:20 のような縦長の端末では上下に、タブレットのような横に広い端末では左右に余白が増えるだけで、
        /// 基準の範囲に置いたものは絶対に画面外へはみ出さない。
        /// 上下端に付けたもの（HUD・戻るボタン等）は端に付いたまま、中央のものは中央に残る。
        /// </summary>
        public static Canvas CreateCanvas(string name, int sortingOrder = 0)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>
        /// セーフエリア（ノッチやホームバーを避けた範囲）に合わせた入れ物。
        /// 背景は Canvas 直下に全画面で敷き、文字やボタンはこの下に置く。
        /// </summary>
        public static RectTransform CreateSafeArea(Transform canvas, string name = "SafeArea")
        {
            var rect = Stretch(CreateRect(canvas, name));
            rect.gameObject.AddComponent<SafeAreaFitter>();
            return rect;
        }

        /// <summary>
        /// 幅を基準(1080)に固定し、上下いっぱいに伸ばした中央の列。
        /// タブレットのような横に広い端末で、左右の端に付けたもの（見出し・立ち絵・戻る等）が
        /// 画面の端まで離れてレイアウトがバラけるのを防ぐ。スマホでは画面幅と同じなので見た目は変わらない。
        /// </summary>
        public static RectTransform CreateColumn(Transform parent, string name = "Column")
        {
            var rect = CreateRect(parent, name);
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(ReferenceResolution.x, 0f);
            return rect;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        public static RectTransform CreateRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public static RectTransform Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        /// <summary>色付きの板。角丸の既定スプライトを使うので、そのままでもカードっぽく見える。</summary>
        public static Image CreatePanel(Transform parent, string name, Color color,
            Vector2 anchor, Vector2 position, Vector2 size, Sprite sprite = null)
        {
            var rect = Place(CreateRect(parent, name), anchor, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite != null ? sprite : Builtin("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Image CreateFullscreen(Transform parent, string name, Color color)
        {
            var rect = Stretch(CreateRect(parent, name));
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static TextMeshProUGUI CreateText(Transform parent, string name, string text, float fontSize,
            Color color, Vector2 anchor, Vector2 position, Vector2 size,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center,
            FontStyles style = FontStyles.Normal)
        {
            var rect = Place(CreateRect(parent, name), anchor, position, size);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.fontStyle = style;
            label.raycastTarget = false;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;

            var font = JapaneseFont();
            if (font != null)
            {
                label.font = font;
                WarnMissingGlyphs(font, name, text);
            }

            return label;
        }

        /// <summary>
        /// 上部エリアに置くプレイヤー情報の帯（本家のステータス行に相当）。
        ///   上段 … アイコン・名前（左）、クリアした難易度の数（右）
        ///   下段 … 累計ハイスコア（左）、難易度ごとの最高ランクとクリア済みの印（右）
        /// 値は <see cref="PlayerStatusView"/> が保存済みの記録から入れる。
        /// </summary>
        public static PlayerStatusView BuildPlayerStatus(Transform parent)
        {
            var white = Color.white;
            var soft = new Color(1f, 1f, 1f, 0.7f);
            var chipColor = new Color(1f, 1f, 1f, 0.14f);
            var topLeft = new Vector2(0f, 1f);
            var topRight = new Vector2(1f, 1f);

            var panel = CreatePanel(parent, "PlayerStatus", new Color(0.36f, 0.27f, 0.6f, 0.95f),
                new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(1040f, 392f));
            var p = panel.transform;

            // --- 上段: アイコンと名前 ---
            var avatar = CreatePanel(p, "Avatar", Hex("#FF8FC8"), topLeft, new Vector2(28f, -28f), new Vector2(140f, 140f),
                Builtin("UI/Skin/Knob.psd"));
            avatar.type = UnityEngine.UI.Image.Type.Simple;
            CreateText(avatar.transform, "Mark", "★", 64f, white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 120f));

            var name = CreateText(p, "Name", "あなた", 46f, white, topLeft, new Vector2(196f, -38f), new Vector2(520f, 60f),
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            CreateText(p, "Title", "アイドルバトル 挑戦者", 24f, soft, topLeft, new Vector2(198f, -106f), new Vector2(460f, 34f),
                TextAlignmentOptions.MidlineLeft);

            // --- 上段右: クリア数 ---
            var clearPill = CreatePanel(p, "ClearPill", chipColor, topRight, new Vector2(-28f, -28f), new Vector2(260f, 140f));
            CreateText(clearPill.transform, "Label", "★ クリア", 24f, new Color(1f, 0.85f, 0.35f), topLeft, new Vector2(18f, -12f),
                new Vector2(220f, 32f), TextAlignmentOptions.TopLeft, FontStyles.Bold);
            var clearCount = CreateText(clearPill.transform, "Value", "0 / 0", 52f, white, new Vector2(1f, 0f), new Vector2(-18f, 10f),
                new Vector2(230f, 70f), TextAlignmentOptions.BottomRight, FontStyles.Bold);

            // --- 下段左: 累計ハイスコア ---
            var totalPill = CreatePanel(p, "TotalScorePill", chipColor, topLeft, new Vector2(28f, -196f), new Vector2(420f, 168f));
            CreateText(totalPill.transform, "Label", "累計ハイスコア", 24f, new Color(1f, 0.85f, 0.35f), topLeft, new Vector2(20f, -14f),
                new Vector2(380f, 32f), TextAlignmentOptions.TopLeft, FontStyles.Bold);
            var total = ShrinkToFit(CreateText(totalPill.transform, "Value", "0", 56f, white, new Vector2(1f, 0f), new Vector2(-20f, 16f),
                new Vector2(380f, 76f), TextAlignmentOptions.BottomRight, FontStyles.Bold), 30f);

            // --- 下段右: 難易度ごとの最高ランク ---
            var difficulties = (RunDifficulty[])Enum.GetValues(typeof(RunDifficulty));
            var chips = new (RunDifficulty difficulty, RankBadgeView badge, GameObject mark)[difficulties.Length];
            for (int i = 0; i < difficulties.Length; i++)
            {
                var chip = CreatePanel(p, "Chip_" + difficulties[i], chipColor, topLeft,
                    new Vector2(470f + i * 136f, -196f), new Vector2(124f, 168f));
                ShrinkToFit(CreateText(chip.transform, "Name", difficulties[i].ToString(), 20f, soft, new Vector2(0.5f, 1f),
                    new Vector2(0f, -8f), new Vector2(116f, 28f), TextAlignmentOptions.Center, FontStyles.Bold), 14f);
                var badge = CreateRankBadge(chip.transform, "RankBadge", new Vector2(0.5f, 0.5f), new Vector2(0f, -4f), 84f);
                var mark = CreateText(chip.transform, "Cleared", "★", 24f, new Color(1f, 0.85f, 0.35f), new Vector2(0.5f, 0f),
                    new Vector2(0f, 6f), new Vector2(116f, 30f));
                chips[i] = (difficulties[i], badge, mark.gameObject);
            }

            var view = panel.gameObject.AddComponent<PlayerStatusView>();
            SetField(view, "nameText", name);
            SetField(view, "totalScoreText", total);
            SetField(view, "clearCountText", clearCount);
            SetObjectArray(view, "chips", chips.Length, (e, i) =>
            {
                SetRelative(e, "difficulty", (int)chips[i].difficulty);
                SetRelative(e, "rankBadge", chips[i].badge);
                SetRelative(e, "clearedMark", chips[i].mark);
            });
            return view;
        }

        /// <summary>
        /// 枠の幅に収まらないときだけ文字を小さくする。大きな演出文字（FEVER! / Let's FIGHT! 等）用。
        /// 縦持ちでは横幅が 1080 しかないので、文言を差し替えたときにはみ出さないようにしておく。
        /// </summary>
        public static TextMeshProUGUI ShrinkToFit(TextMeshProUGUI label, float minSize)
        {
            label.fontSizeMax = label.fontSize;
            label.fontSizeMin = minSize;
            label.enableAutoSizing = true;
            return label;
        }

        /// <summary>
        /// フォントに無い字（豆腐になる字）が含まれていたらエラーを出す。
        /// 記号は日本語フォントでも入っていないものが多い（実際 ↩ と ✦ が豆腐になった）ので、
        /// 生成した時点で気付けるようにしておく。
        /// </summary>
        private static void WarnMissingGlyphs(TMP_FontAsset font, string name, string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            // tryAddCharacter を true にしないと、アトラスに読み込み済みの字しか見ない。
            // この版は足りない字を UTF-32 の値(uint)で返す
            if (font.HasCharacters(text, out uint[] missing, true, true) || missing == null || missing.Length == 0) return;

            var labels = new List<string>(missing.Length);
            foreach (uint code in missing) labels.Add($"'{char.ConvertFromUtf32((int)code)}'(U+{code:X4})");

            Debug.LogError($"[BuilderUtil] フォントに無い字があります（豆腐になります）: {name} \"{text}\" → " +
                           string.Join(" ", labels));
        }

        public static Button CreateButton(Transform parent, string name, string label, float fontSize,
            Color background, Color textColor, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var image = CreatePanel(parent, name, background, anchor, position, size);
            image.raycastTarget = true;

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var colors = button.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            button.colors = colors;

            var text = CreateText(image.transform, "Label", label, fontSize, textColor,
                new Vector2(0.5f, 0.5f), Vector2.zero, size, TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch(text.rectTransform);

            return button;
        }

        /// <summary>
        /// ランクのバッジ（丸＋文字）。<see cref="RankBadgeView"/> 付き。
        /// RankSettings にバッジ画像を入れれば、丸と文字の代わりにそれが出る。
        /// </summary>
        public static RankBadgeView CreateRankBadge(Transform parent, string name,
            Vector2 anchor, Vector2 position, float diameter)
        {
            var rect = Place(CreateRect(parent, name), anchor, position, new Vector2(diameter, diameter));

            var badge = rect.gameObject.AddComponent<Image>();
            badge.sprite = Builtin("UI/Skin/Knob.psd");
            badge.raycastTarget = false;

            var label = CreateText(rect, "Label", "-", diameter * 0.36f, Color.white,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(diameter, diameter),
                TextAlignmentOptions.Center, FontStyles.Bold | FontStyles.Italic);
            Stretch(label.rectTransform);

            var view = rect.gameObject.AddComponent<RankBadgeView>();
            SetField(view, "badge", badge);
            SetField(view, "label", label);
            return view;
        }

        public static Sprite Builtin(string path) => AssetDatabase.GetBuiltinExtraResource<Sprite>(path);

        public static DefaultControls.Resources GetUIResources()
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

        /// <summary>つまみの無い表示専用のバー。Slider と Fill の Image を返す。</summary>
        public static (Slider slider, Image fill) CreateBar(Transform parent, string name,
            Vector2 anchor, Vector2 position, Vector2 size, Color background)
        {
            var go = DefaultControls.CreateSlider(GetUIResources());
            go.name = name;
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, anchor, position, size);

            var slider = go.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.value = 1f;
            slider.interactable = false;

            var handle = go.transform.Find("Handle Slide Area");
            if (handle != null) handle.gameObject.SetActive(false);

            var bg = go.transform.Find("Background");
            if (bg != null && bg.TryGetComponent<Image>(out var bgImage)) bgImage.color = background;

            Image fill = null;
            var fillTransform = go.transform.Find("Fill Area/Fill");
            if (fillTransform != null) fill = fillTransform.GetComponent<Image>();

            return (slider, fill);
        }

        /// <summary>UIの文字に使う日本語フォント。無ければ既定（英数字のみ）。</summary>
        public static TMP_FontAsset JapaneseFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(JpFontAssetPath);
            return font != null ? font : AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpFontPath);
        }

        // =====================================================================
        // 色
        // =====================================================================

        public static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var color) ? color : Color.magenta;
        }
    }
}
