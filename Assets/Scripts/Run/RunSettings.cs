using UnityEngine;

namespace Yakiniku.Run
{
    /// <summary>
    /// ランゲームの数値設定をまとめた ScriptableObject。
    /// コードを触らずにバランス調整できるようにするため、数値は全てここに集約する。
    /// レーン番号は既存の譜面JSON(StageData.NoteData.lane)と同じ「中央を0とした -2〜2」形式。
    /// </summary>
    [CreateAssetMenu(fileName = "RunSettings", menuName = "Yakiniku/Run/Run Settings")]
    public sealed class RunSettings : ScriptableObject
    {
        [Header("レーン")]
        [Tooltip("レーンの総数。中央を0番として左右対称に並べるため奇数を想定する（アイスタは5レーン）")]
        [Min(1)] public int laneCount = 5;

        [Tooltip("隣のレーンまでの距離(m)。譜面の配置もこの値を使うので、ここが唯一の定義")]
        [Min(0.1f)] public float laneWidth = 2.0f;

        [Header("前進")]
        [Tooltip("自動前進の速度(m/s)")]
        [Min(0f)] public float forwardSpeed = 10f;

        [Header("レーン切り替え")]
        [Tooltip("横移動の速度(m/s)。大きいほどキビキビ切り替わる")]
        [Min(0.1f)] public float laneChangeSpeed = 18f;

        [Header("ジャンプ")]
        [Tooltip("跳べる高さ(m)。ジャンプ初速はこの値から逆算する")]
        [Min(0.01f)] public float jumpHeight = 1.6f;

        [Tooltip("重力加速度(m/s^2)。落下させるので負の値を入れる")]
        public float gravity = -25f;

        [Tooltip("接地しているときのY座標")]
        public float groundY = 1f;

        [Header("耐久")]
        [Min(1)] public int maxHealth = 3;

        [Tooltip("被弾後に無敵でいる時間(秒)")]
        [Min(0f)] public float invincibleTime = 1.5f;

        /// <summary>一番左のレーン番号（5レーンなら -2）</summary>
        public int MinLane => -((laneCount - 1) / 2);

        /// <summary>一番右のレーン番号（5レーンなら 2）</summary>
        public int MaxLane => (laneCount - 1) / 2;

        /// <summary>レーン番号をワールドX座標に変換する。プレイヤーも譜面生成もこの関数を通す。</summary>
        public float LaneToX(int lane) => lane * laneWidth;

        /// <summary>レーン番号を有効範囲に丸める。</summary>
        public int ClampLane(int lane) => Mathf.Clamp(lane, MinLane, MaxLane);

        /// <summary>jumpHeight まで到達するのに必要なジャンプ初速。</summary>
        public float JumpVelocity => Mathf.Sqrt(Mathf.Max(0.0001f, -2f * gravity * jumpHeight));
    }
}
