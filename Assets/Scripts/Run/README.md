# ランゲーム基盤（メンバーA担当）

`Assets/Scripts/Run/` 以下がランゲームの基盤。既存の `PlayerController` / `StageManager` は
壊さずそのまま残してあるので、動作比較しながら移行できる。

## 全体構成

```
[入力層]      IRunInputSource  ← Keyboard / DebugButton / Swipe(C担当) / コメント
                  ↓ RunInputAction
[ロジック層]  PlayerRunner ・ RunSpawner ・ BeatGauge
                  ↓ C# event
[表示層]      BeatGaugeView(Slider) ・ 演出(B担当) ・ RunGameBridge → GameManager
[設定]        RunSettings / GaugeSettings (ScriptableObject) ← 数値は全部ここ
```

層をまたぐ依存は全てインターフェースか event 経由。担当者が別ファイルを触るので
コンフリクトしにくく、片方が未完成でも自分の作業を進められる。

## ファイル一覧

| ファイル | 役割 |
| --- | --- |
| `RunSettings.cs` | レーン数・速度・ジャンプ等の数値設定(SO)。**レーン↔X座標の変換もここが唯一の定義** |
| `Inputs/IRunInputSource.cs` | 操作の入力元インターフェース + `RunInputAction` |
| `Inputs/RunInputSourceBehaviour.cs` | 入力元の基底クラス。バッファリングを共通化 |
| `Inputs/KeyboardRunInput.cs` | 開発用キーボード入力（矢印 / WASD / Space） |
| `Inputs/DebugButtonRunInput.cs` | uGUIボタンからの入力（実機手動確認用） |
| `PlayerRunner.cs` | 自動前進・レーン切替・ジャンプ・被弾処理 |
| `IPlayerTouchable.cs` | 「プレイヤーが触れたら何かが起きる物」のインターフェース |
| `Obstacle.cs` / `BeatItem.cs` | 障害物 / ゲージが溜まるアイテム |
| `INoteProvider.cs` / `JsonNoteProvider.cs` | 配置データの供給元。既存の譜面JSONをそのまま使える |
| `RunSpawner.cs` | 先読み生成 + オブジェクトプールによる障害物生成 |
| `SpawnedNote.cs` | プール返却用の目印 |
| `GroundTiler.cs` | 床タイルの無限ループ（既存 StageManager の床処理を分離したもの） |
| `BeatGauge.cs` | キラメキビートゲージの値本体（純C#・テスト可能） |
| `GaugeSettings.cs` | ゲージの増減ルール(SO) |
| `BeatGaugeController.cs` | ルールを実行してゲージを増減させる。フィーバー管理 |
| `BeatGaugeView.cs` | Slider への表示 |
| `ScoreSettings.cs` | スコア倍率とライバルの設定(SO)。**バランス調整は全てここ** |
| `RunRival.cs` | ライバルの体力・ハート・撃破数 |
| `RunScore.cs` | スコアとコンボ、倍率の合成 |
| `RunHudView.cs` | HUDの表示（体力バー・ハート・スコア・倍率） |
| `RunGameBridge.cs` | 既存 `GameManager` への繋ぎ役。Game_Run では未使用（旧シーン向けに残置） |
| `RunGoalWatcher.cs` | ステージ終了の判定とリザルトへの遷移。**譜面基準／曲基準を切替** |
| `PenlightStrip.cs` | 床タイルの脇に並ぶペンライト1列。タイルが回されたら撒き直す |
| `Editor/RunSceneBuilder.cs` | シーン・設定アセット・プレハブの自動生成 |
| `Editor/RunSmokeTest.cs` | 自動プレイで動作確認チェックリストを検証 |
| `Editor/PlayFromTitle.cs` | どのシーンを開いていてもPlayでタイトルから始める |

---

# 動作確認用シーンの作り方

**手作業は不要。** Unityのメニューから

```
Yakiniku > Run > Game_Run シーンを生成
```

を実行すると、`Assets/Scenes/Game_Run.unity` が設定済みの状態で生成される。
中身は `Assets/Scripts/Run/Editor/RunSceneBuilder.cs` にコードとして書いてあるので、
シーンを壊してしまっても同じメニューを押せば作り直せる（何度実行してもよい）。

コマンドラインから実行する場合（Unityを閉じた状態で）:

```
Unity.exe -batchmode -quit -projectPath <プロジェクトのパス> -executeMethod Yakiniku.Run.EditorTools.RunSceneBuilder.BuildAll
```

