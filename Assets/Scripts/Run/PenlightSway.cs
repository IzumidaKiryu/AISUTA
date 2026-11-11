using UnityEngine;

namespace Yakiniku.Run
{
    /// <summary>
    /// 客席のペンライトを曲に合わせて左右に振り、色をステージの状態に合わせる。
    ///
    /// 本家のペンライトは常に左右に揺れていて、ENCORE で水色から橙に変わる。
    /// パーティクルは「出した後に動かない」ので、毎フレーム粒を取り出して
    /// 傾きと色を書き戻している。
    ///
    /// 色は粒ごとの乱数の種（randomSeed）から決めるので、状態が変わっても
    /// 同じ粒は同じ並びの色を取り、ちらつかない。
    /// </summary>
    public sealed class PenlightSway : MonoBehaviour
    {
        [Tooltip("揺らす対象。空なら自分と子から探す")]
        [SerializeField] private ParticleSystem ps;

        [Header("揺れ")]
        [Tooltip("左右に傾く角度(度)")]
        [SerializeField, Range(0f, 60f)] private float amplitude = 22f;

        [Tooltip("何拍で1往復するか。2なら「右・左」で2拍")]
        [SerializeField, Min(0.25f)] private float beatsPerSway = 2f;

        [Tooltip("BPMが分からないときの値")]
        [SerializeField, Min(1f)] private float fallbackBpm = 150f;

        [Tooltip("客席の場所によるずれ。0だと全員揃って振る。大きいと波のように見える")]
        [SerializeField, Range(0f, 1f)] private float phaseSpread = 0.15f;

        private ParticleSystem.Particle[] buffer;
        private float bpm;

        private void Awake()
        {
            if (ps == null) ps = GetComponentInChildren<ParticleSystem>(true);
        }

        private void Start()
        {
            // 曲のテンポは譜面から取る。無ければ既定値
            var provider = FindFirstObjectByType<NoteProviderBehaviour>();
            bpm = provider != null && provider.Bpm > 0f ? provider.Bpm : fallbackBpm;
        }

        private void LateUpdate()
        {
            if (ps == null) return;

            int max = ps.main.maxParticles;
            if (buffer == null || buffer.Length < max) buffer = new ParticleSystem.Particle[max];

            int count = ps.GetParticles(buffer);
            if (count == 0) return;

            var theme = StageTheme.Current;
            float beat = Time.time * bpm / 60f;
            float baseAngle = beat / beatsPerSway * Mathf.PI * 2f;

            for (int i = 0; i < count; i++)
            {
                var p = buffer[i];

                // 粒の位置で少しずらして、客席にうねりを出す
                float phase = (p.position.x * 0.7f + p.position.z * 0.05f) * phaseSpread;
                float angle = Mathf.Sin(baseAngle + phase) * amplitude;
                p.rotation3D = new Vector3(0f, 0f, angle);

                if (theme != null) p.startColor = theme.PenlightColor(p.randomSeed);

                buffer[i] = p;
            }

            ps.SetParticles(buffer, count);
        }
    }
}
