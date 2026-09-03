using UnityEngine;

namespace Yakiniku.Run
{
    /// <summary>
    /// RunSpawner が生成したオブジェクトに自動で付く目印。
    /// 「自分がどのプールから出てきたか」を覚えておき、返却できるようにする。
    /// </summary>
    public sealed class SpawnedNote : MonoBehaviour
    {
        internal RunSpawner Owner { get; set; }
        internal string PoolKey { get; set; }

        private bool despawned;

        /// <summary>プールに返却する（プール管理外なら非アクティブにする）。</summary>
        public void Despawn()
        {
            if (despawned) return;
            despawned = true;

            if (Owner != null)
            {
                Owner.Release(this);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        internal void MarkSpawned() => despawned = false;

        /// <summary>
        /// プール管理されていてもいなくても安全に片付けるためのヘルパー。
        /// 手動でシーンに置いた障害物にも使える。
        /// </summary>
        public static void DespawnObject(GameObject target)
        {
            if (target == null) return;

            var note = target.GetComponentInParent<SpawnedNote>();
            if (note != null)
            {
                note.Despawn();
            }
            else
            {
                target.SetActive(false);
            }
        }
    }
}