## 生成されるもの

| パス | 中身 |
| --- | --- |
| `Assets/Settings_Run/RunSettings.asset` | レーン・速度・ジャンプの数値設定 |
| `Assets/Settings_Run/GaugeSettings.asset` | ゲージの増減ルール |
| `Assets/Prefabs/Run/Run_Item.prefab` | 既存 `Item.prefab` + 当たり判定 + `BeatItem` |
| `Assets/Prefabs/Run/Run_Obstacle.prefab` | 既存 `Obstacle.prefab` + 当たり判定 + `Obstacle` |
| `Assets/Prefabs/Run/Run_JumpObstacle.prefab` | 既存 `JumpObstacle.prefab` + 当たり判定 + `Obstacle` |
| `Assets/Scenes/Game_Run.unity` | 下記のHierarchyが組まれたシーン |

```
Game_Run
├ Main Camera        … CameraController (Player追従)
├ Directional Light
├ Player             … BoxCollider + Rigidbody + PlayerRunner
│   └ PlayerModel    … 既存の Assets/Player/PlayerModel.prefab
├ InputSource        … KeyboardRunInput（矢印 / WASD / Space）
├ Ground             … GroundTiler（既存 Stage_Fixed.prefab を敷き続ける）
├ Spawner            … JsonNoteProvider + RunSpawner
├ GaugeController    … BeatGaugeController
├ Canvas
│   └ BeatGaugeSlider … Slider + BeatGaugeView
└ EventSystem
```

生成したら **シーンを開いて Play を押すだけ**で動く。

## 見た目のアセットは変更していない

`Item` / `Obstacle` / `JumpObstacle` / `PlayerModel` / `Stage_Fixed` は他メンバーの担当なので、
**ファイルは一切書き換えていない**。ランゲーム用のプレハブは、それらを子として抱え込み、
当たり判定とゲームロジックを親側に持たせる構造にしてある。

```
Run_Obstacle (親・自分の担当)
├ CapsuleCollider (isTrigger)  ← ゲーム上の当たり判定はこれだけ
├ Obstacle (スクリプト)
└ Obstacle (子・既存アセット)   ← 見た目専用。元のColliderは無効化してある
```

こうしている理由は2つ。

1. **モデルを差し替えても当たり判定が壊れない。** スケールを変えても親のColliderは変わらない。
2. **既存 `Obstacle.prefab` のColliderが実寸と合っていない。** モデルを100倍スケールで
   使っているため、付いている CapsuleCollider が半径50m・高さ200mになっており、
   そのまま使うとスタート直後に被弾する。元ファイルを直さずに回避したかった。

見た目の大きさや向きがおかしい場合は、`Run_*.prefab` の**子**のTransformを調整すればよい。
当たり判定（親のCollider）は触らなくてよい。

## 寸法の決めごと

数値は `RunSceneBuilder.cs` の先頭にまとめてある。

| 項目 | 値 | 理由 |
| --- | --- | --- |
| 床の上面 | `y = 0` | `Stage_Fixed` の床は厚さ1mで原点が中心なので、原点を `-0.5` に置く |
| `RunSettings.groundY` | `0.35` | プレイヤーの基準点。モデルの原点(足元)が床の上面に来る高さ |
| プレイヤーの当たり判定 | 0.8 × 1.6 × 0.8 の箱 | モデルの胴体ぶん。`isTrigger` はオフ |
| `Item` の生成Y | `1.0` | 半径0.7の球判定。レーン幅2mなので隣のレーンには届かない |
| `Obstacle` の生成Y | `1.0` | 高さ2mのカプセル判定。ジャンプでは越えられない＝横に避ける |
| `JumpObstacle` の生成Y | `0.5` | 判定の高さを0.8mに抑えてある（見た目は1m）ので、ジャンプで越えられる |

`JumpObstacle` を越えられる根拠: `jumpHeight 1.6` / `gravity -25` だと滞空0.72秒で、
判定の上(0.8m)より高い位置にいる時間が約0.5秒。前進10m/sで壁の判定を通過するのに
必要な時間は約0.18秒なので、タイミングが多少ずれても越えられる。
`jumpHeight` を下げるとここが詰まるので注意。

---

---

# 本番シーンは Game_Run

`SceneController.StartGame()` は `Game_Run` を読む。旧 `Game` シーンは消さずに残してあるので、
戻したい場合は `SceneController.cs` の1行を `"Game"` に書き換えればよい。

## 旧 Game シーンに残っている問題（Game_Run では解消済み）

