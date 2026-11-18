using UnityEngine;
using UnityEngine.EventSystems;

namespace Yakiniku.Run.Inputs
{
    /// <summary>
    /// 画面上のボタンを「押した瞬間」に操作を積む入力元（本家の JUMP ボタン）。
    ///
    /// uGUI の Button.onClick は指を離したときに発火するので、音ゲーでは押してから
    /// 離すまでの分だけ遅れる。押下(PointerDown)で即座に積む。
    ///
    /// ボタンの Image(raycastTarget=ON) と同じ GameObject に付ける。押した見た目の変化が
    /// 欲しければ Button も一緒に付ければよい（onClick は使わない）。
    /// </summary>
    public sealed class PressRunInput : RunInputSourceBehaviour, IPointerDownHandler
    {
        [Tooltip("押したときに積む操作")]
        [SerializeField] private RunInputAction action = RunInputAction.Jump;

        public void OnPointerDown(PointerEventData eventData) => Enqueue(action);
    }
}
