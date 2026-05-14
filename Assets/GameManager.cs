using UnityEngine;
using TMPro; // TextMeshProを使用するために必要な名前空間の宣言
using System.Collections;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    // どこからでもアクセスできるようにするための「シングルトン」インスタンス変数
    public static GameManager instance;

    [Header("スコア＆コンボ")]
    // 現在のスコアを保持する変数
    public int score = 0;
    // 現在のコンボ数を保持する変数
    public int currentCombo = 0;

    [Header("ライバル設定")]
    // ライバルの最大HPを定義する変数
    public int rivalMaxHp = 50;
    // ライバルの現在のHPを保持する変数
    private int rivalHp;
    // ボーナスタイム中かどうかのフラグ変数
    public bool isBonusTime = false;

    [Header("音楽・進行管理")]
    // BGMを再生するためのオーディオソース変数
    public AudioSource bgmSource;
    // ゲームがクリア状態かどうかのフラグ変数
    private bool isGameClear = false;

    [Header("UI参照")]
    // スコアを表示するTextMeshProUI要素の変数
    public TextMeshProUGUI scoreText;
    // コンボを表示するTextMeshProUI要素の変数
    public TextMeshProUGUI comboText;
    // プレイヤーのHPを表示するTextMeshProUI要素の変数
    public TextMeshProUGUI playerHpText;
    // ライバルのHPを表示するTextMeshProUI要素の変数
    public TextMeshProUGUI rivalHpText;

    // ゲーム開始時に呼ばれる初期化関数
    void Awake()
    {
        // シングルトンパターンの初期化処理を行う
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

    // 最初のフレーム更新の前に呼ばれる関数
    void Start()
    {
        // ライバルのHPを最大値で初期化する
        rivalHp = rivalMaxHp;

        // UIの初期表示状態を更新する
        UpdateUI();

        // BGMが設定されていれば再生を開始する処理
        if (bgmSource != null)
        {
            bgmSource.Play();
            // BGMの長さに合わせてクリア判定のコルーチンを開始する
            StartCoroutine(GameClearRoutine(bgmSource.clip.length));
        }
    }

    // スコアとコンボを加算する関数
    public void AddComboAndScore(int baseScore)
    {
        // コンボ数を1増やす
        currentCombo++;

        // コンボ数に応じたスコア倍率を計算する変数
        float multiplier = 1.0f + (currentCombo * 0.1f);

        // ボーナスタイム中の場合は倍率をさらに2倍にする処理
        if (isBonusTime) multiplier *= 2.0f;

        // 最終的な加算スコアを計算して追加する
        score += Mathf.RoundToInt(baseScore * multiplier);

        // スコアとコンボのUI表示を更新する
        UpdateUI();

        // ライバルに1ダメージを与える関数を呼び出す
        DamageRival(1);
    }

    // コンボ数をリセットする関数
    public void ResetCombo()
    {
        // コンボ数を0に戻す
        currentCombo = 0;
        // UI表示を更新する
        UpdateUI();
    }

    // ライバルにダメージを与える内部関数
    private void DamageRival(int damage)
    {
        // 既にボーナスタイム（撃破済み）なら処理を抜ける
        if (isBonusTime) return;

        // ライバルのHPを減らす
        rivalHp -= damage;

        // ライバルのHPが0以下になった場合の処理
        if (rivalHp <= 0)
        {
            // HPを0に固定する
            rivalHp = 0;
            // ボーナスタイムフラグを有効化する
            isBonusTime = true;
        }

        // ライバルのHPのUI表示を更新する
        UpdateUI();
    }

    // プレイヤーのHPのUI表示を外部から更新するための関数
    public void UpdatePlayerHP(int currentHealth)
    {
        // プレイヤーのHPテキストUIが割り当てられていれば文字を更新する
        if (playerHpText != null) playerHpText.text = "Player HP: " + currentHealth;
    }

    // UIのテキスト表示をまとめて更新する内部関数
    private void UpdateUI()
    {
        // 各テキストUIに現在の変数の値を代入して画面の文字を更新する
        if (scoreText != null) scoreText.text = "Score: " + score;
        if (comboText != null) comboText.text = "Combo: " + currentCombo;
        if (rivalHpText != null) rivalHpText.text = "Rival HP: " + rivalHp;
    }

    // ゲームクリア判定を行うコルーチン関数
    IEnumerator GameClearRoutine(float delayTime)
    {
        // 指定された秒数（曲の長さ）だけ処理を待機する
        yield return new WaitForSeconds(delayTime);

        // まだクリア処理が行われていない場合の処理
        if (!isGameClear)
        {
            // クリアフラグを立てる
            isGameClear = true;
            Debug.Log("ゲームクリア！");

            // ▼追加: 最終スコアをGameDataに保存する処理
            GameData.finalScore = score;

            // ▼追加: リザルト画面へ遷移する処理
            SceneManager.LoadScene("Result");
        }
    }
}