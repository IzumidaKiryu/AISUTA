using System.Collections.Generic;
using UnityEngine;

namespace Yakiniku.Run
{
    /// <summary>
    /// 床タイルが前方へ回されたときに知らせてほしい物が実装するインターフェース。
    /// ペンライトのように「位置が変わったら作り直す必要がある物」のために用意している。
    /// </summary>
    public interface ITileRecycleListener
    {
        void OnTileRecycled();
    }

    /// <summary>
    /// 床タイルを前方に敷き続け、通り過ぎたものを使い回す。
    /// 既存 StageManager の床ループ処理を、ノーツ生成(RunSpawner)から切り離したもの。
    /// 役割を分けておくと、床の見た目を変える作業と譜面の作業が衝突しない。
    ///
    /// 床の両サイドに並べる物（ペンライトなど）も、タイルの子として一緒に生成する。
    /// 別の仕組みでループさせると床とズレていくので、位置を持つ主体を床に一本化している。
    /// </summary>
    public sealed class GroundTiler : MonoBehaviour
    {
        [Header("参照")]
        [SerializeField] private PlayerRunner player;
        [SerializeField] private GameObject groundPrefab;

        [Header("タイル")]
        [Tooltip("床プレハブ1枚のZ方向の長さ(m)。プレハブの実寸と必ず合わせる")]
        [SerializeField, Min(0.1f)] private float tileLength = 50f;

        [Tooltip("床プレハブの原点が中心にあるか。Unity標準の Plane / Cube はチェックを入れる")]
        [SerializeField] private bool pivotAtCenter = true;

        [Tooltip("常に確保しておく枚数。プレイヤーの前方に (枚数-1) 枚ぶん敷かれる")]
        [SerializeField, Min(2)] private int tileCount = 3;

        [Tooltip("プレイヤーの何m後ろまで下がったタイルを前に回すか")]
        [SerializeField, Min(0f)] private float recycleBehindDistance = 10f;

        [Tooltip("床を置くY座標。プレイヤーの足元(RunSettings.groundY - プレイヤーの高さの半分)に合わせる")]
        [SerializeField] private float groundY;

        [Header("床の両サイドに並べる物")]
        [Tooltip("タイル1枚につき左右それぞれに生成するプレハブ。ペンライトなど")]
        [SerializeField] private GameObject[] sideProps;

        [Tooltip("床の中心から左右へどれだけ離すか(m)。床幅の半分より大きくすると床の外側に出る")]
        [SerializeField] private float sidePropOffsetX = 6.5f;

        [Tooltip("両サイドの物を置くY座標(ワールド)")]
        [SerializeField] private float sidePropY;

        private readonly List<Transform> tiles = new List<Transform>();

        /// <summary>タイルの「手前の端」から原点までの距離。中心原点なら半分ずれる。</summary>
        private float PivotOffset => pivotAtCenter ? tileLength * 0.5f : 0f;

        /// <summary>次に敷くタイルの「手前の端」のZ座標</summary>
        private float nextTileStartZ;

        private void Start()
        {
            if (groundPrefab == null)
            {
                Debug.LogError("床プレハブが設定されていません", this);
                enabled = false;
                return;
            }

            // 足元から前方に向かって初期タイルを敷く
            nextTileStartZ = 0f;
            for (int i = 0; i < tileCount; i++)
            {
                SpawnTile();
            }
        }

        private void Update()
        {
            if (player == null || tiles.Count == 0) return;

            float playerZ = player.DistanceZ;
            Transform oldest = tiles[0];
            float oldestEndZ = oldest.position.z - PivotOffset + tileLength;

            // 一番古いタイルを完全に通り過ぎたら、そのタイルを最前列へ移動させる
            if (playerZ - recycleBehindDistance > oldestEndZ)
            {
                PlaceTile(oldest, nextTileStartZ);
                nextTileStartZ += tileLength;

                tiles.RemoveAt(0);
                tiles.Add(oldest);

                NotifyRecycled(oldest);
            }
        }

        private void SpawnTile()
        {
            var tile = Instantiate(groundPrefab, Vector3.zero, Quaternion.identity, transform);
            PlaceTile(tile.transform, nextTileStartZ);
            AttachSideProps(tile.transform);

            tiles.Add(tile.transform);
            nextTileStartZ += tileLength;
        }

        /// <summary>タイルの左右に装飾を付ける。タイルの子にするので、以後は床と一緒に動く。</summary>
        private void AttachSideProps(Transform tile)
        {
            if (sideProps == null) return;

            foreach (var prop in sideProps)
            {
                if (prop == null) continue;

                for (int sign = -1; sign <= 1; sign += 2)
                {
                    var instance = Instantiate(prop, tile);
                    instance.transform.localPosition =
                        new Vector3(sign * sidePropOffsetX, sidePropY - tile.position.y, 0f);
                    instance.transform.localRotation = prop.transform.rotation;
                }
            }
        }

        /// <summary>位置が変わったことを子に知らせる（ペンライトは並びを作り直す）。</summary>
        private void NotifyRecycled(Transform tile)
        {
            foreach (var listener in tile.GetComponentsInChildren<ITileRecycleListener>(true))
            {
                listener.OnTileRecycled();
            }
        }

        private void PlaceTile(Transform tile, float startZ)
        {
            tile.position = new Vector3(0f, groundY, startZ + PivotOffset);
        }

        /// <summary>タイルを初期配置に戻す。リトライ時に呼ぶ。</summary>
        public void ResetTiles()
        {
            nextTileStartZ = 0f;
            foreach (var tile in tiles)
            {
                if (tile == null) continue;

                PlaceTile(tile, nextTileStartZ);
                nextTileStartZ += tileLength;
                NotifyRecycled(tile);
            }
        }
    }
}
