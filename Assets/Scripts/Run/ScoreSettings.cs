using UnityEngine;

namespace Yakiniku.Run
{
    /// <summary>
    /// スコア倍率とライバルの設定。
    ///
    /// 倍率は「加算で積んでから、フィーバーで全体に掛ける」方式にしている。
    /// 乗算で積むと要素が増えるたびに跳ね上がって調整が効かなくなるため。
    ///
    ///   倍率 = 1
    ///        + コンボボーナス（上限あり）
    ///        + ゲージボーナス（キラメキビートの溜まり具合）
    ///        + 撃破ボーナス（ライバルを倒した数）
    ///   フィーバー中は上の合計に feverMultiplier を掛ける
    /// </summary>
    [CreateAssetMenu(fileName = "ScoreSettings", menuName = "Yakiniku/Run/Score Settings")]
    public sealed class ScoreSettings : ScriptableObject
    {
        [Header("コンボ")]
        [Tooltip("コンボ1つあたりの倍率上乗せ")]
        [Min(0f)] public float comboRate = 0.02f;

        [Tooltip("コンボで上乗せできる倍率の上限。これが無いと長い曲で青天井になる")]
        [Min(0f)] public float comboBonusMax = 2f;

        [Header("キラメキビートゲージ")]
        [Tooltip("ゲージが満タンのときに上乗せされる倍率。溜まり具合に比例する")]
        [Min(0f)] public float gaugeBonusMax = 1f;

        [Header("ライバル撃破")]
        [Tooltip("ライバルを1回倒すごとに上乗せされる倍率。倒すほど伸びる")]
        [Min(0f)] public float rivalDefeatBonus = 0.5f;

        [Header("フィーバー")]
        [Tooltip("フィーバー中に合計倍率へ掛ける値")]
        [Min(1f)] public float feverMultiplier = 2f;

        [Tooltip("スーパーフィーバー中に合計倍率へ掛ける値。" +
                 "ゲージ満タンとライバル全撃破が揃ったときの特別枠")]
        [Min(1f)] public float superFeverMultiplier = 4f;

        [Header("上限")]
        [Tooltip("最終的な倍率の上限。表示が破綻しないように頭を押さえる")]
        [Min(1f)] public float maxMultiplier = 10f;

        [Header("ライバル")]
        [Tooltip("体力バー1本ぶんの最大値")]
        [Min(1)] public int rivalMaxHp = 50;

        [Tooltip("ライバルのハートの数。体力バーを削り切るごとに1つ減る")]
        [Min(1)] public int rivalHearts = 3;

        [Tooltip("アイテム1つでライバルに与えるダメージ")]
        [Min(1)] public int rivalDamagePerHit = 1;

        [Tooltip("ハートを全部割ったあと、ライバルが復活して再戦できるか。" +
                 "オフだと撃破ボーナスはハートの数で打ち止めになる")]
        public bool rivalRespawns = true;

        /// <summary>現在の状態から最終的なスコア倍率を求める。</summary>
        public float CalculateMultiplier(
            int combo, float gaugeValue, int rivalDefeats, bool isFever, bool isSuperFever = false)
        {
            float multiplier = 1f;
            multiplier += Mathf.Min(combo * comboRate, comboBonusMax);
            multiplier += Mathf.Clamp01(gaugeValue) * gaugeBonusMax;
            multiplier += rivalDefeats * rivalDefeatBonus;

            if (isSuperFever) multiplier *= superFeverMultiplier;
            else if (isFever) multiplier *= feverMultiplier;

            return Mathf.Min(multiplier, maxMultiplier);
        }
    }
}
