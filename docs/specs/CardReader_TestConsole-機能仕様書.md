# CardReader_TestConsole.exe 機能仕様書

## 対象 exe 名

`CardReader_TestConsole.exe`

## 概要

`CardReader_TestConsole.exe` は、ACS ACR122U USB NFC リーダーを Windows の PC/SC（WinSCard）経由で操作し、カード検出イベントとカードサマリー表示を確認するためのコンソールアプリケーションです。

## 主な機能

- PC/SC に登録されたスマートカードリーダー一覧から ACR122U リーダーを抽出する。
- 起動時に、PC/SC から取得したリーダー名、取得できる場合はファームウェアバージョン、資料ベースの ACR122U 仕様を表示する。
- `SCardGetStatusChange` を使う監視スレッドでカードの状態変化を検出する。
- ACR122U の PICC operating parameter を `AllOn` に設定する。
- カード検出時に ATR、UID、ATS、ATR から推定したカード種別/規格、ACR122U 状態を表示する。

## ビルド / 実行条件

- Windows
- ACS ACR122U USB NFC Reader
- Windows Smart Card サービス
- PC/SC / CCID ドライバ
- .NET Framework 4.6.2
- .NET SDK または Visual Studio / MSBuild が使える環境

`bin/` と `obj/` は生成物であり、コミット対象にしません。

起動時リーダー概要とカードサマリーでは、カードの保護領域、個人情報、残高などの内容は読み取りません。ファームウェアバージョンは ACR122U の `FF 00 48 00 00` による取得を試み、カード検出時の UID/ATS は `FF CA 00 00 00` / `FF CA 01 00 00` による取得を試みます。取得できない場合は、その旨と簡潔な理由を表示します。

## セキュリティ / SmartScreen / カード保護領域に関する注意

- UID は認証や本人確認の唯一の根拠として扱うべきではありません。
- SmartScreen 警告を減らす正規対策は Authenticode コード署名、タイムスタンプ、一貫した配布元、リリースノートとチェックサムの整備です。
- 署名用の PFX/P12、秘密鍵、証明書パスワード、署名ログはリポジトリに含めないでください。
- `app.manifest` は `CardReader_TestConsole.csproj` から明示的に参照され、`requestedExecutionLevel` は `asInvoker` に設定されています。

## 関連資料

- [ACR122U API V2.04 整理版](../../../API-ACR122U-2.04.md)
- [ACR122U USB NFC Reader Technical Specifications V3.05](../../../TSP-ACR122U-3.05.md)
- [リリースとコード署名](../RELEASE-SIGNING.md)
- [README](../../README.md)

## 未確認事項

- 実機 ACR122U と対象カードを使った実行確認は未実施です。
- `app.manifest` がリリース成果物へ意図どおり埋め込まれているかは、Release ビルド後に確認してください。
- SmartScreen reputation は Microsoft 側の評価に依存するため、署名直後に警告が必ず消えることは保証できません。