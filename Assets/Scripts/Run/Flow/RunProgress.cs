using UnityEngine;

namespace Yakiniku.Run
{
    /// <summary>
    /// 難易度ごとのハイスコア・最高ランク・クリア状況の保存。
    ///
    /// PlayerPrefs に置いている。Android でもそのまま使える。
    /// 端末をまたいだ保存が必要になったら、ここだけ差し替えればよい。
    /// </summary>
    public static class RunProgress
    {
        private const string Prefix = "Yakiniku.Run.";

        private static string Key(RunDifficulty difficulty, string field)
            => Prefix + difficulty + "." + field;

        public static int HighScore(RunDifficulty difficulty)
            => PlayerPrefs.GetInt(Key(difficulty, "HighScore"), 0);

        public static RunRank BestRank(RunDifficulty difficulty)
            => (RunRank)PlayerPrefs.GetInt(Key(difficulty, "BestRank"), (int)RunRank.None);

        /// <summary>一度でも WIN したか</summary>
        public static bool Cleared(RunDifficulty difficulty)
            => PlayerPrefs.GetInt(Key(difficulty, "Cleared"), 0) == 1;

        /// <summary>
        /// 遊べるか。解放条件付きの難易度は、指定の難易度を指定ランク以上で
        /// クリアしているときだけ遊べる（本家: Hard を SS でクリアすると Universe）。
        /// </summary>
        public static bool IsUnlocked(DifficultySettings.Entry entry)
        {
            if (entry == null) return false;
            if (!entry.locked) return true;

            return Cleared(entry.unlockBy) && BestRank(entry.unlockBy) >= entry.unlockRank;
        }

        /// <summary>
        /// 結果を記録する。ハイスコアと最高ランクは良いほうだけ残す。
        /// ランクは WIN したときだけ記録する（負けたのにランクが付くと解放条件がおかしくなる）。
        /// </summary>
        public static void Record(RunResult result)
        {
            if (result == null) return;

            var d = result.difficulty;
            result.previousHighScore = HighScore(d);
            result.isNewRecord = result.score > result.previousHighScore;

            if (result.isNewRecord)
            {
                PlayerPrefs.SetInt(Key(d, "HighScore"), result.score);
            }

            if (result.IsWin)
            {
                PlayerPrefs.SetInt(Key(d, "Cleared"), 1);
                if (result.rank > BestRank(d))
                {
                    PlayerPrefs.SetInt(Key(d, "BestRank"), (int)result.rank);
                }
            }

            PlayerPrefs.Save();
        }

        /// <summary>
        /// 保存内容を丸ごと取り出す。自動テストが走った後に元へ戻すため
        /// （テストの結果で手元のハイスコアや解放状態が書き換わらないように）。
        /// </summary>
        public static System.Collections.Generic.Dictionary<string, int> Snapshot()
        {
            var result = new System.Collections.Generic.Dictionary<string, int>();
            foreach (RunDifficulty d in System.Enum.GetValues(typeof(RunDifficulty)))
            {
                foreach (var field in new[] { "HighScore", "BestRank", "Cleared" })
                {
                    string key = Key(d, field);
                    if (PlayerPrefs.HasKey(key)) result[key] = PlayerPrefs.GetInt(key);
                }
            }

            return result;
        }

        public static void Restore(System.Collections.Generic.Dictionary<string, int> snapshot)
        {
            ResetAll();
            if (snapshot == null) return;

            foreach (var pair in snapshot) PlayerPrefs.SetInt(pair.Key, pair.Value);
            PlayerPrefs.Save();
        }

        /// <summary>保存内容を消す（デバッグ用）。</summary>
        public static void ResetAll()
        {
            foreach (RunDifficulty d in System.Enum.GetValues(typeof(RunDifficulty)))
            {
                PlayerPrefs.DeleteKey(Key(d, "HighScore"));
                PlayerPrefs.DeleteKey(Key(d, "BestRank"));
                PlayerPrefs.DeleteKey(Key(d, "Cleared"));
            }

            PlayerPrefs.Save();
        }
    }
}
