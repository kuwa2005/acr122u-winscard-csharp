# 新カード情報表示プログラム再設計

## 1. 設計目的

ACR122U USB NFC Reader を Windows の PC/SC / WinSCard 経由で扱い、仕様書上取得できるリーダー情報、カード基本情報、カード種別別の公開情報を、利用者が安全に確認できる新規カード情報表示プログラムとして設計する。

この設計は既存 `CardReader_TestConsole` の延長ではなく、仕様書 `API-ACR122U-2.04.md` と `TSP-ACR122U-3.05.md` を根拠にした新規アプリケーションを対象にする。既存実装のイベント重複や疑似 removed 対策は教訓として扱うが、既存クラスの流用を前提にしない。

## 2. 作り直す理由

現行 `CardReader_TestConsole` は動作確認用サンプルとして、リーダー検出、カード検出イベント、UID / ATS 表示、重複表示抑制を確認する目的に寄っている。カード情報をフルスペックに近い形で整理表示するには、次の点で再設計が必要になる。

- **責務が混在している**: PC/SC 接続、ACR122U 固有 APDU、カード分類、表示、イベント制御がサンプルアプリ内の都合で結合している。
- **カード種別別の探索モデルが不足している**: MIFARE Classic、Ultralight、DESFire、FeliCa、Topaz/Jewel などで、取得可能情報、認証要否、レスポンス形式が異なる。
- **安全境界を明示しにくい**: どこまでを公開情報として読むか、どこからを鍵・権限・個人情報領域として読まないかを、アプリの中心概念として持つ必要がある。
- **出力形式の拡張が難しい**: コンソール表示だけでなく JSON / Markdown / ログ出力を追加するには、取得モデルとレンダリングを分離した方がよい。
- **イベント設計を最初から安定化したい**: カード保持中の重複イベント、疑似 removed、空状態の連続通知を、状態機械として明示したい。

## 3. 名称案

- `Acr122uCardInspector`
- `Acr122uInfoViewer`
- `NfcCardInfoViewer`
- `Acr122uProbe`

以降の本文では仮称として `Acr122uCardInspector` を使う。

## 4. 対象環境

| 項目 | 方針 |
|---|---|
| OS | Windows |
| Reader | ACS ACR122U USB NFC Reader |
| Host API | PC/SC / WinSCard |
| Device class | USB CCID |
| Contactless | ISO 14443 Type A/B、MIFARE、FeliCa、ISO/IEC 18092 NFC tags |
| 実装候補 | C# / .NET。新規 console project から開始し、後で library と CLI を分離する |
| 既存コード | 原則として参照のみ。新規プロジェクトに ReaderSession / CardSession などを設計する |

`TSP-ACR122U-3.05.md` から確認できる ACR122U の仕様概要は、13.56 MHz、USB Full Speed 12 Mbps、非接触側 106 / 212 / 424 Kbps、PC/SC、CCID、ISO 14443 Type A/B、MIFARE、FeliCa、NFC tag 対応である。

## 5. 主要ユースケース

1. 利用者が ACR122U を接続し、利用可能な PC/SC reader name とファームウェアを確認する。
2. カードを 1 枚かざし、ATR、UID / NFC ID、ATS、推定規格、推定カード名、PC/SC protocol を表示する。
3. ATR と取得可能な公開 APDU から、カード種別別の表示セクションを出し分ける。
4. MIFARE Ultralight など認証不要の領域は、明示的な読み取り範囲内でページ表示する。
5. MIFARE Classic はキー未入力時にメモリマップと認証要否のみ表示し、キー入力がある場合だけ指定セクタを読む。
6. FeliCa は NFC IDm と、利用者が明示指定したサービス/ブロックの範囲だけを読む。
7. DESFire は UID / ATS と、必要に応じて `GetVersion` など公開候補コマンドの結果を表示する。
8. 結果をコンソールに表示し、必要に応じて JSON / Markdown にエクスポートする。

