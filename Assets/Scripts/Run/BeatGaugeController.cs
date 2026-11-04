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

        /// <summary>
        /// スーパーフィーバー中か。フィーバー中にもう一方のきっかけを満たすと昇格する。
        /// 倍率も演出もここを見て強くする。
        /// </summary>
        public bool IsSuperFever { get; private set; }

        /// <summary>今のフィーバーを起こしたきっかけ（複数立つとスーパーフィーバー）</summary>
        public FeverSource Sources { get; private set; }

        public int Combo { get; private set; }

        /// <summary>コンボ数が変化したとき（演出・UI用）</summary>
        public event Action<int> OnComboChanged;

        public event Action OnFeverStart;
        public event Action OnFeverEnd;

        /// <summary>スーパーフィーバーに昇格したとき。派手な演出はこれを購読する</summary>
        public event Action OnSuperFeverStart;

        private float feverTimer;

        /// <summary>
        /// 見た目を整えるためにゲージを満タンにしている最中か。
        /// フィーバー開始時にゲージを満タンにすると Gauge.OnFull が飛ぶが、
        /// これは「ゲージが溜まって発動した」わけではないので、
        /// スーパーフィーバーへの昇格のきっかけとして数えてはいけない。
        /// </summary>
        private bool isFillingForDisplay;

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

        /// <summary>
        /// 外からフィーバーを発動させる。ライバルのハートを全部割ったときに呼ばれる。
        ///
        /// フィーバーのきっかけは「ゲージ満タン」と「ハート全割り」の2つあるが、
        /// 状態の持ち主をこのクラス1つに保つため、発動はここを通す。
        /// 既にフィーバー中で、きっかけが別物なら**スーパーフィーバーに昇格**する。
        /// </summary>
        public void TriggerFever(FeverSource source = FeverSource.Rival)
        {
            if (settings == null || settings.feverDuration <= 0f) return;

            if (IsFever)
            {
                TryPromote(source);
                return;
            }

            // 先にフィーバー状態にしてからゲージを満タンにする。
            // 逆にすると Gauge.OnFull → HandleGaugeFull が走って二重に発動してしまう
            IsFever = true;
            Sources = source;
            feverTimer = settings.feverDuration;
            FillForDisplay();
            OnFeverStart?.Invoke();
        }

        /// <summary>見た目のためにゲージを満タンにする（昇格のきっかけには数えない）。</summary>
        private void FillForDisplay()
        {
            isFillingForDisplay = true;
            Gauge.Fill();
            isFillingForDisplay = false;
        }

        /// <summary>
        /// 既にフィーバー中に、もう一方のきっかけを満たしたらスーパーフィーバーにする。
        /// 同じきっかけが重ねて来ても昇格しない（アイテム連取で勝手に上がらないように）。
        /// </summary>
        private void TryPromote(FeverSource source)
        {
            if (IsSuperFever || Sources.HasFlag(source)) return;
            if (settings.superFeverDuration <= 0f) return;

            Sources |= source;
            IsSuperFever = true;

            // 残り時間はスーパー用の長さに置き換える（短い残りのまま昇格すると一瞬で終わる）
            feverTimer = settings.superFeverDuration;
            FillForDisplay();

            OnSuperFeverStart?.Invoke();
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
            if (settings.feverDuration <= 0f) return;

            // 演出で満タンにしただけなら、発動のきっかけとしては扱わない
            if (isFillingForDisplay) return;

            // フィーバー中にゲージが満タンになった場合は昇格を試みる。
            // ただし TriggerFever が演出のためにゲージを満タンにした場合は
            // Sources に既に自分の印が付いているので昇格しない
            if (IsFever)
            {
                TryPromote(FeverSource.Gauge);
                return;
            }

            IsFever = true;
            Sources = FeverSource.Gauge;
            feverTimer = settings.feverDuration;
            OnFeverStart?.Invoke();
        }

        private void EndFever()
        {
            if (!IsFever) return;

            IsFever = false;
            IsSuperFever = false;
            Sources = FeverSource.None;
            feverTimer = 0f;
            OnFeverEnd?.Invoke();
        }
    }
}
