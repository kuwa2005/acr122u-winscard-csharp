# NFC Card Reader WinSCard ACR122U CSharp

ACR122U USB NFC リーダーを Windows の PC/SC（WinSCard）経由で扱うための C# 実装です。`winscard.dll` を P/Invoke で呼び出す低レベルなラッパーと、ACS ACR122U の疑似 APDU / カード操作を扱いやすくするクラス、動作確認用のコンソールアプリで構成されています。

この README は、clone 済みのソースコードと親フォルダにある ACR122U 技術資料をもとに日本語で整理したものです。

## プロジェクト概要

このリポジトリには Visual Studio 形式のソリューション `NFC_CardReader.sln` が含まれています。

| プロジェクト | 種別 | 概要 |
|---|---|---|
| `NFC_CardReader` | クラスライブラリ | WinSCard / ACR122U / MIFARE Classic / NTAG 系カード操作のラッパー |
| `CardReader_TestConsole` | コンソールアプリ | ACR122U 接続、カード検出イベント、UID 取得などの動作確認用サンプル |

ターゲットフレームワークはいずれも `.NET Framework 4.6.2` です。SDK-style project ではなく、Visual Studio 2017 世代の旧形式 `.csproj` です。`Directory.Build.props` で `Microsoft.NETFramework.ReferenceAssemblies.net462` を復元し、.NET Framework 4.6.2 Targeting Pack がない環境でも `dotnet build` しやすくしています。

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
- Visual Studio 2017 以降、または MSBuild が使える環境
- .NET Framework 4.6.2 Developer Pack / Targeting Pack
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

コマンドラインでビルドする場合:

```powershell
dotnet build .\NFC_CardReader.sln --configuration Debug
```

または、Visual Studio Build Tools の MSBuild が PATH にある場合:

```powershell
msbuild .\NFC_CardReader.sln /p:Configuration=Debug
```

確認メモ:

- この環境では `dotnet` SDK 10.0.204 を使用しました。
- `Directory.Build.props` の参照アセンブリパッケージにより、`dotnet build .\NFC_CardReader.sln --configuration Debug` が成功することを確認しています。

## 配布とコード署名（SmartScreen 対策）

Windows 向けに exe を配布する場合は、SmartScreen の警告を減らすために Authenticode コード署名と一貫したリリース運用を行ってください。SmartScreen の保護を無効化したり、利用者へ警告を無視するよう促したりする手順は扱いません。

- EV または OV のコード署名証明書を正規の認証局から取得し、秘密鍵や PFX/P12 はリポジトリに含めないでください。
- `signtool.exe` で SHA-256 の Authenticode 署名を行い、信頼できるタイムスタンプ URL を指定してください。
- GitHub Releases など同じ配布元から継続して配布し、バージョン、リリースノート、チェックサムを整理してください。
- 新しい証明書や新しい成果物は、署名済みでも SmartScreen の reputation がすぐに付かない場合があります。

署名スクリプト雛形は `scripts/Sign-Release.ps1`、詳細なリリース手順は [docs/RELEASE-SIGNING.md](docs/RELEASE-SIGNING.md) を参照してください。

## 実行方法

Visual Studio から実行する場合:

1. `CardReader_TestConsole` をスタートアッププロジェクトに設定します。
2. ACR122U を接続します。
3. 対象カードを手元に用意します。
4. デバッグ実行します。
5. コンソールに ACR122U リーダー概要、PICC 設定、開始ステータス、カード検出/取り外し、カードサマリーなどが表示されます。

コマンドラインから実行する場合:

```powershell
.\CardReader_TestConsole\bin\Debug\CardReader_TestConsole.exe
```

現在の `CardReader_TestConsole/Program.cs` は、おおむね次の流れで動作します。

1. `ACR122UManager.GetACR122UReaders().FirstOrDefault()` で最初の ACR122U リーダーを選択
2. `ACR122UManager` を作成し、状態監視スレッドを開始
3. リーダー名と、取得できる場合は `FF 00 48 00 00` でファームウェアバージョンを取得
4. 資料ベースの ACR122U 仕様と実機取得情報を起動時に表示
5. PICC operating parameter を `AllOn` に設定
6. `GlobalCardCheck` で ATR をチェック
7. カード検出イベントを登録
8. 検出カードに対して公開 APDU (`FF CA 00 00 00`, `FF CA 01 00 00`) を試行し、UID、ATS、ATR 由来の推定カード種別、通信状態を表示
9. キー入力まで待機

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
