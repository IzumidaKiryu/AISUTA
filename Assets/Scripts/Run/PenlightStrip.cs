using UnityEngine;

namespace Yakiniku.Run
{
    /// <summary>
    /// 床タイルの脇に並ぶ観客のペンライト1列ぶん。
    ///
    /// 床タイルの子として生成され、タイルが前方に回されたときに
    /// <see cref="OnTileRecycled"/> が呼ばれて並びを作り直す。
    ///
    /// パーティクルの Simulation Space は World にしておくこと。
    /// Local にすると、タイルを前方へ動かした瞬間に既に出ている粒が一緒に移動して
    /// 「ペンライトがプレイヤーに付いてくる」状態になる。
    /// </summary>
    public sealed class PenlightStrip : MonoBehaviour, ITileRecycleListener
    {
        [Tooltip("撒き直す対象。空なら自分と子から探す（見た目のプレハブを子に抱える構成を想定）")]
        [SerializeField] private ParticleSystem ps;

        private void Awake()
        {
            if (ps == null) ps = GetComponentInChildren<ParticleSystem>(true);
            if (ps == null) Debug.LogError("ペンライトのParticleSystemが見つかりません", this);
        }

        public void OnTileRecycled()
        {
            if (ps == null) return;

            // 前の位置に出した粒を消してから、新しい位置で撒き直す。
            // autoRandomSeed が有効なら Play のたびに並びが変わるので、
            // 同じ模様のタイルが繰り返して見えない。
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(true);
            ps.Play(true);
        }
    }
}
