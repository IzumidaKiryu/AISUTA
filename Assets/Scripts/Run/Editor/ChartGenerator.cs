using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Yakiniku.Run.EditorTools
{
    /// <summary>
    /// 譜面JSONを生成する。
    ///
    /// 曲がまだ制作中で長さもBPMも変わるため、手書きの譜面を置くと作り直しになる。
    /// パラメータから組み立てるようにしておけば、曲が固まった時点で数値を変えて
    /// 作り直せる。シード固定なので、同じ設定なら必ず同じ譜面になる。
    ///
    /// 実行方法:
    ///   Unityのメニュー  Yakiniku > Run > 譜面を生成
    ///   コマンドライン    Unity.exe -batchmode -quit -projectPath .
    ///                     -executeMethod Yakiniku.Run.EditorTools.ChartGenerator.GenerateAll
    /// </summary>
    public static class ChartGenerator
    {
        private const string ResourcesDir = "Assets/Resources";

        /// <summary>BGM(ぼららら)の長さ。曲が差し替わったらここを直す。</summary>
        private const float SongSeconds = 121.1f;

        /// <summary>仮のBPM。曲が固まったら実際の値に合わせる。</summary>
        private const float Bpm = 150f;

        /// <summary>RunSettings.forwardSpeed と必ず合わせる。ここがズレると曲と譜面が合わない。</summary>
        private const float ForwardSpeed = 10f;

        /// <summary>走り出してから最初のノーツまでの距離(m)。いきなり出てこないように</summary>
        private const float LeadInZ = 20f;

        /// <summary>最後のノーツから曲の終わりまでの余白(m)</summary>
        private const float TailZ = 40f;

        /// <summary>
        /// 譜面1つぶんの設定。
        ///
        /// 間隔は全て「ステップ数」で数える。1拍を <see cref="Subdivision"/> で割ったものが
        /// 1ステップ。Subdivision=1 なら4分、2なら8分刻みになる。
        /// 難易度を上げるときに障害物だけ増やすとアイテムの枠が潰れて
        /// かえって稼げなくなるので、刻み自体を細かくして密度を上げる。
        /// </summary>
        private sealed class ChartSpec
        {
            public string FileName;
            public int Subdivision;        // 1拍をいくつに割るか
            public int StepsPerItem;       // 何ステップごとにアイテムを置くか
            public int StepsPerObstacle;   // 何ステップごとに障害物を置くか
            public int StepsPerJumpWall;   // 何ステップごとに低い壁を置くか
            public int StepsPerLaneShift;  // 何ステップごとに進路を動かすか
            public int Seed;
        }

        private static readonly ChartSpec[] Specs =
        {
            new ChartSpec
            {
                FileName = "StageData_Easy",
                Subdivision = 1,
                // 障害物の間隔は進路切り替え(8)の倍数にしない。倍数だと障害物が毎回
                // ちょうど切り替え地点に来て、置ける場所が無くなる（実際3個まで減った）
                StepsPerItem = 2, StepsPerObstacle = 20, StepsPerJumpWall = 48,
                StepsPerLaneShift = 8, Seed = 1001,
            },
            new ChartSpec
            {
                FileName = "StageData_Normal",
                Subdivision = 1,
                StepsPerItem = 1, StepsPerObstacle = 10, StepsPerJumpWall = 32,
                StepsPerLaneShift = 6, Seed = 2002,
            },
            new ChartSpec
            {
                // 8分刻み。アイテムが倍になるので撃破数も伸びる
                FileName = "StageData_Hard",
                Subdivision = 2,
                StepsPerItem = 1, StepsPerObstacle = 12, StepsPerJumpWall = 40,
                StepsPerLaneShift = 8, Seed = 3003,
            },
            new ChartSpec
            {
                // Hard を SS でクリアすると遊べる最上位。
                // 刻みは Hard と同じ8分のまま、障害物・壁・進路の切り替えを詰める
                FileName = "StageData_Universe",
                Subdivision = 2,
                StepsPerItem = 1, StepsPerObstacle = 7, StepsPerJumpWall = 26,
                StepsPerLaneShift = 5, Seed = 4004,
            },
        };

        [MenuItem("Yakiniku/Run/譜面を生成", false, 30)]
        public static void GenerateAll()
        {
            var report = new StringBuilder();
            report.AppendLine("===== 譜面を生成しました =====");
            report.AppendLine($"  曲 {SongSeconds:F1}秒 / BPM {Bpm} / 速度 {ForwardSpeed}m/s"
                              + $" → 走行距離 {SongSeconds * ForwardSpeed:F0}m");
            report.AppendLine();

            foreach (var spec in Specs)
            {
                var notes = Build(spec);
                Write(spec.FileName, notes);

                int items = 0, obstacles = 0, walls = 0;
                foreach (var n in notes)
                {
                    if (n.type == "Item") items++;
                    else if (n.type == "Obstacle") obstacles++;
                    else walls++;
                }

                report.AppendLine($"  {spec.FileName}: 全{notes.Count}個"
                                  + $"（アイテム{items} / 障害物{obstacles} / 低い壁{walls}）");
            }

            AssetDatabase.Refresh();
            Debug.Log(report.ToString());
        }

        private static List<NoteData> Build(ChartSpec spec)
        {
            var notes = new List<NoteData>();
            var random = new System.Random(spec.Seed);

            float beatLength = ForwardSpeed * 60f / Bpm;       // 1拍で進む距離(m)
            float stepLength = beatLength / spec.Subdivision;  // 1ステップで進む距離(m)
            float endZ = SongSeconds * ForwardSpeed - TailZ;
            int stepCount = Mathf.FloorToInt((endZ - LeadInZ) / stepLength);

            // 先に進路（どのレーンを走るか）を決めてから、その上にノーツを置く。
            // 進路が分かっていれば「取れるアイテム」「かすめる障害物」を置き分けられる
            var lanePath = BuildLanePath(stepCount, spec.StepsPerLaneShift, random);

            for (int beat = 0; beat < stepCount; beat++)
            {
                float z = LeadInZ + beat * stepLength;
                int lane = lanePath[beat];

                // 低い壁は全レーンを塞ぐ。ジャンプで越える想定なので、
                // 同じ位置にアイテムを置かない（飛んでいる間は取れない）
                if (beat % spec.StepsPerJumpWall == 0 && beat > 0)
                {
                    notes.Add(new NoteData { spawnZ = Round(z), lane = 0, type = "JumpObstacle" });
                    continue;
                }

                if (beat % spec.StepsPerObstacle == 0 && beat > 0)
                {
                    // 走っているレーンの隣に置く。まっすぐ走る分には当たらないが、
                    // 雑にレーンを動かすと引っかかる位置。
                    //
                    // ただし前後数ステップで進路が通るレーンには置かない。
                    // 進路が切り替わった直後だと「さっきまで居たレーン」に障害物が来て、
                    // 避ける猶予が1m程度しか無い（人間でも理不尽）。
                    int clearance = Mathf.Max(1, Mathf.CeilToInt(ObstacleClearanceMeters / stepLength));
                    int obstacleLane = PickObstacleLane(lanePath, beat, clearance, random);
                    if (obstacleLane != int.MinValue)
                    {
                        notes.Add(new NoteData
                        {
                            spawnZ = Round(z), lane = obstacleLane, type = "Obstacle",
                        });
                    }
                }

                if (beat % spec.StepsPerItem == 0)
                {
                    notes.Add(new NoteData { spawnZ = Round(z), lane = lane, type = "Item" });
                }
            }

            return notes;
        }

        /// <summary>
        /// 障害物を置くレーンを選ぶ。進路の隣で、かつ前後 clearance ステップの間に
        /// 進路が通らないレーン。置ける場所が無ければ int.MinValue（置かない）。
        /// </summary>
        private static int PickObstacleLane(int[] lanePath, int step, int clearance, System.Random random)
        {
            int lane = lanePath[step];
            var candidates = new List<int>(2);

            foreach (int offset in new[] { -1, 1 })
            {
                int candidate = lane + offset;
                if (candidate < -2 || candidate > 2) continue;

                bool onPath = false;
                for (int s = step - clearance; s <= step + clearance; s++)
                {
                    if (s < 0 || s >= lanePath.Length) continue;
                    if (lanePath[s] == candidate) { onPath = true; break; }
                }

                if (!onPath) candidates.Add(candidate);
            }

            return candidates.Count == 0 ? int.MinValue : candidates[random.Next(candidates.Count)];
        }

        /// <summary>
        /// 障害物の前後、進路が通ってはいけない距離(m)。
        /// ステップ数で決めると刻みの粗い譜面(Easy は1ステップ4m)で空けすぎて障害物が消えるので、距離で決める。
        /// 横移動は 2m を 0.11秒（前に1.1m進む）なので、それより十分長くとる
        /// </summary>
        private const float ObstacleClearanceMeters = 3f;

        /// <summary>
        /// 走る進路を作る。一定間隔で隣のレーンへ動かし、端では折り返す。
        /// いきなり2レーン飛ばさないので、普通に追いかければ全部取れる。
        /// </summary>
        private static int[] BuildLanePath(int beatCount, int beatsPerShift, System.Random random)
        {
            var path = new int[beatCount];
            int lane = 0;
            int direction = 1;

            for (int beat = 0; beat < beatCount; beat++)
            {
                if (beat > 0 && beat % beatsPerShift == 0)
                {
                    if (random.Next(4) == 0) direction = -direction;

                    int next = lane + direction;
                    if (next < -2 || next > 2)
                    {
                        direction = -direction;
                        next = lane + direction;
                    }

                    lane = Mathf.Clamp(next, -2, 2);
                }

                path[beat] = lane;
            }

            return path;
        }

        private static float Round(float value)
        {
            // JSONを読みやすくするため小数1桁に丸める
            return Mathf.Round(value * 10f) / 10f;
        }

        private static void Write(string fileName, List<NoteData> notes)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");

            // 曲のテンポ。ペンライトを曲に合わせて振るのに使う（StageData には無いので別に読む）
            sb.Append("    \"bpm\": ");
            sb.Append(Bpm.ToString("0.###", CultureInfo.InvariantCulture));
            sb.AppendLine(",");

            sb.AppendLine("    \"notes\": [");

            for (int i = 0; i < notes.Count; i++)
            {
                var n = notes[i];
                sb.Append("        {\"spawnZ\": ");
                sb.Append(n.spawnZ.ToString("0.0", CultureInfo.InvariantCulture));
                sb.Append(", \"lane\": ");
                sb.Append(n.lane.ToString(CultureInfo.InvariantCulture));
                sb.Append(", \"type\": \"");
                sb.Append(n.type);
                sb.Append("\"}");
                sb.AppendLine(i < notes.Count - 1 ? "," : string.Empty);
            }

            sb.AppendLine("    ]");
            sb.AppendLine("}");

            string path = Path.Combine(ResourcesDir, fileName + ".json");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }
    }
}
