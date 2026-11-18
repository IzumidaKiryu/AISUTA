using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static Yakiniku.Run.EditorTools.BuilderUtil;

namespace Yakiniku.Run.EditorTools
{
    /// <summary>
    /// 難易度選択画面(Select)を生成する。本家のアイドルバトルのステージ選択（縦持ち）に合わせてある。
    ///
    ///   上部エリア(TopAreaHeight) … プレイヤー情報の帯（名前・累計ハイスコア・クリア数・難易度ごとの最高ランク）
    ///   その下   … 「★アイドルバトル」の見出し
    ///   その下   … オレンジの帯に「vs.ライバル」とステージ番号、左右の端に◀▶
    ///              ライバルの立ち絵（大）が帯の右寄りに重なる
    ///   下半分   … 難易度カードの枠（スクロール。縦長の端末では枠が伸びて多く見える）
    ///   左下     … もどる
    ///
    /// 全て仮の見た目。生成されたものはただの Image / TextMeshProUGUI なので、
    /// Inspector でスプライトを入れれば差し替わる。
    /// </summary>
    public static class SelectSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Select.unity";

        private static readonly Color Background = Hex("#F1EDFA");
        private static readonly Color PanelColor = Hex("#E4DCF7");
        private static readonly Color Ink = Hex("#4A3A86");
        private static readonly Color Orange = Hex("#FF8A3D");
        private static readonly Color Pink = Hex("#FF6FB5");
        private static readonly Color ScorePill = Hex("#5B4699");

        // 縦の位置（上部エリアの下端からの距離）
        private const float TabY = 150f;
        private const float BandY = 350f;
        private const float BandHeight = 170f;
        private const float PortraitY = 190f;
        private const float PanelTopY = 560f;

        // カードの枠の下端（画面下端から）。下に「もどる」を置く分あける
        private const float PanelBottom = 180f;

        // 指で押すので大きめに取る
        private const float CardWidth = 940f;
        private const float CardHeight = 200f;
        private const float CardSpacing = 20f;
        private const float ListPadding = 20f;

        [MenuItem("Yakiniku/Run/難易度選択シーンを生成", false, 11)]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateUiCamera(Background);

            var canvas = CreateCanvas("Canvas");
            var top = new Vector2(0.5f, 1f);
            var topLeft = new Vector2(0f, 1f);
            float y0 = TopAreaHeight;

            // 背景は画面いっぱい、中身はセーフエリア（ノッチを避けた範囲）の中央の列に置く
            CreateFullscreen(canvas.transform, "Background", Background);
            var root = CreateColumn(CreateSafeArea(canvas.transform));

            // --- 上部エリア: プレイヤー情報の帯 ---
            BuildPlayerStatus(root);

