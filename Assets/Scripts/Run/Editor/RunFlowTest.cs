using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Yakiniku.Run.EditorTools
{
    /// <summary>
    /// 画面遷移を、実際のボタンを押して一周させる。
    ///
    ///   難易度選択でカードを押す → Game_Run → リザルト → もう一度 → Game_Run → ステージ選択
    ///
    /// 自動プレイテストはシーンを直接開くので、☆のローディング（RunLoadingOverlay）を通らない。
    /// 実際にそこで例外が出て、以後どのボタンを押しても遷移しなくなる不具合を見逃した。
    /// その経路を毎回通すためのテスト。
    ///
    /// 実行方法:
    ///   Unityのメニュー  Yakiniku > Run > 画面遷移を確認
    ///   コマンドライン    Unity.exe -batchmode -projectPath . -executeMethod
    ///                     Yakiniku.Run.EditorTools.RunFlowTest.Run （-quit は付けない）
    /// </summary>
    public static class RunFlowTest
    {
        private const string StartScene = "Assets/Scenes/Select.unity";
        private const string SessionKey = "Yakiniku.Run.FlowTest";
        private const float StepTimeout = 30f;

        [MenuItem("Yakiniku/Run/画面遷移を確認", false, 22)]
        public static void Run()
        {
            SessionState.SetBool(SessionKey, true);
            EditorSceneManager.OpenScene(StartScene, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            if (!SessionState.GetBool(SessionKey, false)) return;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode) return;

            var go = new GameObject("~RunFlowTest");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Driver>();
        }

        private sealed class Driver : MonoBehaviour
        {
            private readonly List<string> results = new List<string>();
            private readonly List<string> errors = new List<string>();
            private int failures;
            private Dictionary<string, int> savedProgress;

            private void OnEnable() => Application.logMessageReceived += OnLog;
            private void OnDisable() => Application.logMessageReceived -= OnLog;

            // 遷移の途中で出た例外・エラーを全部拾う
            private void OnLog(string message, string stackTrace, LogType type)
            {
                if (type != LogType.Exception && type != LogType.Error) return;

                // Unity エディタ自身の検索インデックスが起動時に出す例外は無関係
                if (stackTrace != null && stackTrace.Contains("UnityEditor.Search")) return;

                errors.Add(message.Split('\n')[0]);
            }

            private IEnumerator Start()
            {
                AudioListener.volume = 0f;
                savedProgress = RunProgress.Snapshot();

                // --- 難易度選択でカードを押す ---
                yield return WaitFrames(3); // SelectView.Start でボタンに処理が付くのを待つ
                var card = FindCardButton(RunDifficulty.Normal);
                Check(card != null, "難易度選択に Normal のカードがある");
                if (card != null) card.onClick.Invoke();

                yield return WaitForScene(RunFlow.GameScene, "難易度選択 → ゲーム");
                Check(RunSession.Difficulty == RunDifficulty.Normal, $"選んだ難易度が渡る（{RunSession.Difficulty}）");

                // ゲームは最後まで遊ばず、結果だけ作ってリザルトへ
                RunSession.LastResult = SampleResult();
                RunFlow.ToResult();
                yield return WaitForScene(RunFlow.ResultScene, "ゲーム → リザルト");

                // --- リザルト: 次へ → もう一度 ---
                yield return WaitFrames(3);
                var result = FindFirstObjectByType<ResultView>();
                Check(result != null, "リザルトに ResultView がある");
                ClickButton(result, "NextButton");
                yield return WaitFrames(2);
                ClickButton(result, "RetryButton");
                yield return WaitForScene(RunFlow.GameScene, "リザルト → もう一度");

                // --- もう一度リザルトへ行き、ステージ選択に戻る ---
                RunSession.LastResult = SampleResult();
                RunFlow.ToResult();
                yield return WaitForScene(RunFlow.ResultScene, "ゲーム → リザルト（2回目）");
                yield return WaitFrames(3);
                result = FindFirstObjectByType<ResultView>();
                ClickButton(result, "NextButton");
                yield return WaitFrames(2);
                ClickButton(result, "SelectButton");
                yield return WaitForScene(RunFlow.SelectScene, "リザルト → ステージ選択");

                Check(errors.Count == 0, errors.Count == 0
                    ? "遷移中に例外・エラーが出ない"
                    : "遷移中にエラー: " + string.Join(" / ", errors));

                Finish();
            }

            /// <summary>シーンが切り替わり、☆のローディングが消えるまで待つ。</summary>
            private IEnumerator WaitForScene(string scene, string label)
            {
                float t = 0f;
                while (t < StepTimeout)
                {
                    if (SceneManager.GetActiveScene().name == scene && !RunLoadingOverlay.IsBusy) break;
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }

                bool ok = SceneManager.GetActiveScene().name == scene && !RunLoadingOverlay.IsBusy;
                Check(ok, $"{label}（{t:F1}秒）" + (ok ? "" : $" 現在: {SceneManager.GetActiveScene().name} 遷移中={RunLoadingOverlay.IsBusy}"));
            }

            private static Button FindCardButton(RunDifficulty difficulty)
            {
                foreach (var button in FindObjectsByType<Button>(FindObjectsSortMode.None))
                {
                    if (button.name == "Card_" + difficulty) return button;
                }

                return null;
            }

            private void ClickButton(Component root, string name)
            {
                Button button = null;
                if (root != null)
                {
                    foreach (var b in root.GetComponentsInChildren<Button>(true))
                    {
                        if (b.name == name) { button = b; break; }
                    }
                }

                Check(button != null, $"{name} がある");
                if (button != null) button.onClick.Invoke();
            }

            private static IEnumerator WaitFrames(int frames)
            {
                for (int i = 0; i < frames; i++) yield return null;
            }

            private static RunResult SampleResult() => new RunResult
            {
                difficulty = RunSession.Difficulty,
                score = 1000,
                maxCombo = 10,
                itemsCollected = 10,
                totalItems = 279,
                rivalDefeated = false,
                survived = true,
                rank = RunRank.C,
            };

            private void Check(bool ok, string label)
            {
                results.Add((ok ? "  OK   " : "  NG   ") + label);
                if (!ok) failures++;
            }

            private void Finish()
            {
                RunProgress.Restore(savedProgress);

                var sb = new StringBuilder();
                sb.AppendLine("===== 画面遷移の確認 =====");
                foreach (var line in results) sb.AppendLine(line);
                sb.AppendLine(failures == 0 ? "===== 全項目OK =====" : $"===== {failures}件NG =====");
                Debug.Log(sb.ToString());

                SessionState.SetBool(SessionKey, false);
                if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
                else EditorApplication.ExitPlaymode();
            }
        }
    }
}
