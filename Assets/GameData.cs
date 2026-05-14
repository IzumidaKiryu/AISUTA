// シーン間で共有するデータを保持する静的クラス
public static class GameData
{
    // 選択された難易度に応じたJSONファイル名を保持する静的変数（初期値はNormal）
    public static string selectedJsonName = "StageData_Normal";

    // インゲームで獲得した最終スコアを保持する静的変数
    public static int finalScore = 0;
}