using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    [Header("移動パラメータ")]
    public float forwardSpeed = 10f;
    public float laneDistance = 2.0f; // 5レーンなので少し幅を狭く（2.0など）するのがおすすめ
    public float sideSpeed = 15f;
    public float jumpForce = 7f;      // ジャンプ力

    [Header("ステータス")]
    public int maxHealth = 3;
    private int currentHealth;
    public float invincibilityTime = 1.5f;
    private bool isInvincible = false;

    // 5レーン管理: -2(左端), -1(左), 0(中央), 1(右), 2(右端)
    private int currentLane = 0;

    // ジャンプ用の判定
    private bool isGrounded = true;
    private Rigidbody rb;

    void Start()
    {
        currentHealth = maxHealth;
        rb = GetComponent<Rigidbody>();
        if (rb == null) Debug.LogError("Rigidbodyがアタッチされていません！");
        // GameManagerに初期HPを通知してUIを更新させる処理
        GameManager.instance.UpdatePlayerHP(currentHealth);
    }

    void Update()
    {
        if (Keyboard.current == null) return;

        // 左右移動（5レーン対応）
        if (Keyboard.current.leftArrowKey.wasPressedThisFrame)
        {
            currentLane--;
            if (currentLane < -2) currentLane = -2;
        }
        if (Keyboard.current.rightArrowKey.wasPressedThisFrame)
        {
            currentLane++;
            if (currentLane > 2) currentLane = 2;
        }

        // ジャンプ（地面にいる時だけ飛べる）
        if (Keyboard.current.upArrowKey.wasPressedThisFrame && isGrounded)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            isGrounded = false;
        }

        // 横移動と前進の計算
        Vector3 targetPosition = transform.position;
        targetPosition.x = currentLane * laneDistance;

        // Y座標（高さ）はRigidbody（物理演算）に任せるため、XとZのみスクリプトで動かす
        transform.position = new Vector3(
            Mathf.Lerp(transform.position.x, targetPosition.x, Time.deltaTime * sideSpeed),
            transform.position.y,
            transform.position.z + (forwardSpeed * Time.deltaTime)
        );
    }

    // 地面に着地したかどうかの判定
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
        }
    }

    // 障害物・アイテムの判定（前回のロジックのまま）
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Obstacle") && !isInvincible)
        {
            TakeDamage(1);
        }

        if (other.CompareTag("Item"))
        {
            // ▼追加：GameManagerに報告してスコア計算（基本スコアを10点とする）
            GameManager.instance.AddComboAndScore(10);

            Destroy(other.gameObject);
        }
    }

    void TakeDamage(int damage)
    {
        // ▼追加：ダメージを受けたらコンボをリセット
        GameManager.instance.ResetCombo();

        currentHealth -= damage;
        // ダメージを受けた後のHPをGameManagerに通知してUIを更新させる処理
        GameManager.instance.UpdatePlayerHP(currentHealth);
        Debug.Log("ダメージ！ 残りHP: " + currentHealth);

        if (currentHealth <= 0)
        {
            Debug.Log("ゲームオーバー！");
            forwardSpeed = 0f;
            sideSpeed = 0f;
        }
        else
        {
            StartCoroutine(BecomeInvincible());
        }
    }

    IEnumerator BecomeInvincible()
    {
        isInvincible = true;
        yield return new WaitForSeconds(invincibilityTime);
        isInvincible = false;
    }
}