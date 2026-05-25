# NFC Card Reader WinSCard ACR122U CSharp

ACR122U USB NFC リーダーを Windows の PC/SC（WinSCard）経由で扱うための C# 実装です。`winscard.dll` を P/Invoke で呼び出す低レベルなラッパーと、ACS ACR122U の疑似 APDU / カード操作を扱いやすくするクラス、動作確認用のコンソールアプリで構成されています。

この README は、clone 済みのソースコードと親フォルダにある ACR122U 技術資料をもとに日本語で整理したものです。

## プロジェクト概要

このリポジトリには Visual Studio 形式のソリューション `NFC_CardReader.sln` が含まれています。

| プロジェクト | 種別 | 概要 |
|---|---|---|
| `NFC_CardReader` | クラスライブラリ | WinSCard / ACR122U / MIFARE Classic / NTAG 系カード操作のラッパー |
| `CardReader_TestConsole` | コンソールアプリ | ACR122U 接続、カード検出イベント、UID 取得などの動作確認用サンプル |
| `Acr122uCardInspector` | コンソールアプリ | 仕様書ベースで新規実装したカード情報表示プログラム。正式 CLI、JSON 出力、read-only default、ATR / UID / ATS 表示を担当 |

ターゲットフレームワークはいずれも `.NET Framework 4.6.2` です。SDK-style project ではなく、Visual Studio 2017 世代の旧形式 `.csproj` です。`Directory.Build.props` で `Microsoft.NETFramework.ReferenceAssemblies.net462` を復元し、.NET Framework 4.6.2 Targeting Pack がない環境でも `dotnet build` しやすくしています。

## 新規カード情報表示プログラムの再設計

現行 `CardReader_TestConsole` の延長ではなく、仕様書ベースでカード情報表示プログラムを作り直すための設計を整理しています。取得できる情報、取得しない保護領域、APDU / PC/SC レイヤ、イベント設計、段階的実装計画は [docs/design/new-card-info-viewer-design.md](docs/design/new-card-info-viewer-design.md) を参照してください。

2026-05-25 時点で、新規 console project `Acr122uCardInspector` を追加しています。既存 `CardReader_TestConsole` の延長ではなく、新規の WinSCard P/Invoke と `ReaderSession` / `CardSession` / `ApduTransceiver` / `CardProbe` / `CardClassifier` / `SummaryRenderer` 相当の責務分離で Phase 1 を実装しています。

## ACR122U / WinSCard / C# の位置づけ

- **ACR122U**: ACS 製の USB 接続 NFC リーダー/ライターです。PC/SC / CCID に対応し、MIFARE、ISO 14443 Type A/B、FeliCa、NFC tag などを扱えます。
- **WinSCard**: Windows 標準のスマートカード API です。このプロジェクトでは `winscard.dll` の `SCardEstablishContext`、`SCardListReaders`、`SCardConnect`、`SCardTransmit`、`SCardControl`、`SCardGetStatusChange` などを C# から呼び出します。
- **C# ラッパー**: 生の WinSCard API を `WinSmartCardContext` / `WinSmartCard` で包み、さらに `ACR122U_SmartCard` や `ACR122UManager` が ACR122U 固有のコマンドとカード検出イベントを扱います。

## 主な機能

- 接続済みスマートカードリーダー一覧の取得
- ACR122U リーダー名の抽出
  - `ACS ACR122` を含むリーダーを対象にし、ドライバによって追加される `ACS ACR122U PICC Interface` は除外
- PC/SC コンテキスト作成、カード接続、切断、状態取得
- `SCardGetStatusChange` を使ったカード挿入/取り外しの監視
- カード検出、カード取り外し、受理/拒否などのイベント通知
- ACR122U 疑似 APDU による操作
  - PICC operating parameter の取得/設定
  - LED / ブザー制御
  - ACR122U ステータス取得
