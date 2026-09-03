namespace Yakiniku.Run.Inputs
{
    /// <summary>プレイヤーに対して行える操作の種類。</summary>
    public enum RunInputAction
    {
        MoveLeft,
        MoveRight,
        Jump,
    }

    /// <summary>
    /// 操作の入力元を抽象化するインターフェース。
    /// キーボード / 画面ボタン / 視聴者コメント / リプレイ など、
    /// 「どこから入力が来るか」を差し替えてもゲーム側は無改修で動くようにするための境界。
    ///
    /// 【重要】キー状態を毎フレーム見に行くポーリング型ではなく、
    /// 溜まった操作をキューから取り出す方式にしている。
    /// 視聴者コメントは非同期・不定タイミングで飛んでくるため、
    /// 「そのフレームに押されていたか」では取りこぼすことがあるのが理由。
    /// </summary>
    public interface IRunInputSource
    {
        /// <summary>溜まっている操作を1つ取り出す。何も無ければ false を返す。</summary>
        bool TryDequeue(out RunInputAction action);
    }
}
