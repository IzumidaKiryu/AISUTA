using UnityEngine;

namespace Yakiniku.Run
{
    /// <summary>
    /// ゲーム中の出来事に効果音を付ける。
    ///
    /// ゲームロジック側は音を一切知らず、ここがイベントを購読して鳴らすだけ。
    /// 音を変えたい・止めたいときにロジックを触らずに済む。
    /// </summary>
    public sealed class RunSeHooks : MonoBehaviour
    {
        [SerializeField] private PlayerRunner player;
        [SerializeField] private RunRival rival;
        [SerializeField] private BeatGaugeController gauge;

        private void OnEnable()
        {
            if (player != null)
            {
                player.OnItemCollected += OnItem;
                player.OnDamaged += OnDamaged;
                player.OnJumped += OnJumped;
            }

            if (rival != null)
            {
                rival.OnHit += OnRivalHit;
                rival.OnHeartBroken += OnHeartBroken;
            }

            if (gauge != null)
            {
                gauge.OnFeverStart += OnFever;
                gauge.OnEncoreStart += OnEncore;
                gauge.OnSuperFeverStart += OnSuperFever;
            }
        }

        private void OnDisable()
        {
            if (player != null)
            {
                player.OnItemCollected -= OnItem;
                player.OnDamaged -= OnDamaged;
                player.OnJumped -= OnJumped;
            }

            if (rival != null)
            {
                rival.OnHit -= OnRivalHit;
                rival.OnHeartBroken -= OnHeartBroken;
            }

            if (gauge != null)
            {
                gauge.OnFeverStart -= OnFever;
                gauge.OnEncoreStart -= OnEncore;
                gauge.OnSuperFeverStart -= OnSuperFever;
            }
        }

        private static void OnItem(BeatItem item) => RunSe.Play(SeId.ItemGet);
        private static void OnDamaged(int health) => RunSe.Play(SeId.Damage);
        private static void OnJumped() => RunSe.Play(SeId.Jump);
        private static void OnRivalHit() => RunSe.Play(SeId.OrbHit);
        private static void OnHeartBroken(int count) => RunSe.Play(SeId.HeartBreak);

        // スーパーと ENCORE が同時に始まる瞬間（フィーバー中に撃破）は、派手なほうだけ鳴らす
        private void OnFever()
        {
            if (gauge != null && gauge.IsSuperFever) return;
            RunSe.Play(SeId.Fever);
        }

        private void OnEncore()
        {
            if (gauge != null && gauge.IsSuperFever) return;
            RunSe.Play(SeId.Encore);
        }

        private static void OnSuperFever() => RunSe.Play(SeId.SuperFever);
    }
}
