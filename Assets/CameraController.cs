using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("追いかける対象")]
    public Transform player;

    private Vector3 offset; // プレイヤーとの最初の距離

    void Start()
    {
        // ゲーム開始時の、カメラとプレイヤーの距離を計算して記憶しておく
        if (player != null)
        {
            offset = transform.position - player.position;
        }
    }

    // Updateではなく、LateUpdateを使うのがカメラ制御の基本です
    // （プレイヤーがUpdateで移動し終わった"後"にカメラを動かすため）
    void LateUpdate()
    {
        if (player == null) return;

        // プレイヤーの現在位置に、記憶しておいた距離（オフセット）を足す
        Vector3 newPosition = player.position + offset;

        // 【重要】ランゲームなので、カメラが左右にブレないようにX座標は 0（中央）に固定する
        newPosition.x = 0f;

        // カメラの位置を更新
        transform.position = newPosition;
    }
}