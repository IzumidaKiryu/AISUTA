using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Yakiniku.Run.EditorTools
{
    /// <summary>
    /// Game_Run を走らせて、通常／フィーバー／スーパーフィーバーの画面を撮る。
    ///
    /// 見た目の調整（Bloomの強さなど）を数値だけで判断すると外すので、
    /// 実際の絵を並べて比べられるようにしておく。
    ///
    /// 【撮り方】
    /// `ScreenCapture.CaptureScreenshot` はバッチモードでは何も出力しない
    /// （Gameビューが描画されないため）。カメラを RenderTexture へ直接描いて
    /// 読み出す方式にしてある。
    ///
    /// この方法では **Screen Space - Overlay の Canvas は写らない**。
    /// HUDは確認できないが、Overlay のUIはそもそもポストプロセスの影響を受けないので、
    /// Bloomの効き具合を見るぶんには問題ない。
    ///
    /// 実行方法:
    ///   Unityのメニュー  Yakiniku > Run > 画面を撮る
    ///   コマンドライン    Unity.exe -batchmode -projectPath .
    ///                     -executeMethod Yakiniku.Run.EditorTools.RunScreenshot.Capture
    ///                     （出力先は環境変数 YAKINIKU_SHOT_DIR。-quit は付けない）
    /// </summary>
    public static class RunScreenshot
    {
        private const string ScenePath = "Assets/Scenes/Game_Run.unity";
        private const string SessionKey = "Yakiniku.Run.Screenshot";
        public const string OutputDirKey = "Yakiniku.Run.ScreenshotDir";

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
            go.AddComponent<Shooter>();
        }

        private sealed class Shooter : MonoBehaviour
        {
            private enum FeverState { None, Fever, Super }

            /// <summary>撮る手順。Zまで走って、状態を作って、撮る。</summary>
            private readonly List<(string name, float atZ, FeverState state)> shots =
                new List<(string, float, FeverState)>
                {
                    ("1_normal", 90f, FeverState.None),
                    ("2_fever", 130f, FeverState.Fever),
                    ("3_superfever", 170f, FeverState.Super),
                };

            private PlayerRunner player;
            private BeatGaugeController gauge;
            private string outputDir;

            private int index;
            private int waitFrames;
            private bool pendingShot;
            private bool done;
            private float elapsed;

            private void Start()
            {
                AudioListener.volume = 0f;

                player = FindFirstObjectByType<PlayerRunner>();
                gauge = FindFirstObjectByType<BeatGaugeController>();

                // -executeMethod は引数を取れないので、出力先は環境変数で渡す
                outputDir = System.Environment.GetEnvironmentVariable("YAKINIKU_SHOT_DIR");
                if (string.IsNullOrEmpty(outputDir))
                {
                    outputDir = SessionState.GetString(OutputDirKey, string.Empty);
                }

                if (string.IsNullOrEmpty(outputDir)) outputDir = Directory.GetCurrentDirectory();

                if (player == null)
                {
                    Debug.LogError("[RunScreenshot] PlayerRunner が見つかりません");
                    Quit();
                    return;
                }

                Debug.Log($"[RunScreenshot] 出力先 {outputDir}");
                Time.timeScale = 4f;
            }

            private void Update()
            {
                if (done) return;

                elapsed += Time.unscaledDeltaTime;
                if (elapsed > 120f)
                {
                    Debug.LogError("[RunScreenshot] 時間切れ");
                    Quit();
                    return;
                }

                if (waitFrames > 0)
                {
                    waitFrames--;
                    if (waitFrames > 0) return;

                    if (pendingShot)
                    {
                        pendingShot = false;
                        Shoot(shots[index].name);
                        index++;
                    }

                    return;
                }

                if (index >= shots.Count)
                {
                    Quit();
                    return;
                }

                var shot = shots[index];
                if (player.DistanceZ < shot.atZ) return;

                // 状態を変えてもゲージの色やBloomの強さは各コンポーネントのUpdateで
                // 追従するので、1フレームでは絵に乗らない。数フレーム待ってから撮る
                ApplyState(shot.state);
                pendingShot = true;
                waitFrames = 30;
            }

            private void ApplyState(FeverState state)
            {
                if (gauge == null) return;

                switch (state)
                {
                    case FeverState.Fever:
                        gauge.TriggerFever(FeverSource.Rival);
                        break;

                    case FeverState.Super:
                        gauge.TriggerFever(FeverSource.Rival);
                        gauge.TriggerFever(FeverSource.Gauge);
                        break;
                }
            }

            /// <summary>カメラを RenderTexture に描いて PNG に落とす。</summary>
            private void Shoot(string name)
            {
                var cam = Camera.main;
                if (cam == null)
                {
                    Debug.LogError("[RunScreenshot] カメラが見つかりません");
                    return;
                }

                var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
                {
                    antiAliasing = 1,
                };

                var previousTarget = cam.targetTexture;
                var previousActive = RenderTexture.active;

                cam.targetTexture = rt;

                // 2回描く。1回目はシェーダ側の毎フレーム準備（lilToon など）が
                // 間に合わず、キャラが真っ黒に出ることがある
                cam.Render();
                cam.Render();

                RenderTexture.active = rt;
                var texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                texture.Apply();

                cam.targetTexture = previousTarget;
                RenderTexture.active = previousActive;

                string path = Path.Combine(outputDir, name + ".png");
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Debug.Log($"[RunScreenshot] 撮影 {name} (Z={player.DistanceZ:F0}) → {path}");

                Destroy(texture);
                rt.Release();
                Destroy(rt);
            }

            private void Quit()
            {
                if (done) return;
                done = true;

                Time.timeScale = 1f;
                SessionState.SetBool(SessionKey, false);

                if (Application.isBatchMode) EditorApplication.Exit(0);
                else EditorApplication.ExitPlaymode();
            }
        }
    }
}
