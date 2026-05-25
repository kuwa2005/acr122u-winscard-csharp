# CardReader_TestConsole.exe 機能仕様書

## 対象 exe

Release ビルド時のテスト対象は次の実行ファイルです。

```text
CardReader_TestConsole\bin\Release\CardReader_TestConsole.exe
```

Debug 構成で確認する場合は `CardReader_TestConsole\bin\Debug\CardReader_TestConsole.exe` を使います。`bin/`、`obj/`、`.vs/`、`*.exe`、`*.dll`、`*.pdb` は生成物であり、コミット対象にしません。

## 概要

`CardReader_TestConsole.exe` は、ACS ACR122U USB NFC リーダーを Windows の PC/SC（WinSCard）経由で操作し、リーダー概要、カード検出イベント、カードサマリー、イベント重複抑制を実機で確認するためのコンソールアプリケーションです。

プロジェクトは現行 SDK でビルドできるようにしていますが、ターゲットフレームワークは `.NET Framework 4.6.2` のまま維持します。

## 起動方法

通常起動:

```powershell
.\CardReader_TestConsole\bin\Release\CardReader_TestConsole.exe
```

トレース起動:

```powershell
.\CardReader_TestConsole\bin\Release\CardReader_TestConsole.exe --trace
```

または環境変数で有効化します。

```powershell
$env:ACR122U_TRACE = "1"
.\CardReader_TestConsole\bin\Release\CardReader_TestConsole.exe
```

トレース有効時は、実行時のカレントディレクトリ配下に `logs/trace-YYYYMMDD-HHMMSS.log` を作成します。`logs/` と `*.log` は `.gitignore` の対象であり、実機ログは Git 管理に含めません。

## 起動時表示

起動時には次の情報を表示します。

- PC/SC から取得したリーダー名
- ACR122U の `FF 00 48 00 00` で取得できる場合のファームウェアバージョン
- 資料ベースの ACR122U 既知仕様
  - 対象機種
  - USB / PC/SC / CCID / WinSCard のインターフェース
  - 対応カード/タグ
  - 周波数、通信速度、読取距離
- PICC operating parameter の設定値
- 起動時のカード検出状態と ACR122U エラー状態
- 操作ガイド
  - `C` キーでコンソールを手動クリア
  - その他のキーで終了

起動時概要では、カードの保護領域、個人情報、残高などのカード内容は読み取りません。

## カード検出時表示

カード検出時は画面を自動クリアせず、現在の表示内容の下にイベントとカードサマリーを追記します。

カード検出イベントでは次の情報を表示します。

- 「カードを検出しました」
- 状態値
- 状態値の 16 進表記
- ATR

カードサマリーでは次の情報を表示します。

- ATR
- UID
- ATS
- ATR から推定したカード名
- ATR から推定した規格
- Historical bytes
- PC/SC プロトコル
- ACR122U 状態
  - カード有無
  - RF フィールド有無
  - ターゲット数
  - 論理番号
  - 受信速度
  - 送信速度
  - 変調種別
  - エラー状態
- 注意文
  - UID/ATS/ATR などの公開情報のみを表示し、保護領域、残高、個人情報は読み取らない

UID は `FF CA 00 00 00`、ATS は `FF CA 01 00 00` で取得を試みます。取得できない場合は `取得失敗 (...)` として、ステータスワードや例外メッセージを簡潔に表示します。

## イベント重複抑制

カード保持中は、同一カードに対して発生する再検出、受理/拒否、状態変化の連続表示を抑制します。カードを実際に取り外すまで、カードサマリーは原則 1 回だけ表示します。

カード取り外し時は、リーダー状態を再確認します。リーダーがまだカードありと報告している場合は疑似的な取り外しイベントとして扱い、画面表示とゲート解除を行いません。実際にカードなしになった後は取り外しイベントを 1 回表示し、その後の空状態イベントや重複した取り外しイベントは抑制します。

`C` キーによる手動クリアは画面表示だけを消し、カードあり/なしの表示済みゲートは維持します。そのため、カードを置いたまま `C` キーでクリアしても同じカードのサマリーは再表示されません。再表示したい場合は、一度カードを取り外してから再度かざします。

## トレースログの見方

