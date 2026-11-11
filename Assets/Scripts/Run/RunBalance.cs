using UnityEngine;

namespace Yakiniku.Run
{
    /// <summary>
    /// 譜面に合わせてライバルの体力とゲージの溜まり方を決める。
    ///
    /// 数値を固定で持つと、譜面のノーツ数が変わるたび（曲の差し替え・難易度違い）に
    /// 撃破のタイミングやフィーバーの回数がずれる。そこで「どうなってほしいか」を
    /// 設定に書き、実際の数値は譜面から逆算する。
    ///
    ///   ライバル … フルコンボで曲の rivalDefeatAt 割の地点でハートを割り切る
    ///             （本家: Hard 以上は 9割）
    ///   ゲージ   … フルコンボで1曲に feversPerSong 回フィーバーに入る
    /// </summary>
    public sealed class RunBalance : MonoBehaviour
    {
        [Header("参照")]
        [SerializeField] private RunSpawner spawner;
        [SerializeField] private RunRival rival;
        [SerializeField] private BeatGaugeController gauge;
        [SerializeField] private RunSettings runSettings;
        [SerializeField] private GaugeSettings gaugeSettings;
        [SerializeField] private ScoreSettings scoreSettings;

        [Header("難易度が分からないときの既定値")]
        [Range(0.1f, 1f)]
        [SerializeField] private float fallbackRivalDefeatAt = 0.9f;

        /// <summary>今回の譜面で決めた値（確認・テスト用）</summary>
        public int RivalHpPerHeart { get; private set; }
        public float GaugeGainPerItem { get; private set; }
        public float RivalDefeatAt { get; private set; }

        private void Start()
        {
            if (spawner == null || !spawner.IsReady || spawner.ItemCount <= 0)
            {
                Debug.LogWarning("譜面が読めていないので、バランスの自動調整をしません", this);
                return;
            }

            var entry = DifficultySettings.Load()?.Get(RunSession.Difficulty);
            RivalDefeatAt = entry != null ? entry.rivalDefeatAt : fallbackRivalDefeatAt;

            ConfigureRival();
            ConfigureGauge();
        }

        private void ConfigureRival()
        {
            if (rival == null || scoreSettings == null) return;

            // 撃破に必要なノーツ数 ＝ 総ノーツ数 × 撃破タイミング
            int hitsToDefeat = Mathf.Max(1, Mathf.RoundToInt(spawner.ItemCount * RivalDefeatAt));
            int totalHp = hitsToDefeat * scoreSettings.rivalDamagePerHit;

            RivalHpPerHeart = Mathf.Max(1, Mathf.CeilToInt(totalHp / (float)scoreSettings.rivalHearts));
            rival.Configure(RivalHpPerHeart);
        }

        private void ConfigureGauge()
        {
            if (gauge == null || gaugeSettings == null || runSettings == null) return;

            float songSeconds = Mathf.Max(1f, spawner.LastNoteZ / Mathf.Max(0.1f, runSettings.forwardSpeed));
            float itemsPerSecond = spawner.ItemCount / songSeconds;

            // フィーバー中はゲージが溜まらないので、その間に流れるノーツぶんを差し引く。
            //   1周 ＝ 溜める間のノーツ ＋ フィーバー中のノーツ
            //   周回数 × 1周 ＝ 総ノーツ数
            float itemsPerCycle = spawner.ItemCount / (float)gaugeSettings.feversPerSong;
            float itemsDuringFever = gaugeSettings.feverDuration * itemsPerSecond;
            float itemsToFill = Mathf.Max(5f, itemsPerCycle - itemsDuringFever);

            GaugeGainPerItem = 1f / itemsToFill;
            gauge.SetGainPerItem(GaugeGainPerItem);
        }
    }
}
