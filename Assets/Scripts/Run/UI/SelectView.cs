using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Yakiniku.Run
{
    /// <summary>
    /// 難易度選択画面（本家: アイドルバトルのステージ選択）。
    ///
    /// 難易度ごとにハイスコア・最高ランク・クリア済みかを出し、
    /// 解放条件付きの難易度（Universe）は条件を満たすまで鍵をかける。
    ///
    /// 見た目は全て差し替え可能。カードの画像・色はシーン上の Image を直接いじればよく、
    /// このクラスは文字と表示の切り替えしかしない。
    /// </summary>
    public sealed class SelectView : MonoBehaviour
    {
        [Serializable]
        public sealed class Card
        {
            public RunDifficulty difficulty;
            public Button button;
            public TextMeshProUGUI nameText;
            public TextMeshProUGUI highScoreText;
            public RankBadgeView rankBadge;

            [Tooltip("一度でも WIN したら出す（本家の COMPLETE）")]
            public GameObject completeRibbon;

            [Tooltip("解放されていないときに被せる")]
            public GameObject lockOverlay;
            public TextMeshProUGUI lockText;
        }

        [Header("ステージ")]
        [SerializeField] private TextMeshProUGUI rivalNameText;
        [SerializeField] private TextMeshProUGUI stageNumberText;
        [SerializeField] private string rivalName = "ライバル";
        [SerializeField] private int stageNumber = 1;

        [Header("難易度")]
        [SerializeField] private Card[] cards = Array.Empty<Card>();

        [Header("その他")]
        [SerializeField] private Button backButton;

        private void Start()
        {
            if (rivalNameText != null) rivalNameText.text = "vs." + rivalName;
            if (stageNumberText != null) stageNumberText.text = stageNumber.ToString("00");

            var settings = DifficultySettings.Load();

            foreach (var card in cards)
            {
                var entry = settings != null ? settings.Get(card.difficulty) : null;
                Refresh(card, entry, settings);

                // ループ変数をそのまま拾うと全部最後のカードになるので、ここで写す
                var captured = card;
                var capturedEntry = entry;
                if (card.button != null) card.button.onClick.AddListener(() => OnCardClicked(captured, capturedEntry));
            }

            if (backButton != null)
            {
                backButton.onClick.AddListener(() =>
                {
                    RunSe.Play(SeId.UiTap);
                    RunFlow.ToTitle();
                });
            }
        }

        private static void Refresh(Card card, DifficultySettings.Entry entry, DifficultySettings settings)
        {
            var d = card.difficulty;

            if (card.nameText != null) card.nameText.text = entry != null ? entry.displayName : d.ToString();

            int high = RunProgress.HighScore(d);
            if (card.highScoreText != null) card.highScoreText.text = high > 0 ? high.ToString("N0") : "-";

            if (card.rankBadge != null) card.rankBadge.Show(RunProgress.BestRank(d));
            if (card.completeRibbon != null) card.completeRibbon.SetActive(RunProgress.Cleared(d));

            bool unlocked = RunProgress.IsUnlocked(entry);
            if (card.lockOverlay != null) card.lockOverlay.SetActive(!unlocked);

            if (card.lockText != null && entry != null && entry.locked)
            {
                var ranks = RankSettings.Load();
                string rankLabel = ranks != null ? ranks.Label(entry.unlockRank) : entry.unlockRank.ToString();
                var by = settings != null ? settings.Get(entry.unlockBy) : null;
                string byName = by != null ? by.displayName : entry.unlockBy.ToString();
                card.lockText.text = $"{byName} を {rankLabel} 以上でクリアで解放";
            }
        }

        private static void OnCardClicked(Card card, DifficultySettings.Entry entry)
        {
            if (RunLoadingOverlay.IsBusy) return;

            if (!RunProgress.IsUnlocked(entry))
            {
                RunSe.Play(SeId.UiTap);
                return;
            }

            RunSe.Play(SeId.UiDecide);
            RunFlow.StartStage(card.difficulty);
        }
    }
}
