using System;
using UnityEngine;

namespace Yakiniku.Run
{
    /// <summary>
    /// ライバルの体力とハート。
    ///
    /// ノーツを取ると白い球がライバルへ飛び（<see cref="BeatOrbLauncher"/>）、
    /// 着弾するたびに体力バーが削れる。削り切るとハートが1つ減り、
    /// **ハートを全部割ると ENCORE TIME**。そこから曲の終わりまでレーンが金色になる。
    ///
    /// 体力は <see cref="RunBalance"/> が譜面のノーツ数から決める。
    /// 「フルコンボで曲の9割地点で撃破」のように、難易度ごとの撃破タイミングを
    /// 譜面の長さに関係なく揃えるため。
    /// </summary>
    public sealed class RunRival : MonoBehaviour
    {
        [Header("参照")]
        [SerializeField] private ScoreSettings settings;

        [Tooltip("ハートを全部割ったときに ENCORE を頼む相手")]
        [SerializeField] private BeatGaugeController gauge;

        [Header("表示用")]
        [Tooltip("UIに出すライバル名")]
        [SerializeField] private string rivalName = "ライバル";

        /// <summary>体力が変化したとき。引数は (現在値, 最大値)</summary>
        public event Action<int, int> OnHpChanged;

        /// <summary>ハートの数が変化したとき</summary>
        public event Action<int> OnHeartsChanged;

        /// <summary>ダメージを受けたとき（演出用）</summary>
        public event Action OnHit;

        /// <summary>ハートを1つ割ったとき。引数は割ったハートの通算数</summary>
        public event Action<int> OnHeartBroken;

        /// <summary>ハートを全部割ったとき（＝撃破、ENCORE 開始）</summary>
        public event Action OnDefeated;

        public string RivalName => rivalName;
        public int Hp { get; private set; }
        public int MaxHp { get; private set; } = 1;
        public int Hearts { get; private set; }
        public int MaxHearts => settings != null ? settings.rivalHearts : 1;

        /// <summary>割ったハートの数。スコア倍率の加算要素になる</summary>
        public int HeartsBroken { get; private set; }

        /// <summary>互換用。倍率計算は割ったハートの数を使う</summary>
        public int Defeats => HeartsBroken;

        /// <summary>ハートを全部割ったか（勝敗判定に使う）</summary>
        public bool IsDefeated { get; private set; }

        /// <summary>体力の割合(0〜1)。バー表示用</summary>
        public float HpRate => MaxHp > 0 ? Hp / (float)MaxHp : 0f;

        /// <summary>全ハートぶんの総体力</summary>
        public int TotalHp => MaxHp * MaxHearts;

        private void Awake()
        {
            if (settings == null)
            {
                Debug.LogError($"{nameof(ScoreSettings)} が設定されていません", this);
                enabled = false;
                return;
            }

            MaxHp = settings.rivalMaxHp;
            ResetRival();
        }

        /// <summary>
        /// 体力バー1本ぶんの最大値を決める。<see cref="RunBalance"/> が Start で呼ぶ。
        /// </summary>
        public void Configure(int hpPerHeart)
        {
            MaxHp = Mathf.Max(1, hpPerHeart);
            ResetRival();
        }

        /// <summary>初期状態に戻す。リトライ時に呼ぶ。</summary>
        public void ResetRival()
        {
            HeartsBroken = 0;
            IsDefeated = false;
            Hearts = MaxHearts;
            Hp = MaxHp;

            OnHeartsChanged?.Invoke(Hearts);
            OnHpChanged?.Invoke(Hp, MaxHp);
        }

        /// <summary>ノーツ1つぶんのダメージを与える。</summary>
        public bool ApplyHit() => ApplyDamage(settings.rivalDamagePerHit);

        /// <summary>ダメージを与える。ハートを割ったら true。撃破後は何も起きない。</summary>
        public bool ApplyDamage(int amount)
        {
            if (amount <= 0 || IsDefeated) return false;

            Hp = Mathf.Max(0, Hp - amount);
            OnHit?.Invoke();
            OnHpChanged?.Invoke(Hp, MaxHp);

            if (Hp > 0) return false;

            BreakHeart();
            return true;
        }

        private void BreakHeart()
        {
            Hearts--;
            HeartsBroken++;

            OnHeartsChanged?.Invoke(Hearts);
            OnHeartBroken?.Invoke(HeartsBroken);

            if (Hearts > 0)
            {
                // まだハートが残っているので、体力バーだけ満タンに戻して続行
                Hp = MaxHp;
                OnHpChanged?.Invoke(Hp, MaxHp);
                return;
            }

            // 全部割った。ここから曲の終わりまで ENCORE TIME
            IsDefeated = true;
            OnDefeated?.Invoke();
            if (gauge != null) gauge.StartEncore();
        }
    }
}
