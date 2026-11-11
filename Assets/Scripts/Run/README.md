# ランゲーム（Game_Run）

本家「アイ☆スタ IDOL STARS」のアイドルバトルに寄せたランゲーム。
`Assets/Scripts/Run/` 以下が本体で、シーン・設定・プレハブは全てコードから生成する。

```
Title → Select（難易度選択） → Game_Run → Result
                ↑                            │
                └──── ステージ選択 ←─── 次へ ─┤
                       もう一度 ──────────────┘
```

遷移のたびに ☆ を円形に並べたローディングが挟まる（本家と同じ）。

---

# まず動かす

```
Yakiniku > Run > 全部生成
```

で Game_Run / Select / Result の3シーンと、設定アセット・プレハブ・材質が全部できる。
**何度実行してもよい**（壊したときもこれで元に戻る）。

| メニュー | 内容 |
| --- | --- |
| `Yakiniku > Run > 全部生成` | 3シーンと共通アセットをまとめて生成 |
| `Yakiniku > Run > 譜面を生成` | Easy / Normal / Hard / Universe の譜面JSONを生成 |
| `Yakiniku > Run > Game_Run を自動プレイして確認` | ノーミスで通しプレイして29項目を検査 |
| `Yakiniku > Run > 画面を撮る` | VS・通常・フィーバー・ENCORE・スーパー・難易度選択・リザルトを撮影 |
| `Yakiniku > 常にタイトルから再生する` | どのシーンを開いていても Play でタイトルから始める（ON/OFF） |

コマンドライン（Unityを閉じた状態で）:

```
Unity.exe -batchmode -quit -projectPath <パス> -executeMethod Yakiniku.Run.EditorTools.RunSceneBuilder.BuildEverything
```

---

# ゲームの流れ

| 場面 | 内容 | 担当 |
| --- | --- | --- |
| 開始 | 自キャラ vs ライバルのカットイン → **Let's FIGHT!** → 走り出しと BGM を同時に開始 | `RunIntroSequence` |
| 走行 | 5レーン。ノーツ(♪)を取るとスコアとゲージ、障害物で被弾、低い壁はジャンプ | `PlayerRunner` 他 |
| 攻撃 | ♪を取ると、取った位置から奥のライバルへ**白い球が弧を描いて飛び**、着弾で体力が減る | `BeatOrbLauncher` |
| 撃破 | 体力バーを削り切るとハートが1つ減る。**5つ全部割ると ENCORE TIME** | `RunRival` |
| 終了 | **FINISH** → ☆ローディング → リザルト | `RunFinishSequence` |

勝敗は「ライバルを撃破して、かつ生き残ったら WIN」。

## フィーバーは2系統

| 状態 | 入り方 | 長さ | ステージの色 | 倍率 |
| --- | --- | --- | --- | --- |
| フィーバー | キラメキビートゲージが満タン | 8秒 | ピンク | ×1.5 |
| **ENCORE TIME** | ライバルのハートを全部割る | **曲の終わりまで** | **金** | ×2.0 |
| **スーパーフィーバー** | **ENCORE とフィーバーが重なる** | 12秒 | **虹** | ×4.0 |

状態は `BeatGaugeController` の `IsFever` と `IsEncore` の2つで持ち、
**スーパー＝両方ON** と定義している。「ENCORE 中にゲージを溜め切った」でも
「フィーバー中にライバルを撃破した」でも同じくスーパーになる。どちらが先かを覚える必要がない。

## 譜面に合わせた自動調整（RunBalance）

数値を固定で持つと、譜面が変わるたび（曲の差し替え・難易度違い）に撃破の地点やフィーバーの回数がずれる。
そこで「どうなってほしいか」を設定に書き、実際の数値は譜面のノーツ数から逆算する。

| 対象 | 狙い | 設定 |
| --- | --- | --- |
| ライバルの体力 | フルコンボで曲の **N割** 地点で撃破（本家: Hard 以上は9割） | `DifficultySettings.rivalDefeatAt` |
| ゲージの溜まり方 | フルコンボで1曲に **3回** フィーバー | `GaugeSettings.feversPerSong` |

実測（自動プレイ・ノーミス）:

