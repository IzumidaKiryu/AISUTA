using System;
using UnityEngine;
using Yakiniku.Run.Inputs;

namespace Yakiniku.Run
{
    /// <summary>
    /// 自動前進・レーン切り替え・ジャンプを担当するプレイヤー本体。
    ///
    /// 【設計方針】
    /// ・入力は IRunInputSource 経由でしか受け取らない（キーボード実装に依存しない）
    /// ・Rigidbody は isKinematic にして移動は全て自前計算。
    ///   物理エンジンに任せると前進速度やジャンプ滞空時間が環境で微妙にブレるが、
    ///   譜面に合わせて障害物が飛んでくるゲームなのでタイミングは決定的である必要がある。
    ///   （Cが検証する「可変リフレッシュレート」の影響もこれで受けなくなる：
    ///     画面が60Hzでも120Hzでも FixedUpdate の刻みは同じなので挙動が変わらない）
    /// ・他クラスへは event で通知するだけ。GameManager 等を直接呼ばないことで、
    ///   B/C の作業とコンフリクトしないようにしている。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PlayerRunner : MonoBehaviour
    {
        [Header("参照")]
        [SerializeField] private RunSettings settings;

        [Tooltip("操作の入力元。KeyboardRunInput / DebugButtonRunInput / (Cが作る)SwipeRunInput などを差す")]
        [SerializeField] private RunInputSourceBehaviour inputSource;

        /// <summary>レーンが変わったとき。引数は新しいレーン番号(-2〜2)</summary>
        public event Action<int> OnLaneChanged;

        /// <summary>ジャンプしたとき</summary>
        public event Action OnJumped;

        /// <summary>ダメージを受けたとき。引数は残りHP</summary>
        public event Action<int> OnDamaged;

        /// <summary>HPが0になったとき</summary>
        public event Action OnDead;

        /// <summary>アイテムを取得したとき。ゲージやスコアはこれを購読して加算する</summary>
        public event Action<BeatItem> OnItemCollected;

        public RunSettings Settings => settings;
        public int CurrentLane { get; private set; }
        public int Health { get; private set; }
        public bool IsAlive => Health > 0;
        public bool IsGrounded { get; private set; } = true;
        public bool IsInvincible => invincibleTimer > 0f;

        /// <summary>走行中かどうか。false の間は前進もしないし当たり判定も取らない</summary>
        public bool IsRunning { get; private set; }

        /// <summary>スタートからの走行距離(Z座標)。譜面の生成位置判定に使う</summary>
        public float DistanceZ => position.z;

        private Rigidbody rb;
        private Vector3 position;
        private float verticalVelocity;
        private float invincibleTimer;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();

            // 自前で動かすので物理演算は切っておく
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            if (settings == null)
            {
                Debug.LogError($"{nameof(RunSettings)} が設定されていません", this);
                enabled = false;
                return;
            }

            ResetRun();
        }

        /// <summary>初期状態に戻す。リトライ時に呼ぶ。</summary>
        public void ResetRun()
        {
            CurrentLane = 0;
            Health = settings.maxHealth;
            verticalVelocity = 0f;
            invincibleTimer = 0f;
            IsGrounded = true;

            position = new Vector3(settings.LaneToX(CurrentLane), settings.groundY, 0f);
            transform.position = position;
            rb.position = position;

            if (inputSource != null) inputSource.ClearBuffer();

            IsRunning = true;
        }

        /// <summary>走行の一時停止／再開。ゲーム開始演出やポーズで使う。</summary>
        public void SetRunning(bool value) => IsRunning = value;

        // 移動は全て FixedUpdate（固定間隔）で行う。フレームレートに左右されないため。
        private void FixedUpdate()
        {
            if (!IsRunning) return;

            float deltaTime = Time.fixedDeltaTime;

            ConsumeInput();
            UpdateVertical(deltaTime);
            UpdateHorizontal(deltaTime);

            position.z += settings.forwardSpeed * deltaTime;
            rb.MovePosition(position);

            if (invincibleTimer > 0f)
            {
                invincibleTimer -= deltaTime;
            }
        }

        /// <summary>溜まっている操作を全て処理する。</summary>
        private void ConsumeInput()
        {
            if (inputSource == null) return;

            while (inputSource.TryDequeue(out RunInputAction action))
            {
                switch (action)
                {
                    case RunInputAction.MoveLeft:
                        TryChangeLane(-1);
                        break;
                    case RunInputAction.MoveRight:
                        TryChangeLane(+1);
                        break;
                    case RunInputAction.Jump:
                        TryJump();
                        break;
                }
            }
        }

        /// <summary>レーンを相対移動する。端で移動できなかった場合は false。</summary>
        public bool TryChangeLane(int delta)
        {
            int next = settings.ClampLane(CurrentLane + delta);
            if (next == CurrentLane) return false;

            CurrentLane = next;
            OnLaneChanged?.Invoke(CurrentLane);
            return true;
        }

        /// <summary>ジャンプする。空中なら false。</summary>
        public bool TryJump()
        {
            if (!IsGrounded) return false;

            verticalVelocity = settings.JumpVelocity;
            IsGrounded = false;
            OnJumped?.Invoke();
            return true;
        }

        // 縦方向は自前の重力計算。地面のコライダーには依存しない（接地判定が確実になる）
        private void UpdateVertical(float deltaTime)
        {
            if (IsGrounded) return;

            verticalVelocity += settings.gravity * deltaTime;
            position.y += verticalVelocity * deltaTime;

            if (position.y <= settings.groundY)
            {
                position.y = settings.groundY;
                verticalVelocity = 0f;
                IsGrounded = true;
            }
        }

        // 横方向は Lerp ではなく MoveTowards。
        // 「何秒後に隣のレーンに到達するか」が一定になり、譜面の詰め具合を調整しやすいため。
        private void UpdateHorizontal(float deltaTime)
        {
            float targetX = settings.LaneToX(CurrentLane);
            position.x = Mathf.MoveTowards(position.x, targetX, settings.laneChangeSpeed * deltaTime);
        }

        // 当たり判定。触れた相手が何であるかの分岐はここに書かず、
        // 相手側(IPlayerTouchable)に処理を持たせる。障害物やアイテムの種類が増えても
        // このクラスを修正しなくて済むようにするため。
        private void OnTriggerEnter(Collider other)
        {
            if (!IsRunning) return;

            var touchable = other.GetComponentInParent<IPlayerTouchable>();
            touchable?.OnTouched(this);
        }

        /// <summary>ダメージを与える。無敵中や死亡後は無視され false を返す。</summary>
        public bool ApplyDamage(int amount)
        {
            if (amount <= 0 || !IsAlive || IsInvincible) return false;

            Health = Mathf.Max(0, Health - amount);
            invincibleTimer = settings.invincibleTime;
            OnDamaged?.Invoke(Health);

            if (Health <= 0)
            {
                IsRunning = false;
                OnDead?.Invoke();
            }

            return true;
        }

        /// <summary>アイテム取得を通知する。BeatItem から呼ばれる。</summary>
        public void CollectItem(BeatItem item) => OnItemCollected?.Invoke(item);
    }
}
