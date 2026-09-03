using UnityEngine;

namespace Yakiniku.Run
{
    /// <summary>
    /// 取るとキラメキビートゲージが溜まるアイテム。
    /// 加算量とスコアはプレハブ単位で変えられるようにしてあるので、
    /// 「レアアイテムは大きく溜まる」といった調整はプレハブを増やすだけで済む。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class BeatItem : MonoBehaviour, IPlayerTouchable
    {
        [Tooltip("ゲージ加算量(0〜1)。0以下なら GaugeSettings の既定値を使う")]
        [SerializeField] private float gaugeGain = 0.06f;

        [Tooltip("加算スコア")]
        [SerializeField, Min(0)] private int score = 10;

        public float GaugeGain => gaugeGain;
        public int Score => score;

        public void OnTouched(PlayerRunner player)
        {
            // 取得したことを通知するだけ。ゲージ加算もスコア加算もここではやらない
            // （購読側がそれぞれ処理するので、演出やスコア仕様の変更に巻き込まれない）
            player.CollectItem(this);
            SpawnedNote.DespawnObject(gameObject);
        }

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }
    }
}
