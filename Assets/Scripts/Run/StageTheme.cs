using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Yakiniku.Run
{
    /// <summary>
    /// ステージ全体の色。通常／フィーバー／ENCORE／スーパーで、
    /// レーン・ペンライト・光の色味をまとめて切り替える。
    ///
    /// 【明るさではなく色で演出する】
    /// フィーバーで Bloom の強さを上げると画面が白飛びして譜面が見えなくなる。
    /// 明るさは全状態で据え置き、色相だけを変える（本家も ENCORE で
    /// レーンと客席が金色基調になるだけで、眩しくはならない）。
    ///
    /// レーンの材質はここが実行時に複製して持ち、各タイルはそれを共有する。
    /// 色を変えるのは材質の数だけで済み、タイルの枚数に比例しない。
    /// </summary>
    public sealed class StageTheme : MonoBehaviour
    {
        public enum State { Normal, Fever, Encore, Super }

        [System.Serializable]
        public sealed class Palette
        {
            [Tooltip("レーンの境界線（HDR）")]
            [ColorUsage(false, true)] public Color line = Color.cyan;

            [Tooltip("レーン面。奇数レーンと偶数レーンで少し変えて市松っぽくする")]
            public Color laneA = Color.black;
            public Color laneB = Color.black;

            [Tooltip("ペンライトの色相。粒ごとにどれか1つが選ばれる。" +
                     "粒の色は8bit(Color32)なので1.0を超える値は切り捨てられる。" +
                     "明るさはペンライトの材質側(HDR)で持たせているので、ここは彩度の高い色を入れる")]
            public Color[] penlights = new Color[0];

            [Tooltip("光(Bloom)に乗せる色味")]
            public Color bloomTint = Color.white;
        }

        /// <summary>シーン内の1つ。レーンやペンライトが後から生成されても見つけられるように。</summary>
        public static StageTheme Current { get; private set; }

        [Header("参照")]
        [SerializeField] private BeatGaugeController gauge;

        [Tooltip("色味を変える Volume。未設定ならシーンから探す")]
        [SerializeField] private Volume volume;

        [Header("材質の元（実行時に複製する）")]
        [SerializeField] private Material laneMaterial;
        [SerializeField] private Material lineMaterial;

        [Header("配色")]
        [SerializeField] private Palette normal = new Palette();
        [SerializeField] private Palette fever = new Palette();
        [SerializeField] private Palette encore = new Palette();

        [Header("スーパーフィーバー（虹色）")]
        [Tooltip("色相が一周する速さ")]
        [SerializeField, Min(0f)] private float rainbowSpeed = 0.35f;

        [Tooltip("レーン線の明るさ。通常時と揃えて眩しくしない")]
        [SerializeField, Min(0f)] private float rainbowLineIntensity = 2.2f;

        [Header("切り替え")]
        [Tooltip("色が切り替わるまでの時間(秒)")]
        [SerializeField, Min(0.01f)] private float blendTime = 0.4f;

        public const int LaneCount = 5;
        public const int LineCount = LaneCount + 1;

        public State CurrentState { get; private set; }

        private Material[] laneMaterials;
        private Material[] lineMaterials;
        private Bloom bloom;

        // 前の状態から今の状態へ、blend を 0→1 で進める
        private State fromState;
        private float blend = 1f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private void Awake()
        {
            Current = this;

            laneMaterials = Duplicate(laneMaterial, LaneCount, "Lane");
            lineMaterials = Duplicate(lineMaterial, LineCount, "Line");
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;

            // 実行時に作った複製は自分で片付ける（アセットは汚さない）
            DestroyAll(laneMaterials);
            DestroyAll(lineMaterials);
        }

        private void Start()
        {
            if (volume == null) volume = FindFirstObjectByType<Volume>();

            // profile は実行時の複製を返す。sharedProfile を触るとアセットが書き換わる
            if (volume != null && volume.profile != null) volume.profile.TryGet(out bloom);

            CurrentState = Evaluate();
            fromState = CurrentState;
            blend = 1f;
            Apply();
        }

        private void Update()
        {
            var next = Evaluate();
            if (next != CurrentState)
            {
                fromState = CurrentState;
                CurrentState = next;
                blend = 0f;
            }

            blend = Mathf.MoveTowards(blend, 1f, Time.deltaTime / blendTime);
            Apply();
        }

        private State Evaluate()
        {
            if (gauge == null) return State.Normal;
            if (gauge.IsSuperFever) return State.Super;
            if (gauge.IsEncore) return State.Encore;
            if (gauge.IsFever) return State.Fever;
            return State.Normal;
        }

        /// <summary>レーン面の材質。レーン番号(0〜4)ごとに別にしてある（虹色をレーンで変えるため）</summary>
        public Material LaneMaterial(int index) => laneMaterials[Mathf.Clamp(index, 0, LaneCount - 1)];

        /// <summary>境界線の材質。境界番号(0〜5)ごと</summary>
        public Material LineMaterial(int index) => lineMaterials[Mathf.Clamp(index, 0, LineCount - 1)];

        /// <summary>
        /// ペンライト1本の今の色。<paramref name="seed"/> は粒ごとに固定の値を渡す。
        /// 同じ粒は状態が変わっても同じ並びの色を取るので、色がチラつかない。
        /// </summary>
        public Color PenlightColor(uint seed)
        {
            Color to = PenlightColorOf(CurrentState, seed);
            if (blend >= 1f) return to;
            return Color.Lerp(PenlightColorOf(fromState, seed), to, blend);
        }

        // 毎フレーム呼ばれるので、ラムダやクロージャは使わない（Android で GC が走ってカクつく）
        private void Apply()
        {
            bool blending = blend < 1f;

            for (int i = 0; i < LaneCount; i++)
            {
                Color to = LaneColorOf(CurrentState, i);
                SetColor(laneMaterials[i], blending ? Color.Lerp(LaneColorOf(fromState, i), to, blend) : to);
            }

            for (int i = 0; i < LineCount; i++)
            {
                Color to = LineColorOf(CurrentState, i);
                SetColor(lineMaterials[i], blending ? Color.Lerp(LineColorOf(fromState, i), to, blend) : to);
            }

            if (bloom != null)
            {
                Color to = BloomTintOf(CurrentState);
                bloom.tint.Override(blending ? Color.Lerp(BloomTintOf(fromState), to, blend) : to);
            }
        }

        private Palette PaletteOf(State state)
        {
            switch (state)
            {
                case State.Fever: return fever;
                case State.Encore: return encore;
                default: return normal;
            }
        }

        private Color LaneColorOf(State state, int lane)
        {
            if (state == State.Super)
            {
                // レーンごとに色相をずらして、虹がレーンを横切って流れるように
                return Color.HSVToRGB(Hue(lane / (float)LaneCount), 0.75f, 0.55f);
            }

            var palette = PaletteOf(state);
            return lane % 2 == 0 ? palette.laneA : palette.laneB;
        }

        private Color LineColorOf(State state, int line)
        {
            if (state == State.Super)
            {
                return Color.HSVToRGB(Hue(line / (float)LineCount), 0.9f, 1f) * rainbowLineIntensity;
            }

            return PaletteOf(state).line;
        }

        private Color PenlightColorOf(State state, uint seed)
        {
            if (state == State.Super)
            {
                return Color.HSVToRGB(Hue((seed % 997) / 997f), 0.9f, 1f);
            }

            var colors = PaletteOf(state).penlights;
            if (colors == null || colors.Length == 0) return Color.white;
            return colors[seed % (uint)colors.Length];
        }

        private Color BloomTintOf(State state)
        {
            if (state == State.Super) return Color.HSVToRGB(Hue(0f), 0.35f, 1f);
            return PaletteOf(state).bloomTint;
        }

        private float Hue(float offset) => Mathf.Repeat(Time.time * rainbowSpeed + offset, 1f);

        private static void SetColor(Material material, Color color)
        {
            if (material == null) return;
            if (material.HasProperty(BaseColorId)) material.SetColor(BaseColorId, color);
            if (material.HasProperty(ColorId)) material.SetColor(ColorId, color);
        }

        private static Material[] Duplicate(Material source, int count, string label)
        {
            var result = new Material[count];
            for (int i = 0; i < count; i++)
            {
                if (source == null) continue;
                result[i] = new Material(source) { name = $"{source.name} ({label}{i})" };
            }

            return result;
        }

        private static void DestroyAll(Material[] materials)
        {
            if (materials == null) return;
            foreach (var m in materials)
            {
                if (m != null) Destroy(m);
            }
        }
    }
}