## 6. 表示できる情報の棚卸し

### 6.1 リーダー情報

| 情報 | 取得方法 | 表示方針 |
|---|---|---|
| PC/SC reader name | `SCardListReaders` | すべての reader を列挙し、ACR122U 候補を選択表示 |
| 接続状態 | `SCardGetStatusChange` / `SCardStatus` | reader present / card present / empty などを状態名と raw 値で表示 |
| PC/SC protocol | `SCardConnect` / `SCardStatus` | `T=0` / `T=1` / raw value。ACR122U PICC Interface は T=1 想定 |
| Firmware version | `FF 00 48 00 00` | ASCII と hex を表示。例: `ACR122U201` |
| PICC operating parameter | `FF 00 50 00 00` | `90 <param>` を bit ごとに展開 |
| Auto PICC polling | PICC parameter bit 7 | enabled / disabled |
| Auto ATS generation | PICC parameter bit 6 | enabled / disabled。MIFARE 検出時の注意を併記 |
| Polling interval | PICC parameter bit 5 | 250 ms / 500 ms |
| 検出対象 | PICC parameter bit 4..0 | FeliCa 424K、FeliCa 212K、Topaz、Type B、Type A |
| Buzzer on detection | `FF 00 52 <status> 00` は設定系 | 初期表示では変更しない。将来は現在設定を推定できない場合「未取得」とする |
| LED state | `FF 00 40 00 04 00 00 00 00` | 返却 `90 <state>` から赤/緑 LED 状態を表示可能 |
| Contactless interface status | `FF 00 00 00 02 D4 04` | `Err`、RF field、target 数、bit rate、modulation type を表示 |
| Timeout parameter | `FF 00 41 <timeout> 00` は設定系 | 初期表示では変更しない。変更機能は診断モードだけに限定 |

初期実装では、リーダー設定を変更するコマンドは実行しない。設定変更は将来の `--diagnostic-write-reader-settings` のような明示オプションに分離する。

### 6.2 カード基本情報

| 情報 | 取得方法 | 表示方針 |
|---|---|---|
| ATR | PC/SC card state / `SCardStatus` | hex、長さ、historical bytes、解析結果を表示 |
| UID / NFC ID | `FF CA 00 00 00` | hex、byte length。本人確認の根拠にしない注意を表示 |
| ATS | `FF CA 01 00 00` | 取得成功時は hex、TL / T0 / TA / TB / TC 候補を将来解析 |
| 推定規格 | ATR historical bytes / contactless status | ISO 14443-3/4 Type A/B、FeliCa、Topaz/Jewel など |
| 推定カード名 | ATR の Card Name | MIFARE Classic 1K/4K、Ultralight、Mini、FeliCa 212/424K、Topaz/Jewel など |
| Historical bytes | ATR から抽出 | raw hex と PC/SC RID / Standard / Card Name を表示 |
| PC/SC protocol | `SCardStatus` | protocol 名と raw 値 |
| 通信速度 | contactless interface status | 106 / 212 / 424 Kbps |
| 変調種別 | contactless interface status | ISO 14443 or MIFARE、FeliCa、Active mode、Jewel |

ATR の Card Name は `API-ACR122U-2.04.md` の表を基準にする。

| Card Name | 表示名 |
|---|---|
| `00 01` | MIFARE Classic 1K |
| `00 02` | MIFARE Classic 4K |
| `00 03` | MIFARE Ultralight |
| `00 26` | MIFARE Mini |
| `F0 04` | Topaz / Jewel |
| `F0 11` | FeliCa 212K |
| `F0 12` | FeliCa 424K |
| `FF <SAK>` | Undefined / SAK 付き |

### 6.3 ISO 14443 Type A / Type B

