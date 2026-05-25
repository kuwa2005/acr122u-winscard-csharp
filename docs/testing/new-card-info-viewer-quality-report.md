# 新規カード情報表示プログラム 品質チェックレポート

作成日: 2026-05-25

## 判定

**要修正**。

Release ビルドは成功したが、設計書で想定された新規カード情報表示プログラムとしては未達が残る。現時点で品質チェック対象として確認できた実装は、既存 `CardReader_TestConsole` に追加されたリーダー概要、ファームウェア取得、カードサマリー、重複表示抑制、トレース機能であり、設計書にある新規 console project `Acr122uCardInspector`、`--help` / `--version` / `--json` / `--markdown` などの CLI、JSON / Markdown 出力は確認できなかった。

## 対象

- リポジトリ: `kuwa2005/acr122u-winscard-csharp`
- 品質チェック Issue: https://github.com/kuwa2005/acr122u-winscard-csharp/issues/14
- ローカルパス: `D:\00_project\ACR122\acr122u-winscard-csharp`
- ブランチ: `master`
- 仕様書:
  - `D:\00_project\ACR122\API-ACR122U-2.04.md`
  - `D:\00_project\ACR122\TSP-ACR122U-3.05.md`
- 設計/引き継ぎ:
  - `docs/design/new-card-info-viewer-design.md`
  - `docs/design/feature-inventory-handoff.md`
- 実装確認対象:
  - `CardReader_TestConsole`
  - `NFC_CardReader`

## 実施した確認

| 項目 | 結果 | 確認内容 |
|---|---|---|
| Release ビルド | 合格 | `dotnet build .\NFC_CardReader.sln --configuration Release` が警告 0 / エラー 0 で成功 |
| 生成物の Git 管理対象外 | 合格 | `bin/`、`obj/`、`logs/` は `.gitignore` により ignored |
| ACR122U リーダー概要 | 一部合格 | 実機接続状態で `ACS ACR122 0` と `ACR122U215` を表示 |
| ファームウェア取得 | 合格 | `FF 00 48 00 00` の応答から `ACR122U215` を取得 |
| カードなし状態 | 一部合格 | 起動時状態としてカード検出 `False`、エラー `ACR122U_Error_NoError` を表示 |
| トレースログ | 一部合格 | `--trace` で `logs/trace-YYYYMMDD-HHMMSS.log` を作成し、起動、reader 選択、firmware、状態を記録 |
| `--help` | 不合格 | ヘルプを表示せず通常起動した |
| `--version` | 不合格 | バージョンを表示せず通常起動した |
| `--json <path>` | 不合格 | JSON 出力を作成せず通常起動した |
| 不正オプション | 不合格 | 不正オプションをエラー扱いせず通常起動した |
| 日本語表示 | 要確認 | 通常コンソール向けの文言は概ね日本語化されているが、非対話ジョブ実行では文字化けして出力された |
| 非対話実行 | 不合格 | `Console.ReadKey` 待機により、ジョブ実行では未処理例外で `EXIT:-532462766` となった |

## ビルド品質

実行コマンド:

```powershell
dotnet build .\NFC_CardReader.sln --configuration Release
```

結果:

- ビルド成功
- 警告: 0
- エラー: 0
- 出力:
  - `NFC_CardReader\bin\Release\NFC_CardReader.dll`
  - `CardReader_TestConsole\bin\Release\CardReader_TestConsole.exe`

生成物確認:

- `CardReader_TestConsole/bin/`
- `CardReader_TestConsole/obj/`
- `NFC_CardReader/bin/`
- `NFC_CardReader/obj/`
- `logs/`

上記は `git status --ignored` で ignored として確認した。

## CLI / 操作品質

確認コマンド:

```powershell
.\CardReader_TestConsole\bin\Release\CardReader_TestConsole.exe --help
.\CardReader_TestConsole\bin\Release\CardReader_TestConsole.exe --version
.\CardReader_TestConsole\bin\Release\CardReader_TestConsole.exe --json .\logs\qc-json-test.json
.\CardReader_TestConsole\bin\Release\CardReader_TestConsole.exe --trace
.\CardReader_TestConsole\bin\Release\CardReader_TestConsole.exe --does-not-exist
```

確認結果:

- `--trace` 以外の CLI 引数は実装されていない、または無視されている。
- `--help` / `--version` はユーザーが期待する短い情報表示にならず、リーダー接続処理まで進む。
- `--json` は指定ファイルを作成しない。
- 不正オプションはエラーにならない。
- 非対話環境では `Console.ReadKey(true)` で未処理例外が発生するため、自動テストや CI で確認しにくい。

## 設計仕様適合

