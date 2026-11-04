using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Yakiniku.Run.EditorTools
{
    /// <summary>
    /// どのシーンを開いていても、Playを押したらタイトルから始まるようにする。
    ///
    /// Unityは「今開いているシーン」から再生するので、Gameシーンを編集中に実行すると
    /// 難易度選択を通らずに始まってしまい、GameData.selectedJsonName が空のまま走る
    /// （既存 StageManager はこれを無条件に読むので譜面が読めない）。
    ///
    /// 単一シーンだけをデバッグしたい時は邪魔になるので、メニューで切れるようにして
    /// 設定は EditorPrefs に置く（プロジェクト設定ではなく各自の環境ごとの設定にする）。
    /// </summary>
    [InitializeOnLoad]
    public static class PlayFromTitle
    {
        private const string MenuPath = "Yakiniku/常にタイトルから再生する";
        private const string TitleScenePath = "Assets/Scenes/Title.unity";

        /// <summary>プロジェクトごとに分けたいので、キーにプロジェクト名を混ぜる。</summary>
        private static string PrefKey => "Yakiniku.PlayFromTitle." + Application.productName;

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        static PlayFromTitle()
        {
            // InitializeOnLoad の時点ではまだアセットDBが整っていないことがあるので遅らせる
            EditorApplication.delayCall += Apply;
        }

        [MenuItem(MenuPath, false, 100)]
        private static void Toggle()
        {
            Enabled = !Enabled;
            Apply();
            Debug.Log(Enabled
                ? $"[PlayFromTitle] Playは常に {TitleScenePath} から始まります"
                : "[PlayFromTitle] Playは今開いているシーンから始まります（通常のUnityの挙動）");
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        private static void Apply()
        {
            if (!Enabled)
            {
                EditorSceneManager.playModeStartScene = null;
                return;
            }

            var title = AssetDatabase.LoadAssetAtPath<SceneAsset>(TitleScenePath);
            if (title == null)
            {
                Debug.LogWarning($"[PlayFromTitle] {TitleScenePath} が見つかりません。設定を解除します");
                EditorSceneManager.playModeStartScene = null;
                return;
            }

            EditorSceneManager.playModeStartScene = title;
        }
    }
}
