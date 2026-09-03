using System;
using UnityEngine;

namespace Yakiniku.Run
{
    /// <summary>
    /// キラメキビートゲージの値そのもの。0〜1 に正規化して保持する。
    ///
    /// あえて MonoBehaviour にしていない：
    /// ・シーンやプレハブに依存しないので EditMode テストが書ける
    /// ・プレイヤー用とライバル用で単純に2個作れる（B担当のゲージ比較で使う）
    /// 「どういう条件で増減するか」のルールは持たず、増減の指示を受けるだけの箱に徹する。
    /// </summary>
    public sealed class BeatGauge
    {
        /// <summary>現在値(0〜1)</summary>
        public float Value { get; private set; }

        public bool IsFull => Value >= 1f;

        /// <summary>値が変化したとき。引数は新しい値</summary>
        public event Action<float> OnChanged;

        /// <summary>満タンになった瞬間（満タンでない状態から1.0に達したとき）</summary>
        public event Action OnFull;

        /// <summary>ゲージを増減する。負の値を渡せば減る。</summary>
        public void Add(float amount)
        {
            if (amount == 0f) return;
            SetValue(Value + amount);
        }

        /// <summary>0に戻す。</summary>
        public void Reset() => SetValue(0f);

        private void SetValue(float next)
        {
            next = Mathf.Clamp01(next);
            if (next == Value) return;

            bool wasFull = IsFull;
            Value = next;

            OnChanged?.Invoke(Value);

            if (!wasFull && IsFull)
            {
                OnFull?.Invoke();
            }
        }
    }
}