| 難易度 | 撃破の狙い → 実際 | フィーバー | スーパー |
| --- | --- | --- | --- |
| Easy | 75% → 76% | 3回 | 発動 |
| Normal | 85% → 86% | 3回 | 発動 |
| Hard | 90% → 90% | 3回 | 発動 |
| Universe | 90% → 91% | 3回 | 発動 |

3回目のフィーバーがちょうど ENCORE に重なるので、**フルコンボすれば終盤でスーパーフィーバーになる**。

## スコア倍率

**加算で積んでから、最後にフィーバー系を掛ける**。乗算で積むと要素が増えるたびに跳ね上がって調整が効かなくなるため。

```
倍率 = 1 + コンボ×0.02（上限+2.0） + ゲージ値×1.0 + 割ったハート数×0.5
       そこに スーパー×4.0 ＞ ENCORE×2.0 ＞ フィーバー×1.5 のうち一番強いものを掛ける
       最後に×10で頭打ち
```

計算は `ScoreSettings.CalculateMultiplier()` の1か所だけ。数値は全て `Settings_Run/ScoreSettings.asset`。

---

# 見た目

## ステージは暗くする

本家は暗いライブ会場。明るい空のままだと **HUD の白文字が読めず、ペンライトもレーンの光も映えなかった**。
カメラの背景を濃紺の単色にし、遠景は同じ色のフォグに溶かしている。
キャラの環境光は空(`RenderSettings.skybox`)から取るので、背景を消しても暗くならない。

## 色で演出する（光度は上げない）

`StageTheme` が状態（通常／フィーバー／ENCORE／スーパー）に合わせて、
**レーン・ペンライト・光の色味をまとめて切り替える**。明るさは全状態で同じにして、色相だけを変える。

フィーバーで Bloom の強さを上げる方式も試したが、上げた瞬間に画面が白飛びして譜面が見えなくなった
（スーパーフィーバーで画面がほぼ真っ白になった）。本家も ENCORE で金色基調になるだけで眩しくはならない。

## レーン

床のアセットの上に、暗い路面5本と光る境界線6本を1枚重ねている（`Run_LaneTrack`）。
床タイルの子として生成されるので、床と一緒にループする。色は `StageTheme` が持つ共有材質で決まる。

## ペンライト

床の両サイドに、床タイルの子として生成される（`Run_PenlightStrip`）。

- **曲に合わせて左右に揺れる**（2拍で1往復）。BPM は譜面JSONの `bpm` から取る（`PenlightSway`）
- 色は通常が水色主体、フィーバーがピンク、ENCORE が橙〜金、スーパーが虹（本家どおり）

### ペンライトの色の落とし穴

**粒ごとの色は8bit(`Color32`)で、1.0を超える値は切り捨てられる。**
HDRで光らせたいなら明るさは材質側(`_BaseColor`)に持たせ、粒の色は色相だけを担当する。

**材質側の倍率は全成分に掛かるので、上げすぎると白くなる。**
2.6倍にしたらピンク `(1, 0.4, 0.8)` が `(2.6, 1.04, 2.08)` になり、画面上はほぼ白だった。
今は1.15倍に抑え、色そのものを「一番強い成分が1、残りは低め」の鮮やかな色にしてある。

**2色のランダム補間（Random Between Two Colors）は使わない。**
2色の「間」を取るので、補色に近い2色だと中間が白くなる。粒の色は `PenlightSway` が
パレットから粒ごとに1色を選ぶ方式にしている。

## Bloom

プロファイルは `Settings_Run/GameRunVolume.asset`（共有の `Global Volume Profile 1` は Title や旧 Game で使うので書き換えない）。

- **threshold 1.0**。元は0.2で、輝度0.2を超えるもの（床・空・キャラ）が全部光り、画面全体が白く飛んでいた
- 閾値は最大チャンネルではなく**輝度**（`0.2126R + 0.7152G + 0.0722B`）で判定される。緑の重みが大きいので、同じ数値でもピンクや紫は光りにくい
- カメラの `Render Post Processing` をONにしないと一切効かない

光のにじみは主にレーンの境界線（HDR）と白い球が担う。

## 見た目を変えたら絵で確認する

