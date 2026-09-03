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
| `RunGameBridge.cs` | 既存 `GameManager` への繋ぎ役（スコア・HP表示） |

---

# 動作確認用シーンの作り方

まずは**既存シーンを触らず、新しい空シーンでグレーボックス（白い箱だけ）で動かす**。
見た目を後回しにすると、どこが壊れているか切り分けやすい。

所要時間は15分くらい。上から順にやれば動く。

## 前提の確認

始める前に、Project ウィンドウに **`Assets/Scripts/Run/` が存在すること**を確認する。
無ければこのブランチの内容がまだ自分の作業コピーに入っていないので、
先に `git pull` なりマージなりで取り込むこと。取り込めていないと
`Create > Yakiniku > ...` メニューも `Player Runner` コンポーネントも出てこない。

なお `Create > Yakiniku > Run > Run Settings` は**右クリックメニューを辿る順番**であって、
`Yakiniku` というフォルダを作るという意味ではないので注意。

## 0. シーンを作る

1. `File > New Scene` → `Basic (URP)` を選んで `Create`
2. `Ctrl+S` で `Assets/Scenes/Game_Run.unity` として保存

## 1. 設定アセットを2つ作る

1. Project ウィンドウで `Assets` を右クリック → `Create > Folder` → 名前を `Settings_Run` にする
2. `Settings_Run` を開いた状態で右クリック → メニューを `Create` → `Yakiniku` → `Run` → `Run Settings`
   と辿る（フォルダを作るのではなく、メニューの階層をたどる）→ 名前は `RunSettings`
3. 同じ場所で右クリック → `Create` → `Yakiniku` → `Run` → `Gauge Settings` → 名前は `GaugeSettings`

`Create` メニューに `Yakiniku` が出てこない場合は、スクリプトが取り込めていないか
コンパイルが通っていない。Console にエラーが出ていないか確認する。

`RunSettings` は初期値のままでよい（`laneCount = 5`、`laneWidth = 2`、`forwardSpeed = 10`、`groundY = 1`）。

## 2. 床

1. Hierarchy で右クリック → `3D Object > Plane` → 名前を `GroundTile` に変更
2. Inspector の Transform を設定
   - Position `(0, 0, 0)`
   - Scale `(2, 1, 5)` … Plane は Scale 1 で 10m 四方なので、これで **幅20m × 奥行50m** になる
3. Project ウィンドウに `Prefabs` フォルダを作り、`GroundTile` をドラッグしてプレハブ化
4. Hierarchy から `GroundTile` を削除（プレハブ化した実体はもう不要）
5. Hierarchy で右クリック → `Create Empty` → 名前を `Ground` に変更
6. `Ground` に `Add Component` → `Ground Tiler` を追加して設定
   - `Ground Prefab` … 作った `GroundTile` プレハブ
   - `Tile Length` … **50**（Scale Z=5 × 10m。ここがプレハブの実寸と違うと床が途切れる）
   - `Pivot At Center` … **チェックを入れる**（Unity の Plane は原点が中心にあるため）
   - `Ground Y` … 0
   - `Player` は後で割り当てる

## 3. プレイヤー

1. Hierarchy で右クリック → `3D Object > Capsule` → 名前を `Player` に変更
   - Capsule は高さ2mで原点が中心なので、Position `(0, 1, 0)` で床にちょうど立つ
2. `Add Component` → `Rigidbody`（設定はスクリプトが起動時に自動で整えるので触らなくてよい）
3. `Add Component` → `Player Runner`
   - `Settings` … `RunSettings` をドラッグ
   - `Input Source` は次の手順で割り当てる
4. Capsule Collider は最初から付いている。**`Is Trigger` はオフのまま**にする

これで `Ground` の `Player` 欄に `Player` をドラッグしておく。

## 4. 入力

1. Hierarchy で右クリック → `Create Empty` → 名前を `InputSource` に変更
2. `Add Component` → `Keyboard Run Input`
3. `Player` の `Player Runner` の `Input Source` 欄に、`InputSource` をドラッグ

**ここまでで一度 Play してみる。** 前に走り出して、矢印キー / A・D でレーン移動、
↑ / W / Space でジャンプできれば土台は完成。

## 5. 障害物とアイテムのプレハブ

3つ作る。いずれも作ったら `Prefabs` フォルダにドラッグしてプレハブ化し、Hierarchy からは削除する。

| 名前 | 元 | Scale | 付けるコンポーネント |
| --- | --- | --- | --- |
| `Obstacle_Block` | `3D Object > Cube` | `(1.5, 1.5, 1)` | `Obstacle` |
| `Obstacle_Jump` | `3D Object > Cube` | `(12, 0.8, 1)` | `Obstacle` |
| `Item_Beat` | `3D Object > Sphere` | `(0.8, 0.8, 0.8)` | `BeatItem` |

