using UnityEngine;
using UnityEngine.InputSystem;

namespace Yakiniku.Run.Inputs
{
    /// <summary>
    /// 開発用のキーボード入力。矢印キー / WASD に対応。
    /// 本番の入力元（視聴者コメント等）が出来るまでの動作確認用であり、
    /// これを差し替えてもゲーム側のコードは一切変わらないことを保証するための実装でもある。
    /// </summary>
    public sealed class KeyboardRunInput : RunInputSourceBehaviour
    {
        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
            {
                Enqueue(RunInputAction.MoveLeft);
            }

            if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
            {
                Enqueue(RunInputAction.MoveRight);
            }

            if (keyboard.upArrowKey.wasPressedThisFrame
                || keyboard.wKey.wasPressedThisFrame
                || keyboard.spaceKey.wasPressedThisFrame)
            {
                Enqueue(RunInputAction.Jump);
            }
        }
    }
}
