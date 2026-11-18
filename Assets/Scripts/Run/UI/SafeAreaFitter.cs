using UnityEngine;

namespace Yakiniku.Run
{
    /// <summary>
    /// この RectTransform を端末のセーフエリア（ノッチ・パンチホール・ホームバーを避けた範囲）に合わせる。
    ///
    /// 背景は画面いっぱいに敷き、文字やボタンだけをこの下に入れる使い方を想定している。
    /// そうしないと、縦持ちのスマホでは上端の HUD がカメラの穴に隠れる。
    ///
    /// Canvas 直下の全画面ストレッチの子に付けること（親の大きさ＝画面の大きさ、が前提）。
    /// </summary>
    // 再生中だけ動かす。エディタで常に動かすと、Device Simulator で開いただけで
    // シーンの anchor が書き換わって差分が出てしまう
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform rect;
        private Rect appliedSafeArea;
        private Vector2Int appliedScreen;

        private void OnEnable()
        {
            rect = (RectTransform)transform;
            Apply(force: true);
        }

        // 画面回転や分割表示でセーフエリアが変わることがあるので毎フレーム見る（変化が無ければ何もしない）
        private void Update() => Apply(force: false);

        private void Apply(bool force)
        {
            var screen = new Vector2Int(Screen.width, Screen.height);
            var safe = Screen.safeArea;
            if (!force && safe == appliedSafeArea && screen == appliedScreen) return;

            appliedSafeArea = safe;
            appliedScreen = screen;

            if (screen.x <= 0 || screen.y <= 0) return;

            var min = new Vector2(safe.xMin / screen.x, safe.yMin / screen.y);
            var max = new Vector2(safe.xMax / screen.x, safe.yMax / screen.y);

            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
