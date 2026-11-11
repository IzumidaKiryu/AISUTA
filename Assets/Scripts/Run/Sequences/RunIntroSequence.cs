using System.Collections;
using TMPro;
using UnityEngine;

namespace Yakiniku.Run
{
    /// <summary>
    /// ゲーム開始の演出（本家: 自キャラとライバルの VS カットイン → Let's FIGHT! → 開始）。
    ///
    /// 演出中はプレイヤーを止めておき、BGM も鳴らさない。
    /// FIGHT の瞬間に走り出しと BGM を同時に始めるので、曲と譜面の頭が揃う。
    ///
    /// 立ち絵は <see cref="playerPortrait"/> / <see cref="rivalPortrait"/> の Image に
    /// スプライトを入れれば差し替わる。
    /// </summary>
    public sealed class RunIntroSequence : MonoBehaviour
    {
        [Header("参照")]
        [SerializeField] private PlayerRunner player;
        [SerializeField] private RunRival rival;
        [SerializeField] private AudioSource bgm;

        [Header("VS カットイン")]
        [SerializeField] private CanvasGroup versusGroup;
        [SerializeField] private RectTransform playerCard;
        [SerializeField] private RectTransform rivalCard;
        [SerializeField] private RectTransform vsMark;
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private TextMeshProUGUI rivalNameText;
        [SerializeField] private TextMeshProUGUI playerLineText;
        [SerializeField] private TextMeshProUGUI rivalLineText;

        [Tooltip("立ち絵。スプライトを入れれば差し替わる")]
        [SerializeField] private UnityEngine.UI.Image playerPortrait;
        [SerializeField] private UnityEngine.UI.Image rivalPortrait;

        [Header("Let's FIGHT!")]
        [SerializeField] private TextMeshProUGUI fightText;

        [Header("文言（仮）")]
        [SerializeField] private string playerName = "あなた";
        [SerializeField] private string playerLine = "最高のステージにしようね！";
        [SerializeField] private string rivalLine = "簡単には負けないんだから！";

        [Header("時間(秒)")]
        [SerializeField, Min(0f)] private float slideTime = 0.35f;
        [SerializeField, Min(0f)] private float versusHold = 1.6f;
        [SerializeField, Min(0f)] private float fightHold = 0.9f;

        /// <summary>演出中か。自動テストはこれが終わるまで待つ</summary>
        public bool IsPlaying { get; private set; } = true;

        private Vector2 playerCardHome;
        private Vector2 rivalCardHome;

        private void Awake()
        {
            if (playerCard != null) playerCardHome = playerCard.anchoredPosition;
            if (rivalCard != null) rivalCardHome = rivalCard.anchoredPosition;
        }

        private void Start()
        {
            if (player != null) player.SetRunning(false);
            if (bgm != null) bgm.Stop();

            if (playerNameText != null) playerNameText.text = playerName;
            if (rivalNameText != null) rivalNameText.text = rival != null ? rival.RivalName : "ライバル";
            if (playerLineText != null) playerLineText.text = playerLine;
            if (rivalLineText != null) rivalLineText.text = rivalLine;

            if (fightText != null) fightText.gameObject.SetActive(false);

            StartCoroutine(Play());
        }

        private IEnumerator Play()
        {
            // --- VS カットイン: 左右から滑り込む ---
            if (versusGroup != null)
            {
                versusGroup.gameObject.SetActive(true);
                versusGroup.alpha = 1f;
            }

            RunSe.Play(SeId.Versus);

            float width = 1400f;
            yield return Animate(slideTime, t =>
            {
                float e = EaseOutBack(t);
                SetCard(playerCard, playerCardHome + Vector2.left * width * (1f - e));
                SetCard(rivalCard, rivalCardHome + Vector2.right * width * (1f - e));
                if (vsMark != null) vsMark.localScale = Vector3.one * Mathf.Lerp(3f, 1f, e);
            });

            yield return new WaitForSeconds(versusHold);

            // --- 退場 ---
            yield return Animate(slideTime * 0.8f, t =>
            {
                float e = t * t;
                SetCard(playerCard, playerCardHome + Vector2.left * width * e);
                SetCard(rivalCard, rivalCardHome + Vector2.right * width * e);
                if (versusGroup != null) versusGroup.alpha = 1f - t;
            });

            if (versusGroup != null) versusGroup.gameObject.SetActive(false);

            // --- Let's FIGHT! ---
            if (fightText != null)
            {
                fightText.gameObject.SetActive(true);
                RunSe.Play(SeId.Fight);

                yield return Animate(0.25f, t =>
                {
                    fightText.transform.localScale = Vector3.one * Mathf.Lerp(2.2f, 1f, EaseOutBack(t));
                    fightText.alpha = t;
                });

                yield return new WaitForSeconds(fightHold);

                yield return Animate(0.2f, t => fightText.alpha = 1f - t);
                fightText.gameObject.SetActive(false);
            }

            // --- 開始 ---
            if (player != null) player.SetRunning(true);
            if (bgm != null) bgm.Play();

            IsPlaying = false;
        }

        private static void SetCard(RectTransform card, Vector2 position)
        {
            if (card != null) card.anchoredPosition = position;
        }

        private static IEnumerator Animate(float duration, System.Action<float> step)
        {
            if (duration <= 0f)
            {
                step(1f);
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                step(Mathf.Clamp01(t / duration));
                yield return null;
            }

            step(1f);
        }

        /// <summary>少し行き過ぎて戻る。カットインの「バン！」感を出す</summary>
        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}