数値だけで判断すると外す。`Yakiniku > Run > 画面を撮る` で8枚撮れる。
バッチモードでは `ScreenCapture.CaptureScreenshot` が何も出力しないので、カメラを RenderTexture へ直接描いている。
Overlay の Canvas は撮る瞬間だけカメラ描画に切り替えるので HUD も写る。

---

# HUD（仮）

| 位置 | 内容 |
| --- | --- |
| 最上部の紫の枠 | 上段にキラメキビートゲージ、下段にライバルの名前・体力バー・ハート |
| 左上 | COMBO（大）／倍率／SCORE／LIFE（ハート5つ） |
| 中央 | FEVER! / ENCORE TIME / SUPER FEVER!! のバナー（2秒だけ、暗い帯の上に出す） |

**全て仮の見た目**。`Canvas/HUD` 配下は丸ごと作り直してよい。ロジックが知っているのは
`BeatGaugeView`（Slider と Fill）と `RunHudView`（各テキストとバー）の参照だけ。

ハートは今は文字（♥♡）。画像にするなら `RunHudView.ApplyHearts` / `ApplyLife` を書き換える。

---

# 難易度選択・リザルト

どちらも本家に寄せた仮の見た目。生成されるのはただの Image / TextMeshProUGUI なので、
**Inspector でスプライトを入れれば差し替わる**。立ち絵は「立ち絵（仮）」と書いた色板の Image。

## 難易度

| 難易度 | 譜面 | 解放 |
| --- | --- | --- |
| Easy / Normal / Hard | 最初から | |
| Universe | 最初はロック | **Hard を SS 以上で WIN** すると解放（本家どおり） |

難易度ごとの設定は `Resources/DifficultySettings.asset` に1か所でまとまっている。
難易度を足すときはここに1行足すだけで、選択画面にも並ぶ。

## ランク

**ノーミスで走り切ったときのスコア（`perfectScore`）に対する割合**で決める。
譜面の長さや難易度でスコアの桁が変わっても、同じ基準でランクが付く。

| ランク | 割合 |
| --- | --- |
| SS+ | 95% |
| SS | 85% |
| S | 70% |
| A | 55% |
| B | 40% |
| C | それ未満 |

`perfectScore` は自動プレイ（ノーミス）で測った値。**譜面やスコア計算を変えたら、
自動プレイテストが出す `[CALIBRATE]` 行を見て `RunSceneBuilder.CreateDifficultySettings` の値を直す**。

バッジ画像は `Resources/RankSettings.asset` の `badge` に入れれば差し替わる（空なら色付きの丸と文字）。

## 保存

ハイスコア・最高ランク・クリア済みは `RunProgress`（PlayerPrefs）。Android でもそのまま使える。
ランクは WIN したときだけ記録する（負けたのにランクが付くと解放条件がおかしくなるため）。

## リザルト単体で開いたとき

`RunSession.LastResult` が無いので見本の結果が出る。見た目の調整のたびにゲームを1回遊ばなくて済む。

---

# 効果音（仮）

素材が無いので、**コードで合成した仮の音**が鳴る（`SeSynth`）。
本番の音源ができたら `Resources/SeSettings.asset` の該当行に AudioClip を入れるだけで差し替わる。

| 音 | 場面 |
| --- | --- |
| ItemGet / OrbHit | ♪取得 / 白い球がライバルに当たった |
| Damage / Jump | 被弾 / ジャンプ |
| HeartBreak | ライバルのハートを割った |
| Fever / Encore / SuperFever | 各状態の開始 |
| Versus / Fight / Finish | 開始演出・終了 |
| UiTap / UiDecide | ボタン |
| Win / Lose | リザルト |

鳴らすのは `RunSe.Play(SeId.xxx)`。ゲーム中のイベントへの紐付けは `RunSeHooks` に集約してあり、
ロジック側は音を一切知らない。

---

# 譜面

`Yakiniku > Run > 譜面を生成`。曲がまだ制作中で長さもBPMも変わるため、手書きではなくパラメータから組み立てる。
シード固定なので同じ設定なら同じ譜面になる。

**曲を差し替えたら `ChartGenerator` の `SongSeconds` と `Bpm` を直して作り直す。**
`ForwardSpeed` は `RunSettings.forwardSpeed` と必ず合わせる。

