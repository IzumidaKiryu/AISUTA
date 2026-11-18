using System.Collections.Generic;
using UnityEngine;

namespace Yakiniku.Run.Inputs
{
    /// <summary>
    /// 複数の入力元をまとめて1つに見せる入力元。
    /// PlayerRunner は入力元を1つしか持てないので、キーボードとタッチ
    /// （将来は視聴者コメントも）を同時に受け付けたいときはこれを差す。
    ///
    /// 各入力元は自分のキューに溜めたまま持っており、取り出す側が来たときに
    /// 先頭の入力元から順に引き抜く。ここで別キューへ移し替えないのは、
    /// Update の実行順次第で1フレーム遅れる経路を作らないため。
    /// </summary>
    public sealed class CompositeRunInput : RunInputSourceBehaviour
    {
        [Tooltip("まとめる入力元。空なら同じ GameObject と子にある入力元を自動で集める")]
        [SerializeField] private List<RunInputSourceBehaviour> sources = new List<RunInputSourceBehaviour>();

        private void Awake()
        {
            if (sources.Count == 0)
            {
                foreach (var source in GetComponentsInChildren<RunInputSourceBehaviour>(true))
                {
                    sources.Add(source);
                }
            }

            // 自分自身を含めると TryDequeue が無限に再帰する
            sources.RemoveAll(s => s == null || s == this);
        }

        public override int BufferedCount
        {
            get
            {
                int count = base.BufferedCount;
                foreach (var source in sources) count += source.BufferedCount;
                return count;
            }
        }

        public override bool TryDequeue(out RunInputAction action)
        {
            // Enqueue で直接積まれたもの（外部から流し込んだ分）を先に出す
            if (base.TryDequeue(out action)) return true;

            foreach (var source in sources)
            {
                if (source.isActiveAndEnabled && source.TryDequeue(out action)) return true;
            }

            action = default;
            return false;
        }

        public override void ClearBuffer()
        {
            base.ClearBuffer();
            foreach (var source in sources) source.ClearBuffer();
        }
    }
}
