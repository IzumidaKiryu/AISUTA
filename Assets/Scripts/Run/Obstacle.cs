using UnityEngine;

namespace Yakiniku.Run
{
    /// <summary>
    /// 障害物。触れるとダメージ。
    /// ジャンプで避ける障害物は「当たり判定を低く作る」だけでよく、専用クラスは不要。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class Obstacle : MonoBehaviour, IPlayerTouchable
    {
        [SerializeField, Min(1)] private int damage = 1;

        [Tooltip("ぶつかった障害物を消すか。残す場合は無敵時間で多重ヒットを防ぐ")]
        [SerializeField] private bool despawnOnHit = true;

        public void OnTouched(PlayerRunner player)
        {
            // 無敵中などでダメージが通らなかった場合は何もしない（障害物も消さない）
            if (!player.ApplyDamage(damage)) return;

            if (despawnOnHit)
            {
                SpawnedNote.DespawnObject(gameObject);
            }
        }

        // コンポーネントを付けた時点でトリガーにしておく（設定漏れ防止）
        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }
    }
}
