using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static Yakiniku.Run.EditorTools.BuilderUtil;

namespace Yakiniku.Run.EditorTools
{
    /// <summary>
    /// リザルト画面(Result)を生成する。本家のリザルトに寄せてある。
    ///
    ///   上部     … WIN / LOSE の帯
    ///   右       … 立ち絵とセリフ
    ///   中央     … スコア・ハイスコア・ランクバッジ・NEW RECORD・FULL COMBO
    ///   その下   … スコア到達（本家の報酬枠。各ランクに必要なスコアと GET）
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

        [MenuItem("Yakiniku/Run/リザルトシーンを生成", false, 12)]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SelectSceneBuilder.CreateUiCamera(Background);

            var canvas = CreateCanvas("Canvas");
            var root = canvas.transform;
            var top = new Vector2(0.5f, 1f);
            var topLeft = new Vector2(0f, 1f);
            var topRight = new Vector2(1f, 1f);

            CreateFullscreen(root, "Background", Background);

            // --- 勝敗の帯 ---
            var band = CreatePanel(root, "OutcomeBand", Coral, top, new Vector2(-60f, -24f), new Vector2(1000f, 130f));
            var outcome = CreateText(band.transform, "Outcome", "WIN", 120f, Color.white,
                new Vector2(0f, 0.5f), new Vector2(60f, 0f), new Vector2(500f, 140f),
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold | FontStyles.Italic);

            // --- 立ち絵とセリフ（列の右外側） ---
            var portrait = CreatePanel(root, "Portrait", Hex("#5FC8FF"), top, new Vector2(660f, -40f), new Vector2(380f, 520f));
            CreateText(portrait.transform, "Placeholder", "立ち絵（仮）", 28f, new Color(1f, 1f, 1f, 0.75f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(340f, 50f));

            var bubble = CreatePanel(root, "Bubble", Color.white, top, new Vector2(660f, -580f), new Vector2(560f, 120f));
            var speech = CreateText(bubble.transform, "Speech", "セリフ", 24f, Ink,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 100f));
            speech.enableWordWrapping = true;

            // --- スコア ---
            var card = CreatePanel(root, "ScoreCard", Color.white, top, new Vector2(-60f, -176f), new Vector2(1000f, 330f));
            var c = card.transform;

            CreateText(c, "ScoreLabel", "SCORE", 30f, Ink, topLeft, new Vector2(34f, -18f), new Vector2(200f, 40f),
                TextAlignmentOptions.TopLeft, FontStyles.Bold);

            var pill = CreatePanel(c, "ScorePill", ScorePill, topLeft, new Vector2(30f, -62f), new Vector2(600f, 116f));
            var scoreText = CreateText(pill.transform, "Score", "0", 76f, Color.white,
                new Vector2(1f, 0.5f), new Vector2(-28f, 0f), new Vector2(560f, 100f),
                TextAlignmentOptions.MidlineRight, FontStyles.Bold);

