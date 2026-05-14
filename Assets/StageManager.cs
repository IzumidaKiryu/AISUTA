using System.Collections.Generic;
using UnityEngine;

public class StageManager : MonoBehaviour
{
    [Header("ステージ設定")]
    public GameObject stagePrefab;
    public float stageLength = 50f;
    public int startStageCount = 3;
    public Transform player;

    [Header("譜面設定")]
    public string jsonFileName = "StageData_Normal"; // Resourcesフォルダ内のファイル名
    public float laneDistance = 2.0f; // PlayerControllerのlaneDistanceと合わせる

    [Header("プレハブ")]
    public GameObject itemPrefab;
    public GameObject obstaclePrefab;
    public GameObject jumpObstaclePrefab; // ジャンプ専用障害物（後で作成します）

    private float spawnZ = 0f;
    private List<GameObject> activeStages = new List<GameObject>();

    void Start()
    {
        // ▼追加: GameDataから選択された難易度のファイル名を受け取る処理
        jsonFileName = GameData.selectedJsonName;
        // 1. 床をいくつか生成
        for (int i = 0; i < startStageCount; i++)
        {
            SpawnStageFloor();
        }

        // 2. JSONからアイテムと障害物を一気に配置
        LoadAndSpawnNotes();
    }

    void Update()
    {
        // 床の無限ループ生成（既存のまま）
        if (player == null) return;
        if (player.position.z > activeStages[0].transform.position.z + stageLength)
        {
            SpawnStageFloor();
            DeleteOldStage();
        }
    }

    void SpawnStageFloor()
    {
        GameObject stage = Instantiate(stagePrefab, Vector3.forward * spawnZ, Quaternion.identity);
        activeStages.Add(stage);
        spawnZ += stageLength;
    }

    void DeleteOldStage()
    {
        Destroy(activeStages[0]);
        activeStages.RemoveAt(0);
    }

    void LoadAndSpawnNotes()
    {
        // ResourcesフォルダからJSONを読み込む
        TextAsset jsonText = Resources.Load<TextAsset>(jsonFileName);
        if (jsonText == null)
        {
            Debug.LogError($"JSONファイル ({jsonFileName}) がResourcesフォルダに見つかりません！");
            return;
        }

        // JSONをクラスに変換
        StageData stageData = JsonUtility.FromJson<StageData>(jsonText.text);

        // ノーツを順番に生成
        foreach (NoteData note in stageData.notes)
        {
            GameObject prefabToSpawn = null;
            Vector3 spawnPos = new Vector3(note.lane * laneDistance, 1f, note.spawnZ);

            switch (note.type)
            {
                case "Item":
                    prefabToSpawn = itemPrefab;
                    break;
                case "Obstacle":
                    prefabToSpawn = obstaclePrefab;
                    break;
                case "JumpObstacle":
                    prefabToSpawn = jumpObstaclePrefab;
                    // ジャンプ専用障害物はレーンを中央(0)に強制する
                    spawnPos.x = 0;
                    break;
            }

            if (prefabToSpawn != null)
            {
                Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
            }
        }
    }
}