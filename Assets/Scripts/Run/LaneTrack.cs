using UnityEngine;

namespace Yakiniku.Run
{
    /// <summary>
    /// 床タイル1枚ぶんのレーン（暗い路面と、光るレーン境界線）。
    ///
    /// 本家はレーンが暗い路面に水色の境界線で区切られていて、
    /// ENCORE で金色基調に変わる。床のアセット自体は触らず、その上に1枚重ねている。
    /// どのレーンに居るか・ノーツがどのレーンに来るかが読みやすくなる。
    ///
    /// 色は <see cref="StageTheme"/> が持つ共有材質で決まるので、ここは材質を割り当てるだけ。
    /// </summary>
    public sealed class LaneTrack : MonoBehaviour
    {
        [Tooltip("レーン面。左のレーンから順に")]
        [SerializeField] private Renderer[] lanes = new Renderer[0];

        [Tooltip("境界線。左端から順に")]
        [SerializeField] private Renderer[] lines = new Renderer[0];

        private void Start()
        {
            var theme = StageTheme.Current;
            if (theme == null) return;

            for (int i = 0; i < lanes.Length; i++)
            {
                if (lanes[i] != null) lanes[i].sharedMaterial = theme.LaneMaterial(i);
            }

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i] != null) lines[i].sharedMaterial = theme.LineMaterial(i);
            }
        }
    }
}
