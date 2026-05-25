# 新カード情報表示プログラム Phase 1 検証レポート

作成日: 2026-05-25

## 検証対象

- リポジトリ: `kuwa2005/acr122u-winscard-csharp`
- ローカルパス: `D:\00_project\ACR122\acr122u-winscard-csharp`
- ブランチ: `master`
- 直近コミット: `e4a246b 現行仕様に合わせてドキュメントを整備`
- 設計書: `docs/design/new-card-info-viewer-design.md`
- 機能洗い出し引き継ぎ: `docs/design/feature-inventory-handoff.md`
- 仕様書: `API-ACR122U-2.04.md`, `TSP-ACR122U-3.05.md`
- 実装確認対象: 既存 `NFC_CardReader.sln`, `CardReader_TestConsole`, `NFC_CardReader`

## 判定サマリー

Phase 1 は未完了として差し戻しが必要。

差し戻し Issue: https://github.com/kuwa2005/acr122u-winscard-csharp/issues/13

設計書の Phase 1 は、新規 console project `Acr122uCardInspector` を追加し、`PcscContext` / `ReaderCatalog` / `ReaderSession` / `CardSession` / `ApduTransceiver` を最小実装することを求めている。現状のソリューションには新規プロジェクトが存在せず、既存 `CardReader_TestConsole` にリーダー概要、firmware、ATR / UID / ATS 表示、トレース、重複表示抑制が追加されている状態である。

Release ビルドは成功したが、`--help` / `--version` / `--json` は専用オプションとして実装されていない。`--trace` はログ出力を開始するが、確認実行では各コマンドが終了時に `-532462766` で非ゼロ終了した。

## 確認方法

1. `git status --short --branch` と `git log --oneline -10` でブランチ、差分、直近履歴を確認。
2. `docs/design/new-card-info-viewer-design.md` と `docs/design/feature-inventory-handoff.md` を読み、Phase 1 と追加引き継ぎ内容を確認。
3. `*.sln` / `*.csproj` / `*.cs` を確認し、新規実装プロジェクトの有無と既存実装の範囲を確認。
4. `dotnet build .\NFC_CardReader.sln --configuration Release` を実行。
5. `CardReader_TestConsole.exe --help`, `--version`, `--json`, `--trace` を、標準入力で終了キーを渡して実行。

## 実行結果

| 項目 | 結果 |
|---|---|
| `git status --short --branch` | `master...origin/master`。README と設計書がステージ済み、`feature-inventory-handoff.md` が未追跡として存在 |
| `git log --oneline -10` | 直近は `e4a246b 現行仕様に合わせてドキュメントを整備` |
| Release build | 成功。警告 0、エラー 0 |
| `--help` | ヘルプではなく通常起動。終了コード `-532462766` |
| `--version` | バージョン表示ではなく通常起動。終了コード `-532462766` |
| `--json` | JSON ファイルは作成されない。終了コード `-532462766` |
| `--trace` | `logs/trace-YYYYMMDD-HHMMSS.log` を作成しようとするが、通常起動扱い。終了コード `-532462766` |
| 実機リーダー | `ACS ACR122 0` を検出。firmware `ACR122U215` を取得 |
| 実カード | 未確認。確認時の起動時状態はカード未検出 |

## Phase 1 要件チェックリスト

