# 機能洗い出し引き継ぎ

作成日: 2026-05-25

## 照合対象ドキュメント

- 仕様書原本: `API-ACR122U-2.04.md`
- 仕様書原本: `TSP-ACR122U-3.05.md`
- 設計班作成設計書: `docs/design/new-card-info-viewer-design.md`
- 参考既存仕様: `docs/specs/CardReader_TestConsole-機能仕様書.md`
- 参考既存実装: `NFC_CardReader` / `CardReader_TestConsole`

## 照合結果サマリー

設計書は、カード情報表示プログラムとして必要な安全境界、ATR / UID / ATS、リーダー情報、カード種別別 probe、JSON / Markdown 出力、トレース、イベント安定化まで広く含んでいる。

追加で設計班に引き継ぐべき漏れは、主に次の 4 系統である。

- リーダー制御のうち、設定系・状態変更系コマンドを通常表示から切り離した「診断モード」として明確化する。
- ATS / ATR / ACR122U error / DESFire status / FeliCa response など、人間向け解析辞書を独立コンポーネントとして厚くする。
- カード種別別 probe の結果を `取得成功` / `取得失敗` / `安全方針で未取得` / `対象外` として保存・出力する。
- 書き込み系、Value Block、Topaz / Ultralight の不可逆領域は、実装可能だが初期版では採用せず、将来の明示診断モード候補として分離する。

## 設計書に既に入っている機能

| 分類 | 既に含まれている内容 | 根拠 |
|---|---|---|
| リーダー情報 | PC/SC reader name、接続状態、PC/SC protocol、Firmware Version、PICC operating parameter、LED state、Contactless interface status | 設計書 6.1、API manual `Get Firmware Version` / `Get PICC Operating Parameter` / `Bi-color LED and Buzzer Control` / `Contactless Interface Status` |
| カード基本 | ATR、UID / NFC ID、ATS、推定規格、推定カード名、Historical bytes、通信速度、変調種別 | 設計書 6.2、API manual `ATR` / `Get Data` / `Contactless Interface Status` |
| ATR カード名推定 | MIFARE Classic 1K/4K、Ultralight、Mini、FeliCa 212/424K、Topaz/Jewel、Undefined SAK | 設計書 6.2、API manual `ATR` |
| ISO 14443 Type A/B | UID / ATS / upper response、ISO 7816-4 APDU、Direct Transmit は通常外 | 設計書 6.3、API manual `ISO 14443-4 PC/SC-Compliant Tags` |
| MIFARE Classic | キー未入力時のメモリマップ表示、キー指定時のみ key load / auth / read block、trailer mask | 設計書 6.4、API manual `Load Authentication Keys` / `Authentication` / `Read Binary Blocks` |
| MIFARE Ultralight | page 0-15 の読み取り候補、Serial / Lock / OTP / Data の分類、読み取り専用方針 | 設計書 6.5、API manual `MIFARE Ultralight メモリマップ` / `Read Binary Blocks` |
| FeliCa | NFC IDm、通信速度、明示指定サービス/ブロックのみ読み取り、総当たり禁止 | 設計書 6.6、API manual `FeliCa` |
| DESFire | ATR / ATS、UID、ISO 7816 wrapping の GetVersion、frame chaining の候補、mode 混在禁止 | 設計書 6.7、API manual `MIFARE DESFire` |
| Topaz/Jewel | ATR 推定、UID 相当、明示範囲読み取り候補、Read all は既定無効 | 設計書 6.8、API manual `NFC Forum Type 1 Tags / Topaz / Jewel` |
| 出力 | Console、JSON、Markdown、APDU trace、機密値マスク | 設計書 10、11、API manual `APDU/Response Flow` |
| 安全境界 | 残高・個人情報・認証回避・鍵総当たり・書き込み系の初期版除外 | 設計書 7、12、API manual `注意事項` |

## 追加で実装可能そうな機能候補