旧シーンを使い続ける場合は、以下が未修正であることに注意する。

| 症状 | 原因 |
| --- | --- |
| 障害物が倒れてスポーンする | `StageManager` が `Quaternion.identity` で生成し、`Obstacle.prefab` の -90°(X) 補正回転を捨てている |
| 開始1秒で見えないままHPが減る | `Obstacle.prefab` を100倍スケールで使っているため、CapsuleCollider が**半径50m・高さ200m**になっている |
| ジャンプ障害物に当たると体力が残っていても止まる | 上記で開始時に体力を2つ失っているため、壁でHPが0になる。前進を止めているのはHP0のみ |
| レーン±2のアイテムが取れない | `StageManager.laneDistance = 2.5` と `PlayerController.laneDistance = 2` が不一致 |

`Game_Run` では当たり判定をスケール1の親に持たせ、レーン↔X変換を
`RunSettings.LaneToX()` の1箇所に集約しているので、これらは構造的に起きない。

## ゴール判定

`RunGoalWatcher` が持つ。`condition` で基準を切り替える。

| 値 | 判定 | 使う場面 |
| --- | --- | --- |
| `ChartEnd`（現在） | 譜面の最後のノーツから `goalMarginZ`(15m) 先まで走ったら終了 | **曲の制作中。** 曲121秒に対し譜面17秒なので、曲基準だと100秒以上空の床を走ることになる |
| `BgmLength` | BGMが鳴り終わったら終了 | **曲と譜面が揃ったらこちらに戻す** |

切り替えは `Game_Run` の `GoalWatcher` の Inspector で `Condition` を変えるだけ。

なお `GameManager` も曲の長さでクリア判定を持っているが、二重に発火しないよう
`Game_Run` では `GameManager.bgmSource` を**意図的に空**にしてある。
BGM は `BGM` オブジェクトの `AudioSource` が持ち、`GoalWatcher` がそれを見る。
`GameManager.bgmSource` に割り当て直すと終了判定が二重になるので注意。

## 床の両サイドのペンライト

`GroundTiler` の `Side Props` に `Run_PenlightStrip.prefab` を入れてあり、
**床タイル1枚につき左右へ1列ずつ、タイルの子として生成**される。
床と同じ仕組みで前方へ回るので、床とズレることがない。

| 項目 | 値 | 備考 |
| --- | --- | --- |
| `Side Prop Offset X` | 6.5 | 床幅10m（端は5m）の外側に出す |
| 1列の粒数 | 450 | 元の `Particle System.prefab` は1回のバーストで50000個だった |
| Simulation Space | **World** | Local にするとタイルを動かした瞬間に粒が付いてきて「ペンライトが追尾する」状態になる |

見た目のプレハブ（`Assets/Particle System.prefab`）は**書き換えていない**。
子として抱えて、並べ方だけ上書きしている。

### Bloom が無いと光らない

`Game_Run` には `Global Volume`（プロファイルは `Global Volume Profile 1`、Bloom の
threshold 0.2 / intensity 2）を置き、**カメラの `Render Post Processing` をONにしてある**。
Volume を置いてもカメラ側がOFFだと Bloom は一切効かず、ペンライトは
ただの明るいベタ塗り＝白い棒になる。どちらか片方でも欠けると白くなるので注意。

### 発光について

元の `PenlightMat` は `_EmissionColor` が 0.749 で、Bloom の threshold 0.9 に届かないため
どれだけ Bloom を足しても光らなかった（これが「のっぺり」の主因）。シェーダが
`ParticlesUnlit` で陰影も付かないので、ベタ塗りのシルエットになっていた。

`Game_Run` では複製した `Settings_Run/PenlightMat_Run.mat` を使い、

- マテリアルの一律な灰色加算を切る（全体に乗って立体感を消していた）
- 代わりに**粒ごとの色**で光らせる

としてある。色を変えたい場合は `Run_PenlightStrip.prefab` の
ParticleSystem → Start Color を触る。

**2色のランダム補間（Random Between Two Colors）にしてはいけない。**
2色の「間」をランダムに取る挙動なので、ピンクと水色のような補色に近い
組み合わせだと中間がほぼ白になり、大半のペンライトが白くなる。
Gradient を **Fixed（段階）** にして **Random Color** で引けば、各粒は必ず
指定した色のどれかになり彩度が残る。現在はピンク／水色／黄緑／橙／紫の5色。

既存 `PenlightMat` は旧 Game シーンでも使われているため、あえて書き換えていない。