- ISO 14443 Type A の UID 取得
- MIFARE Classic 向け操作
  - 認証キー読み込み
  - Key A / Key B 認証
  - ブロック読み取り/書き込み
  - value block の読み書き、increment、decrement、copy
- NTAG215 用クラス
  - 現状は ISO 14443 Type A の UID 取得を利用する薄い派生クラスです

## 必要環境

- Windows
- ACS ACR122U USB NFC Reader
- PC/SC / CCID ドライバ
  - Windows 標準ドライバまたは ACS 公式ドライバ
  - デバイスマネージャーやスマートカードサービスで ACR122U が認識されていること
- Visual Studio 2017 以降、MSBuild、または .NET SDK が使える環境
- .NET Framework 4.6.2
  - `Directory.Build.props` で参照アセンブリを復元するため、Targeting Pack 未導入環境でも `dotnet build` しやすくしています。
- 実カード
  - UID 取得: ISO 14443 Type A / NTAG 系など
  - MIFARE Classic 操作: MIFARE Classic 1K など

## セットアップ手順

1. ACR122U を PC に接続します。
2. Windows がスマートカードリーダーとして認識していることを確認します。
3. 必要に応じて ACS 公式ドライバをインストールします。
4. Visual Studio で `NFC_CardReader.sln` を開きます。
5. `.NET Framework 4.6.2 Developer Pack / Targeting Pack` が未導入の場合はインストールします。
6. `CardReader_TestConsole` をスタートアッププロジェクトに設定します。
7. 実行前に `CardReader_TestConsole/Program.cs` の受け入れ ATR 条件やカード操作内容を、自分のカードに合わせて確認します。

## ビルド方法

Visual Studio で開く場合:

```powershell
start .\NFC_CardReader.sln
```

Visual Studio 上で `Debug|Any CPU` または `Release|Any CPU` を選択し、ソリューションをビルドします。

コマンドラインで Release ビルドする場合:

```powershell
dotnet build .\NFC_CardReader.sln --configuration Release
```

または、Visual Studio Build Tools の MSBuild が PATH にある場合:

```powershell
msbuild .\NFC_CardReader.sln /p:Configuration=Release
```

テスト対象の exe は次の場所に出力されます。

```text
CardReader_TestConsole\bin\Release\CardReader_TestConsole.exe
Acr122uCardInspector\bin\Release\Acr122uCardInspector.exe
```

確認メモ:

- この環境では `dotnet` SDK 10.0.204 を使用しました。
- `Directory.Build.props` の参照アセンブリパッケージにより、`dotnet build .\NFC_CardReader.sln --configuration Release` が成功することを確認しています。

## 配布とコード署名（SmartScreen 対策）

Windows 向けに exe を配布する場合は、SmartScreen の警告を減らすために Authenticode コード署名と一貫したリリース運用を行ってください。SmartScreen の保護を無効化したり、利用者へ警告を無視するよう促したりする手順は扱いません。

- EV または OV のコード署名証明書を正規の認証局から取得し、秘密鍵や PFX/P12 はリポジトリに含めないでください。
- `signtool.exe` で SHA-256 の Authenticode 署名を行い、信頼できるタイムスタンプ URL を指定してください。
- GitHub Releases など同じ配布元から継続して配布し、バージョン、リリースノート、チェックサムを整理してください。
- 新しい証明書や新しい成果物は、署名済みでも SmartScreen の reputation がすぐに付かない場合があります。

署名スクリプト雛形は `scripts/Sign-Release.ps1`、詳細なリリース手順は [docs/RELEASE-SIGNING.md](docs/RELEASE-SIGNING.md) を参照してください。

## 実行方法

### Acr122uCardInspector

新しいカード情報表示プログラムは `Acr122uCardInspector` です。通常起動では ACR122U 候補リーダーを自動選択し、カードの安定検出後に ATR / UID / ATS / ATR 由来の推定カード名と、これらから生成した簡易的な識別コードを表示します。既定動作は read-only で、reader 設定変更、書き込み系 APDU、鍵探索、FeliCa サービス総当たり、残高/履歴/個人情報の読み取りは行いません。