| 優先度 | 候補 | 仕様書上の根拠 | 実装難度/リスク | 設計班への反映依頼 |
|---|---|---|---|---|
| 高 | Probe 結果モデルの明確化 | API manual `Get Data`、`Read Binary Blocks`、`FeliCa`、`Topaz/Jewel`、`Error Codes` | 低。表示・出力モデル中心。カード別の失敗理由を揃える設計が必要 | `CardProbeResult` に `Success` / `Failed` / `SkippedByPolicy` / `NotApplicable` / `Unsupported` を持たせ、JSON / Markdown に必ず出す |
| 高 | ACR122U / Direct Transmit error code の人間向け辞書 | API manual `Error Codes`、早見表 `レスポンスコード` | 低。辞書実装。SW と Direct Transmit error の位置が異なる点に注意 | `ErrorCatalog` を設計に追加し、`90 00`、`63 00`、`6A 81`、`01` timeout、`14` MIFARE authentication error などを説明表示する |
| 高 | リーダー診断レポート | API manual `Get Firmware Version`、`Get PICC Operating Parameter`、`Contactless Interface Status`、TSP `USB / CCID` / `Smart Card Reader` | 低から中。カードなし/カードあり、Direct mode/shared mode の差を吸収する必要あり | `--diagnostic-report` で reader 名、Firmware、PICC parameter bit 展開、RF field、bit rate、modulation、TSP 静的仕様をまとめる |
| 中 | Timeout Parameter 設定の診断モード | API manual `Set Timeout Parameter` | 中。設定変更コマンドであり、通常表示では実行しない | `--set-timeout` のような明示オプション、現在値が取得できない場合の表示、実行前確認、実行ログを設計に追記する |
| 中 | カード検出時 buzzer 設定の診断モード | API manual `Set Buzzer Output During Card Detection`、TSP `Built-in Peripherals` | 中。現在値取得コマンドは仕様上見当たらず、設定変更だけになる | 通常起動では変更しない。`--set-detection-buzzer on/off` を将来候補にし、復元不能な現在値不明を明記する |
| 中 | アンテナ ON/OFF 診断 | API manual `Contactless Application Flow` の antenna power 例 | 中から高。カード検出を阻害し、テスト中に reader 状態を変える | 通常機能から除外し、保守/診断向けの明示コマンドとして二重確認付きにする |
| 中 | LED / Buzzer パターンテスト | API manual `Bi-color LED and Buzzer Control` | 中。物理出力なので自動テストしにくい。既存実装は T2 引数利用の再確認が必要 | LED state 読み取りとは別に `--test-led-buzzer` を設計し、時間・回数の上限を設ける |
| 中 | ATS 詳細 parser | API manual `ISO 14443 Part 4 PICC の ATR`、`Get Data` | 中。ATS の TL/T0/TA/TB/TC 解釈が必要 | 設計書の「将来解析」を Phase 2 の候補へ格上げし、raw と解析結果を併記する |
| 中 | ATR parser の失敗耐性と Card Name 未定義表示 | API manual `ATR` | 低から中。historical bytes 長、RID 不一致、`FF <SAK>` の扱いが必要 | `AtrParser` の出力に `ParseWarnings` と raw fallback を入れる |
| 中 | MIFARE Classic access bits / trailer 表示方針 | API manual `MIFARE Classic 1K メモリマップ`、`Authentication`、`Read Binary Blocks` | 中。trailer は機密・破損リスクがある | 読めた場合でも既定マスク。access bits の構造解析だけを明示オプション候補にする |
| 中 | MIFARE Classic Value Block 操作の将来診断候補 | API manual `Value Block Operation` / `Read Value Block` / `Restore Value Block` | 高。残高等の意味付けや改変リスクがある | 初期版は `Read Value Block` まで。store / increment / decrement / restore は所有テストカード限定の別 Issue に分離する |
| 中 | MIFARE Ultralight lock / OTP bit 展開 | API manual `MIFARE Ultralight メモリマップ`、`Read Binary Blocks` | 中。タグ派生品で意味が異なる可能性 | page 2/3 は raw に加えて「不可逆領域」警告と bit-level 表示候補を追加する |
| 低 | MIFARE Ultralight 書き込み候補 | API manual `Update Binary Blocks` | 高。OTP / lock / UID 近傍への誤書き込みが不可逆 | 初期版では実装しない。将来も page 4 以降、二重確認、読み戻し検証、テストカード限定にする |
| 中 | DESFire Native Mode と共通 frame chaining | API manual `MIFARE DESFire` の `ISO 7816-4 APDU Wrapping` / `Native Mode` | 中から高。status 位置が異なり、セッション内 mode 混在禁止 | Phase 2/3 の候補として、ISO wrapping と native を排他的 strategy に分離する |
| 中 | DESFire GetVersion の field 表示 | API manual `MIFARE DESFire` GetVersion chain | 中。DESFire 仕様の field 解釈が必要 | 初期は raw chain、次段で vendor/type/subtype/storage/UID 候補を解析する |
| 中 | FeliCa Read Without Encryption の response parser | API manual `FeliCa` | 中。status flag、service count、block count、block data の構造化が必要 | サービス/ブロック明示時だけ、IDm 照合・status flag・block data を構造表示する |
| 低 | Topaz/Jewel アドレス範囲読み取り | API manual `NFC Forum Type 1 Tags / Topaz / Jewel` | 中。Type 1 tag の内容そのものに踏み込む | 通常概要では ATR/UID まで。明示範囲指定で read byte、read all はオプトインにする |
| 低 | TSP 静的仕様のレポート出力 | TSP `重要パラメータ早見表`、`技術仕様`、`実装時の注意点` | 低。静的表示のみ | 診断レポートに周波数、速度、距離、one tag 制約、対応 OS は参考情報として載せる |