| 項目 | Type A | Type B |
|---|---|---|
| ATR | PC/SC から取得 | PC/SC から取得 |
| UID | `FF CA 00 00 00` | カード/ドライバ依存。取得不可の場合は失敗として表示 |
| ATS / 上位応答 | `FF CA 01 00 00` | Type B の ATTRIB / ATQB 上位応答が historical bytes に現れる可能性 |
| ISO 14443-4 APDU | ISO 7816-4 APDU を送信可能 | ISO 7816-4 APDU を送信可能 |
| 表示方針 | UID、ATS、historical bytes、protocol、推定種別 | ATR、historical bytes、protocol、取得できる上位応答 |

Type A/B の低レベル anti-collision や任意の RF フレーム送信は、カード情報表示の通常機能には含めない。必要な場合のみ Direct Transmit の診断機能として設計する。

### 6.4 MIFARE Classic 1K / 4K / Mini

MIFARE Classic はセクタごとに Key A / Key B 認証が必要である。キーがない状態では、次だけを表示する。

- ATR、UID、推定カード名
- 1K / 4K / Mini の推定メモリ構造
- セクタ数、ブロック数、trailer block の位置
- 認証が必要なため読まなかったセクタ一覧
- 読み取りには利用者が所有・許可されたカードとキーを指定する必要がある旨

認証ありモードでは、利用者が明示入力したキーだけを volatile slot にロードする。

| 操作 | APDU | 制約 |
|---|---|---|
| Key load | `FF 82 00 <KeyNo> 06 <Key6>` | volatile key slot `00` - `01`。キーはログに出さない |
| Authenticate | `FF 86 00 00 05 01 00 <Block> <60/61> <KeyNo>` | Key A=`60`、Key B=`61` |
| Read block | `FF B0 00 <Block> 10` | Classic は 16 bytes |
| Read value block | `FF B1 00 <Block> 04` | 表示候補。残高などの意味付けはしない |

メモリマップ表示案:

| カード | 表示 |
|---|---|
| Classic 1K | sector 0-15、各 4 block、trailer は 3/7/.../63 |
| Classic 4K | sector 0-31 は各 4 block、sector 32-39 は各 16 block |
| Mini | Card Name から推定し、詳細はカード仕様に委ねる |

表示時は `data block`、`sector trailer`、`key/access bits` を区別する。Sector Trailer の Key A/B と access bits はカード運用上の機密に近いため、読み取れた場合でも既定では値をマスクし、`--show-trailer-raw` のような明示オプションなしには raw 表示しない。

### 6.5 MIFARE Ultralight

MIFARE Ultralight は仕様書上、認証不要で読める領域がある。初期実装では読み取り専用とし、書き込みは実装しない。

| ページ | 用途 | 表示 |
|---|---|---|
| 0-1 | Serial Number | UID と突合し、raw bytes 表示 |
| 2 | BCC / Internal / Lock | lock bytes を bit 展開候補 |
| 3 | OTP | raw bytes と「不可逆領域」注意 |
| 4-15 | Data | 4 bytes page と ASCII preview |

読み取り APDU:

- 1 page: `FF B0 00 <Page> 04`
- 4 pages 相当: `FF B0 00 <Page> 10`

将来、NTAG 系や Ultralight C / EV1 を扱う場合は、GET_VERSION 相当や容量推定を別 probe として追加する。ただしパスワード保護領域は既定では読まない。

### 6.6 FeliCa

FeliCa は ATR Card Name `F0 11` / `F0 12`、または contactless status の modulation type から推定する。基本表示は次に限定する。

| 項目 | 取得方法 | 表示 |
|---|---|---|
| NFC IDm | `FF CA 00 00 00` | 8 bytes 想定。hex 表示 |
| ATR | PC/SC | FeliCa 212K / 424K 推定 |
| 通信速度 | Contactless status | 212 / 424 Kbps |
| サービス/ブロック読み取り | FeliCa native command / Direct Transmit | 利用者がサービスコードとブロックを明示した場合のみ |

Direct Transmit 例:

```text
FF 00 00 00 <Lc> D4 40 01 10 06 <8-byte NFC ID> 01 09 01 01 80 00
```

