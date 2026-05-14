using UnityEngine;
using UnityEngine.SceneManagement; // シーン遷移機能を使用するための名前空間

public class SceneController : MonoBehaviour
{
    // タイトル画面へ遷移する関数
    public void GoToTitle()
    {
        // "Title"シーンをロードする
        SceneManager.LoadScene("Title");
    }

    // 難易度選択画面へ遷移する関数
    public void GoToSelect()
    {
        // "Select"シーンをロードする
        SceneManager.LoadScene("Select");
    }

    // 難易度（JSONファイル名）を受け取ってゲームを開始する関数
    public void StartGame(string jsonFileName)
    {
        // 渡されたJSONファイル名をGameDataに保存し、次のシーンへ引き継ぐ
        GameData.selectedJsonName = jsonFileName;

        // "Game"シーンをロードする
        SceneManager.LoadScene("Game");
    }

    // リザルト画面へ遷移する関数
    public void GoToResult()
    {
        // "Result"シーンをロードする
        SceneManager.LoadScene("Result");
    }
}