## スコア倍率とライバル

### ライバル

本家はこの位置がライバルの体力バーで、削り切るとハートが1つ減り、
ハートが全部なくなるとフィーバーという作り。キラメキビートゲージを
オリジナル要素として最上部に置いたので、ライバル体力はそのすぐ下に出している。

| 流れ | 実装 |
| --- | --- |
| アイテムを取る | `RunScore` が `RunRival.ApplyHit()` を呼ぶ |
| 体力バーを削り切る | ハートが1つ減り、撃破数が1増える。体力バーは満タンに戻る |
| ハートを全部割る | `BeatGaugeController.TriggerFever()` でフィーバー発動 |

フィーバーのきっかけは「ゲージ満タン」と「ハート全割り」の2つあるが、
**状態の持ち主は `BeatGaugeController` 1つ**に保っている。
`TriggerFever()` は発動を依頼するだけ。

`rivalRespawns` をオンにしてあるので、ハートを全部割ったあとライバルは復活する。
撃破数は通算で積み上がり、そのままスコア倍率の加算要素になる。

### 倍率の計算

```
倍率 = 1
     + コンボ数 × comboRate   （上限 comboBonusMax）
     + ゲージの値 × gaugeBonusMax
     + 撃破数 × rivalDefeatBonus
フィーバー中は上の合計に feverMultiplier を掛ける
最後に maxMultiplier で頭を押さえる
```

**加算で積んでから最後に掛ける**方式にしている。乗算で積むと要素が増えるたびに
跳ね上がって調整が効かなくなるため。計算は `ScoreSettings.CalculateMultiplier()`
の1箇所だけなので、式を変えたい場合もそこを直せばよい。

初期値:

| 項目 | 値 | 意味 |
| --- | --- | --- |
| `comboRate` | 0.02 | 100コンボで +2.0（上限に到達） |
| `comboBonusMax` | 2.0 | コンボぶんの上限 |
| `gaugeBonusMax` | 1.0 | ゲージ満タンで +1.0 |
| `rivalDefeatBonus` | 0.5 | 1撃破ごとに +0.5 |
| `feverMultiplier` | 2.0 | フィーバー中は合計を2倍 |
| `maxMultiplier` | 10.0 | 最終的な頭打ち |
| `rivalMaxHp` | 50 | 体力バー1本ぶん |
| `rivalHearts` | 3 | ハートの数 |
| `rivalDamagePerHit` | 1 | アイテム1個のダメージ |

### 注意: 今の譜面ではライバルを倒せない

`StageData_Normal.json` はアイテムが8個しか無く、1個1ダメージなので
体力50のライバルは削り切れない。**撃破ボーナスは曲と譜面が揃ってから効き始める**。
短い譜面で挙動を見たい場合は `rivalMaxHp` を一時的に下げる。

### 既存 GameManager との関係

`GameManager` にも score / combo / rivalHp / isBonusTime があるが、
コンボ倍率しか無く数値がコードに直書きされている。`Game_Run` では
`RunScore` / `RunRival` を使い、`GameManager` は置いていない。
旧 `Game` シーンはこれまで通り `GameManager` で動く。

リザルトへのスコア受け渡しは従来どおり `GameData.finalScore` 経由。
`RunGoalWatcher` が `RunScore` から取って書き込む。

## HUD（仮）

`Canvas/HUD` 配下は**全て仮の見た目**。

日本語を出すためのフォントは `Assets/Fonts/` にある（詳細はそちらのREADME）。
TMP の Fallback に登録してあるので、他のシーンのテキストも日本語が出る。
差し替えるときは `HUD` ごと作り直してよい。
ゲームロジックが知っているのは次の参照だけなので、これを繋ぎ直せば見た目は自由。

| 繋ぎ先 | 必要なもの |
| --- | --- |
| `BeatGaugeView` | `Slider` と、色を変える `Image`（Slider の Fill） |
| `GameManager` | `scoreText` / `comboText` / `playerHpText` / `rivalHpText` の4つ |

`BeatGaugeView` は Slider の値を書くだけの一方通行なので、Slider をやめて
独自の画像演出にする場合も、ゲージ側のロジックは無改修で済む。

## シーン生成スクリプトを触るときの注意

`EditorSceneManager.NewScene` は、シーンを切り替える際に**どこからも参照されていない
アセットをメモリから破棄する**。そのため