制約:

- Suica / PASMO などの交通系カードから残高、履歴、個人情報を抽出する目的では実装しない。
- サービスコードを総当たりしない。
- 利用者が所有し、読み取り権限と目的が明確なカードの公開サービスだけを扱う。
- レスポンスは IDm、status flag、block data の構造として表示し、業務的な意味付けは行わない。

### 6.7 MIFARE DESFire

DESFire は ISO 14443-4 カードとして ATR / ATS を表示し、追加情報は公開的な識別・バージョン取得候補に限定する。

| 項目 | 取得方法 | 表示 |
|---|---|---|
| ATR / ATS | PC/SC / `FF CA 01 00 00` | ISO 14443-4 として表示 |
| UID | `FF CA 00 00 00` | 取得可能な場合のみ |
| Mode | 最初に送る APDU で ISO wrapping / native が決まる | 1 セッションで混在させない |
| GetVersion | ISO wrapping: `90 60 00 00 00` + `90 AF 00 00 00` chain | hardware/software/storage/UID など raw field 候補 |

DESFire のファイル一覧、アプリケーション一覧、認証、暗号化通信、ファイル読み取りは、本設計の初期範囲外とする。実装する場合でも権限と鍵があるカードだけを対象にし、鍵や暗号処理はログに出さない。

### 6.8 Topaz / Jewel / NFC Forum Type 1

Topaz / Jewel は ATR Card Name `F0 04` から推定する。

| 項目 | 取得方法 | 表示 |
|---|---|---|
| ATR | PC/SC | Topaz / Jewel 推定 |
| UID 相当 | `FF CA 00 00 00` が成功した場合 | hex 表示 |
| Read byte | native `01 <addr>` または Direct Transmit | 診断モード候補 |
| Read all | native `00` | 通常機能では既定無効 |

Type 1 tag の全メモリ読み取りはカード内容そのものに踏み込むため、既定のカード概要表示では行わない。利用者が明示指定したアドレス範囲だけを読み取り候補にする。

## 7. 取得しない/できない情報

このプログラムはカード情報表示を目的にするが、次の情報は取得しない、または取得できないものとして明確に扱う。

- 暗号鍵が必要な領域
- 所有者・発行者の許可がないカードデータ
- 残高、乗降履歴、決済履歴、個人情報、社員番号など、利用者の権限や法的根拠が不明な情報
- MIFARE Classic の未知キー探索、既定キー総当たり、辞書攻撃
- FeliCa サービスコードの総当たり
- DESFire の認証回避、鍵回復、暗号化ファイルの復号
- Sector Trailer、OTP、lock bytes、アクセス条件への書き込み
- Reader 設定の永続変更を伴う操作

表示では「取得失敗」と「安全方針により未取得」を区別する。例:

- `取得失敗: SW=6A81 (機能未サポート)`
- `未取得: MIFARE Classic sector 4 はキー未指定のため読み取りません`
- `未対象: FeliCa サービス自動探索は行いません`

## 8. APDU / PC/SC レイヤ設計

### 8.1 コンポーネント責務