| 設計項目 | 結果 | コメント |
|---|---|---|
| 新規 console project `Acr122uCardInspector` | 不合格 | ソリューションには `NFC_CardReader` と `CardReader_TestConsole` のみ存在 |
| リーダー一覧 / 選択 | 要修正 | `ACR122UManager.GetACR122UReaders().FirstOrDefault()` で先頭を自動選択するのみ。一覧表示と明示選択がない |
| リーダー概要 | 一部合格 | reader name、firmware、TSP ベースの静的仕様を表示 |
| ファームウェア取得 | 合格 | `FF 00 48 00 00` で取得できた |
| PICC operating parameter | 要修正 | 起動時に `AllOn` を設定しており、設計の「初期実装では設定変更しない」に反する |
| カード検出 / 取り外し状態機械 | 一部合格 | 重複抑制用のゲートはあるが、設計書の状態機械名と安定化フローとしては未分離 |
| ATR / UID / ATS 表示 | 未確認 | 実カード未使用のため実機結果は未確認。コード上は public APDU を試行する実装あり |
| カード分類 | 一部合格 | ATR historical bytes と Card Name の簡易辞書はある |
| 読み取り制約 / 安全注意 | 要修正 | カードサマリーは公開情報のみだが、通常起動で reader 設定変更を行う点が安全境界に反する |
| JSON 出力 | 不合格 | 未実装 |
| Markdown 出力 | 不合格 | 未実装 |
| ログ出力 | 一部合格 | `--trace` で `logs/` 配下にトレースを作成。APDU trace / 機密値マスク方針は限定的 |

## 実機不要テスト

| 項目 | 結果 | コメント |
|---|---|---|
| リーダー未接続時 | 未確認 | この環境では ACR122U が接続済みだったため未実測。コード上は reader 未検出時のユーザー向けメッセージが不足している可能性が高い |
| カード未接続時 | 一部合格 | ACR122U 接続、カードなしで起動時状態を表示 |
| 不正オプション | 不合格 | エラーにならず通常起動 |
| JSON 出力構造 | 不合格 | JSON ファイルが作成されない |
| トレースログ作成先 | 合格 | `logs/trace-YYYYMMDD-HHMMSS.log` |
| トレースログ Git 除外 | 合格 | `logs/` と `*.log` は `.gitignore` 対象 |

## 実機必要テスト項目

次は実カードを使った確認が必要。

- ACR122U を接続し、reader 一覧と選択結果が意図通り表示されること。
- MIFARE Classic 1K / 4K / Mini の ATR、UID、カード分類、キー未指定時の未取得理由が表示されること。
- MIFARE Ultralight / NTAG 系で UID、ATS、公開ページ読み取り範囲が設計通り制限されること。
- FeliCa 212K / 424K で IDm、ATR、通信速度推定が表示され、サービス総当たりや残高/履歴読み取りを行わないこと。
- DESFire で ATS と UID が表示され、ISO wrapping / native mode を混在させないこと。
- カード保持中に同一カードの表示が重複しないこと。
- カード取り外し後に表示ゲートが解除され、再セット時に再表示されること。
- 疑似 removed でカードあり状態なら表示ゲートを解除しないこと。
- APDU 失敗時に SW / ACR122U error code と人間向け説明が表示されること。

## 実装班への修正指示

1. CLI 引数パーサーを追加し、少なくとも `--help`、`--version`、`--trace`、`--json <path>`、`--markdown <path>`、不正オプションエラーを実装する。
2. `--help` / `--version` / 不正オプションでは、リーダー接続やカード待機を行わず即時終了する。
3. JSON / Markdown 出力は `CardInfoModel` 相当の構造を定義し、取得成功、取得失敗、安全方針で未取得、対象外を区別して出力する。
4. 通常起動で `SetPICCOperatingParameterState(AllOn)` を実行しない。初期実装は read-only default とし、reader 設定変更は診断用の明示オプションに分離する。
5. 複数 reader の一覧表示と明示選択を実装する。reader 未接続時は分かりやすい日本語メッセージで終了する。
6. 非対話実行や CI でも落ちないように、`Console.ReadKey` 待機を `--once`、`--watch` などの動作モードに分ける。
7. 設計どおり新規プロジェクトとして切り出すのか、既存 `CardReader_TestConsole` を正式対象に変更するのかを Issue / README / 設計書で明確にする。

## 総括

現実装は、既存サンプルに対するカード概要表示の強化としては前進している。一方で、新規カード情報表示プログラムとしての CLI、出力モデル、JSON / Markdown、read-only default、reader 選択、非対話実行の品質が不足しているため、このまま完了扱いにはできない。
