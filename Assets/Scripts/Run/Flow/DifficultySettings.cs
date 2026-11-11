using System;
using UnityEngine;

namespace Yakiniku.Run
{
    /// <summary>難易度。並び順がそのまま難易度選択画面の並びになる。</summary>
    public enum RunDifficulty
    {
        Easy = 0,
        Normal = 1,
        Hard = 2,
        Universe = 3,
    }

    /// <summary>
    /// 難易度ごとの設定。難易度選択・バランス調整・ランク計算・解放条件を1か所にまとめる。
    ///
    /// 難易度を足したいときはここに1行足すだけで、難易度選択画面にも並ぶ。
    /// </summary>
    [CreateAssetMenu(fileName = "DifficultySettings", menuName = "Yakiniku/Run/Difficulty Settings")]
    public sealed class DifficultySettings : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public RunDifficulty difficulty;

            [Tooltip("画面に出す名前")]
            public string displayName;

            [Tooltip("Resources フォルダの譜面ファイル名（拡張子なし）")]
            public string chartFile;

            [Tooltip("フルコンボしたとき、曲の何割の地点でライバルを撃破するか。" +
                     "本家は Hard 以上で 9割")]
            [Range(0.1f, 1f)] public float rivalDefeatAt = 0.9f;

            [Tooltip("ランク計算の基準になる、ノーミスで走り切ったときのスコア。" +
                     "自動プレイテストで測った値を入れる")]
            [Min(1)] public int perfectScore = 20000;

            [Tooltip("難易度選択画面での色")]
            public Color themeColor = Color.white;

            [Header("解放条件")]
            [Tooltip("最初は遊べない難易度か")]
            public bool locked;

            [Tooltip("どの難易度を")]
            public RunDifficulty unlockBy = RunDifficulty.Hard;

            [Tooltip("どのランク以上でクリアすれば解放されるか")]
            public RunRank unlockRank = RunRank.SS;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public Entry[] Entries => entries;

        public Entry Get(RunDifficulty difficulty)
        {
            foreach (var entry in entries)
            {
                if (entry != null && entry.difficulty == difficulty) return entry;
            }

            return null;
        }

        /// <summary>譜面ファイル名から逆引きする（旧来の GameData.selectedJsonName との互換用）。</summary>
        public Entry FindByChart(string chartFile)
        {
            if (string.IsNullOrEmpty(chartFile)) return null;

            foreach (var entry in entries)
            {
                if (entry != null && entry.chartFile == chartFile) return entry;
            }

            return null;
        }

        /// <summary>シーンから参照するための既定の置き場所（Resources 配下）。</summary>
        public const string ResourcePath = "DifficultySettings";

        private static DifficultySettings cached;

        /// <summary>
        /// どのシーンからでも取れるように Resources から読む。
        /// 難易度選択・ゲーム・リザルトの3つのシーンが同じ設定を見る必要があるため。
        /// </summary>
        public static DifficultySettings Load()
        {
            if (cached == null) cached = Resources.Load<DifficultySettings>(ResourcePath);
            return cached;
        }
    }
}