| コンポーネント | 責務 |
|---|---|
| `PcscContext` | `SCardEstablishContext` / `SCardReleaseContext`、reader 一覧取得 |
| `ReaderCatalog` | ACR122U 候補 reader の抽出、reader name の正規化 |
| `ReaderSession` | reader への接続、direct/shared mode、reader command 実行、reader status 取得 |
| `CardSession` | カード接続、ATR / protocol / status の保持、カード単位の transmit |
| `ApduTransceiver` | APDU 送受信、timeout、SW 解析、trace 記録 |
| `Acr122uReaderProbe` | firmware、PICC parameter、LED state、contactless status の取得 |
| `AtrParser` | ATR historical bytes、PC/SC RID、standard、card name の解析 |
| `CardClassifier` | ATR / UID / ATS / contactless status からカード種別を推定 |
| `CardProbe` | カード基本情報を収集し、種別別 probe を呼び出す |
| `MifareClassicProbe` | メモリマップ生成、認証あり読み取り、trailer マスク |
| `UltralightProbe` | ページ読み取り、OTP / lock / data 領域の分類 |
| `FelicaProbe` | IDm 表示、明示指定サービス/ブロックの読み取り |
| `DesfireProbe` | ATS、mode 方針、GetVersion 候補 |
| `TopazProbe` | Topaz/Jewel の明示範囲読み取り候補 |
| `CardInfoModel` | 表示に依存しない結果モデル |
| `SummaryRenderer` | コンソール向けの要約表示 |
| `JsonExporter` | JSON 出力 |
| `MarkdownExporter` | Markdown / 診断レポート出力 |
| `TraceSink` | APDU trace、イベント trace、機密値マスク |

### 8.2 APDU 結果モデル

`ApduResponse` はレスポンス形式の違いを吸収しすぎない。PC/SC 標準 APDU、ACR122U pseudo APDU、DESFire native mode では status の位置が違うため、次のように扱う。

| モデル | 内容 |
|---|---|
| `CommandBytes` | 送信 APDU。キーや機密 payload はマスク可能にする |
| `RawResponse` | 受信 raw bytes |
| `ResponseData` | SW を除いた data。SW がない形式では raw を維持 |
| `StatusKind` | `IsoStatusWord` / `Acr122uStatus` / `DesfireStatus` / `NativeStatus` / `Unknown` |
| `StatusCode` | `90 00`、`91 AF`、`AF` など |
| `IsSuccess` | status kind ごとの成功判定 |
| `Elapsed` | 送受信時間 |
| `Error` | WinSCard 例外、timeout、unsupported など |

### 8.3 代表 APDU

| 目的 | APDU | 備考 |
|---|---|---|
| UID 取得 | `FF CA 00 00 00` | 4/7/10 bytes などカード依存 |
| ATS 取得 | `FF CA 01 00 00` | ISO 14443 Type A の ATS |
| Firmware 取得 | `FF 00 48 00 00` | ASCII 文字列 |
| PICC parameter 取得 | `FF 00 50 00 00` | `90 <parameter>` |
| Contactless status | `FF 00 00 00 02 D4 04` | `D5 05 ... 90 00` |
| Direct Transmit | `FF 00 00 00 <Lc> <payload>` | FeliCa / Topaz / PN532 系 |
| MIFARE key load | `FF 82 00 <KeyNo> 06 <Key6>` | キーはログに出さない |
| MIFARE auth | `FF 86 00 00 05 01 00 <Block> <60/61> <KeyNo>` | Classic のみ |
| MIFARE read | `FF B0 00 <Block/Page> <Len>` | Classic 16 bytes、Ultralight 4/16 bytes |
| DESFire GetVersion | `90 60 00 00 00` / `90 AF 00 00 00` | ISO wrapping mode |

## 9. イベント設計

### 9.1 状態機械

カード監視は `SCardGetStatusChange` の生イベントをそのまま UI に出さず、次の状態機械に通す。

| 状態 | 意味 |
|---|---|
| `ReaderMissing` | reader がない |
| `ReaderReadyEmpty` | reader はあり、カードなし |
| `CardCandidate` | カードあり状態を検出したが、安定確認前 |
| `CardPresentStable` | 同一 ATR / UID が安定している |
| `CardProcessing` | probe 実行中 |
| `CardDisplayed` | 表示済み。同一カードの再表示を抑制 |
| `RemovalCandidate` | removed 相当を検出したが、再確認前 |
| `CardRemovedStable` | カードなしが安定し、ゲート解除済み |
| `ReaderError` | WinSCard / reader error |

### 9.2 重複・疑似イベント対策

