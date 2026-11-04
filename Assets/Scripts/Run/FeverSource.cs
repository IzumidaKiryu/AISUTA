using System;

namespace Yakiniku.Run
{
    /// <summary>
    /// フィーバーが始まったきっかけ。
    ///
    /// フィーバー中にもう一方のきっかけを満たすとスーパーフィーバーに昇格するので、
    /// 「どれで発動したか」を覚えておく必要がある。
    /// 複数立てられるようにビットで持つ。
    /// </summary>
    [Flags]
    public enum FeverSource
    {
        None = 0,

        /// <summary>キラメキビートゲージが満タンになった</summary>
        Gauge = 1 << 0,

        /// <summary>ライバルのハートを全部割った</summary>
        Rival = 1 << 1,
    }
}