            var newRecord = CreatePanel(c, "NewRecord", Gold, topLeft, new Vector2(420f, -14f), new Vector2(210f, 42f));
            CreateText(newRecord.transform, "Label", "NEW RECORD!", 22f, Ink,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 38f), TextAlignmentOptions.Center, FontStyles.Bold);

            var highScore = CreateText(c, "HighScore", "HIGH SCORE  0", 26f, Ink, topLeft,
                new Vector2(36f, -190f), new Vector2(600f, 36f), TextAlignmentOptions.TopLeft, FontStyles.Bold);

            var detail = CreateText(c, "Detail", "", 24f, Ink, topLeft,
                new Vector2(36f, -232f), new Vector2(600f, 92f), TextAlignmentOptions.TopLeft, FontStyles.Bold);
            detail.enableWordWrapping = false;

            var badge = CreateRankBadge(c, "RankBadge", topRight, new Vector2(-60f, -40f), 200f);

            var fullCombo = CreatePanel(c, "FullCombo", Coral, topRight, new Vector2(-46f, -262f), new Vector2(230f, 42f));
            CreateText(fullCombo.transform, "Label", "FULL COMBO", 22f, Color.white,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220f, 38f), TextAlignmentOptions.Center, FontStyles.Bold);

            // --- スコア到達（本家の報酬枠） ---
            CreateText(root, "MilestoneHeader", "★ スコア到達 ★", 28f, Coral, top,
                new Vector2(-60f, -526f), new Vector2(600f, 40f), TextAlignmentOptions.Center, FontStyles.Bold);

            var ranks = new[] { RunRank.C, RunRank.B, RunRank.A, RunRank.S, RunRank.SS, RunRank.SSPlus };
            var milestones = new (TextMeshProUGUI rank, TextMeshProUGUI score, GameObject get, GameObject dim)[ranks.Length];
            for (int i = 0; i < ranks.Length; i++)
            {
                int col = i % 3;
                int row = i / 3;
                var pos = new Vector2(-60f + (col - 1) * 330f, -572f - row * 104f);
                milestones[i] = BuildMilestone(root, "Milestone_" + ranks[i], pos);
            }

            // --- 次へ ---
            var next = CreateButton(root, "NextButton", "次へ", 40f, Coral, Color.white,
                new Vector2(0.5f, 0f), new Vector2(-60f, 40f), new Vector2(380f, 96f));

            // --- もう一度／ステージ選択 ---
            var choice = Stretch(CreateRect(root, "ChoicePanel"));
            var dim = CreateFullscreen(choice, "Dim", new Color(0.12f, 0.08f, 0.24f, 0.6f));
            dim.raycastTarget = true; // 後ろのボタンを押せないように
            var box = CreatePanel(choice, "Box", Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 380f));
            CreateText(box.transform, "Title", "どうする？", 44f, Ink, new Vector2(0.5f, 1f),
                new Vector2(0f, -40f), new Vector2(700f, 60f), TextAlignmentOptions.Center, FontStyles.Bold);
            var retry = CreateButton(box.transform, "RetryButton", "もう一度", 38f, Coral, Color.white,
                new Vector2(0.5f, 0f), new Vector2(-190f, 50f), new Vector2(340f, 110f));
            var select = CreateButton(box.transform, "SelectButton", "ステージ選択", 34f, ScorePill, Color.white,
                new Vector2(0.5f, 0f), new Vector2(190f, 50f), new Vector2(340f, 110f));
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

        private static (TextMeshProUGUI rank, TextMeshProUGUI score, GameObject get, GameObject dim)
            BuildMilestone(Transform parent, string name, Vector2 position)
        {
            var tile = CreatePanel(parent, name, Hex("#FFF4D6"), new Vector2(0.5f, 1f), position, new Vector2(300f, 90f));
            var t = tile.transform;

            var rank = CreateText(t, "Rank", "-", 40f, Ink, new Vector2(0f, 0.5f), new Vector2(22f, 0f),
                new Vector2(100f, 60f), TextAlignmentOptions.MidlineLeft, FontStyles.Bold | FontStyles.Italic);
            var score = CreateText(t, "Score", "0", 26f, Ink, new Vector2(1f, 0.5f), new Vector2(-20f, -14f),
                new Vector2(180f, 34f), TextAlignmentOptions.MidlineRight, FontStyles.Bold);

            var get = CreatePanel(t, "Get", Gold, new Vector2(1f, 1f), new Vector2(-14f, -8f), new Vector2(84f, 34f));
            CreateText(get.transform, "Label", "GET", 22f, Ink, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(80f, 30f), TextAlignmentOptions.Center, FontStyles.Bold);

            var dim = CreatePanel(t, "Dim", new Color(0.55f, 0.52f, 0.65f, 0.55f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 90f));

            return (rank, score, get.gameObject, dim.gameObject);
        }
    }
}