- `CardCandidate` では短い debounce を置き、ATR と PC/SC 状態を再取得する。
- `RemovalCandidate` では contactless status または PC/SC state を再確認し、カードありなら疑似 removed として無視する。
- 同一カード判定キーは `readerName + ATR + UID/NFC ID + protocol` を基本にする。
- `CardDisplayed` 中に同一キーの検出イベントが来ても再 probe しない。
- カードが実際に `ReaderReadyEmpty` へ遷移したときだけ表示ゲートを解除する。
- probe 中にカードが外れた場合は `CardLostDuringProbe` として途中結果を破棄または partial 表示にする。
- 監視スレッドと UI 出力は channel / queue で分離し、同時出力を避ける。

## 10. 表示設計

### 10.1 コンソール表示

初期表示:

```text
ACR122U Card Inspector
Reader: ACS ACR122U PICC Interface 0
Firmware: ACR122U201
PICC Parameter: FF
  Auto PICC Polling: Enabled
  Auto ATS Generation: Enabled
  Polling Interval: 250 ms
  Targets: Type A, Type B, Topaz, FeliCa 212K, FeliCa 424K
```

カード表示:

```text
Card Summary
  ATR: ...
  UID/NFC ID: ...
  ATS: ...
  Estimated Standard: ISO 14443-4 Type A
  Estimated Card: MIFARE DESFire
  PC/SC Protocol: T=1
  Historical Bytes: ...

Reader RF Status
  RF Field: Present
  Bit Rate RX/TX: 424 Kbps / 424 Kbps
  Modulation: ISO 14443 or MIFARE

Security Boundary
  Protected, encrypted, balance, and personal data are not read.
```

### 10.2 JSON 出力

`--json <path>` で `CardInfoModel` を機械可読に出力する。APDU trace は既定では含めず、`--include-trace` 指定時のみ機密値をマスクして含める。

### 10.3 Markdown / ログ出力

`--markdown <path>` では実機確認レポートとして、reader 情報、カード概要、未取得理由、エラー、APDU 一覧を出す。ログは `logs/` 配下へ出し、Git 管理対象外にする。

## 11. エラー / タイムアウト / トレース設計

| 種別 | 方針 |
|---|---|
| WinSCard error | error code、API 名、reader/card 状態を表示 |
| APDU status error | SW / ACR122U error code と意味を表示 |
| timeout | 操作名、elapsed、再試行有無を表示 |
| unsupported | `6A 81` などは「仕様またはカードが未サポート」として扱う |
| card removed | probe 中断、partial result の扱いを明示 |
| trace | 時刻、連番、thread id、状態遷移、APDU 名、status、elapsed |
| secret masking | MIFARE key、trailer raw、FeliCa block data の任意指定部分はマスク可能 |

Direct Transmit のエラーコードは、`00` No Error、`01` Time Out、`02` CRC error、`03` Parity error、`14` MIFARE authentication error などを辞書化する。

## 12. セキュリティ / 倫理 / 法的注意

- このプログラムはカード診断と公開情報の表示を目的にする。
- 所有権、利用規約、法令、発行者ポリシーに反する読み取りを支援しない。
- 残高、履歴、個人情報、認証済みデータ領域を抽出する機能は既定で実装しない。
- 鍵の総当たり、既知キー辞書の自動試行、権限回避は実装しない。
- 認証キーは標準入力や安全な設定経由で受け取り、ログ、JSON、Markdown、例外メッセージに出さない。
- 書き込み系 APDU は初期版では実装しない。将来も診断用の明示オプションと二重確認を必須にする。
- UID は識別子であり、本人確認や認証の唯一の根拠として扱わない。

## 13. 段階的実装計画

### Phase 1: リーダー検出 / ファーム取得 / ATR UID ATS 表示

- 新規 console project `Acr122uCardInspector` を追加する。
- `PcscContext`、`ReaderCatalog`、`ReaderSession`、`CardSession`、`ApduTransceiver` を最小実装する。
- reader 一覧、ACR122U 候補選択、firmware version、PICC operating parameter を表示する。
- カード検出時に ATR、UID、ATS、historical bytes、PC/SC protocol、推定カード名を表示する。
- 既定ではカード内容ブロックを読まない。

