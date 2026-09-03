using UnityEngine;

namespace Yakiniku.Run
{
    /// <summary>
    /// キラメキビートゲージの増減ルール。
    /// バランス調整は必ず何度も発生するので、数値は全てここに出してコード改修なしで触れるようにする。
    /// アイドルごとにステータス差を付けたい場合（B担当）は、このアセットを複数作って差し替える。
    /// </summary>
    [CreateAssetMenu(fileName = "GaugeSettings", menuName = "Yakiniku/Run/Gauge Settings")]
    public sealed class GaugeSettings : ScriptableObject
    {
        [Header("加算")]
        [Tooltip("アイテム1個あたりの加算量(0〜1)。アイテム側で個別指定されていればそちらを優先")]
        [Range(0f, 1f)] public float gainPerItem = 0.06f;

        [Tooltip("走り続けている間の毎秒加算量")]
        [Range(0f, 1f)] public float gainPerSecond = 0.02f;

        [Tooltip("コンボ1つにつき加算量に上乗せされる倍率")]
        [Range(0f, 1f)] public float comboGainRate = 0.05f;

        [Tooltip("コンボ倍率の上限")]
        [Min(1f)] public float maxComboMultiplier = 2f;

        [Header("減算")]
        [Tooltip("何もしていないときの毎秒の自然減衰")]
        [Range(0f, 1f)] public float decayPerSecond = 0.01f;

        [Tooltip("被弾したときに失う量")]
        [Range(0f, 1f)] public float damagePenalty = 0.15f;

        [Header("フィーバー")]
        [Tooltip("ゲージ満タンで発動するフィーバーの継続時間(秒)。0なら発動しない")]
        [Min(0f)] public float feverDuration = 8f;

        [Tooltip("フィーバー中はゲージを消費していく（残量がそのまま残り時間の表示になる）")]
        public bool drainDuringFever = true;
    }
}
