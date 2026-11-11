using System;
using UnityEngine;

namespace Yakiniku.Run
{
    /// <summary>
    /// キラメキビートゲージと、フィーバー／ENCORE の状態を持つ層。
    ///
    /// 【状態は2つの真偽値で表す】
    ///   IsFever  … ゲージ満タンで入る小フィーバー。一定時間で終わり、1曲に2〜3回
    ///   IsEncore … ライバルを撃破すると入る。曲の終わりまで続く（本家の ENCORE TIME）
    ///   スーパーフィーバー ＝ 両方ON
    ///
    /// 「ENCORE 中にゲージが満タンになった」でも「フィーバー中にライバルを撃破した」でも
    /// 同じくスーパーになる。どちらが先かを覚えておく必要がないので、判定が単純になる。
    ///
    /// player は任意。未設定でも AddGauge() を外から呼べば動くようにしてある。
    /// </summary>
    public sealed class BeatGaugeController : MonoBehaviour
    {
        [Header("参照")]
        [SerializeField] private GaugeSettings settings;

        [Tooltip("ゲージを溜める対象のプレイヤー。自動加算しない場合は空でよい")]
        [SerializeField] private PlayerRunner player;

        /// <summary>ゲージ本体。UI や演出はこの OnChanged / OnFull を購読する</summary>
        public BeatGauge Gauge { get; private set; }

        /// <summary>現在値(0〜1)</summary>
        public float Value => Gauge != null ? Gauge.Value : 0f;

        /// <summary>ゲージ満タンで入る小フィーバー中か</summary>
        public bool IsFever { get; private set; }

        /// <summary>ライバル撃破で入る ENCORE TIME 中か（曲の終わりまで続く）</summary>
        public bool IsEncore { get; private set; }

        /// <summary>スーパーフィーバー中か（フィーバーと ENCORE が重なっている）</summary>
        public bool IsSuperFever => IsFever && IsEncore;

        public int Combo { get; private set; }

        /// <summary>アイテム1個あたりのゲージ加算量。<see cref="RunBalance"/> が譜面に合わせて決める</summary>
        public float GainPerItem { get; private set; }

        public event Action<int> OnComboChanged;
        public event Action OnFeverStart;
        public event Action OnFeverEnd;
        public event Action OnEncoreStart;

        /// <summary>スーパーフィーバーが始まったとき（フィーバーと ENCORE が重なった瞬間）</summary>
        public event Action OnSuperFeverStart;

        private float feverTimer;
        private float feverLength;

        private void Awake()
        {
            Gauge = new BeatGauge();

            if (settings == null)
            {
                Debug.LogError($"{nameof(GaugeSettings)} が設定されていません", this);
                enabled = false;
                return;
            }

            GainPerItem = settings.gainPerItem;
        }

        private void OnEnable()
        {
            if (Gauge != null) Gauge.OnFull += HandleGaugeFull;

            if (player != null)
            {
                player.OnItemCollected += HandleItemCollected;
                player.OnDamaged += HandleDamaged;
            }
        }

        private void OnDisable()
        {
            if (Gauge != null) Gauge.OnFull -= HandleGaugeFull;

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

            // 残り時間がそのままゲージの見た目になるように減らす
            if (settings.drainDuringFever && feverLength > 0f)
            {
                Gauge.Add(-deltaTime / feverLength);
            }

            if (feverTimer <= 0f) EndFever();
        }

        /// <summary>
        /// 譜面に合わせてゲージの溜まり方を決める。<see cref="RunBalance"/> から呼ばれる。
        /// 譜面ごとにアイテム数が違うので、固定値だとフィーバーの回数が難易度でばらつく。
        /// </summary>
        public void SetGainPerItem(float gain)
        {
            GainPerItem = Mathf.Max(0f, gain);
        }

        /// <summary>
        /// ENCORE TIME に入る。ライバルのハートを全部割ったときに呼ばれる。
        /// 曲の終わりまで続くので終了処理は持たない。
        /// フィーバー中だった場合はその場でスーパーフィーバーになる。
        /// </summary>
        public void StartEncore()
        {
            if (IsEncore) return;

            IsEncore = true;
            OnEncoreStart?.Invoke();

            if (IsFever)
            {
                // 残り僅かのフィーバーで重なってもすぐ終わってしまうので、
                // スーパー用の長さで仕切り直す
                BeginFeverTimer(settings.superFeverDuration);
                OnSuperFeverStart?.Invoke();
            }
        }

        /// <summary>外部からゲージを増減する（デバッグや演出連動に使う）。</summary>
        public void AddGauge(float amount)
        {
            if (IsFever) return;
            Gauge.Add(amount);
        }

        public void AddCombo(int amount = 1)
        {
            Combo += amount;
            OnComboChanged?.Invoke(Combo);
        }

        public void ResetCombo()
        {
            if (Combo == 0) return;
            Combo = 0;
            OnComboChanged?.Invoke(Combo);
        }

        /// <summary>初期状態に戻す。リトライ時に呼ぶ。</summary>
        public void ResetAll()
        {
            EndFever();
            IsEncore = false;
            Gauge.Reset();
            ResetCombo();
        }

        private void HandleItemCollected(BeatItem item)
        {
            AddCombo();

            // フィーバー中はゲージが「残り時間」の表示に変わるので加算しない
            if (IsFever) return;

            float multiplier = Mathf.Min(1f + Combo * settings.comboGainRate, settings.maxComboMultiplier);
            Gauge.Add(GainPerItem * multiplier);
        }

        private void HandleDamaged(int remainingHealth)
        {
            ResetCombo();

            if (IsFever) return;
            Gauge.Add(-settings.damagePenalty);
        }

        private void HandleGaugeFull()
        {
            // フィーバー中にゲージを満タンにする（演出用）ことがあるので、二重に入らない
            if (IsFever || settings.feverDuration <= 0f) return;

            IsFever = true;

            // ENCORE 中に溜め切った場合はスーパーフィーバー。尺も長くする
            BeginFeverTimer(IsEncore ? settings.superFeverDuration : settings.feverDuration);

            OnFeverStart?.Invoke();
            if (IsEncore) OnSuperFeverStart?.Invoke();
        }

        private void BeginFeverTimer(float length)
        {
            feverLength = Mathf.Max(0.01f, length);
            feverTimer = feverLength;

            // ゲージの残量が残り時間を表すので、開始時は満タンに揃える。
            // IsFever を先に立ててあるので HandleGaugeFull は素通りする
            Gauge.Fill();
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
