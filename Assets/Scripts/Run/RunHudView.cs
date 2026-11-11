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
        [SerializeField] private BeatGaugeController gauge;

        [Header("ライバル")]
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

        [Header("バナー（状態が変わった瞬間に出す）")]
        [SerializeField] private TextMeshProUGUI bannerText;

        [Tooltip("バナーの土台（文字の後ろに敷く帯）。文字と一緒に出し入れ・フェードする")]
        [SerializeField] private CanvasGroup bannerGroup;

        [Tooltip("バナーを出しておく時間(秒)")]
        [SerializeField, Min(0.1f)] private float bannerTime = 2f;

        [Header("見た目")]
        [Tooltip("体力バーが実際の値に追いつくまでの時間(秒)。0で即時")]
        [SerializeField, Min(0f)] private float hpSmoothTime = 0.12f;

        [SerializeField] private Color rivalBarColor = new Color(0.35f, 0.85f, 1f);
        [SerializeField] private Color rivalBarLowColor = new Color(1f, 0.45f, 0.6f);

        [Tooltip("残り何割から色を変えるか")]
        [SerializeField, Range(0f, 1f)] private float rivalLowThreshold = 0.3f;

        private enum Banner { None, Fever, Encore, Super }

        private readonly StringBuilder builder = new StringBuilder(16);
        private float displayedHpRate = 1f;
        private float hpVelocity;

        private Banner banner;
        private float bannerTimer;

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

            SetBannerVisible(false);

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

            if (gauge != null)
            {
                gauge.OnFeverStart += ShowFever;
                gauge.OnEncoreStart += ShowEncore;
                gauge.OnSuperFeverStart += ShowSuper;
            }
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

            if (gauge != null)
            {
                gauge.OnFeverStart -= ShowFever;
                gauge.OnEncoreStart -= ShowEncore;
                gauge.OnSuperFeverStart -= ShowSuper;
            }
        }

        // ラムダで購読すると解除できないので名前付きにしておく
        private void HandleHeartsChanged(int hearts)
        {
            ApplyHearts(hearts, rival != null ? rival.MaxHearts : hearts);
        }

        private void Update()
        {
            UpdateBanner();

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

        // スーパーは ENCORE やフィーバーと同じ瞬間に始まることがある。
        // 後から来た弱いバナーで上書きしないよう、強さの順で判定する
        private void ShowFever() => Show(gauge != null && gauge.IsSuperFever ? Banner.Super : Banner.Fever);
        private void ShowEncore() => Show(gauge != null && gauge.IsSuperFever ? Banner.Super : Banner.Encore);
        private void ShowSuper() => Show(Banner.Super);

        private void Show(Banner next)
        {
            if (bannerTimer > 0f && next < banner) return;

            banner = next;
            bannerTimer = bannerTime;

            if (bannerText == null) return;
            SetBannerVisible(true);
            bannerText.text = next == Banner.Super ? "SUPER FEVER!!"
                : next == Banner.Encore ? "ENCORE TIME"
                : "FEVER!";
        }

        private void UpdateBanner()
        {
            if (bannerText == null || bannerTimer <= 0f) return;

            bannerTimer -= Time.deltaTime;
            float elapsed = bannerTime - bannerTimer;

            // 出た瞬間に大きく、すぐ定位置へ。最後は消えていく
            float pop = Mathf.Clamp01(elapsed / 0.2f);
            var scaleTarget = bannerGroup != null ? bannerGroup.transform : bannerText.transform;
            scaleTarget.localScale = Vector3.one * Mathf.Lerp(1.8f, 1f, pop);

            float fade = Mathf.Clamp01(bannerTimer / 0.3f);
            Color color;
            if (banner == Banner.Super)
            {
                color = Color.HSVToRGB(Mathf.Repeat(Time.time * 1.6f, 1f), 0.8f, 1f);
            }
            else if (banner == Banner.Encore)
            {
                color = new Color(1f, 0.82f, 0.25f);
            }
            else
            {
                color = new Color(1f, 0.45f, 0.85f);
            }

            if (bannerGroup != null)
            {
                bannerGroup.alpha = fade;
            }
            else
            {
                color.a = fade;
            }

            bannerText.color = color;

            if (bannerTimer <= 0f)
            {
                SetBannerVisible(false);
                banner = Banner.None;
            }
        }

        private void SetBannerVisible(bool visible)
        {
            if (bannerGroup != null) bannerGroup.gameObject.SetActive(visible);
            else if (bannerText != null) bannerText.gameObject.SetActive(visible);
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