            // --- 見出し ---
            var header = CreatePanel(root, "Header", Pink, topLeft, new Vector2(24f, -(y0 + TabY)), new Vector2(330f, 56f));
            CreateText(header.transform, "Label", "★ アイドルバトル", 28f, Color.white,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320f, 50f), TextAlignmentOptions.Center, FontStyles.Bold);

            // --- ライバルとステージ番号（本家のオレンジの帯。画面の端から端まで） ---
            var banner = CreatePanel(root, "StageBanner", Orange, top, new Vector2(0f, -(y0 + BandY)), new Vector2(1080f, BandHeight));
            var rivalName = CreateText(banner.transform, "RivalName", "vs.ライバル", 36f, Color.white,
                new Vector2(0f, 1f), new Vector2(130f, -14f), new Vector2(520f, 46f),
                TextAlignmentOptions.TopLeft, FontStyles.Bold);
            var stageNumber = CreateText(banner.transform, "StageNumber", "01", 104f, Color.white,
                new Vector2(0f, 0f), new Vector2(130f, 4f), new Vector2(300f, 112f),
                TextAlignmentOptions.BottomLeft, FontStyles.Bold | FontStyles.Italic);
            CreateText(banner.transform, "PrevArrow", "◀", 64f, new Color(0.45f, 0.35f, 0.75f, 0.75f),
                new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(80f, 100f));
            CreateText(banner.transform, "NextArrow", "▶", 64f, new Color(0.45f, 0.35f, 0.75f, 0.75f),
                new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(80f, 100f));

            // ライバルの立ち絵（仮）。帯の右寄りに大きく重ね、下端はカードの枠の後ろに隠れる
            var portrait = CreatePanel(root, "RivalPortrait", Hex("#C93D8B"), top, new Vector2(130f, -(y0 + PortraitY)), new Vector2(380f, 460f));
            CreateText(portrait.transform, "Placeholder", "立ち絵（仮）", 26f, new Color(1f, 1f, 1f, 0.75f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(340f, 40f));

            // --- 難易度カードの枠（スクロール） ---
            var difficulties = (RunDifficulty[])Enum.GetValues(typeof(RunDifficulty));
            var content = BuildScrollPanel(root, y0 + PanelTopY, difficulties.Length);

            var cards = new CardRefs[difficulties.Length];
            for (int i = 0; i < difficulties.Length; i++)
            {
                var position = new Vector2(0f, -(ListPadding + i * (CardHeight + CardSpacing)));
                cards[i] = BuildCard(content, difficulties[i], position);
            }

            // --- 戻る（本家: 左下の四角いボタン） ---
            var back = CreateButton(root, "BackButton", "◀", 56f, Color.white, Ink,
                new Vector2(0f, 0f), new Vector2(32f, 32f), new Vector2(130f, 120f));

            // --- 表示の更新 ---
            var view = canvas.gameObject.AddComponent<SelectView>();
            SetField(view, "rivalNameText", rivalName);
            SetField(view, "stageNumberText", stageNumber);
            SetField(view, "rivalName", "ライバル");
            SetField(view, "stageNumber", 1);
            SetField(view, "backButton", back);
            SetObjectArray(view, "cards", cards.Length, (e, i) =>
            {
                var c = cards[i];
                SetRelative(e, "difficulty", (int)c.Difficulty);
                SetRelative(e, "button", c.Button);
                SetRelative(e, "nameText", c.Name);
                SetRelative(e, "highScoreText", c.HighScore);
                SetRelative(e, "rankBadge", c.Badge);
                SetRelative(e, "completeRibbon", c.Complete);
                SetRelative(e, "lockOverlay", c.Lock);
                SetRelative(e, "lockText", c.LockText);
            });

            EnsureEventSystem();
            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterSceneInBuildSettings(ScenePath);
            Debug.Log("[SelectSceneBuilder] 生成完了: " + ScenePath);
        }

        /// <summary>
        /// カードを入れる縦スクロールの枠。上端は固定、下端は画面下から <see cref="PanelBottom"/> の位置まで伸ばすので、
        /// 縦長の端末ほど一度に多くのカードが見える。カードを置く Content を返す。
        /// </summary>
        private static RectTransform BuildScrollPanel(Transform parent, float topY, int cardCount)
        {
            var panel = CreatePanel(parent, "CardPanel", PanelColor, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.offsetMin = new Vector2(-500f, PanelBottom);
            panelRect.offsetMax = new Vector2(500f, -topY);

            // 表示範囲。はみ出したカードを切り取る。カードの隙間もドラッグできるよう透明な板を敷く
            var viewport = Stretch(CreateRect(panelRect, "Viewport"));
            viewport.offsetMin = new Vector2(0f, 8f);
            viewport.offsetMax = new Vector2(0f, -8f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var hit = viewport.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);

            var content = CreateRect(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, ListPadding * 2f + cardCount * CardHeight + (cardCount - 1) * CardSpacing);

            var scroll = panelRect.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;

            return content;
        }

        private sealed class CardRefs
        {
            public RunDifficulty Difficulty;
            public Button Button;
            public TextMeshProUGUI Name;
            public TextMeshProUGUI HighScore;
            public RankBadgeView Badge;
            public GameObject Complete;
            public GameObject Lock;
            public TextMeshProUGUI LockText;
        }

        /// <summary>
        /// 難易度のカード（本家: 上段に COMPLETE のリボン、下段に名前とハイスコアの枠、右端にランクのバッジ）。
        /// <paramref name="position"/> はカードの上端中央。
        /// </summary>
        private static CardRefs BuildCard(Transform parent, RunDifficulty difficulty, Vector2 position)
        {
            var entry = DifficultySettings.Load();
            var row = entry != null ? entry.Get(difficulty) : null;
            Color theme = row != null ? row.themeColor : Ink;

            var button = CreateButton(parent, "Card_" + difficulty, string.Empty, 10f, Color.white, Ink,
                new Vector2(0.5f, 1f), position, new Vector2(CardWidth, CardHeight));
            var card = button.transform;

            // 左の色帯（難易度の色）
            CreatePanel(card, "Accent", theme, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(18f, CardHeight));

            // COMPLETE（上段の左）
            var complete = CreatePanel(card, "Complete", Pink, new Vector2(0f, 1f), new Vector2(30f, -10f), new Vector2(320f, 46f));
            // ✦ は日本語フォントに無く豆腐になるので ★ を使う
            CreateText(complete.transform, "Label", "★ COMPLETE ★", 24f, Color.white,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(310f, 42f), TextAlignmentOptions.Center, FontStyles.Bold);
            complete.gameObject.SetActive(false);

            // 下段: 名前（左）
            var name = CreateText(card, "Name", difficulty.ToString(), 48f, Ink,
                new Vector2(0f, 0.5f), new Vector2(52f, -24f), new Vector2(340f, 76f),
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

            // 下段: HIGH SCORE（本家の紫の枠。右端のバッジが右端に重なる）
            var pill = CreatePanel(card, "ScorePill", ScorePill, new Vector2(1f, 0.5f), new Vector2(-110f, -24f), new Vector2(420f, 104f));
            CreateText(pill.transform, "Label", "HIGH SCORE", 22f, new Color(1f, 1f, 1f, 0.75f),
                new Vector2(0f, 1f), new Vector2(18f, -8f), new Vector2(240f, 28f), TextAlignmentOptions.TopLeft, FontStyles.Bold);
            var highScore = CreateText(pill.transform, "HighScore", "-", 44f, Color.white,
                new Vector2(1f, 0f), new Vector2(-56f, 6f), new Vector2(330f, 56f), TextAlignmentOptions.BottomRight, FontStyles.Bold);

            var badge = CreateRankBadge(card, "RankBadge", new Vector2(1f, 0.5f), new Vector2(-14f, -24f), 132f);

            // 鍵（解放されていない難易度に被せる）
            var lockImage = CreatePanel(card, "Lock", new Color(0.12f, 0.08f, 0.24f, 0.85f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardWidth, CardHeight));
            CreateText(lockImage.transform, "Title", "LOCKED", 48f, Color.white,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 26f), new Vector2(860f, 60f),
                TextAlignmentOptions.Center, FontStyles.Bold);
            var lockText = CreateText(lockImage.transform, "Condition", "", 26f, new Color(1f, 0.85f, 0.4f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, -32f), new Vector2(900f, 38f));
            lockImage.gameObject.SetActive(false);

            return new CardRefs
            {
                Difficulty = difficulty,
                Button = button,
                Name = name,
                HighScore = highScore,
                Badge = badge,
                Complete = complete.gameObject,
                Lock = lockImage.gameObject,
                LockText = lockText,
            };
        }

        /// <summary>UIだけのシーン用のカメラ。背景色で塗るだけ。</summary>
        public static Camera CreateUiCamera(Color background)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.orthographic = true;
            go.AddComponent<AudioListener>();
            return camera;
        }
    }
}
