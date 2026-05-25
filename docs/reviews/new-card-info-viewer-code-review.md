# 新規カード情報表示プログラム コードレビュー

作成日: 2026-05-25

## 判定

**差し戻し**。

設計書 Phase 1 が求める新規 console project `Acr122uCardInspector` は存在せず、現状の実装は既存 `CardReader_TestConsole` への機能追加に留まっている。既存ソリューションの Release ビルドは成功したが、read-only default、CLI、JSON 出力、責務分離、状態機械の明示実装が完了基準に達していない。

## レビュー対象

- リポジトリ: `kuwa2005/acr122u-winscard-csharp`
- ブランチ: `master`
- 確認コミット: `49a26a3 機能洗い出しの引き継ぎ資料を追加`
- 設計書: `docs/design/new-card-info-viewer-design.md`
- 引き継ぎ: `docs/design/feature-inventory-handoff.md`
- 実装確認対象: `NFC_CardReader.sln`, `CardReader_TestConsole`, `NFC_CardReader`
- 差し戻し Issue: https://github.com/kuwa2005/acr122u-winscard-csharp/issues/13

## 重要指摘

### Critical: Phase 1 の新規プロジェクトが未実装

`NFC_CardReader.sln` には `NFC_CardReader` と `CardReader_TestConsole` の 2 プロジェクトのみが含まれており、Phase 1 の必須項目である新規 console project `Acr122uCardInspector` が存在しない。`PcscContext`、`ReaderCatalog`、`ReaderSession`、`CardSession`、`ApduTransceiver` の責務分離も確認できない。

修正指示:

- `Acr122uCardInspector` を新規 console project としてソリューションに追加する。
- 既存 `CardReader_TestConsole` への追記ではなく、設計書のコンポーネント境界に沿って最小実装を切り出す。
- 既存コードは参照に留め、流用する場合も設計書上の責務名・境界に合わせて再設計する。

### High: 通常起動で PICC operating parameter を変更している

`CardReader_TestConsole` の起動処理で `ACR122U_PICCOperatingParametersControl.AllOn` を `SetPICCOperatingParameterState` に渡している。これは設計書の「初期実装では、リーダー設定を変更するコマンドは実行しない」に反する。通常表示プログラムが reader 設定を書き換えるため、安全境界と再現性の両面で問題がある。

修正指示:

- 通常起動では `FF 00 50 00 00` による取得のみ行い、`FF 00 51 ...` の設定変更は行わない。
- 設定変更は将来の診断用明示オプションに分離し、実行前確認とログを必須にする。
- 表示は bit 展開として `Auto PICC Polling`、`Auto ATS Generation`、polling interval、検出対象を示す。

### High: CLI が実機不要確認に対応していない

`--help` を実行してもヘルプを表示せず通常起動し、リーダー接続と `Console.ReadKey(true)` 待機に進む。非対話実行では未処理例外で exit code `-532462766` になった。`--version`、`--json <path>` も正式オプションとして確認できない。

修正指示:

- `--help`、`--version`、`--trace`、`--json <path>`、必要なら `--once` / `--watch` を正式オプションとして実装する。
- `--help` / `--version` / 不正オプションでは reader 接続やカード待機を行わず、exit code `0` または明確なエラーコードで終了する。
- 非対話・CI 環境で `Console.ReadKey` 由来の未処理例外が出ないよう、動作モードを分離する。

### High: JSON 出力と結果モデルが未実装

設計書は `CardInfoModel` と `--json <path>` による機械可読出力を Phase 1 以降の中心モデルとしているが、現状はコンソール表示用の文字列生成に寄っている。取得成功、取得失敗、安全方針で未取得、対象外の区別も構造化されていない。

修正指示:

- `CardInfoModel` 相当を定義し、reader 情報、ATR、UID、ATS、historical bytes、protocol、推定カード名を構造化する。
- APDU 失敗は SW / WinSCard error / unsupported / timeout を区別して保持する。
- 保護領域、残高、個人情報、FeliCa サービス総当たりなどは `SkippedByPolicy` または同等の状態で出力する。

### Medium: イベント状態機械が設計どおり明示されていない

重複表示抑制や疑似 removed 再確認の工夫はあるが、設計書の `CardCandidate`、`CardPresentStable`、`CardProcessing`、`CardDisplayed`、`RemovalCandidate`、`CardRemovedStable` という状態として分離されていない。既存 `ACR122UManager` のポーリングイベントに UI 側ゲートを重ねているため、実カードでの疑似 removed ループや probe 中断時の扱いをテストしにくい。

修正指示:

- 新規アプリ側に状態 enum と遷移ログを持たせ、同一カード判定キーを `readerName + ATR + UID/NFC ID + protocol` として扱う。
- removed 相当は即解除せず、再確認に成功した場合だけゲート解除する。
- probe 中の取り外しは `CardLostDuringProbe` として結果モデルに残す。

### Medium: リソース解放の境界が不明確

カードサマリー取得用の一時接続では `SCARD_LEAVE_CARD` が使われており、この点は reset 回避として妥当。一方で既定の `WinSmartCard.Dispose()` と `ACR122UManager.DisconnectToCard()` は `SCARD_RESET_CARD` に流れるため、どのコードパスがカードを reset するかが呼び出し側から見えにくい。Phase 1 の新規 `CardSession` では disposition を明示すべき。

修正指示:

- 読み取り専用の一時接続は `SCARD_LEAVE_CARD` を既定にする。
- reset が必要な診断機能は明示オプションに分離する。
- `CardSession.Dispose` で disposition の既定値と理由をコード上で明確化する。

## セキュリティ / 倫理

現状のカードサマリーは UID / ATS / ATR など公開情報の表示に留まっており、残高、履歴、個人情報、未知キー探索、FeliCa サービス総当たりを狙う実装は確認していない。ただし通常起動で reader 設定を書き換える点は read-only default に反するため修正が必要。

## ビルド / 実機不要確認

```powershell
dotnet build .\NFC_CardReader.sln --configuration Release
```

結果: 成功。警告 0、エラー 0。

```powershell
cmd /c "echo x| .\CardReader_TestConsole\bin\Release\CardReader_TestConsole.exe --help"
```

結果: ヘルプではなく通常起動し、ACR122U reader と firmware を表示した後、未処理例外で exit code `-532462766`。

## Git 管理

Release ビルド後、`bin/` / `obj/` は Git 差分に出ていない。別作業として `README.md`、`docs/RELEASE-SIGNING.md`、`docs/design/new-card-info-viewer-design.md`、`docs/specs/CardReader_TestConsole-機能仕様書.md`、`docs/testing/` の未コミット差分が存在するため、本レビューのコミットではレビュー文書のみを対象にする。

## 結論

現状は既存サンプルの改善としては有用だが、設計書 Phase 1 の新規カード情報表示プログラムとしては未完了。上記 Critical / High 指摘を解消し、`Acr122uCardInspector` と正式 CLI / JSON 出力 / read-only default / 明示状態機械を実装した後に再レビューする。
