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

        /// <summary>
        /// 画面全体の Canvas。基準解像度 1920x1080・幅と高さの中間で合わせるので、
        /// 中央に寄せた縦長のレイアウトは縦持ち(1080x1920)でもそのまま収まる。
        /// </summary>
        public static Canvas CreateCanvas(string name, int sortingOrder = 0)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
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