```powershell
.\Acr122uCardInspector\bin\Release\Acr122uCardInspector.exe
```

非対話で 1 回だけ確認する場合:

```powershell
.\Acr122uCardInspector\bin\Release\Acr122uCardInspector.exe --once
```

JSON を標準出力へ出す場合:

```powershell
.\Acr122uCardInspector\bin\Release\Acr122uCardInspector.exe --json --once
```

JSON をファイルへ出す場合:

```powershell
.\Acr122uCardInspector\bin\Release\Acr122uCardInspector.exe --json .\card-result.json --once
```

識別コード生成用キーを変更する場合:

```powershell
.\Acr122uCardInspector\bin\Release\Acr122uCardInspector.exe --once --identity-key 0000
```

主な CLI:

| 指定 | 説明 |
|---|---|
| `--help` | ヘルプを表示して exit code 0 で終了 |
| `--version` | バージョンを表示して exit code 0 で終了 |
| `--once` | リーダー/カード状態を 1 回だけ確認して終了 |
| `--reader <name>` | 使用する PC/SC リーダー名を指定。部分一致も許可 |
| `--identity-key <value>` | 識別コード生成用キーを指定。既定値は暫定仕様の `0000` |
| `--trace` | `logs/trace-YYYYMMDD-HHMMSS.log` に診断トレースを出力 |
| `--json [path]` | `ProbeResult` を JSON 出力。path 省略時は標準出力 |

識別コードは `UID=<...>|ATR=<...>|ATS=<...>|PMM=<...>|TYPE=<...>|KEY=<...>` を正規化して UTF-8 文字列として扱い、`System.Security.Cryptography.MD5` で計算した 32 桁の小文字 hex です。hex 値は大文字かつ区切りなしに正規化し、未取得値は `unknown` として扱います。現時点では PMm を取得していないため `PMM=unknown` です。JSON には `identityCode` と `identitySource` を出力しますが、キー値そのものは出さず `keyConfigured` とキー以外の正規化元だけを含めます。

この識別コードは簡易識別用です。MD5 と既定キー `0000` は暫定仕様であり、強い認証や改ざん耐性は保証しません。認証用途では HMAC-SHA256、カード固有の暗号認証、適切な鍵管理を検討してください。

カード未検出やリーダー未検出でも未処理例外にはせず、コンソール表示または JSON の `errors` / `probeItems` に理由を残して終了します。

### CardReader_TestConsole

Visual Studio から実行する場合:

1. `CardReader_TestConsole` をスタートアッププロジェクトに設定します。
2. ACR122U を接続します。
3. 対象カードを手元に用意します。
4. デバッグ実行します。
5. コンソールに ACR122U リーダー概要、PICC 設定、開始ステータス、カード検出/取り外し、カードサマリーなどが表示されます。

コマンドラインから実行する場合:

```powershell
.\CardReader_TestConsole\bin\Release\CardReader_TestConsole.exe
```

イベント順序を追跡したい場合は、明示的にデバッグトレースを有効化して実行します。通常実行ではトレースは出力されません。

```powershell
.\CardReader_TestConsole\bin\Release\CardReader_TestConsole.exe --trace
```

または環境変数でも有効化できます。

```powershell
$env:ACR122U_TRACE = "1"
.\CardReader_TestConsole\bin\Release\CardReader_TestConsole.exe
```

トレース有効時は、実行時のカレントディレクトリ配下の `logs/trace-YYYYMMDD-HHMMSS.log` に追記します。ログには時刻、連番、スレッド ID、`CardDetected` / `CardRemoved` / `CardRejected` / `StateChanged` / 手動 C クリア時の `Console.Clear` / UID・ATS 取得開始終了、ゲート状態、状態値、ATR 要約、UID・ATS の結果または失敗理由、早期 return の `SKIP reason=...` が記録されます。`logs/` と `*.log` は `.gitignore` の対象であり、Git 管理には含めません。

