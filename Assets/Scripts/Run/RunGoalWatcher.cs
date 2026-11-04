using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Yakiniku.Run
{
    /// <summary>ステージをクリアと判定する基準。</summary>
    public enum RunClearCondition
    {
        /// <summary>譜面の最後のノーツを通過したら終わり。曲が未完成の間はこちら。</summary>
        ChartEnd = 0,

        /// <summary>BGMが鳴り終わったら終わり。曲が完成したらこちらに戻す。</summary>
        BgmLength = 1,
    }

    /// <summary>
    /// ステージの終わりを判定して、リザルトへ送り出す。
    ///
    /// 【なぜ切り替え式にしてあるか】
    /// 本来は「曲が鳴り終わったらクリア」が正しい。ただし曲の制作中は
    /// 曲の長さ(121秒)と譜面の長さ(17秒)が噛み合わず、ノーツが尽きた後に
    /// 100秒以上空の床を走ることになる。そこで当面は譜面基準で終わらせ、
    /// 曲と譜面が揃ったら <see cref="condition"/> を BgmLength に戻せばよい。
    ///
    /// 既存 GameManager も曲の長さでクリア判定を持っているが、二重に発火すると
    /// どちらが先か分からなくなるので、Game_Run では GameManager の bgmSource を
    /// 空にしておき、終了判定はこのクラスだけが持つようにしている。
    /// </summary>
    public sealed class RunGoalWatcher : MonoBehaviour
    {
        [Header("判定基準")]
        [Tooltip("曲が完成したら BgmLength に戻す。それまでは ChartEnd")]
        [SerializeField] private RunClearCondition condition = RunClearCondition.ChartEnd;

        [Header("参照")]
        [SerializeField] private PlayerRunner player;
        [SerializeField] private RunSpawner spawner;

        [Tooltip("BgmLength のときに見るBGM。ChartEnd のときは使わない")]
        [SerializeField] private AudioSource bgmSource;

        [Header("ChartEnd の設定")]
        [Tooltip("最後のノーツから何m先まで走ったら終わりにするか。通り抜ける余韻ぶん")]
        [SerializeField, Min(0f)] private float goalMarginZ = 15f;

        [Header("遷移")]
        [Tooltip("クリア後にこのシーンへ移る。空ならシーン遷移しない（確認用）")]
        [SerializeField] private string resultSceneName = "Result";

        [Tooltip("クリア／ゲームオーバーからシーン遷移までの待ち時間(秒)")]
        [SerializeField, Min(0f)] private float transitionDelay = 1.5f;

        /// <summary>クリアしたとき。演出はこれを購読する</summary>
        public event Action OnCleared;

        /// <summary>HPが尽きたとき</summary>
        public event Action OnFailed;

        public bool IsFinished { get; private set; }

        private float finishedAt;
        private bool leaving;

        /// <summary>BGMが一度でも鳴り始めたか。再生開始前の isPlaying==false を終了と誤判定しないため。</summary>
        private bool bgmStarted;

        private void OnEnable()
        {
            if (player != null) player.OnDead += HandleDead;
        }

        private void OnDisable()
        {
            if (player != null) player.OnDead -= HandleDead;
        }

        private void Update()
        {
            if (IsFinished)
            {
                WaitAndLeave();
                return;
            }

            if (IsGoalReached()) Finish(true);
        }

        private bool IsGoalReached()
        {
            switch (condition)
            {
                case RunClearCondition.BgmLength:
                    if (bgmSource == null || bgmSource.clip == null) return false;

                    // 再生が始まる前は isPlaying が false なので、一度鳴り始めたかを覚えておく
                    if (bgmSource.isPlaying)
                    {
                        bgmStarted = true;
                        return false;
                    }

                    return bgmStarted;

                case RunClearCondition.ChartEnd:
                default:
                    return player != null
                           && spawner != null
                           && spawner.IsReady
                           && !spawner.HasRemainingNotes
                           && player.DistanceZ > spawner.LastNoteZ + goalMarginZ;
            }
        }

        private void HandleDead() => Finish(false);

        private void Finish(bool cleared)
        {
            if (IsFinished) return;

            IsFinished = true;
            finishedAt = Time.time;

            if (player != null) player.SetRunning(false);

            // スコアをリザルトへ引き継ぐ（既存 GameData をそのまま使う）
            if (GameManager.instance != null)
            {
                GameData.finalScore = GameManager.instance.score;
            }

            if (cleared)
            {
                Debug.Log("ステージクリア");
                OnCleared?.Invoke();
            }
            else
            {
                Debug.Log("ゲームオーバー");
                OnFailed?.Invoke();
            }
        }

        private void WaitAndLeave()
        {
            if (leaving || string.IsNullOrEmpty(resultSceneName)) return;
            if (Time.time - finishedAt < transitionDelay) return;

            leaving = true; // Update は遷移が終わるまで何度も回るので二重ロードを防ぐ
            SceneManager.LoadScene(resultSceneName);
        }
    }
}
