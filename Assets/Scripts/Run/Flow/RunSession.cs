namespace Yakiniku.Run
{
    /// <summary>1回のプレイの結果。ゲームからリザルトへ渡す。</summary>
    public sealed class RunResult
    {
        public RunDifficulty difficulty;
        public int score;
        public int maxCombo;
        public int itemsCollected;
        public int totalItems;
        public int damageTaken;
        public int heartsBroken;

        /// <summary>ライバルを撃破したか（ハートを全部割ったか）</summary>
        public bool rivalDefeated;

        /// <summary>最後まで生き残ったか</summary>
        public bool survived;

        public RunRank rank;
        public int previousHighScore;
        public bool isNewRecord;

        /// <summary>勝敗。ライバルを撃破して、かつ生き残っていれば WIN</summary>
        public bool IsWin => rivalDefeated && survived;

        public bool IsFullCombo => totalItems > 0 && maxCombo >= totalItems;
    }

    /// <summary>
    /// シーンをまたいで持ち回る情報。
    ///
    /// 既存の <see cref="GameData"/> も同じ役割だが、譜面ファイル名とスコアしか持てないので
    /// 難易度や結果の詳細はこちらに置く。GameData も互換のために一緒に更新している。
    /// </summary>
    public static class RunSession
    {
        /// <summary>選ばれた難易度</summary>
        public static RunDifficulty Difficulty = RunDifficulty.Normal;

        /// <summary>直前のプレイ結果。リザルト画面が読む</summary>
        public static RunResult LastResult;
    }
}
