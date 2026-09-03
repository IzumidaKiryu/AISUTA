namespace Yakiniku.Run.Inputs
{
    /// <summary>
    /// uGUI のボタンから操作を流し込むための入力元。
    /// Button の OnClick に下記メソッドを登録して使う（実機やエディタ上での手動確認用）。
    /// 外部システムから操作を流し込む実装のサンプルも兼ねている。
    /// </summary>
    public sealed class DebugButtonRunInput : RunInputSourceBehaviour
    {
        public void PushMoveLeft() => Enqueue(RunInputAction.MoveLeft);

        public void PushMoveRight() => Enqueue(RunInputAction.MoveRight);

        public void PushJump() => Enqueue(RunInputAction.Jump);
    }
}
