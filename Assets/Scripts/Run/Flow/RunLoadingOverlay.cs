using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Yakiniku.Run
{
    /// <summary>
    /// シーン遷移のローディング画面。☆を円形に並べて順に光らせる（本家のロード画面）。
    ///
    /// シーンをまたいで残る（DontDestroyOnLoad）ので、
    /// 「覆う → シーンを読む → 新しいシーンで明ける」が1本の流れで書ける。
    /// どこからでも <see cref="Transition"/> を呼べばよい。
    ///
    /// 【見た目の差し替え】
    /// Resources に次の名前のスプライトを置くと、それが使われる。
    ///   RunLoadingStar        … 星1つぶん（無ければ文字の★）
    ///   RunLoadingBackground  … 背景（無ければ単色）
    /// </summary>
    public sealed class RunLoadingOverlay : MonoBehaviour
    {
        private const int StarCount = 10;
        private const float Radius = 120f;
        private const float FadeTime = 0.25f;
        private const float MinShowTime = 0.8f;

        private static readonly Color BackgroundColor = new Color(0.95f, 0.94f, 0.99f, 1f);
        private static readonly Color StarOff = new Color(0.82f, 0.79f, 0.92f, 1f);
        private static readonly Color StarOn = new Color(1f, 0.85f, 0.35f, 1f);

        private static RunLoadingOverlay current;

        private CanvasGroup group;
        private Graphic[] stars;

        /// <summary>☆のローディングを挟んでシーンを切り替える。遷移中に呼ばれたら無視する。</summary>
        public static void Transition(string sceneName)
        {
            if (current != null) return;

            var go = new GameObject("~RunLoadingOverlay");
            DontDestroyOnLoad(go);

            current = go.AddComponent<RunLoadingOverlay>();
            current.Build();
            current.StartCoroutine(current.Run(sceneName));
        }

        /// <summary>遷移中かどうか（二重押し防止に使う）</summary>
        public static bool IsBusy => current != null;

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000; // 何よりも手前

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>(); // 遷移中の押し間違いを吸う

            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = true;

            var background = CreateChild("Background").AddComponent<Image>();
            Stretch(background.rectTransform);
            var bgSprite = Resources.Load<Sprite>("RunLoadingBackground");
            if (bgSprite != null)
            {
                background.sprite = bgSprite;
                background.color = Color.white;
            }
            else
            {
                background.color = BackgroundColor;
            }

            var starSprite = Resources.Load<Sprite>("RunLoadingStar");
            stars = new Graphic[StarCount];

            for (int i = 0; i < StarCount; i++)
            {
                // 真上から時計回りに並べる
                float angle = Mathf.PI * 0.5f - i * Mathf.PI * 2f / StarCount;
                var pos = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Radius;

                var starGo = CreateChild("Star" + i);
                var rect = starGo.AddComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = pos;
                rect.sizeDelta = new Vector2(56f, 56f);

                if (starSprite != null)
                {
                    var image = starGo.AddComponent<Image>();
                    image.sprite = starSprite;
                    stars[i] = image;
                }
                else
                {
                    var text = starGo.AddComponent<TextMeshProUGUI>();
                    text.text = "★"; // ★
                    text.fontSize = 48f;
                    text.alignment = TextAlignmentOptions.Center;
                    text.raycastTarget = false;
                    stars[i] = text;
                }

                stars[i].color = StarOff;
            }
        }

        private IEnumerator Run(string sceneName)
        {
            yield return Fade(0f, 1f);

            float shownAt = Time.unscaledTime;
            var op = SceneManager.LoadSceneAsync(sceneName);

            // 読み込みが速すぎても一瞬で消えないよう、最低限は見せる
            while (!op.isDone || Time.unscaledTime - shownAt < MinShowTime)
            {
                Animate();
                yield return null;
            }

            yield return Fade(1f, 0f);

            current = null;
            Destroy(gameObject);
        }

        /// <summary>☆を1つずつ順に光らせる。光った星は少しずつ消えていく。</summary>
        private void Animate()
        {
            float head = Time.unscaledTime * 8f;
            for (int i = 0; i < stars.Length; i++)
            {
                float distance = Mathf.Repeat(head - i, StarCount);
                float glow = Mathf.Clamp01(1f - distance / 4f);

                stars[i].color = Color.Lerp(StarOff, StarOn, glow);
                stars[i].rectTransform.localScale = Vector3.one * (1f + 0.3f * glow);
            }
        }

        private IEnumerator Fade(float from, float to)
        {
            float t = 0f;
            while (t < FadeTime)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, t / FadeTime);
                Animate();
                yield return null;
            }

            group.alpha = to;
        }

        private GameObject CreateChild(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            return go;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