## 安全上・法的に実装しない/慎重に扱うべき機能

| 扱い | 機能 | 理由/根拠 |
|---|---|---|
| 実装しない | MIFARE Classic の未知キー探索、既定キー辞書の自動試行、総当たり | API manual `注意事項`、設計書 12。権限回避に該当し得る |
| 実装しない | FeliCa サービスコード総当たり、交通系カードの残高・履歴・個人情報抽出目的の機能 | API manual `FeliCa` / `注意事項`。公開領域以外は鍵や仕様が必要 |
| 実装しない | DESFire 認証回避、鍵回復、暗号化ファイル復号 | DESFire は認証・暗号通信が前提の領域がある |
| 初期版では実装しない | 書き込み系 APDU 全般 | API manual `Update Binary Blocks`、`Topaz/Jewel Update`、`注意事項`。カード破損・不可逆変更のリスク |
| 初期版では実装しない | Sector Trailer、access bits、lock bytes、OTP への書き込み | API manual `Update Binary Blocks` / `注意事項`。カードを読めなくする可能性 |
| 慎重扱い | Antenna ON/OFF、Timeout、検出時 buzzer など reader 状態変更 | API manual `Contactless Application Flow`、`Set Timeout Parameter`、`Set Buzzer Output During Card Detection`。通常表示から分離する |
| 慎重扱い | PC/SC Escape Command 有効化の自動化 | API manual `PC/SC Escape Command`。Windows レジストリ変更と管理者権限を伴う |
| 慎重扱い | APDU trace の raw payload 出力 | MIFARE key、FeliCa block data、trailer raw などが含まれ得るため、既定マスクが必要 |

## 設計班への具体的な反映依頼

1. `docs/design/new-card-info-viewer-design.md` に、本 handoff を参照資料として追記する。
2. `CardInfoModel` / `CardProbe` 周辺に、probe ごとの結果状態と未取得理由を明示する設計を追加する。
3. `ErrorCatalog` または同等の辞書コンポーネントを追加し、SW、ACR122U response、Direct Transmit error、DESFire status を人間向け表示に変換する。
4. リーダー制御系は `read-only default`、`diagnostic read`、`diagnostic setting change` の 3 段に分ける。
5. Timeout、検出時 buzzer、Antenna ON/OFF、LED/Buzzer pattern は通常表示から除外し、明示オプション・二重確認・ログ記録付きの将来候補にする。
6. DESFire は ISO wrapping と native mode の排他 strategy と、frame chaining helper を設計に追加する。
7. MIFARE Classic の Value Block store / increment / decrement / restore、Ultralight / Topaz 書き込みは、初期版の外に出し、所有テストカード限定の別 Issue とする。
8. JSON / Markdown export には、取得値だけでなく `SkippedByPolicy` と `NotApplicable` を出す。

## Issue 分割候補

- `ProbeResult` と未取得理由モデルを追加する
- ACR122U / Direct Transmit / DESFire のエラー辞書を追加する
- リーダー診断レポートを設計・実装する
- ATS / ATR 詳細 parser を追加する
- DESFire GetVersion の frame chaining と mode strategy を追加する
- FeliCa Read Without Encryption の明示読み取り parser を追加する
- 慎重扱いの書き込み/設定系コマンドを別 Issue に分離する