```csharp
var settings = AssetDatabase.LoadAssetAtPath<RunSettings>(path);  // ここで読んで
var scene = EditorSceneManager.NewScene(...);                     // ここで破棄され
SetField(runner, "settings", settings);                           // null が入る
```

という順で書くと、**シーンは問題なく生成されるのに参照だけ空**という壊れ方をする。
Play しても「RunSettings が設定されていません」と出てプレイヤーが動かない。

アセットの取得は必ず `NewScene` の**後**に行うこと。`SetField` には破棄済み参照を
弾くガードを入れてあるので、間違えた場合は例外で止まる。

## 自動テストと音

`RunSmokeTest` は `-batchmode` でも Play モードに入るため、そのままだと
BGM がスピーカーから鳴る。作業中に裏で鳴り出すのを防ぐため、
テスト開始時に `AudioListener.volume = 0` にしてある。

## Playを押すとタイトルから始まる

`Editor/PlayFromTitle.cs` が `EditorSceneManager.playModeStartScene` に Title を指定している。
`Game_Run` を編集中に実行しても、難易度選択を通ってから始まる。

単一シーンだけをデバッグしたいときは

```
Yakiniku > 常にタイトルから再生する
```

のチェックを外す。設定は `EditorPrefs`（各自の環境ごと）なので、他のメンバーには影響しない。

---

# 動作確認チェックリスト

## 自動で確認する

```
Yakiniku > Run > Game_Run を自動プレイして確認
```

これを実行すると Play モードに入り、下のチェックリストを機械的に確認して
Console に結果を出す（`Assets/Scripts/Run/Editor/RunSmokeTest.cs`）。
入力はキーボードの代わりに `PlayerRunner` のAPIを直接呼んでいるので、
操作しなくても最後まで走る。4倍速で動かすので数秒で終わる。

CIやコマンドラインから回す場合（Unityを閉じた状態で。全項目OKなら終了コード0）:

```
Unity.exe -batchmode -projectPath <プロジェクトのパス> -executeMethod Yakiniku.Run.EditorTools.RunSmokeTest.Run
```

譜面を差し替えると期待値（ノーツのZ座標）が合わなくなるので、
`RunSmokeTest.cs` の先頭の定数も直すこと。

## 自分の目で確認する

Play して以下を確認する。譜面は `spawnZ` が 20〜170 なので、約17秒で終わる。

- [ ] 自動で前に進む
- [ ] 矢印キー / A・D で5レーン（-2〜2）を移動でき、両端で止まる
- [ ] ↑ / W / Space でジャンプし、着地する
- [ ] 床が途切れずに続く
- [ ] 20m地点あたりから音符（アイテム）が出てくる
- [ ] 音符に触れると消えて、画面上部のゲージが増える
- [ ] 60m地点の障害物に当たるとダメージを受ける（Consoleで確認）
- [ ] 100m地点の低い壁はジャンプで越えられる
- [ ] ゲージが満タンになると点滅する（フィーバー）

## うまく動かないとき

シーンを自動生成しているので参照の割当漏れは起きないはずだが、
手で触って壊した場合は以下を疑う。迷ったら `Yakiniku > Run > Game_Run シーンを生成` を
もう一度実行すれば元に戻る。

| 症状 | 原因 |
| --- | --- |
| `Create` / `Yakiniku` メニューが出てこない | スクリプトがコンパイルできていない。Console を見る |
| プレイヤーが動かない | `PlayerRunner` の `Settings` 未割当（Console にエラーが出る） |
| キー入力が効かない | `PlayerRunner` の `Input Source` 未割当 |
| アイテムをすり抜ける | プレハブの Collider の `Is Trigger` がオフ／`Player` に Rigidbody が無い |
| 障害物に当たらない | `Player` の Collider が `Is Trigger` になっている（オフにする） |
| 障害物が出てこない | `Note Prefabs` の `Type` 文字列が譜面JSONと不一致 |
| 障害物が床に埋まる／浮く | `RunSpawner` の `Spawn Y`、または `Run_*.prefab` の子のY位置 |
| スタート直後に被弾する | `Run_*.prefab` の子（見た目側）の Collider が有効に戻っている |
| モデルが見えない／遠くにある | `PlayerModel` の子の localPosition が 0 になっていない |
| 床が途切れる | `Tile Length` がプレハブの実寸と不一致／`Pivot At Center` の設定ミス |
| ゲージが動かない | `BeatGaugeController` の `Player` 未割当 |
| ゲージUIが動かない | `BeatGaugeView` の `Controller` / `Slider` 未割当 |
| ジャンプで壁を越えられない | `RunSettings.jumpHeight` に対して壁が高い |

