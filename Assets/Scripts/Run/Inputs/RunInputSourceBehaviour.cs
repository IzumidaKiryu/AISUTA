using System.Collections.Generic;
using UnityEngine;

namespace Yakiniku.Run.Inputs
{
    /// <summary>
    /// IRunInputSource を実装した MonoBehaviour の共通基底クラス。
    /// Unity のインスペクタはインターフェース型をそのまま参照できないため、
    /// この抽象クラスを型として指定することで入力元を差し替え可能にしている。
    /// 入力のバッファリング処理はここに集約し、派生クラスは Enqueue を呼ぶだけでよい。
    /// </summary>
    public abstract class RunInputSourceBehaviour : MonoBehaviour, IRunInputSource
    {
        [Header("バッファ")]
        [Tooltip("溜めておける操作の最大数。溢れた場合は古い操作から捨てる")]
        [SerializeField, Min(1)] private int maxBufferedInputs = 4;

        private readonly Queue<RunInputAction> queue = new Queue<RunInputAction>();

        /// <summary>現在バッファに溜まっている操作の数。</summary>
        public int BufferedCount => queue.Count;

        /// <summary>派生クラスから操作を積む。上限を超えた場合は最古の操作を破棄する。</summary>
        protected void Enqueue(RunInputAction action)
        {
            // 古いものを捨てる方針。溜まった操作を順に消化すると
            // 操作が後追いで遅れて発火し続けてしまい、体感が悪くなるため。
            while (queue.Count >= maxBufferedInputs)
            {
                queue.Dequeue();
            }

            queue.Enqueue(action);
        }

        /// <summary>溜まっている操作を全て破棄する（リトライ時など）。</summary>
        public void ClearBuffer() => queue.Clear();

        public bool TryDequeue(out RunInputAction action) => queue.TryDequeue(out action);
    }
}
