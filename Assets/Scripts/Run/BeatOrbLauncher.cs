using System.Collections.Generic;
using UnityEngine;

namespace Yakiniku.Run
{
    /// <summary>
    /// ノーツを取ったとき、取った位置から奥のライバルへ白い球を飛ばす（本家の演出）。
    /// **球がライバルに届いた時点で**体力を削る。
    ///
    /// ライバルへのダメージはここだけが入れる。取った瞬間に削らないのは、
    /// 「球が飛んでいって当たる」という因果が見えないと、体力バーが勝手に減っているように
    /// 見えてしまうため。
    /// </summary>
    public sealed class BeatOrbLauncher : MonoBehaviour
    {
        [Header("参照")]
        [SerializeField] private PlayerRunner player;
        [SerializeField] private RunRival rival;

        [Tooltip("球の着弾点。ライバルの胸あたりに置く")]
        [SerializeField] private Transform target;

        [Tooltip("飛ばす球のプレハブ")]
        [SerializeField] private GameObject orbPrefab;

        [Header("軌道")]
        [Tooltip("届くまでの時間(秒)")]
        [SerializeField, Min(0.05f)] private float flightTime = 0.55f;

        [Tooltip("弧の高さ(m)。取ったレーンの左右位置によって少しずつ変える")]
        [SerializeField, Min(0f)] private float arcHeight = 6f;

        [Tooltip("左右にふくらませる量(m)。外側のレーンほど外に膨らむ")]
        [SerializeField] private float sideBulge = 1.5f;

        private sealed class Flight
        {
            public Transform orb;
            public TrailRenderer trail;
            public Vector3 start;
            public Vector3 control;
            public float elapsed;
        }

        private readonly List<Flight> flying = new List<Flight>();
        private readonly Stack<Flight> pool = new Stack<Flight>();

        /// <summary>今飛んでいる球の数（テスト用）</summary>
        public int InFlight => flying.Count;

        /// <summary>着弾した球の数（テスト用）</summary>
        public int Landed { get; private set; }

        private void OnEnable()
        {
            if (player != null) player.OnItemCollected += Launch;
        }

        private void OnDisable()
        {
            if (player != null) player.OnItemCollected -= Launch;
        }

        private void Launch(BeatItem item)
        {
            if (rival == null || target == null || orbPrefab == null) return;

            // 撃破した後も球は飛ばす（スコアには関係ないが、手応えが消えると寂しい）
            Vector3 start = item != null ? item.transform.position : player.transform.position;

            var flight = pool.Count > 0 ? pool.Pop() : CreateFlight();
            flight.start = start;
            flight.elapsed = 0f;

            // 取ったレーンの外側へ膨らませると、球が重ならずに扇状に飛んでいく
            float side = Mathf.Sign(start.x - target.position.x);
            Vector3 mid = (start + target.position) * 0.5f;
            flight.control = mid + Vector3.up * arcHeight + Vector3.right * side * sideBulge;

            flight.orb.position = start;
            flight.orb.gameObject.SetActive(true);
            if (flight.trail != null) flight.trail.Clear();

            flying.Add(flight);
        }

        private Flight CreateFlight()
        {
            var instance = Instantiate(orbPrefab, transform);
            instance.SetActive(false);

            return new Flight
            {
                orb = instance.transform,
                trail = instance.GetComponentInChildren<TrailRenderer>(),
            };
        }

        private void Update()
        {
            if (flying.Count == 0) return;

            // 走っている間にライバルも前へ進むので、終点は毎フレーム取り直す
            Vector3 end = target.position;

            for (int i = flying.Count - 1; i >= 0; i--)
            {
                var flight = flying[i];
                flight.elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(flight.elapsed / flightTime);
                flight.orb.position = Bezier(flight.start, flight.control, end, EaseIn(t));

                if (t < 1f) continue;

                flying.RemoveAt(i);
                flight.orb.gameObject.SetActive(false);
                pool.Push(flight);

                Landed++;
                rival.ApplyHit();
            }
        }

        /// <summary>だんだん加速して吸い込まれるように</summary>
        private static float EaseIn(float t) => t * t * (2f - t * 0.6f) / 1.4f;

        private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }
    }
}