---

# 他メンバー向けの接続点

## メンバーC（タッチ・スワイプ入力）

`RunInputSourceBehaviour` を継承して `Enqueue()` を呼ぶだけ。ゲーム側の改修は不要。

```csharp
public sealed class SwipeRunInput : RunInputSourceBehaviour
{
    private void Update()
    {
        // スワイプを検出したら…
        Enqueue(RunInputAction.MoveLeft);
    }
}
```

作ったら `PlayerRunner` の `Input Source` を差し替えるだけで動く。

**可変リフレッシュレートについて**: `PlayerRunner` の移動・ジャンプは全て `FixedUpdate`
（固定間隔）で計算しているので、画面が 60Hz でも 120Hz でも挙動は変わらない設計。
検証時はここが崩れていないかを見てほしい。

## メンバーB（バトル・演出）

- **ゲージ比較**: `BeatGaugeController.Value`（0〜1）を比較すれば勝敗判定できる。
  ライバル側も同じ `BeatGaugeController` を `player` 未設定で置き、
  `AddGauge()` を外から呼べばAIとして動かせる。
- **アイドルごとのステータス差**: `GaugeSettings` を複数作って差し替える。
  移動性能に差を付けたい場合は `RunSettings` も同様。
- **演出のトリガー**: 以下の event を購読する。

```csharp
gaugeController.Gauge.OnChanged += value => { /* ゲージ変動 */ };
gaugeController.Gauge.OnFull    += ()    => { /* 満タン演出 */ };
gaugeController.OnFeverStart    += ()    => { /* フィーバー開始 */ };
gaugeController.OnComboChanged  += combo => { /* コンボ演出 */ };
player.OnItemCollected          += item  => { /* 取得エフェクト */ };
player.OnDamaged                += hp    => { /* 被弾エフェクト */ };
```

---

# 既存実装からの移行

既存の一連の流れ（Title → Select → Game → Result）は動いているので、**壊さずに並行して立ち上げる**。

| 既存スクリプト | 扱い |
| --- | --- |
| `GameManager` | **そのまま使う**。`RunGameBridge` 経由で接続する |
| `GameData` / `SceneController` | **そのまま使う**。シーン遷移と難易度選択は変更なし |
| `CameraController` | **そのまま使う**。`PlayerRunner` でも問題なく追従する |
| `StageData` / 譜面JSON | **そのまま使う**。`JsonNoteProvider` が同じ形式を読む |
| `PlayerController` | `PlayerRunner` に置き換え |
| `StageManager` | `RunSpawner`(ノーツ) + `GroundTiler`(床) に分割して置き換え |

上の手順で作った `Game_Run` シーンが問題なく動いたら、既存 `Game` シーンの
UI（Canvas 配下のテキスト類）と `GameManager` をこちらに持ってきて差し替える。
既存シーンはそのまま残るので、うまくいかなければ戻せる。

## 注意点

- `PlayerRunner` は起動時にプレイヤーを `Z=0` / `groundY` の高さへスナップさせる。
  シーン上の初期配置は無視される。
- 既存 `PlayerController` は `transform.position` 直書きと Rigidbody を併用していたため、
  同じ GameObject に両方を付けたままにすると位置の取り合いになる。必ずどちらかを無効化する。

# 設計上の決めごと

- **レーン番号は中央を0とした -2〜2**。既存の譜面JSON(`NoteData.lane`)と同じ形式。
  レーン↔X座標の変換は `RunSettings.LaneToX()` のみを使い、他の場所で計算しない。
- **プレイヤーの Rigidbody は kinematic**。物理任せにすると前進速度やジャンプ滞空時間が
  環境でブレるが、譜面に合わせて障害物が来るゲームなのでタイミングは決定的である必要がある。
  地面判定もコライダーではなく `groundY` との比較で行う。
- **障害物の生成は先読み方式 + プール**。開始時に全ノーツを生成すると曲が長いほど重くなる。
  `Destroy` の連発は Android で GC によるカクつきの原因になる。
- **asmdef は作っていない**。既存の `GameManager` / `StageData` が Assembly-CSharp にあり、
  asmdef からは参照できないため。将来 EditMode テストを書く場合は、
  それらを含めて asmdef 化する必要がある。
- **新規ファイルは UTF-8(BOM付き)**。既存ファイルは Shift-JIS なので、
  混在させると環境によって日本語コメントが化ける。以後は UTF-8 に揃えたい。
