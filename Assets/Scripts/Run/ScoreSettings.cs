using UnityEngine;

namespace Yakiniku.Run
{
    /// <summary>
    /// スコア倍率とライバルの設定。
    ///
    /// 倍率は「加算で積んでから、フィーバー系で全体に掛ける」方式にしている。
    /// 乗算で積むと要素が増えるたびに跳ね上がって調整が効かなくなるため。
    ///
    ///   倍率 = 1
    ///        + コンボボーナス（上限あり）
    ///        + ゲージボーナス（キラメキビートの溜まり具合）
    ///        + 撃破ボーナス（ライバルのハートを割った数）
    ///   そこに、スーパー ＞ ENCORE ＞ フィーバー のうち一番強いものを掛ける
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
        [Tooltip("ライバルのハートを1つ割るごとに上乗せされる倍率")]
        [Min(0f)] public float rivalDefeatBonus = 0.5f;

        [Header("フィーバー系（一番強いものだけ掛かる）")]
        [Tooltip("ゲージ満タンの小フィーバー中")]
        [Min(1f)] public float feverMultiplier = 1.5f;

        [Tooltip("ライバル撃破後の ENCORE TIME 中")]
        [Min(1f)] public float encoreMultiplier = 2f;

        [Tooltip("ENCORE とフィーバーが重なったスーパーフィーバー中")]
        [Min(1f)] public float superFeverMultiplier = 4f;

        [Header("上限")]
        [Tooltip("最終的な倍率の上限。表示が破綻しないように頭を押さえる")]
        [Min(1f)] public float maxMultiplier = 10f;

        [Header("ライバル")]
        [Tooltip("ライバルのハートの数。体力バーを削り切るごとに1つ減り、全部割ると ENCORE")]
        [Min(1)] public int rivalHearts = 5;

        [Tooltip("体力バー1本ぶんの最大値。RunBalance が譜面に合わせて上書きするので、" +
                 "ここは RunBalance を置かない場合の既定値")]
        [Min(1)] public int rivalMaxHp = 50;

        [Tooltip("ノーツ1つでライバルに与えるダメージ")]
        [Min(1)] public int rivalDamagePerHit = 1;

        /// <summary>現在の状態から最終的なスコア倍率を求める。</summary>
        public float CalculateMultiplier(
            int combo, float gaugeValue, int rivalDefeats, bool isFever, bool isEncore)
        {
            float multiplier = 1f;
            multiplier += Mathf.Min(combo * comboRate, comboBonusMax);
            multiplier += Mathf.Clamp01(gaugeValue) * gaugeBonusMax;
            multiplier += rivalDefeats * rivalDefeatBonus;

            if (isFever && isEncore) multiplier *= superFeverMultiplier;
            else if (isEncore) multiplier *= encoreMultiplier;
            else if (isFever) multiplier *= feverMultiplier;

            return Mathf.Min(multiplier, maxMultiplier);
        }
    }
}
