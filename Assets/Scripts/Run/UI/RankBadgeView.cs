using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Yakiniku.Run
{
    /// <summary>
    /// ランクのバッジ（SS+ など）。難易度選択とリザルトで共通に使う。
    ///
    /// <see cref="RankSettings"/> にバッジ画像が入っていればそれを出し、
    /// 無ければ色付きの丸と文字で仮表示する。画像を用意したら設定に入れるだけで差し替わる。
    /// </summary>
    public sealed class RankBadgeView : MonoBehaviour
    {
        [SerializeField] private Image badge;
        [SerializeField] private TextMeshProUGUI label;

        [Tooltip("ランクが付いていないときに出す文字")]
        [SerializeField] private string noneLabel = "-";

        private Sprite placeholderSprite;

        private void Awake()
        {
            if (badge != null) placeholderSprite = badge.sprite;
        }

        public void Show(RunRank rank)
        {
            var settings = RankSettings.Load();
            var tier = settings != null ? settings.Get(rank) : null;

            bool hasRank = rank != RunRank.None && tier != null;
            bool hasArt = hasRank && tier.badge != null;

            if (badge != null)
            {
                badge.sprite = hasArt ? tier.badge : placeholderSprite;
                badge.color = hasArt ? Color.white
                    : hasRank ? tier.color
                    : new Color(0.75f, 0.73f, 0.85f, 0.6f);
            }

            if (label != null)
            {
                // 画像があるなら文字は画像に描いてある前提で消す
                label.gameObject.SetActive(!hasArt);
                label.text = hasRank ? tier.label : noneLabel;
            }
        }
    }
}
