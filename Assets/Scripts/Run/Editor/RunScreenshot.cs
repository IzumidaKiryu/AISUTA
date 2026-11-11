using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Yakiniku.Run.EditorTools
{
    /// <summary>
    /// 画面を撮る。VSカットイン／通常／フィーバー／ENCORE／スーパー、難易度選択、リザルト。
    ///
    /// 見た目の調整を数値だけで判断すると外すので、実際の絵を並べて比べられるようにしておく。
    ///
    /// 【撮り方】
    /// ScreenCapture.CaptureScreenshot はバッチモードでは何も出力しない（Gameビューが描画されない）。
    /// カメラを RenderTexture へ直接描いて読み出す。その際、Screen Space - Overlay の Canvas は
    /// カメラに写らないので、撮る瞬間だけ Screen Space - Camera に切り替えて HUD も写す。
    ///
    /// 実行方法:
    ///   Unityのメニュー  Yakiniku > Run > 画面を撮る
    ///   コマンドライン    Unity.exe -batchmode -projectPath . -executeMethod
    ///                     Yakiniku.Run.EditorTools.RunScreenshot.Capture
    ///                     （出力先は環境変数 YAKINIKU_SHOT_DIR。-quit は付けない）
    /// </summary>
    public static class RunScreenshot
    {
        private const string ScenePath = "Assets/Scenes/Game_Run.unity";
        private const string SessionKey = "Yakiniku.Run.Screenshot";

        private const int Width = 1280;
        private const int Height = 720;

        [MenuItem("Yakiniku/Run/画面を撮る", false, 21)]
        public static void Capture()
        {
            SessionState.SetBool(SessionKey, true);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
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

            var go = new GameObject("~RunScreenshot");
            Object.DontDestroyOnLoad(go); // 難易度選択・リザルトも続けて撮るので残す
            go.AddComponent<Shooter>();
        }

        private sealed class Shooter : MonoBehaviour
        {
            private string outputDir;

            private IEnumerator Start()
            {
                AudioListener.volume = 0f;

                outputDir = System.Environment.GetEnvironmentVariable("YAKINIKU_SHOT_DIR");
                if (string.IsNullOrEmpty(outputDir)) outputDir = Directory.GetCurrentDirectory();
                Debug.Log($"[RunScreenshot] 出力先 {outputDir}");

                // 結果画面に遷移されると撮りたい状態が崩れるので止めておく
                var finish = FindFirstObjectByType<RunFinishSequence>();
                if (finish != null) finish.SetGoToResult(false);

                var player = FindFirstObjectByType<PlayerRunner>();
                var gauge = FindFirstObjectByType<BeatGaugeController>();
                var rival = FindFirstObjectByType<RunRival>();
                var intro = FindFirstObjectByType<RunIntroSequence>();

                if (player == null)
                {
                    Debug.LogError("[RunScreenshot] PlayerRunner が見つかりません");
                    Quit();
                    yield break;
                }

                // --- 開始演出 ---
                yield return WaitScaled(0.9f);
                yield return Shoot("0_versus");

                while (intro != null && intro.IsPlaying) yield return null;

                Time.timeScale = 4f;

                // --- 通常 ---
                yield return WaitUntilZ(player, 90f);
                yield return Shoot("1_normal");

                // --- フィーバー（ゲージを満タンに） ---
                yield return WaitUntilZ(player, 120f);
                if (gauge != null) gauge.AddGauge(1f);
                yield return Settle();
                yield return Shoot("2_fever");

                // --- ENCORE（フィーバーが切れるのを待ってからライバルを撃破） ---
                while (gauge != null && gauge.IsFever) yield return null;
                if (rival != null) while (!rival.IsDefeated) rival.ApplyHit();
                yield return Settle();
                yield return Shoot("3_encore");

                // --- スーパー（ENCORE 中にゲージを満タンに） ---
                if (gauge != null) gauge.AddGauge(1f);
                yield return Settle();
                yield return Shoot("4_superfever");

                // --- 難易度選択 ---
                Time.timeScale = 1f;
                SceneManager.LoadScene(RunFlow.SelectScene);
                yield return WaitFrames(20);
                yield return Shoot("5_select");

                // --- リザルト（単体で開くと見本の結果が出る） ---
                RunSession.LastResult = null;
                SceneManager.LoadScene(RunFlow.ResultScene);
                yield return WaitScaled(1.6f); // スコアの数え上げを待つ
                yield return Shoot("6_result");

                var view = FindFirstObjectByType<ResultView>();
                var panel = view != null ? view.transform.Find("ChoicePanel") : null;
                if (panel != null)
                {
                    panel.gameObject.SetActive(true);
                    yield return WaitFrames(5);
                    yield return Shoot("7_result_choice");
                }

                Quit();
            }

            private static IEnumerator WaitUntilZ(PlayerRunner player, float z)
            {
                while (player != null && player.DistanceZ < z) yield return null;
            }

            /// <summary>状態を変えても色はふわっと切り替わるので、落ち着くまで待つ</summary>
            private static IEnumerator Settle() => WaitScaled(0.8f);

            private static IEnumerator WaitScaled(float seconds)
            {
                float t = 0f;
                while (t < seconds)
                {
                    t += Time.deltaTime;
                    yield return null;
                }
            }

            private static IEnumerator WaitFrames(int frames)
            {
                for (int i = 0; i < frames; i++) yield return null;
            }

            /// <summary>カメラを RenderTexture に描いて PNG に落とす。Overlay の UI も一緒に写す。</summary>
            private IEnumerator Shoot(string name)
            {
                // WaitForEndOfFrame はバッチモードだと呼ばれないことがあるので、普通に1フレーム待つ
                yield return null;

                var cam = Camera.main;
                if (cam == null)
                {
                    Debug.LogError("[RunScreenshot] カメラが見つかりません");
                    yield break;
                }

                // Overlay の Canvas はカメラに写らないので、撮る間だけカメラ描画に切り替える
                var switched = new List<Canvas>();
                foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                {
                    if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = cam;
                    canvas.planeDistance = cam.nearClipPlane + 0.1f;
                    switched.Add(canvas);
                }

                var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
                var previousTarget = cam.targetTexture;
                var previousActive = RenderTexture.active;

                cam.targetTexture = rt;
                Canvas.ForceUpdateCanvases();

                // 2回描く。1回目はシェーダ側の準備（lilToon など）が間に合わず、キャラが黒く写ることがある
                cam.Render();
                cam.Render();

                RenderTexture.active = rt;
                var texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                texture.Apply();

                cam.targetTexture = previousTarget;
                RenderTexture.active = previousActive;

                foreach (var canvas in switched)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                }

                string path = Path.Combine(outputDir, name + ".png");
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Debug.Log($"[RunScreenshot] 撮影 {name} → {path}");

                Destroy(texture);
                rt.Release();
                Destroy(rt);
            }

            private void Quit()
            {
                Time.timeScale = 1f;
                SessionState.SetBool(SessionKey, false);

                if (Application.isBatchMode) EditorApplication.Exit(0);
                else EditorApplication.ExitPlaymode();
            }
        }
    }
}
