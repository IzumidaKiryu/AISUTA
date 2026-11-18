using UnityEngine;

namespace Yakiniku.Run
{
    /// <summary>
    /// ゲーム中の出来事を実況ログ欄に流す。
    ///
    /// <see cref="RunSeHooks"/> と同じく、ゲームロジック側は実況のことを知らず、
    /// ここがイベントを購読して文言を決めるだけ。文言はインスペクタで変えられる。
    ///
    /// アイテム取得のように頻繁に起きるものは流さない（欄があっという間に流れて読めなくなる）。
    /// COMBO は区切りの数でだけ流す。
    /// </summary>
    public sealed class RunEventFeedHooks : MonoBehaviour
    {
        [Header("参照")]
        [SerializeField] private RunEventFeed feed;
        [SerializeField] private PlayerRunner player;
        [SerializeField] private RunRival rival;
        [SerializeField] private BeatGaugeController gauge;
        [SerializeField] private RunScore score;

        [Header("名前とアイコンの色")]
        [SerializeField] private string announcerName = "実況";
        [SerializeField] private Color announcerColor = new Color(1f, 0.72f, 0.2f);
        [SerializeField] private Color rivalColor = new Color(1f, 0.45f, 0.7f);
        [SerializeField] private Color dangerColor = new Color(0.95f, 0.3f, 0.35f);

        [Header("COMBO")]
        [Tooltip("この数ごとに「n COMBO 達成！」を流す")]
        [SerializeField, Min(1)] private int comboStep = 50;
        [Tooltip("この数以上続いた COMBO が途切れたら知らせる")]
        [SerializeField, Min(1)] private int comboBreakNotice = 20;

        [Header("ライバルのセリフ（仮）")]
        [SerializeField] private string rivalHeartLine = "まだまだ、これからなんだから！";
        [SerializeField] private string rivalDefeatLine = "くっ……やるじゃない！";

        private int lastCombo;

        private string RivalName => rival != null ? rival.RivalName : "ライバル";

        private void OnEnable()
        {
            if (player != null)
            {
                player.OnDamaged += OnDamaged;
                player.OnDead += OnDead;
            }

            if (rival != null)
            {
                rival.OnHeartBroken += OnHeartBroken;
                rival.OnDefeated += OnDefeated;
            }

            if (gauge != null)
            {
                gauge.OnFeverStart += OnFever;
                gauge.OnEncoreStart += OnEncore;
                gauge.OnSuperFeverStart += OnSuperFever;
            }

            if (score != null) score.OnComboChanged += OnComboChanged;
        }

        private void OnDisable()
        {
            if (player != null)
            {
                player.OnDamaged -= OnDamaged;
                player.OnDead -= OnDead;
            }

            if (rival != null)
            {
                rival.OnHeartBroken -= OnHeartBroken;
                rival.OnDefeated -= OnDefeated;
            }

            if (gauge != null)
            {
                gauge.OnFeverStart -= OnFever;
                gauge.OnEncoreStart -= OnEncore;
                gauge.OnSuperFeverStart -= OnSuperFever;
            }

            if (score != null) score.OnComboChanged -= OnComboChanged;
        }

        private void Start()
        {
            Announce($"{RivalName}とのアイドルバトル開始！");
        }

        private void Announce(string message) => Post(announcerName, message, announcerColor);

        private void Post(string author, string message, Color color)
        {
            if (feed != null) feed.Post(author, message, color);
        }

        private void OnDamaged(int health)
        {
            Post(announcerName, health > 0 ? $"被弾！ 残りライフ {health}" : "被弾！", dangerColor);
        }

        private void OnDead() => Post(announcerName, "ライフが尽きてしまった……", dangerColor);

        private void OnHeartBroken(int brokenCount)
        {
            Announce($"ライバルのハートを割った！（{brokenCount}つ目）");
            Post(RivalName, rivalHeartLine, rivalColor);
        }

        private void OnDefeated() => Post(RivalName, rivalDefeatLine, rivalColor);

        // スーパーと ENCORE が同時に始まる瞬間（フィーバー中に撃破）は、派手なほうだけ流す
        private void OnFever()
        {
            if (gauge != null && gauge.IsSuperFever) return;
            Announce("FEVER突入！ スコアがぐんぐん伸びるよ！");
        }

        private void OnEncore()
        {
            if (gauge != null && gauge.IsSuperFever) return;
            Announce("ENCORE TIME！ 会場が金色に！");
        }

        private void OnSuperFever() => Announce("SUPER FEVER!! 最高潮だ！");

        private void OnComboChanged(int combo)
        {
            if (combo > 0 && combo % comboStep == 0)
            {
                Announce($"{combo} COMBO 達成！");
            }
            else if (combo == 0 && lastCombo >= comboBreakNotice)
            {
                Post(announcerName, $"COMBO が途切れた……（{lastCombo} COMBO）", dangerColor);
            }

            lastCombo = combo;
        }
    }
}
