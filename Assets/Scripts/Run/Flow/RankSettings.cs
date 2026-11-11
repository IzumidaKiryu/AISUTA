using System;
using UnityEngine;

namespace Yakiniku.Run
{
    /// <summary>ランク。値が大きいほど上。</summary>
    public enum RunRank
    {
        None = 0,
        C = 1,
        B = 2,
        A = 3,
        S = 4,
        SS = 5,
        SSPlus = 6,
    }

    /// <summary>
    /// ランクの基準と見た目。
    ///
    /// 基準は「ノーミスで走り切ったときのスコア（<see cref="DifficultySettings.Entry.perfectScore"/>）
    /// に対して何割取れたか」で決める。譜面の長さや難易度でスコアの桁が変わっても、
    /// 同じ基準でランクが付くようにするため。
    ///
    /// バッジの画像は <see cref="badges"/> に入れれば差し替わる。
    /// 空のままなら色付きの丸と文字で仮表示する。
    /// </summary>
    [CreateAssetMenu(fileName = "RankSettings", menuName = "Yakiniku/Run/Rank Settings")]
    public sealed class RankSettings : ScriptableObject
    {
        [Serializable]
        public sealed class Tier
        {
            public RunRank rank;
            public string label;

            [Tooltip("ノーミス時のスコアに対する割合。これ以上でこのランク")]
            [Range(0f, 1.5f)] public float threshold;

            [Tooltip("仮表示の色")]
            public Color color = Color.white;

            [Tooltip("差し替え用のバッジ画像。空なら仮表示")]
            public Sprite badge;
        }

        [Tooltip("上のランクから順に並べる")]
        [SerializeField] private Tier[] tiers = Array.Empty<Tier>();

        public Tier[] Tiers => tiers;

        public RunRank Evaluate(int score, int perfectScore)
        {
            if (perfectScore <= 0 || tiers.Length == 0) return RunRank.C;

            float ratio = score / (float)perfectScore;
            foreach (var tier in tiers)
            {
                if (ratio >= tier.threshold) return tier.rank;
            }

            return RunRank.C;
        }

        public Tier Get(RunRank rank)
        {
            foreach (var tier in tiers)
            {
                if (tier.rank == rank) return tier;
            }

            return null;
        }

        /// <summary>そのランクに必要なスコア。リザルトの到達表示に使う。</summary>
        public int ScoreFor(RunRank rank, int perfectScore)
        {
            var tier = Get(rank);
            return tier == null ? 0 : Mathf.CeilToInt(tier.threshold * perfectScore);
        }

        public string Label(RunRank rank)
        {
            var tier = Get(rank);
            return tier != null ? tier.label : "-";
        }

        public const string ResourcePath = "RankSettings";

        private static RankSettings cached;

        public static RankSettings Load()
        {
            if (cached == null) cached = Resources.Load<RankSettings>(ResourcePath);
            return cached;
        }
    }
}