| 設計項目 | 実装状況 | 確認方法 | 結果 / 差し戻し指示 |
|---|---|---|---|
| 新規 console project `Acr122uCardInspector` | 未実装 | `.sln` / `.csproj` を確認 | `CardReader_TestConsole` と `NFC_CardReader` のみ。設計通り新規プロジェクトを追加すること |
| `PcscContext` / `ReaderCatalog` / `ReaderSession` / `CardSession` / `ApduTransceiver` | 未実装 | ソース構成を確認 | 既存 `WinSmartCardContext` / `ACR122UManager` への追加に留まる。Phase 1 の責務分離で実装すること |
| リーダー一覧 / 選択 | 部分完了 | `ACR122UManager.GetACR122UReaders()` と起動出力を確認 | 最初の ACR122U を自動選択するのみ。reader 一覧表示、明示選択、未接続時の分かりやすいエラーを実装すること |
| リーダー概要表示 | 部分完了 | `CardReader_TestConsole` 起動出力を確認 | reader 名、firmware、静的仕様は表示。PICC parameter の bit 展開、接続状態 raw 値、contactless status の整理表示が不足 |
| firmware 取得 | 部分完了 | `FF 00 48 00 00` 実行結果を確認 | `ACR122U215` を取得できた。ただし新規アプリでは未実装のため、`Acr122uCardInspector` 側へ移植すること |
| PICC operating parameter | 部分完了 | 起動出力とコードを確認 | `AllOn` 表示のみ。設計通り `Auto PICC Polling`、`Auto ATS Generation`、polling interval、対象カード bit を日本語で展開すること |
| カード検出 / 取り外し状態機械 | 部分完了 | `ManagerTest` のゲート処理を確認 | 重複表示抑制と疑似 removed 再確認はあるが、設計の `CardCandidate` / `CardPresentStable` / `RemovalCandidate` など明示状態機械ではない。新規アプリで状態として実装すること |
| ATR 取得 | 部分完了 | `CardSummaryWriter` を確認 | カード検出時に ATR を表示する実装はあるが、実カード未確認。新規アプリ側に移し、historical bytes と解析結果をモデル化すること |
| UID / NFC ID 取得 | 部分完了 | `FF CA 00 00 00` 実装を確認 | カード検出時の取得処理はあるが、実カード未確認。取得失敗理由と SW を構造化して表示すること |
| ATS 取得 | 部分完了 | `FF CA 01 00 00` 実装を確認 | カード検出時の取得処理はあるが、実カード未確認。未サポート時の `取得失敗` と理由を明確に表示すること |
| カード分類 | 部分完了 | `AtrSummary` を確認 | MIFARE / FeliCa / Topaz / Undefined SAK の基本推定はある。独立した `AtrParser` / `CardClassifier` として分離すること |
| 日本語表示 | 部分完了 | コンソール出力を確認 | 既存コンソールの表示は日本語化済み。新規アプリ、ヘルプ、エラー、JSON / Markdown の説明文にも反映すること |
| トレース出力 | 部分完了 | `--trace` 実行 | ログ出力機能はあるが通常起動扱いで、終了コードが非ゼロ。新規アプリで `--trace` を正式オプション化し、正常終了できるようにすること |
| JSON 出力 | 未実装 | `--json .\docs\testing\cli-json-check.json` 実行 | JSON ファイルは作成されない。設計に従い `--json <path>` を実装すること |
| Markdown / 診断レポート出力 | 未実装 | オプションとコードを確認 | `--markdown` / `--diagnostic-report` 相当はない。Phase 1 外にする場合も実装計画に明記すること |
| 読み取り制約 / 安全注意 | 部分完了 | 起動出力とコードを確認 | 保護領域、残高、個人情報を読まない注意は表示される。`SkippedByPolicy` / `NotApplicable` などの未取得理由モデルは未実装 |
| CLI `--help` / `--version` | 未実装 | 実行確認 | 通常起動扱い。実機不要で成功するヘルプとバージョン表示を追加すること |

## 差し戻し項目

実装班への追加実装指示:

1. 設計書 Phase 1 の通り、新規 console project `Acr122uCardInspector` をソリューションに追加する。
2. 既存 `CardReader_TestConsole` への追加ではなく、最小責務として `PcscContext`、`ReaderCatalog`、`ReaderSession`、`CardSession`、`ApduTransceiver` を実装する。
3. reader 一覧、ACR122U 候補選択、reader 未接続時エラー、firmware、PICC operating parameter bit 展開を実装する。
4. カード検出時に ATR、UID、ATS、historical bytes、PC/SC protocol、推定カード名を `CardInfoModel` 相当へ収集し、日本語のコンソール表示に出す。
5. 設計書の状態機械に沿って、カード保持中の重複表示、疑似 removed、実 removed 後のゲート解除を明示状態として実装する。
6. `--help`、`--version`、`--trace`、`--json <path>` を実機不要で確認できる正式オプションとして実装する。
7. CLI は正常終了時に exit code `0` を返すようにし、監視スレッド終了時の未処理例外を解消する。
8. 保護領域、個人情報、残高、未知キー探索、FeliCa サービス総当たりを行わない方針を、表示モデルと JSON に `安全方針で未取得` として残す。

## 実機未確認項目

- 実カードを置いた状態での ATR / UID / ATS 取得。
- MIFARE Classic / Ultralight / DESFire / FeliCa / Topaz のカード分類。
- 同一カード保持中の重複表示抑制。
- 実カード取り外し後のゲート解除。
- 疑似 removed の再確認挙動。
- APDU status error、timeout、card lost の表示。
- `--trace` ログ内容のカードイベント詳細。

## 結論

Release ビルドは成功し、既存コンソールで reader 名と firmware を取得できることは確認できた。一方、設計書 Phase 1 が要求する新規 `Acr122uCardInspector` と責務分離、CLI オプション、JSON 出力、状態機械の明示実装は未完了である。

このため、本検証では Phase 1 実装を未完了と判定し、GitHub Issue で差し戻す。
