using UnityEngine;
using TMPro; // TextMeshProを使用するために必要な名前空間の宣言

public class ResultManager : MonoBehaviour
{
    [Header("UI参照")]
    // 最終スコアの数字を表示するためのテキストUI要素の変数
    public TextMeshProUGUI finalScoreText;

    // シーンが読み込まれた直後に呼ばれる初期化関数
    void Start()
    {
        // テキストUIが正常に割り当てられているかを確認する処理
        if (finalScoreText != null)
        {
            // GameDataに静的保存されている最終スコアを文字に変換して画面に表示する
            finalScoreText.text = "Final Score\n" + GameData.finalScore;
        }
        else
        {
            Debug.LogWarning("FinalScoreTextがインスペクターで割り当てられていません！");
        }
    }
}