現時点の CLI オプション整理:

| 指定 | 状況 | 説明 |
|---|---|---|
| 指定なし | 実装済み | 既存 `CardReader_TestConsole` を通常起動します。 |
| `--trace` | 部分実装 | トレースログを有効化します。ただし Phase 1 検証では正式オプションとしての終了制御に課題があります。 |
| `ACR122U_TRACE=1` | 部分実装 | 環境変数でトレースログを有効化します。 |
| `--json <path>` | 未実装 | 設計上の予定です。現行 exe は JSON ファイルを作成しません。 |
| `--markdown <path>` | 未実装 | 設計上の予定です。現行 exe は Markdown レポートを作成しません。 |
| `--help` / `--version` | 未実装 | 現行 exe では専用表示にならず、通常起動扱いです。 |

現在の `CardReader_TestConsole/Program.cs` は、おおむね次の流れで動作します。

1. `ACR122UManager.GetACR122UReaders().FirstOrDefault()` で最初の ACR122U リーダーを選択
2. `ACR122UManager` を作成し、状態監視スレッドを開始
3. リーダー名と、取得できる場合は `FF 00 48 00 00` でファームウェアバージョンを取得
4. 資料ベースの ACR122U 仕様と実機取得情報を起動時に表示
5. PICC operating parameter を `AllOn` に設定
6. `GlobalCardCheck` で ATR をチェック
7. カード検出イベントを登録
8. 検出カードに対して公開 APDU (`FF CA 00 00 00`, `FF CA 01 00 00`) を試行し、ATR、UID、ATS、推定カード、推定規格、Historical bytes、PC/SC プロトコル、ACR122U 状態を表示
9. カード検出時は画面を自動クリアせず、既存の起動時表示とイベント履歴の下にカードサマリーを追記
10. カード保持中の再検出、受理/拒否、状態変化と、カード取り外し後の空状態イベントは重複表示を抑制
11. キー入力まで待機し、`C` キーでは手動でコンソールをクリアして起動時概要を再表示、その他のキーでは終了

## 実機確認手順

1. ACR122U を接続し、Windows が `ACS ACR122` 系リーダーとして認識していることを確認します。
2. `dotnet build .\NFC_CardReader.sln --configuration Release` を実行します。
3. `.\CardReader_TestConsole\bin\Release\CardReader_TestConsole.exe` を起動し、リーダー概要、PICC 設定、起動時状態、操作ガイドが表示されることを確認します。
4. カードを 1 枚かざし、カード検出イベントとカードサマリーが 1 回表示されることを確認します。
5. カードを置いたまま数秒待ち、同じカードのサマリーや受理/拒否、状態変化が連続表示されないことを確認します。
6. カードを取り外し、取り外しイベントが 1 回表示され、その後の空状態イベントが連続表示されないことを確認します。
7. `C` キーを押し、手動クリア後に起動時概要が再表示されることを確認します。
8. 必要に応じて `--trace` または `ACR122U_TRACE=1` で再実行し、`logs/trace-YYYYMMDD-HHMMSS.log` にイベント順序と `SKIP reason=...` が記録されることを確認します。

Phase 1 検証、品質チェック、コードレビューでは、Release ビルドと ACR122U リーダー検出、firmware 取得は確認できています。一方、実カードを置いた状態での ATR / UID / ATS、カード分類、JSON / Markdown 出力、正式な `--help` / `--version`、正常終了コード、read-only default は未完了または要修正です。詳細は [Phase 1 検証レポート](docs/testing/new-card-info-viewer-phase1-test-report.md)、[品質チェックレポート](docs/testing/new-card-info-viewer-quality-report.md)、[コードレビュー](docs/reviews/new-card-info-viewer-code-review.md) を参照してください。

## 使い方の例

