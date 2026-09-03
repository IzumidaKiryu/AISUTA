using System.Collections.Generic;
using UnityEngine;

namespace Yakiniku.Run
{
    /// <summary>
    /// Resources フォルダの JSON 譜面(StageData)からノーツを読み込む供給元。
    /// 既存の StageData / NoteData / GameData をそのまま使うので、
    /// これまで作った譜面データは無駄にならない。
    /// </summary>
    public sealed class JsonNoteProvider : NoteProviderBehaviour
    {
        [Tooltip("Resources フォルダ内のファイル名（拡張子なし）")]
        [SerializeField] private string resourcesFileName = "StageData_Normal";

        [Tooltip("難易度選択画面で選ばれたファイル名(GameData)を優先して使う")]
        [SerializeField] private bool useSelectedDifficulty = true;

        public override IReadOnlyList<NoteData> GetNotes()
        {
            string fileName = resourcesFileName;
            if (useSelectedDifficulty && !string.IsNullOrEmpty(GameData.selectedJsonName))
            {
                fileName = GameData.selectedJsonName;
            }

            var jsonText = Resources.Load<TextAsset>(fileName);
            if (jsonText == null)
            {
                Debug.LogError($"譜面JSON ({fileName}) が Resources フォルダに見つかりません", this);
                return new List<NoteData>();
            }

            var stageData = JsonUtility.FromJson<StageData>(jsonText.text);
            if (stageData?.notes == null)
            {
                Debug.LogError($"譜面JSON ({fileName}) の解析に失敗しました", this);
                return new List<NoteData>();
            }

            return stageData.notes;
        }
    }
}
