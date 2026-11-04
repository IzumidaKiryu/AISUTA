using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Yakiniku.Run
{
    /// <summary>
    /// フィーバー中に画面全体の Bloom を強めて派手にする。
    /// スーパーフィーバーではさらに強く、脈打たせる。
    ///
    /// ペンライトは HDR の明るい色で描かれているので、Bloom を上げるだけで
    /// 客席全体がぶわっと光る。個々のパーティクルを触る必要がない。
    ///
    /// 【重要】`volume.sharedProfile` ではなく `volume.profile` を使うこと。
    /// sharedProfile を実行時に書き換えると、アセットそのものが書き換わって
    /// エディタを止めても値が戻らない。`profile` は実行時に複製を作る。
    /// </summary>
    public sealed class FeverScreenEffect : MonoBehaviour
    {
        [Header("参照")]
        [SerializeField] private BeatGaugeController gauge;

        [Tooltip("Bloom を持つ Volume。未設定ならシーンから探す")]
        [SerializeField] private Volume volume;

        [Header("Bloom の強さ")]
        [Tooltip("通常時。0以下ならプロファイルの元の値を使う")]
        [SerializeField] private float normalIntensity = -1f;

        [SerializeField, Min(0f)] private float feverIntensity = 4f;
        [SerializeField, Min(0f)] private float superFeverIntensity = 9f;

        [Tooltip("スーパーフィーバー中の脈打ちの速さ")]
        [SerializeField, Min(0f)] private float superPulseSpeed = 8f;

        [Tooltip("スーパーフィーバー中に強さが揺れる幅")]
        [SerializeField, Min(0f)] private float superPulseAmount = 3f;

        [Tooltip("強さが変わるときの追従の速さ。小さいほどゆっくり")]
        [SerializeField, Min(0.01f)] private float blendSpeed = 6f;

        private Bloom bloom;
        private float baseIntensity;
        private float current;

        private void Start()
        {
            if (volume == null) volume = FindFirstObjectByType<Volume>();
            if (volume == null)
            {
                enabled = false;
                return;
            }

            // profile は実行時の複製を返す。元のアセットは汚れない
            if (volume.profile == null || !volume.profile.TryGet(out bloom))
            {
                Debug.LogWarning("Volume に Bloom がありません。フィーバーの光り方は変わりません", this);
                enabled = false;
                return;
            }

            baseIntensity = normalIntensity > 0f ? normalIntensity : bloom.intensity.value;
            current = baseIntensity;
        }

        private void Update()
        {
            if (bloom == null) return;

            float target = baseIntensity;

            if (gauge != null && gauge.IsSuperFever)
            {
                float pulse = Mathf.PingPong(Time.time * superPulseSpeed, 1f);
                target = superFeverIntensity + pulse * superPulseAmount;
            }
            else if (gauge != null && gauge.IsFever)
            {
                target = feverIntensity;
            }

            current = Mathf.Lerp(current, target, 1f - Mathf.Exp(-blendSpeed * Time.deltaTime));
            bloom.intensity.Override(current);
        }
    }
}
