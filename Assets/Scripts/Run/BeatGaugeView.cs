using UnityEngine;
using UnityEngine.UI;

namespace Yakiniku.Run
{
    /// <summary>
    /// キラメキビートゲージの表示。Slider の値を更新するだけの一方通行の層。
    /// ゲームロジックは UI を一切知らないので、見た目を作り変えても
    /// （Slider をやめて独自の画像演出にしても）ゲーム側は無改修で済む。
    /// </summary>
    public sealed class BeatGaugeView : MonoBehaviour
    {
        [Header("参照")]
        [SerializeField] private BeatGaugeController controller;
        [SerializeField] private Slider slider;

        [Tooltip("色を変える対象。Slider の Fill を割り当てる")]
        [SerializeField] private Image fillImage;

        [Header("見た目")]
        [Tooltip("実際の値に追いつくまでの時間(秒)。0で即時反映")]
        [SerializeField, Min(0f)] private float smoothTime = 0.08f;

        [SerializeField] private Color normalColor = new Color(0.35f, 0.8f, 1f);
        [SerializeField] private Color feverColor = new Color(1f, 0.85f, 0.25f);

        [Tooltip("フィーバー中の点滅の速さ")]
        [SerializeField, Min(0f)] private float feverPulseSpeed = 6f;

        private float displayedValue;
        private float smoothVelocity;

        private void Start()
        {
            if (slider != null)
            {
                slider.minValue = 0f;
                slider.maxValue = 1f;
                slider.interactable = false;
            }

            if (controller != null)
            {
                displayedValue = controller.Value;
            }

            ApplyToSlider();
            ApplyColor();
        }

        private void Update()
        {
            if (controller == null) return;

            float target = controller.Value;

            displayedValue = smoothTime > 0f
                ? Mathf.SmoothDamp(displayedValue, target, ref smoothVelocity, smoothTime)
                : target;

            ApplyToSlider();
            ApplyColor();
        }

        private void ApplyToSlider()
        {
            if (slider != null) slider.value = displayedValue;
        }

        private void ApplyColor()
        {
            if (fillImage == null) return;

            if (controller != null && controller.IsFever)
            {
                // 0〜1を往復させて点滅させる
                float pulse = Mathf.PingPong(Time.time * feverPulseSpeed, 1f);
                fillImage.color = Color.Lerp(normalColor, feverColor, pulse);
            }
            else
            {
                fillImage.color = normalColor;
            }
        }
    }
}