トレースログには、各行に時刻、連番、スレッド ID、イベント名、詳細値を出力します。主なイベントは次のとおりです。

- `TraceEnabled`: トレース有効化条件とログパス
- `Startup` / `ReaderSelected` / `StartupStatusRead` / `FirmwareReadStart` / `FirmwareReadResponse`: 起動時のリーダー確認
- `PrintStartupSummary`: 起動時概要の表示
- `CardDetected` / `CardAccepted` / `CardRejected` / `StateChanged` / `CardRemoved`: カードイベント
- `CardSummaryStart` / `UIDReadStart` / `UIDReadEnd` / `ATSReadStart` / `ATSReadEnd` / `ReaderStatusReadEnd`: カードサマリー取得
- `ManualClearStart` / `ConsoleClearStart` / `ManualClearEnd`: `C` キーによる手動クリア
- `SKIP reason=...`: 重複表示や疑似イベントを抑制した理由

ログ内の UID、ATR、ATS は公開 APDU で取得できる範囲の情報です。実機ログは `logs/` 配下に保存されますが、`logs/` は Git 管理対象外です。

## 実機確認手順

1. ACR122U を接続し、Windows Smart Card サービスと PC/SC / CCID ドライバで認識されていることを確認します。
2. `dotnet build .\NFC_CardReader.sln --configuration Release` で Release ビルドします。
3. `.\CardReader_TestConsole\bin\Release\CardReader_TestConsole.exe` を起動し、リーダー概要、PICC 設定、起動時状態、操作ガイドが表示されることを確認します。
4. カードをかざし、カード検出イベントとカードサマリーが表示されることを確認します。
5. カードを置いたまま数秒待ち、同じカードのカードサマリー、受理/拒否、状態変化が連続表示されないことを確認します。
6. カードを取り外し、取り外しイベントが 1 回表示され、後続の空状態イベントが連続表示されないことを確認します。
7. `C` キーを押し、画面がクリアされて起動時概要が再表示されることを確認します。
8. `--trace` または `ACR122U_TRACE=1` で再起動し、`logs/trace-YYYYMMDD-HHMMSS.log` にイベント順序、UID/ATS 取得結果、`SKIP reason=...` が記録されることを確認します。
9. `git status --short --ignored` などで、`logs/`、`bin/`、`obj/`、`.vs/`、`*.exe`、`*.dll`、`*.pdb` が Git 管理対象外であることを確認します。

## ビルド / 実行条件

- Windows
- ACS ACR122U USB NFC Reader
- Windows Smart Card サービス
- PC/SC / CCID ドライバ
- .NET Framework 4.6.2
- .NET SDK または Visual Studio / MSBuild が使える環境

## セキュリティ / SmartScreen / カード保護領域に関する注意

- UID は認証や本人確認の唯一の根拠として扱うべきではありません。
- このコンソールは UID/ATS/ATR など公開 APDU で取得できる範囲を表示します。カードの保護領域、個人情報、残高、暗号鍵、認証が必要なデータは読み取りません。
- SmartScreen 警告を減らす正規対策は、正規の認証局から発行された EV または OV の Authenticode コード署名、タイムスタンプ、一貫した配布元、リリースノートとチェックサムの整備です。
- 署名済みであっても、新しい証明書や新しい成果物は SmartScreen reputation がすぐに付かない場合があります。SmartScreen の無効化や警告を無視させる運用は行いません。
- 署名用の PFX/P12、秘密鍵、証明書パスワード、署名ログはリポジトリに含めないでください。
- `app.manifest` は `CardReader_TestConsole.csproj` から明示的に参照され、`requestedExecutionLevel` は `asInvoker` に設定されています。

## 関連資料

- [ACR122U API V2.04 整理版](../../../API-ACR122U-2.04.md)
- [ACR122U USB NFC Reader Technical Specifications V3.05](../../../TSP-ACR122U-3.05.md)
- [リリースとコード署名](../RELEASE-SIGNING.md)
- [README](../../README.md)

## 残る注意点

- `app.manifest` がリリース成果物へ意図どおり埋め込まれているかは、Release ビルド後に必要に応じて確認してください。
- SmartScreen reputation は Microsoft 側の評価に依存するため、正規コード署名を行っても警告が必ず即時に消えるとは限りません。