### Phase 2: カード分類 / 規格別詳細表示

- `AtrParser` と `CardClassifier` を実装する。
- ISO 14443 Type A/B、MIFARE Classic、Ultralight、DESFire、FeliCa、Topaz/Jewel の表示セクションを分ける。
- Ultralight のページ 0-15 読み取りを明示オプションで実装する。
- DESFire GetVersion の ISO wrapping 候補を実装し、mode 混在を防ぐ。
- FeliCa は IDm と通信速度のみを既定表示にし、サービス読み取りは明示指定に限定する。

### Phase 3: 認証キー入力がある場合のみ MIFARE 読み取り

- MIFARE Classic のメモリマップを表示する。
- キー未入力時は未取得理由だけを表示する。
- キー入力時のみ `LoadKey`、`Authenticate`、指定 sector/block の読み取りを実行する。
- Sector Trailer は既定マスクし、キーや access bits を不用意に出さない。
- 認証失敗、カード取り外し、権限なしを明確に表示する。

### Phase 4: JSON / Markdown export, tests, release signing

- `CardInfoModel` を JSON / Markdown に出力する。
- APDU mock と WinSCard adapter mock で単体テストを作る。
- 実機ログのサンプルを個人情報なしで検証する。
- Release ビルド、配布、コード署名、チェックサム、リリースノートを整備する。

## 14. テスト計画

| テスト | 内容 |
|---|---|
| ビルド | `dotnet build .\NFC_CardReader.sln --configuration Release` |
| Reader なし | reader 未接続で分かりやすいエラーを出す |
| Reader あり / card なし | firmware、PICC parameter、empty state を表示 |
| ISO 14443 Type A | ATR、UID、ATS、protocol を確認 |
| MIFARE Classic 1K | キーなしでメモリマップのみ表示、キーありで指定 block のみ読む |
| MIFARE Classic 4K | sector 32 以降の 16 block sector 表示を確認 |
| Ultralight | page 0-15 の分類表示、lock/OTP 注意を確認 |
| DESFire | ATS と GetVersion chain の status 解析 |
| FeliCa | IDm、FeliCa 212/424K 推定、サービス未指定時に読まないこと |
| Topaz/Jewel | ATR 推定と明示範囲読み取りの扱い |
| イベント | 同一カード保持中の重複表示抑制、実 removed 後のゲート解除 |
| 疑似 removed | removed 相当後に再確認し、カードありなら無視 |
| Timeout | APDU timeout と card lost を区別 |
| Mock APDU | UID / ATS / firmware / PICC parameter / contactless status のレスポンス解析 |
| 出力 | Console / JSON / Markdown の snapshot テスト |

実機テストログは `logs/` 配下に保存し、コミットしない。テストカードの UID などを公開資料に載せる場合はマスクする。

## 15. Issue 分割案

親 Issue:

- 再設計: 仕様書ベースの新カード情報表示プログラムを設計する

実装時の候補 Issue:

1. `Acr122uCardInspector` 新規プロジェクトを追加し、reader / card session を実装する
2. ATR / UID / ATS / PICC parameter の表示モデルを実装する
3. ATR parser と CardClassifier を実装する
4. カード検出状態機械と重複イベント抑制を実装する
5. Ultralight / DESFire / FeliCa / Topaz の読み取り境界を実装する
6. MIFARE Classic のキー指定読み取りとメモリマップ表示を実装する
7. JSON / Markdown export と APDU mock テストを追加する
8. Release ビルド、署名、配布手順を整備する

まずは親 Issue で本設計書を紐づけ、Phase 1 実装に入る時点で必要な子 Issue を切り出す。

## 16. 参照仕様書

- `API-ACR122U-2.04.md`
- `TSP-ACR122U-3.05.md`

