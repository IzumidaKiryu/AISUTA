using System;
using UnityEngine;

namespace Yakiniku.Run
{
    /// <summary>
    /// スコアとコンボ、そしてスコア倍率の合成を担当する。
    ///
    /// 倍率の材料は3つ。
    ///   ・コンボ数
    ///   ・キラメキビートゲージの溜まり具合（<see cref="BeatGaugeController"/>）
    ///   ・ライバルの撃破数（<see cref="RunRival"/>）
    /// 計算式は <see cref="ScoreSettings"/> に置いてあるので、バランス調整は
    /// このクラスを触らずにアセット側で行える。
    ///
    /// 既存の GameManager も同じ役割を持っているが、あちらはコンボ倍率しか無く
    /// 数値がコードに直書きされている。Game_Run ではこちらを使う。
    /// </summary>
    public sealed class RunScore : MonoBehaviour
    {
        [Header("参照")]
        [SerializeField] private ScoreSettings settings;
        [SerializeField] private PlayerRunner player;
        [SerializeField] private BeatGaugeController gauge;
        [SerializeField] private RunRival rival;

        /// <summary>スコアが変わったとき</summary>
        public event Action<int> OnScoreChanged;

        /// <summary>コンボが変わったとき</summary>
        public event Action<int> OnComboChanged;

        /// <summary>倍率が変わったとき（表示用）</summary>
        public event Action<float> OnMultiplierChanged;

        public int Score { get; private set; }
        public int Combo { get; private set; }
        public int MaxCombo { get; private set; }

        /// <summary>今アイテムを取ったら掛かる倍率</summary>
        public float Multiplier { get; private set; } = 1f;

        private void Awake()
        {
            if (settings == null)
            {
                Debug.LogError($"{nameof(ScoreSettings)} が設定されていません", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (player != null)
            {
                player.OnItemCollected += HandleItemCollected;
                player.OnDamaged += HandleDamaged;
            }
        }

        private void OnDisable()
        {
            if (player != null)
            {
                player.OnItemCollected -= HandleItemCollected;
                player.OnDamaged -= HandleDamaged;
            }
        }

        private void Update()
        {
            // ゲージは毎フレーム動くので、倍率表示も追従させる
            RefreshMultiplier();
        }

        private void HandleItemCollected(BeatItem item)
        {
            Combo++;
            if (Combo > MaxCombo) MaxCombo = Combo;
            OnComboChanged?.Invoke(Combo);

            // ライバルへのダメージは倍率を更新する前に入れる。
            // 撃破した瞬間に取ったアイテムへ、その撃破ボーナスを乗せたいため
            if (rival != null) rival.ApplyHit();

            RefreshMultiplier();

            int baseScore = item != null ? item.Score : 0;
            Score += Mathf.RoundToInt(baseScore * Multiplier);
            OnScoreChanged?.Invoke(Score);
        }

        private void HandleDamaged(int remainingHealth)
        {
            if (Combo == 0) return;

            Combo = 0;
            OnComboChanged?.Invoke(Combo);
            RefreshMultiplier();
        }

        private void RefreshMultiplier()
        {
            float gaugeValue = gauge != null ? gauge.Value : 0f;
            bool isFever = gauge != null && gauge.IsFever;
            int defeats = rival != null ? rival.Defeats : 0;

            float next = settings.CalculateMultiplier(Combo, gaugeValue, defeats, isFever);
            if (Mathf.Approximately(next, Multiplier)) return;

            Multiplier = next;
            OnMultiplierChanged?.Invoke(Multiplier);
        }

        /// <summary>初期状態に戻す。リトライ時に呼ぶ。</summary>
        public void ResetScore()
        {
            Score = 0;
            Combo = 0;
            MaxCombo = 0;
            Multiplier = 1f;

            OnScoreChanged?.Invoke(Score);
            OnComboChanged?.Invoke(Combo);
            OnMultiplierChanged?.Invoke(Multiplier);
        }
    }
}
