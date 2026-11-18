using UnityEngine;

namespace Yakiniku.Run
{
    /// <summary>
    /// 画面の縦横比が変わっても、プレイヤーの位置で全レーンが横に収まるよう縦の画角を調整する。
    ///
    /// Unity のカメラは「縦の画角」が固定なので、縦長の画面ほど横が狭く写る。
    /// 縦持ちで 9:16 と 9:20 の端末を比べると、後者は端のレーンが画面外に出てしまう。
    /// そこで「プレイヤーの深さで横に何m見えてほしいか」から必要な縦の画角を逆算する。
    ///
    /// 横に広い画面（タブレット等）では <see cref="baseFieldOfView"/> より狭めない。
    /// 広げすぎると周辺が歪むので <see cref="maxFieldOfView"/> で頭打ちにする。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraWidthFitter : MonoBehaviour
    {
        [Tooltip("横に収めたい範囲の中心（プレイヤー）。null ならワールド原点")]
        [SerializeField] private Transform focus;

        [Tooltip("フォーカス位置で、中心から左右それぞれに見えてほしい幅(m)。5レーン×2m なら端のレーンの外側まで 5m + 余白")]
        [SerializeField, Min(0.1f)] private float halfWidth = 5.6f;

        [Tooltip("これより画角を狭めない（横長の画面で寄りすぎないように）")]
        [SerializeField, Range(10f, 120f)] private float baseFieldOfView = 60f;

        [Tooltip("これより画角を広げない（周辺の歪み防止）")]
        [SerializeField, Range(10f, 120f)] private float maxFieldOfView = 80f;

        private Camera cam;

        private void Awake() => cam = GetComponent<Camera>();

        // カメラ追従(LateUpdate)の後でも前でも結果は同じ（位置ではなく画角だけを触るため）
        private void LateUpdate() => Apply();

        /// <summary>今のアスペクト比で画角を合わせる。描画先を差し替えた直後（スクショ等）にも呼ぶ。</summary>
        public void Apply()
        {
            if (cam == null) cam = GetComponent<Camera>();
            if (cam == null || cam.orthographic) return;

            float aspect = cam.aspect;
            if (aspect <= 0f) return;

            // フォーカス位置までの「カメラの前方向に沿った」距離。横の見え幅はこれで決まる
            Vector3 target = focus != null ? focus.position : Vector3.zero;
            target.x = transform.position.x; // 左右にずれていても深さだけを見る
            float depth = Vector3.Dot(target - transform.position, transform.forward);
            if (depth <= 0.01f) return;

            float tanHalfHorizontal = halfWidth / depth;
            float tanHalfVertical = tanHalfHorizontal / aspect;
            float fov = 2f * Mathf.Atan(tanHalfVertical) * Mathf.Rad2Deg;

            cam.fieldOfView = Mathf.Clamp(fov, baseFieldOfView, maxFieldOfView);
        }
    }
}
