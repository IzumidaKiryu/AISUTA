namespace Yakiniku.Run
{
    /// <summary>
    /// 画面遷移の入口。難易度選択・ゲーム・リザルトのどこからでもここを呼ぶ。
    ///
    /// シーン名をあちこちに直書きしないためと、遷移のたびに必ず
    /// ☆のローディング（<see cref="RunLoadingOverlay"/>）を挟むためにまとめている。
    /// </summary>
    public static class RunFlow
    {
        public const string TitleScene = "Title";
        public const string SelectScene = "Select";
        public const string GameScene = "Game_Run";
        public const string ResultScene = "Result";

        /// <summary>難易度を選んでゲームを始める。</summary>
        public static void StartStage(RunDifficulty difficulty)
        {
            RunSession.Difficulty = difficulty;

            // 既存の譜面読み込み(JsonNoteProvider / StageManager)は GameData を見るので合わせておく
            var entry = DifficultySettings.Load()?.Get(difficulty);
            if (entry != null) GameData.selectedJsonName = entry.chartFile;

            RunLoadingOverlay.Transition(GameScene);
        }

        /// <summary>同じ難易度でもう一度遊ぶ。</summary>
        public static void Retry() => StartStage(RunSession.Difficulty);

        public static void ToSelect() => RunLoadingOverlay.Transition(SelectScene);

        public static void ToResult() => RunLoadingOverlay.Transition(ResultScene);

        public static void ToTitle() => RunLoadingOverlay.Transition(TitleScene);
    }
}
