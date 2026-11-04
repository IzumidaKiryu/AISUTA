using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Yakiniku.Run
{
    /// <summary>
    /// HUDの表示。値を受け取って文字と画像に反映するだけの一方通行の層。
    ///
    /// ゲームロジックはこのクラスを知らないので、見た目を全部作り変えても
    /// ロジック側は無改修で済む。差し替えるときはここの参照を繋ぎ直すだけでよい。
    ///
    /// ハートは文字で出している（♥♥♡）。画像にしたい場合は
    /// <see cref="ApplyHearts"/> と <see cref="ApplyLife"/> を書き換える。
    /// </summary>
    public sealed class RunHudView : MonoBehaviour
    {
        [Header("参照するロジック")]
        [SerializeField] private RunScore score;
        [SerializeField] private RunRival rival;
        [SerializeField] private PlayerRunner player;

        [Header("ライバル")]
        [Tooltip("ライバルの体力バー")]
        [SerializeField] private Slider rivalHpBar;

        [SerializeField] private Image rivalHpFill;
        [SerializeField] private TextMeshProUGUI rivalNameText;
        [SerializeField] private TextMeshProUGUI rivalHeartsText;

        [Header("スコア")]
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI comboText;
        [SerializeField] private TextMeshProUGUI multiplierText;

        [Header("プレイヤー")]
        [SerializeField] private TextMeshProUGUI lifeText;

        [Header("フィーバー")]
        [SerializeField] private BeatGaugeController gauge;

        [Tooltip("フィーバー中だけ出す文字。通常時は非表示にする")]
        [SerializeField] private TextMeshProUGUI feverText;

        [Header("見た目")]
        [Tooltip("体力バーが実際の値に追いつくまでの時間(秒)。0で即時")]
        [SerializeField, Min(0f)] private float hpSmoothTime = 0.12f;

        [SerializeField] private Color rivalBarColor = new Color(1f, 0.35f, 0.55f);
        [SerializeField] private Color rivalBarLowColor = new Color(1f, 0.8f, 0.2f);

        [Tooltip("残り何割から色を変えるか")]
        [SerializeField, Range(0f, 1f)] private float rivalLowThreshold = 0.3f;

        private readonly StringBuilder builder = new StringBuilder(16);
        private float displayedHpRate = 1f;
        private float hpVelocity;

        private void Start()
        {
            if (rivalHpBar != null)
            {
                rivalHpBar.minValue = 0f;
                rivalHpBar.maxValue = 1f;
                rivalHpBar.interactable = false;
            }

            if (rival != null)
            {
                displayedHpRate = rival.HpRate;
                if (rivalNameText != null) rivalNameText.text = rival.RivalName;
                ApplyHearts(rival.Hearts, rival.MaxHearts);
            }

            ApplyScore(score != null ? score.Score : 0);
            ApplyCombo(score != null ? score.Combo : 0);
            ApplyMultiplier(score != null ? score.Multiplier : 1f);
            ApplyLife(player != null ? player.Health : 0);
        }

        private void OnEnable()
        {
            if (score != null)
            {
                score.OnScoreChanged += ApplyScore;
                score.OnComboChanged += ApplyCombo;
                score.OnMultiplierChanged += ApplyMultiplier;
            }

            if (rival != null) rival.OnHeartsChanged += HandleHeartsChanged;
            if (player != null) player.OnDamaged += ApplyLife;
        }

        // ラムダで購読すると解除できないので名前付きにしておく
        private void HandleHeartsChanged(int hearts)
        {
            ApplyHearts(hearts, rival != null ? rival.MaxHearts : hearts);
        }

        private void OnDisable()
        {
            if (score != null)
            {
                score.OnScoreChanged -= ApplyScore;
                score.OnComboChanged -= ApplyCombo;
                score.OnMultiplierChanged -= ApplyMultiplier;
            }

            if (rival != null) rival.OnHeartsChanged -= HandleHeartsChanged;
            if (player != null) player.OnDamaged -= ApplyLife;
        }

        private void Update()
        {
            UpdateFeverText();

            if (rival == null || rivalHpBar == null) return;

            float target = rival.HpRate;
            displayedHpRate = hpSmoothTime > 0f
                ? Mathf.SmoothDamp(displayedHpRate, target, ref hpVelocity, hpSmoothTime)
                : target;

            rivalHpBar.value = displayedHpRate;

            if (rivalHpFill != null)
            {
                rivalHpFill.color = displayedHpRate <= rivalLowThreshold
                    ? rivalBarLowColor
                    : rivalBarColor;
            }
        }

        /// <summary>
        /// フィーバー表示。スーパーフィーバーは虹色に回して通常と区別する。
        /// 通常時は文字ごと消す。
        /// </summary>
        private void UpdateFeverText()
        {
            if (feverText == null || gauge == null) return;

            bool show = gauge.IsFever;
            if (feverText.gameObject.activeSelf != show)
            {
                feverText.gameObject.SetActive(show);
            }

            if (!show) return;

            if (gauge.IsSuperFever)
            {
                feverText.text = "SUPER FEVER!!";
                float hue = Mathf.Repeat(Time.time * 1.6f, 1f);
                float pulse = 0.75f + 0.25f * Mathf.PingPong(Time.time * 12f, 1f);
                feverText.color = Color.HSVToRGB(hue, 0.85f, 1f) * pulse;
            }
            else
            {
                feverText.text = "FEVER!";
                float pulse = 0.7f + 0.3f * Mathf.PingPong(Time.time * 6f, 1f);
                feverText.color = new Color(1f, 0.85f, 0.25f) * pulse;
            }
        }

        private void ApplyScore(int value)
        {
            if (scoreText != null) scoreText.text = value.ToString("N0");
        }

        private void ApplyCombo(int value)
        {
            if (comboText != null) comboText.text = value.ToString();
        }

        private void ApplyMultiplier(float value)
        {
            if (multiplierText != null) multiplierText.text = "x" + value.ToString("0.00");
        }

        private void ApplyHearts(int hearts, int maxHearts)
        {
            if (rivalHeartsText == null) return;

            builder.Clear();
            for (int i = 0; i < maxHearts; i++)
            {
                builder.Append(i < hearts ? '♥' : '♡'); // ♥ と ♡
            }

            rivalHeartsText.text = builder.ToString();
        }

        private void ApplyLife(int health)
        {
            if (lifeText == null) return;

            int max = player != null && player.Settings != null ? player.Settings.maxHealth : health;

            builder.Clear();
            for (int i = 0; i < max; i++)
            {
                builder.Append(i < health ? '♥' : '♡');
            }

            lifeText.text = builder.ToString();
        }
    }
}
