# リリースとコード署名

Windows 向けに `CardReader_TestConsole.exe` を配布する場合は、SmartScreen の警告を減らすために正規の Authenticode コード署名と一貫した配布元を使います。SmartScreen の評価は Microsoft 側の reputation に依存するため、署名した直後の新しい証明書や新しい実行ファイルでは警告がすぐに消えない場合があります。

Release ビルド時の署名対象は次の実行ファイルです。

```text
CardReader_TestConsole\bin\Release\CardReader_TestConsole.exe
```

## 推奨方針

- EV または OV のコード署名証明書を正規の認証局から取得する。
- `signtool.exe` で SHA-256 の Authenticode 署名を行う。
- 信頼できるタイムスタンプ局を `/tr` と `/td SHA256` で指定し、証明書更新後も署名時点の有効性を確認できるようにする。
- GitHub Releases など、同じリポジトリ・同じ配布元から継続してリリースする。
- リリースごとにファイル名、バージョン、リリースノート、チェックサムを整理し、利用者が配布元と成果物を確認できる状態にする。
- 証明書、秘密鍵、PFX/P12、パスワード、署名ログはリポジトリにコミットしない。

## 署名手順

Release ビルド後、Windows SDK の `signtool.exe` が使える端末で署名します。証明書は正規の認証局から発行された EV または OV 証明書を使い、PFX ファイルまたは Windows 証明書ストアのサムプリントで指定できます。

PFX ファイルを使う例:

```powershell
$env:SIGN_CERT_PATH = "C:\secure\codesigning.pfx"
$env:SIGN_CERT_PASSWORD = "PFX のパスワード"
$env:SIGN_TIMESTAMP_URL = "http://timestamp.digicert.com"
.\scripts\Sign-Release.ps1 .\CardReader_TestConsole\bin\Release\CardReader_TestConsole.exe
```

証明書ストアのサムプリントを使う例:

```powershell
$env:SIGN_CERT_THUMBPRINT = "証明書の SHA-1 サムプリント"
$env:SIGN_CERT_STORE_NAME = "My"
$env:SIGN_TIMESTAMP_URL = "http://timestamp.digicert.com"
.\scripts\Sign-Release.ps1 .\CardReader_TestConsole\bin\Release\CardReader_TestConsole.exe
```

`SIGNTOOL_PATH` を指定すると、PATH 上にない Windows SDK の `signtool.exe` を明示できます。`SIGN_CERT_STORE_LOCATION=LocalMachine` を指定した場合はローカルコンピューターの証明書ストアを使います。

## リリース前チェック

1. `AssemblyInfo.cs` の `Company`、`Product`、`Description`、`Version` がリリース内容と一致していることを確認する。
2. `CardReader_TestConsole/app.manifest` の `requestedExecutionLevel` が `asInvoker` のままであることを確認する。管理者権限が不要なアプリでは昇格要求を追加しない。
3. `dotnet build .\NFC_CardReader.sln --configuration Release` で Release 構成のビルドが成功することを確認する。
4. `CardReader_TestConsole\bin\Release\CardReader_TestConsole.exe` を実機で通常起動し、起動時リーダー概要とカード検出時のカードサマリーが表示されることを確認する。
5. 必要に応じて `--trace` または `ACR122U_TRACE=1` でトレースログを確認する。`logs/` と `*.log` は Git 管理対象外のままにする。
6. 生成された exe を Authenticode 署名し、タイムスタンプが付いていることを確認する。
7. 署名後の成果物を GitHub Releases など一貫した配布元へアップロードする。
8. リリースノートにバージョン、対象 OS、必要な ACR122U ドライバ、既知の注意点、チェックサムを記載する。

## 注意

この文書は SmartScreen を無効化したり、警告を無視させたりする手順ではありません。配布者として信頼できる署名と配布運用を整えるための手順です。