`Obstacle_Jump` は全レーンを塞ぐ低い壁。ジャンプで飛び越える想定。

Collider の `Is Trigger` はコンポーネントを付けた時点で自動的にオンになる。
（もしオフだったら手動でオンにする。ここがオフだと当たり判定が飛ばない）

## 6. 障害物の生成

1. Hierarchy で右クリック → `Create Empty` → 名前を `Spawner` に変更
2. `Add Component` → `Json Note Provider`
   - `Resources File Name` … `StageData_Normal`
   - `Use Selected Difficulty` … チェックを入れたままでよい（難易度選択から来た場合はそちらが優先される）
3. `Add Component` → `Run Spawner`
   - `Settings` … `RunSettings`
   - `Player` … `Player`
   - `Note Provider` … 空のままでよい（同じ GameObject から自動で探す）
   - `Note Prefabs` の `Size` に **3** を入れて、以下のように設定する

| 要素 | Type | Prefab | Spawn Y | Force Center Lane |
| --- | --- | --- | --- | --- |
| Element 0 | `Item` | `Item_Beat` | 1 | オフ |
| Element 1 | `Obstacle` | `Obstacle_Block` | 0.75 | オフ |
| Element 2 | `JumpObstacle` | `Obstacle_Jump` | 0.4 | **オン** |

`Type` の文字列は譜面JSONの `type` と**完全一致**させる（大文字小文字も区別される）。
`Spawn Y` は床に接地して見える高さ（プレハブの高さの半分）。

## 7. ゲージ

1. Hierarchy で右クリック → `Create Empty` → 名前を `GaugeController` に変更
2. `Add Component` → `Beat Gauge Controller`
   - `Settings` … `GaugeSettings`
   - `Player` … `Player`
3. Hierarchy で右クリック → `UI > Slider`（Canvas と EventSystem が自動で作られる）
4. 作られた `Slider` を選び、`Add Component` → `Beat Gauge View`
   - `Controller` … `GaugeController`
   - `Slider` … `Slider` 自身をドラッグ
   - `Fill Image` … Hierarchy の `Slider > Fill Area > Fill` をドラッグ
5. Slider の見た目を整える（任意）
   - `Slider > Handle Slide Area` は不要なのでチェックを外して非表示にする
   - Rect Transform で画面上部などに配置する

## 8. カメラ

1. Hierarchy の `Main Camera` を選ぶ
2. Transform を Position `(0, 6, -9)`、Rotation `(20, 0, 0)` にする
3. `Add Component` → `Camera Controller`（既存スクリプト）
   - `Player` … `Player` をドラッグ

## 9. 既存UIとの接続（任意）

既存の `GameManager` を置いたシーンなら、`Create Empty` に `Run Game Bridge` を付けて
`Player` を割り当てると、スコア・HP のテキスト表示が動く。
`GameManager` が無いシーンでは付けなくてよい（付けても何も起きないだけでエラーにはならない）。

---

# 動作確認チェックリスト

Play して以下を確認する。譜面は `spawnZ` が 20〜170 なので、約17秒で終わる。

- [ ] 自動で前に進む
- [ ] 矢印キー / A・D で5レーン（-2〜2）を移動でき、両端で止まる
- [ ] ↑ / W / Space でジャンプし、着地する
- [ ] 床が途切れずに続く
- [ ] 20m地点あたりから球（アイテム）が出てくる
- [ ] 球に触れると消えて、ゲージのスライダーが増える
- [ ] 60m地点の箱（障害物）に当たるとダメージを受ける（Consoleで確認）
- [ ] 100m地点の低い壁はジャンプで越えられる
- [ ] ゲージが満タンになると点滅する（フィーバー）

## うまく動かないとき

| 症状 | 原因 |
| --- | --- |
| プレイヤーが動かない | `PlayerRunner` の `Settings` 未割当（Console にエラーが出る） |
| キー入力が効かない | `PlayerRunner` の `Input Source` 未割当 |
| アイテムをすり抜ける | プレハブの Collider の `Is Trigger` がオフ／`Player` に Rigidbody が無い |
| 障害物に当たらない | `Player` の Collider が `Is Trigger` になっている（オフにする） |
| 障害物が出てこない | `Note Prefabs` の `Type` 文字列が譜面JSONと不一致 |
| 障害物が床に埋まる／浮く | `Spawn Y` がプレハブの高さの半分になっていない |
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
