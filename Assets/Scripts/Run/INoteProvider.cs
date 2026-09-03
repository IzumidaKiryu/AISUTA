using System.Collections.Generic;
using UnityEngine;

namespace Yakiniku.Run
{
    /// <summary>
    /// 障害物・アイテムの配置データを供給するインターフェース。
    /// 「JSON譜面から読む」「ランダム生成する」「難易度に応じて合成する」など
    /// 供給方法が増えても RunSpawner を書き換えずに済むようにするための境界。
    /// </summary>
    public interface INoteProvider
    {
        IReadOnlyList<NoteData> GetNotes();
    }

    /// <summary>インスペクタから差し替えられるようにするための基底クラス。</summary>
    public abstract class NoteProviderBehaviour : MonoBehaviour, INoteProvider
    {
        public abstract IReadOnlyList<NoteData> GetNotes();
    }
}