| 難易度 | 刻み | ノーツ | アイテム | 障害物 | 低い壁 |
| --- | --- | --- | --- | --- | --- |
| Easy | 2拍ごと | 156 | 139 | 12 | 5 |
| Normal | 4分 | 313 | 279 | 26 | 8 |
| Hard | 8分 | 612 | 561 | 37 | 14 |
| Universe | 8分 | 634 | 553 | 59 | 22 |

## 置き方

1. 先に**進路**（どのレーンを走るか）を決める。一定間隔で隣のレーンへ動かし、端では折り返す
2. アイテムは進路の上に置く。普通に追いかければ全部取れる
3. 障害物は進路の**隣**に置く。ただし**前後3mの間に進路が通るレーンには置かない**
4. 低い壁は全レーンを塞ぐ。同じ位置にアイテムは置かない

3 の制限が無いと、進路が切り替わった直後に「さっきまで居たレーン」へ障害物が来て、避ける猶予が1m程度しか無い。
Universe ではこれで自動プレイヤーが死んだ（人間でも理不尽）。距離で決めているのは、
ステップ数で決めると刻みの粗い Easy で空けすぎて障害物がほぼ消えたため。

障害物の間隔は進路切り替えの間隔の倍数にしないこと。倍数だと毎回ちょうど切り替え地点に来て置けなくなる。

---

# 自動プレイテスト

```
Yakiniku > Run > Game_Run を自動プレイして確認
```

譜面を読んで追従する自動プレイヤーがノーミスで通しプレイし、29項目を検査する。
コマンドラインでは環境変数 `YAKINIKU_DIFFICULTY=Easy|Normal|Hard|Universe` で難易度を変えられる。

- バッチモードでも音は鳴るので、テスト中は消音している
- テストの結果で手元のハイスコアや解放状態が書き換わらないよう、**開始前に退避して終了後に戻す**
- 出力の `[CALIBRATE]` 行がノーミス時のスコア。ランク基準の校正に使う

検査項目の例: 演出中は走り出さない／LIFE 5／白い球が全部届く／狙った地点で撃破／フィーバー2〜4回／
ENCORE で金色／スーパー発動／ノーミスで SS+／ペンライトが揺れる／画面の文字が全部フォントにある 等。

---

# シーン生成スクリプトを触るときの注意

## アセットは NewScene の後に取る

`EditorSceneManager.NewScene` はシーン切り替えの際に**どこからも参照されていないアセットをメモリから破棄する**。
先に読んだ参照を使うと、シーンは生成できるのに参照だけ空、という壊れ方をする（Play するとプレイヤーが動かない）。
`BuilderUtil.SetField` は破棄済みの参照を渡すと例外を出すようにしてある。

## 豆腐（□）

`BuilderUtil.CreateText` は生成時にフォントに無い字が無いか検査し、あれば `[BuilderUtil] フォントに無い字があります` を出す。
記号は日本語フォントでも入っていないものが多い（実際 `↩` `✦` が豆腐になった）。

検査には `HasCharacters(text, out missing, true, true)` を使う。第4引数(tryAddCharacter)が false だと
**アトラスに読み込み済みの字しか見ない**ので、まだ一度も描画していない字を「無い」と誤判定する。

## 日本語フォント

`Assets/Fonts/` の Noto Sans JP（SIL OFL 1.1）。詳細はそちらの README。
TMP の Fallback に登録してあるので、他シーンのテキストも日本語が出る。

---

# ファイル一覧

## ゲーム

