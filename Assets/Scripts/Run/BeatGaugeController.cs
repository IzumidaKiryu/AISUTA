using System;
using UnityEngine;

namespace Yakiniku.Run
{
    /// <summary>
    /// キラメキビートゲージの加算ルールを実行する層。
    /// BeatGauge(値) と GaugeSettings(ルール) を繋ぎ、プレイヤーのイベントに反応して増減させる。
    ///
    /// player は任意。未設定でも AddGauge() を外から呼べば動くようにしてあるので、
    /// ライバルアイドル側のゲージ（B担当の勝敗判定用）にも同じコンポーネントを使える。
    /// </summary>
    public sealed class BeatGaugeController : MonoBehaviour
    {
        [Header("参照")]
        [SerializeField] private GaugeSettings settings;

        [Tooltip("ゲージを溜める対象のプレイヤー。ライバル用など、自動加算しない場合は空でよい")]
        [SerializeField] private PlayerRunner player;

        /// <summary>ゲージ本体。UI や演出はこの OnChanged / OnFull を購読する</summary>
        public BeatGauge Gauge { get; private set; }

        /// <summary>現在値(0〜1)。勝敗判定はこれを比較する</summary>
        public float Value => Gauge != null ? Gauge.Value : 0f;

        public bool IsFever { get; private set; }
        public int Combo { get; private set; }

        /// <summary>コンボ数が変化したとき（演出・UI用）</summary>
        public event Action<int> OnComboChanged;

        public event Action OnFeverStart;
        public event Action OnFeverEnd;

        private float feverTimer;

        private void Awake()
        {
            Gauge = new BeatGauge();

            if (settings == null)
            {
                Debug.LogError($"{nameof(GaugeSettings)} が設定されていません", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            Gauge.OnFull += HandleGaugeFull;

            if (player != null)
            {
                player.OnItemCollected += HandleItemCollected;
                player.OnDamaged += HandleDamaged;
            }
        }

        private void OnDisable()
        {
            Gauge.OnFull -= HandleGaugeFull;

            if (player != null)
            {
                player.OnItemCollected -= HandleItemCollected;
                player.OnDamaged -= HandleDamaged;
            }
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;

            if (IsFever)
            {
                UpdateFever(deltaTime);
                return;
            }

            // 走行中は「毎秒加算 － 自然減衰」、止まっているときは減衰のみ
            bool isRunning = player != null && player.IsRunning && player.IsAlive;
            float perSecond = isRunning
                ? settings.gainPerSecond - settings.decayPerSecond
                : -settings.decayPerSecond;

            Gauge.Add(perSecond * deltaTime);
        }

        private void UpdateFever(float deltaTime)
        {
            feverTimer -= deltaTime;

            if (settings.drainDuringFever && settings.feverDuration > 0f)
            {
                // 残り時間がそのままゲージの見た目になるように減らす
                Gauge.Add(-deltaTime / settings.feverDuration);
            }

            if (feverTimer <= 0f)
            {
                EndFever();
            }
        }

        /// <summary>外部からゲージを増減する（ライバル用のAI加算や、デバッグ、演出連動に使う）。</summary>
        public void AddGauge(float amount)
        {
            if (IsFever) return;
            Gauge.Add(amount);
        }

        /// <summary>コンボを加算する。</summary>
        public void AddCombo(int amount = 1)
        {
            Combo += amount;
            OnComboChanged?.Invoke(Combo);
        }

        /// <summary>コンボを切る。</summary>
        public void ResetCombo()
        {
            if (Combo == 0) return;
            Combo = 0;
            OnComboChanged?.Invoke(Combo);
        }

        /// <summary>ゲージとコンボを初期状態に戻す。リトライ時に呼ぶ。</summary>
        public void ResetAll()
        {
            EndFever();
            Gauge.Reset();
            ResetCombo();
        }

        private void HandleItemCollected(BeatItem item)
        {
            AddCombo();

            if (IsFever) return;

            float gain = item != null && item.GaugeGain > 0f ? item.GaugeGain : settings.gainPerItem;
            float multiplier = Mathf.Min(1f + Combo * settings.comboGainRate, settings.maxComboMultiplier);

            Gauge.Add(gain * multiplier);
        }

        private void HandleDamaged(int remainingHealth)
        {
            ResetCombo();

            if (IsFever) return;
            Gauge.Add(-settings.damagePenalty);
        }

        private void HandleGaugeFull()
        {
            if (settings.feverDuration <= 0f || IsFever) return;

            IsFever = true;
            feverTimer = settings.feverDuration;
            OnFeverStart?.Invoke();
        }

        private void EndFever()
        {
            if (!IsFever) return;

            IsFever = false;
            feverTimer = 0f;
            OnFeverEnd?.Invoke();
        }
    }
}
