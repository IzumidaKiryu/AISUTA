using UnityEngine;

namespace Yakiniku.Run
{
    /// <summary>
    /// ランゲーム側のイベントを既存の GameManager に橋渡しする繋ぎ役。
    ///
    /// PlayerRunner や BeatGaugeController から GameManager を直接呼ばないのは、
    /// スコアや勝敗の仕様（B担当）が変わってもランゲームの実装を触らずに済ませるため。
    /// 仕様が固まったらこのクラスだけ差し替えれば対応できる。
    /// </summary>
    public sealed class RunGameBridge : MonoBehaviour
    {
        [SerializeField] private PlayerRunner player;
        [SerializeField] private BeatGaugeController gauge;

        private void OnEnable()
        {
            if (player == null) return;

            player.OnItemCollected += HandleItemCollected;
            player.OnDamaged += HandleDamaged;
            player.OnDead += HandleDead;
        }

        private void OnDisable()
        {
            if (player == null) return;

            player.OnItemCollected -= HandleItemCollected;
            player.OnDamaged -= HandleDamaged;
            player.OnDead -= HandleDead;
        }

        private void Start()
        {
            if (player != null && GameManager.instance != null)
            {
                GameManager.instance.UpdatePlayerHP(player.Health);
            }
        }

        private void HandleItemCollected(BeatItem item)
        {
            if (GameManager.instance == null || item == null) return;
            GameManager.instance.AddComboAndScore(item.Score);
        }

        private void HandleDamaged(int remainingHealth)
        {
            if (GameManager.instance == null) return;

            GameManager.instance.ResetCombo();
            GameManager.instance.UpdatePlayerHP(remainingHealth);
        }

        private void HandleDead()
        {
            Debug.Log("ゲームオーバー");
            // リザルト遷移はB担当のリザルト実装に合わせてここから呼ぶ
        }
    }
}