| ファイル | 役割 |
| --- | --- |
| `RunSettings.cs` | レーン・速度・ジャンプ・LIFE。**レーン↔X座標の変換はここが唯一の定義** |
| `PlayerRunner.cs` | 自動前進・レーン切替・ジャンプ・被弾 |
| `Inputs/` | 入力元（キーボード・デバッグボタン）。差し替え可能 |
| `RunSpawner.cs` / `JsonNoteProvider.cs` | 譜面の読み込みと先読み生成（プール） |
| `GroundTiler.cs` | 床のループ。両サイド（ペンライト）と中央（レーン）も一緒に生成 |
| `BeatItem.cs` / `Obstacle.cs` | ノーツと障害物 |
| `BeatGauge.cs` / `BeatGaugeController.cs` / `GaugeSettings.cs` | ゲージとフィーバー／ENCORE の状態 |
| `RunRival.cs` | ライバルの体力・ハート・撃破 |
| `RivalView.cs` | 奥に立つライバルの姿 |
| `BeatOrbLauncher.cs` | 白い球。着弾でライバルの体力を削る |
| `RunScore.cs` / `ScoreSettings.cs` | スコアと倍率 |
| `RunBalance.cs` | 譜面に合わせてライバル体力とゲージを決める |
| `RunGoalWatcher.cs` | 終了判定（譜面基準／曲基準を切替） |
| `StageTheme.cs` | レーン・ペンライト・光の色 |
| `LaneTrack.cs` / `PenlightStrip.cs` / `PenlightSway.cs` | レーン、ペンライト1列、揺れ |
| `RunHudView.cs` / `BeatGaugeView.cs` | HUD |
| `Sequences/` | 開始演出・終了演出 |
| `Audio/` | 効果音 |

## 画面の流れ・難易度・保存

| ファイル | 役割 |
| --- | --- |
| `Flow/RunFlow.cs` | 遷移の入口（シーン名はここだけ） |
| `Flow/RunLoadingOverlay.cs` | ☆のローディング。シーンをまたいで残る |
| `Flow/DifficultySettings.cs` | 難易度ごとの設定 |
| `Flow/RankSettings.cs` | ランクの基準とバッジ |
| `Flow/RunSession.cs` | シーン間で持ち回る結果 |
| `Flow/RunProgress.cs` | ハイスコア・ランク・解放状態の保存 |
| `UI/SelectView.cs` / `UI/ResultView.cs` / `UI/RankBadgeView.cs` | 難易度選択・リザルト |

## エディタ

| ファイル | 役割 |
| --- | --- |
| `Editor/RunSceneBuilder.cs` | Game_Run と共通アセット |
| `Editor/SelectSceneBuilder.cs` / `Editor/ResultSceneBuilder.cs` | 難易度選択・リザルト |
| `Editor/BuilderUtil.cs` | 生成の共通処理（UI部品・参照の書き込み・豆腐検査） |
| `Editor/ChartGenerator.cs` | 譜面 |
| `Editor/RunSmokeTest.cs` | 自動プレイテスト |
| `Editor/RunScreenshot.cs` | 画面の撮影 |
| `Editor/PlayFromTitle.cs` | Play でタイトルから始める |

---

# 残っていること

- **縦画面（Android）**: 本家は縦持ち。今は横で組んであるが、難易度選択・リザルトは中央寄せの縦長レイアウトなので
  縦(1080x1920)でもほぼそのまま収まる。ゲーム画面の HUD とカメラは縦用に組み直しが要る
- **JUMP ボタン**: 本家の画面下の JUMP。`RunInputSourceBehaviour` を継承して `Enqueue(RunInputAction.Jump)` するだけ
- **立ち絵・バッジ・SE の本番素材**: 全て差し替えるだけで済む形にしてある
- **曲が完成したら**: `ChartGenerator` の `SongSeconds` / `Bpm` を直して譜面を作り直し、ランク基準を校正する。
  終了判定を曲基準にするなら `GoalWatcher` の `Condition` を `BgmLength` に

---

# 設計上の決めごと

- **レーン番号は中央を0とした -2〜2**。レーン↔X座標の変換は `RunSettings.LaneToX()` のみを使う
- **プレイヤーの Rigidbody は kinematic**。譜面に合わせて障害物が来るゲームなのでタイミングは決定的である必要がある
- **移動は FixedUpdate**。画面が 60Hz でも 120Hz でも挙動は変わらない
- **障害物の生成は先読み + プール**。`Destroy` の連発は Android で GC によるカクつきの原因になる
- **毎フレーム呼ばれる処理でラムダを作らない**。同じく GC の原因になる
- **見た目のアセットは子として抱えるだけ**。当たり判定はスケール1の親に持たせ、モデルを差し替えても壊れないようにする
- **新規ファイルは UTF-8(BOM付き)**。既存ファイルは Shift-JIS
- **旧 Game シーンは残してある**。`SceneController.StartGame()` の1行を `"Game"` に戻せば切り替わるが、
  倒れた障害物・巨大な当たり判定・レーン座標の不一致はそちらに残ったまま