ACR122U を検出して UID を読む最小例のイメージです。`CardReader_TestConsole` 本体では、カード検出時に UID だけでなく ATR/ATS と推定カード種別も表示します。

```csharp
using System;
using System.Linq;
using NFC_CardReader.ACR122UManager;

class Program
{
    static void Main()
    {
        var readerName = ACR122UManager.GetACR122UReaders().FirstOrDefault();
        if (readerName == null)
        {
            Console.WriteLine("ACR122U reader was not found.");
            return;
        }

        using (var manager = new ACR122UManager(readerName))
        {
            var card = manager.ConnectToNTAGCard();
            byte[] uid;
            var result = card.GetcardUIDBytes(out uid);
            Console.WriteLine("{0}: {1}", result, BitConverter.ToString(uid));
            manager.DisconnectToCard();
        }
    }
}
```

MIFARE Classic の読み書きでは、先にキーをリーダーへロードし、対象ブロックに対して Key A または Key B で認証してから read/write/value 操作を行います。サンプルコード内にはブロック 5 に対する読み書きや value block 操作例がありますが、実カードのデータを上書きする可能性があるため、そのまま実行しないでください。

## 関連資料

親フォルダにある次の資料も参照してください。

- [ACR122U API V2.04 整理版](../API-ACR122U-2.04.md)
- [ACR122U USB NFC Reader Technical Specifications V3.05](../TSP-ACR122U-3.05.md)
- [新カード情報表示プログラム Phase 1 検証レポート](docs/testing/new-card-info-viewer-phase1-test-report.md)
- [新規カード情報表示プログラム 品質チェックレポート](docs/testing/new-card-info-viewer-quality-report.md)
- [新規カード情報表示プログラム コードレビュー](docs/reviews/new-card-info-viewer-code-review.md)
- [作業班体制と進捗管理計画](docs/project/workstream-plan.md)

特に APDU、PICC パラメータ、レスポンスコード、対応カード種別、LED/ブザー制御、PC/SC / CCID 周辺の仕様確認に有用です。

## 注意事項

- カード種別によって利用できるコマンド、ブロックサイズ、認証方式が異なります。
- MIFARE Classic のセクタートレーラーやアクセスビット、鍵領域を書き換えると、カードを読めなくしたり復旧困難にしたりする可能性があります。
- サンプルには固定 ATR、固定ブロック番号、デフォルトキー `FF FF FF FF FF FF` を前提にしたコードがあります。実運用では対象カードごとに条件を見直してください。
- UID は個体識別に便利ですが、認証や本人確認の唯一の根拠として扱うべきではありません。複製可能なカードやランダム UID のカードもあります。
- ACR122U のドライバ更新により、利用しない `ACS ACR122U PICC Interface` が列挙される場合があります。この実装では該当インターフェースを除外しています。
- Windows の Smart Card サービス、ドライバ、USB 接続状態により、リーダー列挙やカード接続に失敗することがあります。
- 複数アプリケーションが同じリーダー/カードへ同時アクセスすると、共有モードやトランザクションの競合が発生する場合があります。
- セキュリティ用途では、カード固有の暗号・認証方式、鍵管理、リプレイ耐性、ログ保護を別途設計してください。
- 本プロジェクトの型名やコメントには `Athentication`、`Respose` など元コード由来の綴りが残っています。既存 API 互換を考える場合は変更に注意してください。

## ライセンス / 出典

この作業時点では、リポジトリ内に LICENSE ファイルや明示的なライセンス記述は見つかりませんでした。`AssemblyInfo.cs` には `Copyright © 2019` の記載がありますが、利用・再配布・改変条件は不明です。

出典およびライセンス条件は、fork 元の `TheTrueTrooper/NFC_CardReader_WinSCard_ACR122U_CSharp` と権利者の公開情報を確認してください。社内利用、再配布、商用利用、派生物公開を行う前にライセンス確認が必要です。
