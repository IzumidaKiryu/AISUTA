using System.Collections.Generic;
using UnityEngine;

namespace Yakiniku.Run
{
    /// <summary>
    /// ゲーム画面の上部に出す実況ログ欄（本家のコメント欄のような吹き出しが下から積み上がる）。
    ///
    /// 新しいものほど下（ライバルの枠のすぐ上）に出し、古いものは上へ押し出されて、
    /// <see cref="maxEntries"/> を超えるか <see cref="lifetime"/> を過ぎると消える。
    ///
    /// 何を流すかはこのクラスは知らない。<see cref="Post"/> を呼ぶ側
    /// （<see cref="RunEventFeedHooks"/>、将来は視聴者コメントの入力元など）が決める。
    /// </summary>
    public sealed class RunEventFeed : MonoBehaviour
    {
        [Tooltip("複製して使う1行の雛形（非表示にしておく）。下端に付けた、横いっぱいの RectTransform であること")]
        [SerializeField] private RunEventFeedEntry template;

        [Header("並べ方")]
        [SerializeField, Min(1)] private int maxEntries = 3;
        [SerializeField, Min(1f)] private float entryHeight = 116f;
        [SerializeField, Min(0f)] private float spacing = 12f;

        [Header("時間(秒)")]
        [Tooltip("出てから薄くなり始めるまで")]
        [SerializeField, Min(0.1f)] private float lifetime = 9f;
        [SerializeField, Min(0.01f)] private float fadeTime = 0.6f;
        [Tooltip("上へ押し出されるときの追従の速さ")]
        [SerializeField, Min(0.1f)] private float slideSpeed = 14f;

        private readonly List<RunEventFeedEntry> entries = new List<RunEventFeedEntry>();

        private void Awake()
        {
            if (template != null) template.gameObject.SetActive(false);
        }

        /// <summary>1行流す。author はアイコンの頭文字と名前に、iconColor はアイコンの丸の色に使う。</summary>
        public void Post(string author, string message, Color iconColor)
        {
            if (template == null || string.IsNullOrEmpty(message)) return;

            var entry = Instantiate(template, template.transform.parent);
            entry.gameObject.SetActive(true);
            entry.Set(author, message, iconColor);

            // 少し下から滑り上がってくる
            entry.Rect.anchoredPosition = new Vector2(0f, -entryHeight * 0.4f);
            if (entry.Group != null) entry.Group.alpha = 0f;

            entries.Add(entry);

            while (entries.Count > maxEntries)
            {
                Destroy(entries[0].gameObject);
                entries.RemoveAt(0);
            }
        }

        private void Update()
        {
            // スクショ用に時間を早送りしても見た目が破綻しないよう、ゲーム内時間で動かす
            float dt = Time.deltaTime;
            float follow = 1f - Mathf.Exp(-slideSpeed * dt);

            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var entry = entries[i];
                entry.Age += dt;

                int fromBottom = entries.Count - 1 - i;
                var position = entry.Rect.anchoredPosition;
                position.y = Mathf.Lerp(position.y, fromBottom * (entryHeight + spacing), follow);
                entry.Rect.anchoredPosition = position;

                float fadeIn = Mathf.Clamp01(entry.Age / 0.15f);
                float fadeOut = 1f - Mathf.Clamp01((entry.Age - lifetime) / fadeTime);
                if (entry.Group != null) entry.Group.alpha = fadeIn * fadeOut;

                if (entry.Age > lifetime + fadeTime)
                {
                    Destroy(entry.gameObject);
                    entries.RemoveAt(i);
                }
            }
        }
    }
}
