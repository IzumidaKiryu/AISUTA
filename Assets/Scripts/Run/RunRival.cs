using System;
using UnityEngine;

namespace Yakiniku.Run
{
    /// <summary>
    /// ライバルの体力とハート。
    ///
    /// アイテムを取るとライバルの体力バーが削れ、削り切ると**ハートが1つ減る**（＝1撃破）。
    /// ハートを全部割るとフィーバーが発動する。
    ///
    /// 撃破数はスコア倍率の加算要素になるので、`rivalRespawns` がオンなら
    /// ハートを全部割ったあとに復活させて、何度でも倒せるようにしている。
    ///
    /// フィーバーの状態そのものは <see cref="BeatGaugeController"/> が持つ。
    /// ここから発動だけ依頼することで、「ゲージ満タン」と「ハート全割り」の
    /// 2つのきっかけがあってもフィーバーの持ち主は1つに保たれる。
    /// </summary>
    public sealed class RunRival : MonoBehaviour
    {
        [Header("参照")]
        [SerializeField] private ScoreSettings settings;

        [Tooltip("ハートを全部割ったときにフィーバーを頼む相手")]
        [SerializeField] private BeatGaugeController gauge;

        [Header("表示用")]
        [Tooltip("UIに出すライバル名")]
        [SerializeField] private string rivalName = "ライバル";

        /// <summary>体力が変化したとき。引数は (現在値, 最大値)</summary>
        public event Action<int, int> OnHpChanged;

        /// <summary>ハートの数が変化したとき</summary>
        public event Action<int> OnHeartsChanged;

        /// <summary>体力バーを削り切ったとき。引数は通算の撃破数</summary>
        public event Action<int> OnDefeated;

        /// <summary>ハートを全部割ったとき</summary>
        public event Action OnAllHeartsBroken;

        public string RivalName => rivalName;
        public int Hp { get; private set; }
        public int MaxHp => settings != null ? settings.rivalMaxHp : 1;
        public int Hearts { get; private set; }
        public int MaxHearts => settings != null ? settings.rivalHearts : 1;

        /// <summary>通算の撃破数。スコア倍率の加算要素になる</summary>
        public int Defeats { get; private set; }

        /// <summary>体力の割合(0〜1)。バー表示用</summary>
        public float HpRate => MaxHp > 0 ? Hp / (float)MaxHp : 0f;

        private void Awake()
        {
            if (settings == null)
            {
                Debug.LogError($"{nameof(ScoreSettings)} が設定されていません", this);
                enabled = false;
                return;
            }

            ResetRival();
        }

        /// <summary>初期状態に戻す。リトライ時に呼ぶ。</summary>
        public void ResetRival()
        {
            Defeats = 0;
            Hearts = settings.rivalHearts;
            Hp = settings.rivalMaxHp;

            OnHeartsChanged?.Invoke(Hearts);
            OnHpChanged?.Invoke(Hp, MaxHp);
        }

        /// <summary>ダメージを与える。削り切ったら true。</summary>
        public bool ApplyDamage(int amount)
        {
            if (amount <= 0 || Hearts <= 0) return false;

            Hp = Mathf.Max(0, Hp - amount);
            OnHpChanged?.Invoke(Hp, MaxHp);

            if (Hp > 0) return false;

            return BreakOneHeart();
        }

        /// <summary>アイテム1つぶんのダメージを与える。</summary>
        public bool ApplyHit() => ApplyDamage(settings.rivalDamagePerHit);

        private bool BreakOneHeart()
        {
            Hearts--;
            Defeats++;

            OnHeartsChanged?.Invoke(Hearts);
            OnDefeated?.Invoke(Defeats);

            if (Hearts > 0)
            {
                // まだハートが残っているので、体力バーだけ満タンに戻して続行
                Hp = settings.rivalMaxHp;
                OnHpChanged?.Invoke(Hp, MaxHp);
                return true;
            }

            OnAllHeartsBroken?.Invoke();
            if (gauge != null) gauge.TriggerFever();

            if (settings.rivalRespawns)
            {
                Hearts = settings.rivalHearts;
                Hp = settings.rivalMaxHp;
                OnHeartsChanged?.Invoke(Hearts);
                OnHpChanged?.Invoke(Hp, MaxHp);
            }

            return true;
        }
    }
}
