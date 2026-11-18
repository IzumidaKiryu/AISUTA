using System;
using TMPro;
using UnityEngine;

namespace Yakiniku.Run
{
    /// <summary>
    /// 難易度選択・リザルトの上部に出すプレイヤー情報の帯（本家のステータス行に相当）。
    ///
    /// 出すのは保存済みの記録（<see cref="RunProgress"/>）から分かるものだけ:
    ///   名前 / 累計ハイスコア（全難易度のハイスコアの合計）/ クリアした難易度の数 /
    ///   難易度ごとの最高ランクとクリア済みの印
    /// リザルトでは記録が保存された後に開くので、今回の結果も反映された値が出る。
    /// </summary>
    public sealed class PlayerStatusView : MonoBehaviour
    {
        [Serializable]
        public sealed class DifficultyChip
        {
            public RunDifficulty difficulty;
            public RankBadgeView rankBadge;

            [Tooltip("一度でも WIN したら出す")]
            public GameObject clearedMark;
        }

        [SerializeField] private string playerName = "あなた";

        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI totalScoreText;
        [SerializeField] private TextMeshProUGUI clearCountText;
        [SerializeField] private DifficultyChip[] chips = Array.Empty<DifficultyChip>();

        private void Start() => Refresh();

        public void Refresh()
        {
            if (nameText != null) nameText.text = playerName;

            var difficulties = (RunDifficulty[])Enum.GetValues(typeof(RunDifficulty));
            long total = 0;
            int cleared = 0;
            foreach (var d in difficulties)
            {
                total += RunProgress.HighScore(d);
                if (RunProgress.Cleared(d)) cleared++;
            }

            if (totalScoreText != null) totalScoreText.text = total.ToString("N0");
            if (clearCountText != null) clearCountText.text = $"{cleared} / {difficulties.Length}";

            foreach (var chip in chips)
            {
                if (chip.rankBadge != null) chip.rankBadge.Show(RunProgress.BestRank(chip.difficulty));
                if (chip.clearedMark != null) chip.clearedMark.SetActive(RunProgress.Cleared(chip.difficulty));
            }
        }
    }
}
