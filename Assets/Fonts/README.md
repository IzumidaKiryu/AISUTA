# フォント

## NotoSansJP-VF.ttf

UIで日本語を表示するためのフォント。TextMesh Pro の既定フォント
`LiberationSans SDF` には日本語グリフが無く、日本語を入れると豆腐（□）になるため
これを使う。

| | |
| --- | --- |
| 書体名 | Noto Sans JP |
| バージョン | 2.04 |
| ライセンス | SIL Open Font License, Version 1.1 |
| ライセンス全文 | http://scripts.sil.org/OFL |
| 著作権表示 | © 2014-2021 Adobe (http://www.adobe.com/), with Reserved Font Name 'Source'. |

SIL OFL はゲームへの埋め込みと再配布を認めている。
**ただし配布時にはライセンス全文の同梱が必要**なので、リリース前に上記URLから
`OFL.txt` を取得してこのフォルダに置くこと。

### 他のフォントを使う場合の注意

Windows に入っている **Meiryo / Yu Gothic / MS Gothic はビルドに同梱できない**。
Microsoft のライセンスが再配布を認めていないため。日本語フォントを差し替える場合は、
OFL などの再配布可能なライセンスのものを選ぶこと。
（例: Noto Sans JP、BIZ UDGothic のOFL版、M PLUS など）

## TMP Font Asset

`NotoSansJP SDF.asset` は `RunSceneBuilder` が自動生成する。
手で作り直す必要はなく、消してしまっても

```
Yakiniku > Run > Game_Run シーンを生成
```

で再生成される。

**Atlas Population Mode は Dynamic。** 日本語は常用漢字だけでも数千字あるので、
全字をアトラスに焼くと巨大になる。実際に画面に出た字だけを実行時にアトラスへ
足していく方式にしてある。

## 既存テキストへの反映

このフォントは TMP Settings の **Fallback Font Assets** に登録してある。
そのため Title / Select / Result など他のシーンのテキストが
`LiberationSans SDF` のままでも、日本語の字だけ自動でこちらから引かれる。
テキストを1つずつ差し替える必要はない。
