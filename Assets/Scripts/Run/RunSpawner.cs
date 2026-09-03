using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Yakiniku.Run
{
    /// <summary>譜面の type 文字列とプレハブの対応。</summary>
    [Serializable]
    public sealed class NotePrefabEntry
    {
        [Tooltip("譜面JSONの type と一致させる文字列 (Item / Obstacle / JumpObstacle など)")]
        public string type;

        public GameObject prefab;

        [Tooltip("生成時のY座標")]
        public float spawnY = 1f;

        [Tooltip("レーン指定を無視して中央(0)に出す。全レーンを塞ぐジャンプ用障害物などに使う")]
        public bool forceCenterLane;
    }

    /// <summary>
    /// 障害物・アイテムの生成を担当する。
    ///
    /// 【設計方針】
    /// ・開始時に全部生成せず、プレイヤーの手前 spawnAheadDistance だけ先読みして生成する。
    ///   曲が長くなってもメモリと初期ロードが膨らまない。
    /// ・生成/破棄は ObjectPool で使い回す。Destroy を連発するとモバイルで
    ///   GC が走ってカクつく（Cが担当するAndroid実機で特に効く）。
    /// ・配置データは INoteProvider から受け取る。JSON譜面でもランダム生成でも同じ処理で動く。
    /// </summary>
    public sealed class RunSpawner : MonoBehaviour
    {
        [Header("参照")]
        [SerializeField] private RunSettings settings;
        [SerializeField] private PlayerRunner player;

        [Tooltip("配置データの供給元。未設定なら同じGameObjectから探す")]
        [SerializeField] private NoteProviderBehaviour noteProvider;

        [Header("プレハブ")]
        [SerializeField] private NotePrefabEntry[] notePrefabs;

        [Header("生成範囲")]
        [Tooltip("プレイヤーの何m先まで先読みして生成するか")]
        [SerializeField, Min(1f)] private float spawnAheadDistance = 60f;

        [Tooltip("プレイヤーの何m後ろまで下がったら回収するか")]
        [SerializeField, Min(1f)] private float despawnBehindDistance = 15f;

        [Header("プール")]
        [SerializeField, Min(1)] private int poolDefaultCapacity = 16;
        [SerializeField, Min(1)] private int poolMaxSize = 128;

        private readonly Dictionary<string, ObjectPool<GameObject>> pools =
            new Dictionary<string, ObjectPool<GameObject>>();

        private readonly Dictionary<string, NotePrefabEntry> entries =
            new Dictionary<string, NotePrefabEntry>();

        private readonly List<SpawnedNote> activeNotes = new List<SpawnedNote>();

        private List<NoteData> notes = new List<NoteData>();
        private int cursor;

        /// <summary>まだ生成されていないノーツが残っているか（＝ステージがまだ続くか）</summary>
        public bool HasRemainingNotes => cursor < notes.Count;

        private void Awake()
        {
            if (settings == null)
            {
                Debug.LogError($"{nameof(RunSettings)} が設定されていません", this);
                enabled = false;
                return;
            }

            if (noteProvider == null) noteProvider = GetComponent<NoteProviderBehaviour>();

            BuildPools();
        }

        private void Start()
        {
            var source = noteProvider != null ? noteProvider.GetNotes() : null;
            notes = source != null ? new List<NoteData>(source) : new List<NoteData>();

            // 先読み判定のため、必ずZ座標の昇順に並べておく
            notes.Sort((a, b) => a.spawnZ.CompareTo(b.spawnZ));
            cursor = 0;
        }

        private void BuildPools()
        {
            if (notePrefabs == null) return;

            foreach (var entry in notePrefabs)
            {
                if (entry == null || string.IsNullOrEmpty(entry.type) || entry.prefab == null) continue;

                if (entries.ContainsKey(entry.type))
                {
                    Debug.LogWarning($"ノーツ種別 '{entry.type}' が重複して登録されています", this);
                    continue;
                }

                entries[entry.type] = entry;

                string key = entry.type;
                GameObject prefab = entry.prefab;

                pools[key] = new ObjectPool<GameObject>(
                    createFunc: () => CreateInstance(prefab, key),
                    actionOnGet: null,   // 位置を決めてから有効化したいので Spawn 側で SetActive する
                    actionOnRelease: instance => instance.SetActive(false),
                    actionOnDestroy: Destroy,
                    collectionCheck: false,
                    defaultCapacity: poolDefaultCapacity,
                    maxSize: poolMaxSize);
            }
        }

        private void Update()
        {
            if (player == null) return;

            float playerZ = player.DistanceZ;

            // 先読み生成
            while (cursor < notes.Count && notes[cursor].spawnZ <= playerZ + spawnAheadDistance)
            {
                Spawn(notes[cursor]);
                cursor++;
            }

            // 通り過ぎたものを回収（Release でリストから消えるので後ろから走査する）
            for (int i = activeNotes.Count - 1; i >= 0; i--)
            {
                var note = activeNotes[i];
                if (note == null)
                {
                    activeNotes.RemoveAt(i);
                    continue;
                }

                if (note.transform.position.z < playerZ - despawnBehindDistance)
                {
                    note.Despawn();
                }
            }
        }

        private GameObject CreateInstance(GameObject prefab, string key)
        {
            var instance = Instantiate(prefab, transform);

            var note = instance.GetComponent<SpawnedNote>();
            if (note == null) note = instance.AddComponent<SpawnedNote>();
            note.Owner = this;
            note.PoolKey = key;

            instance.SetActive(false);
            return instance;
        }

        private void Spawn(NoteData data)
        {
            if (!pools.TryGetValue(data.type, out var pool))
            {
                Debug.LogWarning($"プレハブが登録されていないノーツ種別です: '{data.type}'", this);
                return;
            }

            var entry = entries[data.type];
            int lane = entry.forceCenterLane ? 0 : settings.ClampLane(data.lane);

            var instance = pool.Get();
            instance.transform.SetPositionAndRotation(
                new Vector3(settings.LaneToX(lane), entry.spawnY, data.spawnZ),
                Quaternion.identity);

            var note = instance.GetComponent<SpawnedNote>();
            note.MarkSpawned();
            instance.SetActive(true);

            activeNotes.Add(note);
        }

        internal void Release(SpawnedNote note)
        {
            activeNotes.Remove(note);

            if (pools.TryGetValue(note.PoolKey, out var pool))
            {
                pool.Release(note.gameObject);
            }
            else
            {
                note.gameObject.SetActive(false);
            }
        }

        /// <summary>全て回収して最初から生成しなおす。リトライ時に呼ぶ。</summary>
        public void ResetSpawner()
        {
            for (int i = activeNotes.Count - 1; i >= 0; i--)
            {
                if (activeNotes[i] != null) activeNotes[i].Despawn();
            }

            activeNotes.Clear();
            cursor = 0;
        }
    }
}
