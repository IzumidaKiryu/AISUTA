using UnityEngine;

namespace Yakiniku.Run
{
    /// <summary>
    /// レーンの奥にライバルを立たせる（本家はステージの奥の中央に立っている）。
    ///
    /// プレイヤーと一緒に前へ進み、距離を一定に保つ。ノーツの生成位置より奥に置くので、
    /// ノーツはライバルの手前から流れてくるように見える。
    /// 球が当たるたびに少し跳ね、ハートを割ると大きく跳ねる。
    /// </summary>
    public sealed class RivalView : MonoBehaviour
    {
        [Header("参照")]
        [SerializeField] private PlayerRunner player;
        [SerializeField] private RunRival rival;

        [Tooltip("揺らす対象（モデル）。自分自身を動かすと着弾点もずれるので子を揺らす")]
        [SerializeField] private Transform model;

        [Header("配置")]
        [Tooltip("プレイヤーの何m先に立つか。ノーツの先読み距離より少し奥にする")]
        [SerializeField, Min(1f)] private float distanceAhead = 64f;

        [Tooltip("足元の高さ")]
        [SerializeField] private float groundY;

        [Header("被弾の反応")]
        [SerializeField, Min(0f)] private float hitBounce = 0.15f;
        [SerializeField, Min(0f)] private float heartBreakBounce = 0.6f;
        [SerializeField, Min(0.1f)] private float recoverSpeed = 10f;

        private Vector3 modelBasePosition;
        private float bounce;

        private void Awake()
        {
            if (model != null) modelBasePosition = model.localPosition;
        }

        private void OnEnable()
        {
            if (rival == null) return;
            rival.OnHit += HandleHit;
            rival.OnHeartBroken += HandleHeartBroken;
        }

        private void OnDisable()
        {
            if (rival == null) return;
            rival.OnHit -= HandleHit;
            rival.OnHeartBroken -= HandleHeartBroken;
        }

        private void LateUpdate()
        {
            if (player != null)
            {
                transform.position = new Vector3(0f, groundY, player.DistanceZ + distanceAhead);
            }

            if (model == null) return;

            bounce = Mathf.MoveTowards(bounce, 0f, recoverSpeed * Time.deltaTime * Mathf.Max(0.2f, bounce));
            model.localPosition = modelBasePosition + Vector3.up * bounce;
        }

        private void HandleHit() => bounce = Mathf.Max(bounce, hitBounce);

        private void HandleHeartBroken(int brokenCount) => bounce = Mathf.Max(bounce, heartBreakBounce);
    }
}
