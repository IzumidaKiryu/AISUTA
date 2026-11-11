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
    /// 難易度選択画面(Select)を生成する。本家のアイドルバトルのステージ選択に寄せてある。
    ///
    ///   上部     … 「アイドルバトル」の見出し
    ///   その下   … オレンジの帯に「vs.ライバル」とステージ番号
    ///   中央     … 難易度カード（名前・ハイスコア・ランクバッジ・COMPLETE・鍵）
    ///
    /// 全て仮の見た目。生成されたものはただの Image / TextMeshProUGUI なので、
    /// Inspector でスプライトを入れれば差し替わる。
    /// </summary>
    public static class SelectSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Select.unity";

        private static readonly Color Background = Hex("#F1EDFA");
        private static readonly Color BackgroundBand = Hex("#E4DCF7");
        private static readonly Color Ink = Hex("#4A3A86");
        private static readonly Color Orange = Hex("#FF8A3D");
        private static readonly Color Pink = Hex("#FF6FB5");
        private static readonly Color ScorePill = Hex("#5B4699");

        [MenuItem("Yakiniku/Run/難易度選択シーンを生成", false, 11)]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateUiCamera(Background);

            var canvas = CreateCanvas("Canvas");
            var root = canvas.transform;
            var top = new Vector2(0.5f, 1f);

            CreateFullscreen(root, "Background", Background);
            CreatePanel(root, "BackgroundBand", BackgroundBand, new Vector2(0.5f, 0.5f),
                new Vector2(0f, -40f), new Vector2(1100f, 2400f));

            // --- 見出し ---
            var header = CreatePanel(root, "Header", Pink, top, new Vector2(-330f, -24f), new Vector2(300f, 56f));
            CreateText(header.transform, "Label", "★ アイドルバトル", 28f, Color.white,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(290f, 50f), TextAlignmentOptions.Center, FontStyles.Bold);

            // --- ライバルとステージ番号（本家のオレンジの帯） ---
            var banner = CreatePanel(root, "StageBanner", Orange, top, new Vector2(0f, -96f), new Vector2(1000f, 150f));
            var rivalName = CreateText(banner.transform, "RivalName", "vs.ライバル", 36f, Color.white,
                new Vector2(0f, 1f), new Vector2(120f, -14f), new Vector2(500f, 46f),
                TextAlignmentOptions.TopLeft, FontStyles.Bold);
            var stageNumber = CreateText(banner.transform, "StageNumber", "01", 96f, Color.white,
                new Vector2(0f, 0f), new Vector2(120f, 6f), new Vector2(300f, 100f),
                TextAlignmentOptions.BottomLeft, FontStyles.Bold | FontStyles.Italic);
            CreateText(banner.transform, "PrevArrow", "◀", 44f, new Color(1f, 1f, 1f, 0.5f),
                new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(60f, 60f));
            CreateText(banner.transform, "NextArrow", "▶", 44f, new Color(1f, 1f, 1f, 0.5f),
                new Vector2(1f, 0.5f), new Vector2(-30f, 0f), new Vector2(60f, 60f));

            // ライバルの立ち絵（仮）。帯に重ねて置く
            // 右端の矢印(▶)を隠さないよう、帯の中ほどに置く
            var portrait = CreatePanel(root, "RivalPortrait", Hex("#C93D8B"), top, new Vector2(230f, -70f), new Vector2(200f, 200f));
            CreateText(portrait.transform, "Placeholder", "立ち絵（仮）", 22f, new Color(1f, 1f, 1f, 0.75f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(190f, 40f));

            // --- 難易度カード ---
            var difficulties = (RunDifficulty[])Enum.GetValues(typeof(RunDifficulty));
            var cards = new CardRefs[difficulties.Length];
            for (int i = 0; i < difficulties.Length; i++)
            {
                cards[i] = BuildCard(root, difficulties[i], new Vector2(0f, -280f - i * 168f));
            }

            // --- 戻る ---
            var back = CreateButton(root, "BackButton", "◀ もどる", 30f, Color.white, Ink,
                new Vector2(0f, 0f), new Vector2(40f, 40f), new Vector2(220f, 80f));

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

        private static CardRefs BuildCard(Transform parent, RunDifficulty difficulty, Vector2 position)
        {
            var entry = DifficultySettings.Load();
            var row = entry != null ? entry.Get(difficulty) : null;
            Color theme = row != null ? row.themeColor : Ink;

            var button = CreateButton(parent, "Card_" + difficulty, string.Empty, 10f, Color.white, Ink,
                new Vector2(0.5f, 1f), position, new Vector2(900f, 148f));
            var card = button.transform;

            // 左の色帯（難易度の色）
            CreatePanel(card, "Accent", theme, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(18f, 148f));

            var name = CreateText(card, "Name", difficulty.ToString(), 48f, Ink,
                new Vector2(0f, 0.5f), new Vector2(48f, 0f), new Vector2(300f, 70f),
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

            // HIGH SCORE（本家の紫の枠）
            var pill = CreatePanel(card, "ScorePill", ScorePill, new Vector2(1f, 0.5f), new Vector2(-170f, 0f), new Vector2(380f, 96f));
            CreateText(pill.transform, "Label", "HIGH SCORE", 20f, new Color(1f, 1f, 1f, 0.75f),
                new Vector2(0f, 1f), new Vector2(18f, -8f), new Vector2(240f, 26f), TextAlignmentOptions.TopLeft, FontStyles.Bold);
            var highScore = CreateText(pill.transform, "HighScore", "-", 40f, Color.white,
                new Vector2(1f, 0f), new Vector2(-18f, 8f), new Vector2(340f, 52f), TextAlignmentOptions.BottomRight, FontStyles.Bold);

            var badge = CreateRankBadge(card, "RankBadge", new Vector2(1f, 0.5f), new Vector2(-24f, 0f), 112f);

            // COMPLETE（カードの上辺にかぶせる）
            var complete = CreatePanel(card, "Complete", Pink, new Vector2(0f, 1f), new Vector2(40f, 18f), new Vector2(260f, 40f));
            CreateText(complete.transform, "Label", "★ COMPLETE ★", 22f, Color.white,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(250f, 36f), TextAlignmentOptions.Center, FontStyles.Bold);
            complete.gameObject.SetActive(false);

            // 鍵（解放されていない難易度に被せる）
            var lockImage = CreatePanel(card, "Lock", new Color(0.12f, 0.08f, 0.24f, 0.78f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 148f));
            CreateText(lockImage.transform, "Title", "LOCKED", 44f, Color.white,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 22f), new Vector2(800f, 56f),
                TextAlignmentOptions.Center, FontStyles.Bold);
            var lockText = CreateText(lockImage.transform, "Condition", "", 24f, new Color(1f, 0.85f, 0.4f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, -28f), new Vector2(860f, 36f));
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
