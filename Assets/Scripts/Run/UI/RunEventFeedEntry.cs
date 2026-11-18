using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Yakiniku.Run
{
    /// <summary>
    /// 実況ログ欄の1行（吹き出し）。アイコンの丸・名前・本文を持つ。
    /// シーン上の雛形を <see cref="RunEventFeed"/> が複製して使う。
    /// </summary>
    public sealed class RunEventFeedEntry : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI iconLabel;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private CanvasGroup group;

        public RectTransform Rect => (RectTransform)transform;
        public CanvasGroup Group => group;

        /// <summary>出てからの経過秒数。一定時間で薄くなって消える</summary>
        public float Age { get; set; }

        public void Set(string author, string message, Color iconColor)
        {
            if (icon != null) icon.color = iconColor;

            // アイコンは名前の頭文字（画像が用意できたら icon にスプライトを入れればよい）
            if (iconLabel != null) iconLabel.text = string.IsNullOrEmpty(author) ? string.Empty : author.Substring(0, 1);
            if (nameText != null) nameText.text = author;
            if (messageText != null) messageText.text = message;

            Age = 0f;
        }
    }
}
