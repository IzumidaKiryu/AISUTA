using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static Yakiniku.Run.EditorTools.BuilderUtil;

namespace Yakiniku.Run.EditorTools
{
    /// <summary>
    /// リザルト画面(Result)を生成する。本家のリザルト（縦持ち）に合わせてある。
    ///
    ///   上部エリア(TopAreaHeight) … プレイヤー情報の帯（名前・累計ハイスコア・クリア数・難易度ごとの最高ランク）
    ///   その下   … 画面を斜めに横切る WIN / LOSE の帯と、右に立ち絵（帯より手前）
    ///   その下   … 立ち絵の左にセリフの吹き出し
    ///   中央〜下 … 大きなスコアの枠（SCORE・ハイスコア・ランクバッジ、
    ///              その下に「スコア到達」の見出しと 3×2 の報酬マス）
    ///   最下部   … 次へ → 「もう一度」「ステージ選択」
    ///
    /// 全て仮の見た目。Inspector でスプライトを入れれば差し替わる。
    /// </summary>
    public static class ResultSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Result.unity";

        private static readonly Color Background = Hex("#F6F2FC");
        private static readonly Color Ink = Hex("#4A3A86");
        private static readonly Color Coral = Hex("#FF7A9C");
        private static readonly Color ScorePill = Hex("#5B4699");
        private static readonly Color Gold = Hex("#FFC93C");
        private static readonly Color Orange = Hex("#FF9A4D");
        private static readonly Color PanelEdge = Hex("#B9A7E6");

        // 縦の位置（上部エリアの下端からの距離）
        private const float BandY = 140f;
        private const float PortraitY = 60f;
        private const float BubbleY = 336f;
        private const float PanelY = 480f;

        private const float PanelWidth = 940f;
        private const float PanelHeight = 800f;

        // 報酬マス
        private const float TileWidth = 270f;
        private const float TileHeight = 180f;

        [MenuItem("Yakiniku/Run/リザルトシーンを生成", false, 12)]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SelectSceneBuilder.CreateUiCamera(Background);

            var canvas = CreateCanvas("Canvas");
            var top = new Vector2(0.5f, 1f);
            var topLeft = new Vector2(0f, 1f);
            var topRight = new Vector2(1f, 1f);
            float y0 = TopAreaHeight;

            // 背景は画面いっぱい、中身はセーフエリア（ノッチを避けた範囲）の中央の列に置く
            CreateFullscreen(canvas.transform, "Background", Background);
            var root = CreateColumn(CreateSafeArea(canvas.transform));

            // --- 上部エリア: プレイヤー情報の帯 ---
            BuildPlayerStatus(root);

