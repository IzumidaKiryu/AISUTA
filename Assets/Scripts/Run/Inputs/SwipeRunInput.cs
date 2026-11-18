using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Yakiniku.Run.Inputs
{
    /// <summary>
    /// スマホ向けのタッチ入力。左右スワイプでレーン移動、上スワイプでジャンプ。
    ///
    /// 【指を離すのを待たずに発火する】
    /// 譜面に合わせて障害物が来るゲームなので、離した瞬間に判定すると
    /// 「スワイプの長さ＋離すまで」の分だけ操作が遅れて体感が悪い。
    /// 指がしきい値を超えて動いた時点で即座に操作を積み、その指はそれ以上反応させない
    /// （1回のスワイプで2レーン動いてしまう事故を防ぐため）。
    ///
    /// 【しきい値は画面の短辺に対する割合】
    /// 端末ごとに解像度・DPIが大きく違い、Screen.dpi は 0 を返す端末もあるため、
    /// ピクセル固定ではなく画面サイズ比で決めている。
    ///
    /// 複数の指は独立して扱うので、片手で移動しながらもう片手でジャンプもできる。
    ///
    /// 【ボタンの上は無視する】JUMP ボタンなどを押した指がそのまま少し動くと
    /// スワイプ扱いになってしまうので、<see cref="ignoreAreas"/> の中で始まった指は見ない。
    /// </summary>
    public sealed class SwipeRunInput : RunInputSourceBehaviour
    {
        [Header("スワイプ判定")]
        [Tooltip("スワイプと見なす移動量（画面の短辺に対する割合）。小さいほど敏感")]
        [SerializeField, Range(0.01f, 0.3f)] private float swipeThreshold = 0.06f;

        [Tooltip("ここで触り始めた指はスワイプとして扱わない（JUMP ボタン等）。Screen Space - Overlay の UI を想定")]
        [SerializeField] private List<RectTransform> ignoreAreas = new List<RectTransform>();

        [Header("タップ")]
        [Tooltip("短いタップでジャンプする。誤爆しやすいので既定は OFF")]
        [SerializeField] private bool tapToJump;

        [Tooltip("タップと見なす最大の押下時間（秒）")]
        [SerializeField, Min(0.01f)] private float tapMaxDuration = 0.25f;

#if UNITY_EDITOR
        [Header("エディタ")]
        [Tooltip("エディタ上でマウスドラッグをタッチとして扱う（実機なしで動作確認するため）")]
        [SerializeField] private bool simulateTouchWithMouseInEditor = true;

        private bool ownsTouchSimulation;
#endif

        // 既に操作を発火済みの指。離されるまで再発火させない
        private readonly HashSet<int> consumedTouches = new HashSet<int>();

        private void OnEnable()
        {
            // EnhancedTouch は参照カウント式なので、他所で有効化されていても問題ない
            EnhancedTouchSupport.Enable();

#if UNITY_EDITOR
            if (simulateTouchWithMouseInEditor
                && (TouchSimulation.instance == null || !TouchSimulation.instance.enabled))
            {
                TouchSimulation.Enable();
                ownsTouchSimulation = true;
            }
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            // 自分で有効化したときだけ戻す。Input Debugger から手動で ON にしている場合は触らない
            if (ownsTouchSimulation)
            {
                TouchSimulation.Disable();
                ownsTouchSimulation = false;
            }
#endif

            EnhancedTouchSupport.Disable();
            consumedTouches.Clear();
        }

        private void Update()
        {
            float threshold = swipeThreshold * Mathf.Min(Screen.width, Screen.height);

            foreach (var touch in Touch.activeTouches)
            {
                int id = touch.touchId;

                if (touch.phase == TouchPhase.Began) consumedTouches.Remove(id);
                if (IsIgnored(touch.startScreenPosition)) consumedTouches.Add(id);

                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        break;

                    case TouchPhase.Moved:
                    case TouchPhase.Stationary:
                        if (!consumedTouches.Contains(id)) TryFireSwipe(touch, threshold);
                        break;

                    case TouchPhase.Ended:
                        // 1フレーム内で押して大きく動かして離した場合もここで拾う
                        if (!consumedTouches.Contains(id) && !TryFireSwipe(touch, threshold))
                        {
                            TryFireTap(touch);
                        }
                        consumedTouches.Remove(id);
                        break;

                    case TouchPhase.Canceled:
                        consumedTouches.Remove(id);
                        break;
                }
            }
        }

        private bool IsIgnored(Vector2 screenPosition)
        {
            foreach (var area in ignoreAreas)
            {
                if (area == null || !area.gameObject.activeInHierarchy) continue;

                // Overlay の Canvas はカメラ無し(null)で判定する
                if (RectTransformUtility.RectangleContainsScreenPoint(area, screenPosition, null)) return true;
            }

            return false;
        }

        /// <summary>しきい値を超えていれば操作を積み、その指を発火済みにする。</summary>
        private bool TryFireSwipe(Touch touch, float threshold)
        {
            Vector2 delta = touch.screenPosition - touch.startScreenPosition;
            if (delta.sqrMagnitude < threshold * threshold) return false;

            // しきい値を超えた時点で、その指はもう判定しない。
            // 下スワイプは割り当てる操作が無いので何もしないが、タップ扱いにもしない
            consumedTouches.Add(touch.touchId);

            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            {
                Enqueue(delta.x < 0f ? RunInputAction.MoveLeft : RunInputAction.MoveRight);
            }
            else if (delta.y > 0f)
            {
                Enqueue(RunInputAction.Jump);
            }

            return true;
        }

        private void TryFireTap(Touch touch)
        {
            if (!tapToJump) return;

            double duration = touch.time - touch.startTime;
            if (duration <= tapMaxDuration) Enqueue(RunInputAction.Jump);
        }
    }
}
