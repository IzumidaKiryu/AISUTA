using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Yakiniku.Run
{
    /// <summary>
    /// リザルト画面（本家: WIN → スコアとランク → スコア到達報酬 → 次へ）。
    ///
    /// 「次へ」を押すと「もう一度」か「ステージ選択」かを選ぶパネルが出る。
    ///
    /// 見た目は全て差し替え可能。画像はシーン上の Image を直接いじればよく、
    /// このクラスは文字と表示の切り替えしかしない。
    /// </summary>
    public sealed class ResultView : MonoBehaviour
    {
        [Serializable]
        public sealed class Milestone
        {
            public RunRank rank;
            public TextMeshProUGUI rankText;
            public TextMeshProUGUI scoreText;

            [Tooltip("到達したら出す（本家の GET）")]
            public GameObject reachedMark;

            [Tooltip("到達していないときに暗くするための覆い")]
            public GameObject dimmer;
        }

        [Header("勝敗")]
        [SerializeField] private TextMeshProUGUI outcomeText;
        [SerializeField] private TextMeshProUGUI speechText;

        [Tooltip("キャラクターの立ち絵。スプライトを入れれば差し替わる")]
        [SerializeField] private Image portrait;

        [Header("スコア")]
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI highScoreText;
        [SerializeField] private GameObject newRecordMark;
        [SerializeField] private RankBadgeView rankBadge;
        [SerializeField] private TextMeshProUGUI detailText;
        [SerializeField] private GameObject fullComboMark;

        [Header("スコア到達")]
        [SerializeField] private Milestone[] milestones = Array.Empty<Milestone>();

        [Header("ボタン")]
        [SerializeField] private Button nextButton;
        [SerializeField] private GameObject choicePanel;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button selectButton;

        [Header("文言（仮）")]
        [SerializeField] private string winLine = "やったね！最高のステージだったよ！";
        [SerializeField] private string loseLine = "次こそは負けないんだから……！";

        [Header("演出")]
        [Tooltip("スコアを数え上げる時間(秒)")]
        [SerializeField, Min(0f)] private float countUpTime = 1.2f;

        private RunResult result;

        private void Start()
        {
            result = RunSession.LastResult ?? SampleResult();

            if (choicePanel != null) choicePanel.SetActive(false);

            if (outcomeText != null) outcomeText.text = result.IsWin ? "WIN" : "LOSE";
            if (speechText != null) speechText.text = result.IsWin ? winLine : loseLine;

            int high = Mathf.Max(result.score, result.previousHighScore);
            if (highScoreText != null) highScoreText.text = "HIGH SCORE  " + high.ToString("N0");
            if (newRecordMark != null) newRecordMark.SetActive(result.isNewRecord);
            if (fullComboMark != null) fullComboMark.SetActive(result.IsFullCombo);

            if (detailText != null)
            {
                detailText.text =
                    $"MAX COMBO  {result.maxCombo}\n" +
                    $"NOTES  {result.itemsCollected} / {result.totalItems}\n" +
                    $"RIVAL  {(result.rivalDefeated ? "撃破！" : $"ハート {result.heartsBroken} 個")}";
            }

            if (rankBadge != null) rankBadge.Show(result.rank);
            ShowMilestones();

            if (nextButton != null) nextButton.onClick.AddListener(OnNext);
            if (retryButton != null) retryButton.onClick.AddListener(OnRetry);
            if (selectButton != null) selectButton.onClick.AddListener(OnSelect);

            RunSe.Play(result.IsWin ? SeId.Win : SeId.Lose);
            StartCoroutine(CountUp());
        }

        private void ShowMilestones()
        {
            var ranks = RankSettings.Load();
            var entry = DifficultySettings.Load()?.Get(result.difficulty);
            int perfect = entry != null ? entry.perfectScore : 1;

            foreach (var m in milestones)
            {
                int need = ranks != null ? ranks.ScoreFor(m.rank, perfect) : 0;
                bool reached = result.score >= need;

                if (m.rankText != null) m.rankText.text = ranks != null ? ranks.Label(m.rank) : m.rank.ToString();
                if (m.scoreText != null) m.scoreText.text = need.ToString("N0");
                if (m.reachedMark != null) m.reachedMark.SetActive(reached);
                if (m.dimmer != null) m.dimmer.SetActive(!reached);
            }
        }

        private IEnumerator CountUp()
        {
            if (scoreText == null) yield break;

            float t = 0f;
            while (t < countUpTime)
            {
                t += Time.deltaTime;
                float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / countUpTime), 3f);
                scoreText.text = Mathf.RoundToInt(result.score * e).ToString("N0");
                yield return null;
            }

            scoreText.text = result.score.ToString("N0");
        }

        private void OnNext()
        {
            RunSe.Play(SeId.UiTap);
            if (choicePanel != null) choicePanel.SetActive(true);
        }

        private void OnRetry()
        {
            if (RunLoadingOverlay.IsBusy) return;
            RunSe.Play(SeId.UiDecide);
            RunFlow.Retry();
        }

        private void OnSelect()
        {
            if (RunLoadingOverlay.IsBusy) return;
            RunSe.Play(SeId.UiDecide);
            RunFlow.ToSelect();
        }

        /// <summary>
        /// リザルト画面を単体で開いたとき用の見本。
        /// 見た目を調整するたびにゲームを1回遊ばなくて済むように。
        /// </summary>
        private static RunResult SampleResult()
        {
            return new RunResult
            {
                difficulty = RunDifficulty.Normal,
                score = 18500,
                maxCombo = 279,
                itemsCollected = 279,
                totalItems = 279,
                heartsBroken = 5,
                rivalDefeated = true,
                survived = true,
                rank = RunRank.SS,
                previousHighScore = 16000,
                isNewRecord = true,
            };
        }
    }
}
