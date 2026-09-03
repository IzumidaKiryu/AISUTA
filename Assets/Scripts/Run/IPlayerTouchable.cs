namespace Yakiniku.Run
{
    /// <summary>
    /// プレイヤーが触れたときに何かが起きるオブジェクトのインターフェース。
    /// 障害物・アイテム・回復など種類が増えても PlayerRunner 側を修正せずに済むよう、
    /// 「触れたら何が起きるか」は相手のオブジェクトが持つ。
    /// </summary>
    public interface IPlayerTouchable
    {
        void OnTouched(PlayerRunner player);
    }
}
