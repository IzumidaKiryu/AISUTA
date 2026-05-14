using System;
using System.Collections.Generic;

[Serializable]
public class NoteData
{
    public float spawnZ;   // 配置するZ座標
    public int lane;       // -2(左端) ～ 2(右端)
    public string type;    // "Item", "Obstacle", "JumpObstacle"
}

[Serializable]
public class StageData
{
    public List<NoteData> notes;
}