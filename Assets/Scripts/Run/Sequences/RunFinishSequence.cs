using System.Collections;
using TMPro;
using UnityEngine;

namespace Yakiniku.Run
{
    /// <summary>
    /// ゲーム終了の演出（本家: FINISH → ☆のローディング → リザルト）。
    ///
    /// 終了の判定そのものは <see cref="RunGoalWatcher"/> が持つ。
    /// ここはその通知を受けて、FINISH を出し、結果をまとめて保存し、リザルトへ送る。
    /// </summary>
    public sealed class RunFinishSequence : MonoBehaviour
    {
        [Header("参照")]
        [SerializeField] private RunGoalWatcher goal;
        [SerializeField] private PlayerRunner player;
        [SerializeField] private RunScore score;
        [SerializeField] private RunRival rival;
        [SerializeField] private RunSpawner spawner;

        [Header("表示")]
        [SerializeField] private TextMeshProUGUI finishText;

        [Header("時間(秒)")]
        [SerializeField, Min(0f)] private float hold = 1.6f;

        [Tooltip("リザルトへ遷移するか。自動テストでは止めておく")]
        [SerializeField] private bool goToResult = true;

        /// <summary>直前にまとめた結果（自動テスト用）</summary>
        public RunResult Result { get; private set; }

        private bool started;

        private void Awake()
        {
            if (finishText != null) finishText.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (goal == null) return;
            goal.OnCleared += HandleFinished;
            goal.OnFailed += HandleFinished;
        }

        private void OnDisable()
        {
            if (goal == null) return;
            goal.OnCleared -= HandleFinished;
            goal.OnFailed -= HandleFinished;
        }

        /// <summary>自動テストから遷移を止めるため。</summary>
        public void SetGoToResult(bool value) => goToResult = value;

        private void HandleFinished()
        {
            if (started) return;
            started = true;

            Result = BuildResult();
            RunProgress.Record(Result);
            RunSession.LastResult = Result;
            GameData.finalScore = Result.score;

            StartCoroutine(Play());
        }

        private RunResult BuildResult()
        {
            var result = new RunResult
            {
                difficulty = RunSession.Difficulty,
                score = score != null ? score.Score : 0,
                maxCombo = score != null ? score.MaxCombo : 0,
                itemsCollected = score != null ? score.ItemsCollected : 0,
                damageTaken = score != null ? score.DamageTaken : 0,
                totalItems = spawner != null ? spawner.ItemCount : 0,
                heartsBroken = rival != null ? rival.HeartsBroken : 0,
                rivalDefeated = rival != null && rival.IsDefeated,
                survived = player != null && player.IsAlive,
            };

            var entry = DifficultySettings.Load()?.Get(result.difficulty);
            var ranks = RankSettings.Load();
            result.rank = ranks != null && entry != null
                ? ranks.Evaluate(result.score, entry.perfectScore)
                : RunRank.C;

            return result;
        }

        private IEnumerator Play()
        {
            if (finishText != null)
            {
                finishText.gameObject.SetActive(true);
                RunSe.Play(SeId.Finish);

                float t = 0f;
                while (t < 0.3f)
                {
                    t += Time.deltaTime;
                    float e = Mathf.Clamp01(t / 0.3f);
                    finishText.transform.localScale = Vector3.one * Mathf.Lerp(2.4f, 1f, 1f - (1f - e) * (1f - e));
                    finishText.alpha = e;
                    yield return null;
                }
            }

            yield return new WaitForSeconds(hold);

            if (goToResult) RunFlow.ToResult();
        }
    }
}