            // --- 勝敗の帯（本家: 画面を斜めに横切る。傾けても端が切れないよう画面より広く取る） ---
            var band = CreatePanel(root, "OutcomeBand", Coral, top, new Vector2(0f, -(y0 + BandY)), new Vector2(1300f, 150f));
            band.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 4f);
            var outcome = CreateText(band.transform, "Outcome", "WIN", 130f, Color.white,
                new Vector2(0.5f, 0.5f), new Vector2(-150f, 0f), new Vector2(560f, 160f),
                TextAlignmentOptions.Center, FontStyles.Bold | FontStyles.Italic);

            // --- 立ち絵（右。帯より手前） ---
            var portrait = CreatePanel(root, "Portrait", Hex("#5FC8FF"), topRight, new Vector2(-30f, -(y0 + PortraitY)), new Vector2(340f, 400f));
            CreateText(portrait.transform, "Placeholder", "立ち絵（仮）", 28f, new Color(1f, 1f, 1f, 0.75f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320f, 50f));

            // --- セリフ（立ち絵の左下） ---
            var bubble = CreatePanel(root, "Bubble", Color.white, topLeft, new Vector2(60f, -(y0 + BubbleY)), new Vector2(560f, 130f));
            var speech = CreateText(bubble.transform, "Speech", "セリフ", 28f, Ink,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(510f, 110f));
            speech.enableWordWrapping = true;

            // --- スコアの枠（本家: 白い大きな枠に、スコアと報酬マスがまとめて入る） ---
            var edge = CreatePanel(root, "ScorePanelEdge", PanelEdge, top, new Vector2(0f, -(y0 + PanelY) + 6f),
                new Vector2(PanelWidth + 12f, PanelHeight + 12f));
            var card = CreatePanel(root, "ScorePanel", Color.white, top, new Vector2(0f, -(y0 + PanelY)), new Vector2(PanelWidth, PanelHeight));
            var c = card.transform;

            CreateText(c, "ScoreLabel", "SCORE", 32f, Ink, topLeft, new Vector2(36f, -20f), new Vector2(200f, 44f),
                TextAlignmentOptions.TopLeft, FontStyles.Bold);

            var newRecord = CreatePanel(c, "NewRecord", Gold, topLeft, new Vector2(390f, -18f), new Vector2(220f, 44f));
            CreateText(newRecord.transform, "Label", "NEW RECORD!", 22f, Ink,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(210f, 40f), TextAlignmentOptions.Center, FontStyles.Bold);

            var pill = CreatePanel(c, "ScorePill", ScorePill, topLeft, new Vector2(30f, -68f), new Vector2(580f, 110f));
            var scoreText = CreateText(pill.transform, "Score", "0", 76f, Color.white,
                new Vector2(1f, 0.5f), new Vector2(-28f, 0f), new Vector2(540f, 100f),
                TextAlignmentOptions.MidlineRight, FontStyles.Bold);

            var highScore = CreateText(c, "HighScore", "HIGH SCORE  0", 26f, Ink, topLeft,
                new Vector2(36f, -186f), new Vector2(570f, 36f), TextAlignmentOptions.TopLeft, FontStyles.Bold);

            var detail = CreateText(c, "Detail", "", 22f, Ink, topLeft,
                new Vector2(36f, -226f), new Vector2(570f, 90f), TextAlignmentOptions.TopLeft, FontStyles.Bold);
            detail.enableWordWrapping = false;

            var badge = CreateRankBadge(c, "RankBadge", topRight, new Vector2(-70f, -26f), 210f);

            var fullCombo = CreatePanel(c, "FullCombo", Coral, topRight, new Vector2(-60f, -250f), new Vector2(230f, 42f));
            CreateText(fullCombo.transform, "Label", "FULL COMBO", 22f, Color.white,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220f, 38f), TextAlignmentOptions.Center, FontStyles.Bold);

            // --- スコア到達（本家: 枠の中のオレンジの見出しと 3×2 の報酬マス） ---
            var milestoneHeader = CreatePanel(c, "MilestoneHeader", Orange, top, new Vector2(0f, -326f), new Vector2(880f, 54f));
            CreateText(milestoneHeader.transform, "Label", "★ スコア到達 ★", 28f, Color.white,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 48f), TextAlignmentOptions.Center, FontStyles.Bold);

            var ranks = new[] { RunRank.C, RunRank.B, RunRank.A, RunRank.S, RunRank.SS, RunRank.SSPlus };
            var milestones = new (TextMeshProUGUI rank, TextMeshProUGUI score, GameObject get, GameObject dim)[ranks.Length];
            for (int i = 0; i < ranks.Length; i++)
            {
                int col = i % 3;
                int row = i / 3;
                var pos = new Vector2((col - 1) * (TileWidth + 34f), -400f - row * (TileHeight + 20f));
                milestones[i] = BuildMilestone(c, "Milestone_" + ranks[i], pos);
            }

            // --- 次へ（本家: 枠の下の中央） ---
            var next = CreateButton(root, "NextButton", "次へ", 44f, Coral, Color.white,
                new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(380f, 110f));

            // --- もう一度／ステージ選択 ---
            // 暗幕は画面いっぱいに掛けたいので、セーフエリアではなく Canvas 直下に置く。
            // ResultView は参照で持つので場所はどこでもよい（画面撮影ツールは Canvas 直下を探す）
            var choice = Stretch(CreateRect(canvas.transform, "ChoicePanel"));
            var dim = CreateFullscreen(choice, "Dim", new Color(0.12f, 0.08f, 0.24f, 0.6f));
            dim.raycastTarget = true; // 後ろのボタンを押せないように
            // 縦持ちなのでボタンは縦に並べる（横に2つ並べると1つずつが細くて押しにくい）
            var box = CreatePanel(choice, "Box", Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 560f));
            CreateText(box.transform, "Title", "どうする？", 48f, Ink, new Vector2(0.5f, 1f),
                new Vector2(0f, -44f), new Vector2(760f, 64f), TextAlignmentOptions.Center, FontStyles.Bold);
            var retry = CreateButton(box.transform, "RetryButton", "もう一度", 42f, Coral, Color.white,
                new Vector2(0.5f, 0f), new Vector2(0f, 186f), new Vector2(680f, 120f));
            var select = CreateButton(box.transform, "SelectButton", "ステージ選択", 40f, ScorePill, Color.white,
                new Vector2(0.5f, 0f), new Vector2(0f, 44f), new Vector2(680f, 120f));
            choice.gameObject.SetActive(false);

            // --- 表示の更新 ---
            var view = canvas.gameObject.AddComponent<ResultView>();
            SetField(view, "outcomeText", outcome);
            SetField(view, "speechText", speech);
            SetField(view, "portrait", portrait);
            SetField(view, "scoreText", scoreText);
            SetField(view, "highScoreText", highScore);
            SetField(view, "newRecordMark", newRecord.gameObject);
            SetField(view, "rankBadge", badge);
            SetField(view, "detailText", detail);
            SetField(view, "fullComboMark", fullCombo.gameObject);
            SetField(view, "nextButton", next);
            SetField(view, "choicePanel", choice.gameObject);
            SetField(view, "retryButton", retry);
            SetField(view, "selectButton", select);
            SetObjectArray(view, "milestones", ranks.Length, (e, i) =>
            {
                SetRelative(e, "rank", (int)ranks[i]);
                SetRelative(e, "rankText", milestones[i].rank);
                SetRelative(e, "scoreText", milestones[i].score);
                SetRelative(e, "reachedMark", milestones[i].get);
                SetRelative(e, "dimmer", milestones[i].dim);
            });

            EnsureEventSystem();
            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterSceneInBuildSettings(ScenePath);
            Debug.Log("[ResultSceneBuilder] 生成完了: " + ScenePath);
        }

        /// <summary>
        /// 報酬マス（本家: 上にランクの印と GET のスタンプ、下に必要スコア）。
        /// <paramref name="position"/> はマスの上端中央。
        /// </summary>
        private static (TextMeshProUGUI rank, TextMeshProUGUI score, GameObject get, GameObject dim)
            BuildMilestone(Transform parent, string name, Vector2 position)
        {
            var tile = CreatePanel(parent, name, Hex("#FFF4D6"), new Vector2(0.5f, 1f), position, new Vector2(TileWidth, TileHeight));
            var t = tile.transform;

            var rank = CreateText(t, "Rank", "-", 44f, Ink, new Vector2(0f, 1f), new Vector2(20f, -12f),
                new Vector2(140f, 60f), TextAlignmentOptions.TopLeft, FontStyles.Bold | FontStyles.Italic);

            var get = CreatePanel(t, "Get", Gold, new Vector2(0.5f, 0.5f), new Vector2(30f, 8f), new Vector2(120f, 50f));
            get.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -8f);
            CreateText(get.transform, "Label", "GET", 30f, Ink, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(110f, 46f), TextAlignmentOptions.Center, FontStyles.Bold);

            var score = CreateText(t, "Score", "0", 28f, Ink, new Vector2(0.5f, 0f), new Vector2(0f, 12f),
                new Vector2(TileWidth - 20f, 38f), TextAlignmentOptions.Center, FontStyles.Bold);

            var dim = CreatePanel(t, "Dim", new Color(0.55f, 0.52f, 0.65f, 0.55f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(TileWidth, TileHeight));

            return (rank, score, get.gameObject, dim.gameObject);
        }
    }
}
