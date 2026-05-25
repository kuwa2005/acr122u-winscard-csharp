using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Acr122uCardInspector
{
    internal static class Program
    {
        private const int ExitOk = 0;
        private const int ExitInvalidOption = 1;
        private const int ExitReaderError = 2;

        private static int Main(string[] args)
        {
            TrySetUtf8Console();

            CliParseResult parseResult = CliOptions.Parse(args);
            if (!parseResult.IsValid)
            {
                Console.Error.WriteLine("エラー: " + parseResult.ErrorMessage);
                Console.Error.WriteLine("使い方は `Acr122uCardInspector --help` を確認してください。");
                return ExitInvalidOption;
            }

            CliOptions options = parseResult.Options;
            if (options.ShowHelp)
            {
                SummaryRenderer.RenderHelp();
                return ExitOk;
            }

            if (options.ShowVersion)
            {
                Console.WriteLine("Acr122uCardInspector " + VersionInfo.InformationalVersion);
                return ExitOk;
            }

            ProbeResult result = null;
            try
            {
                using (TraceSink trace = new TraceSink(options.Trace))
                {
                    result = new AppRunner(trace).Run(options);
                }

                if (options.Json)
                {
                    JsonExporter.Write(result, options.JsonPath);
                }
                else if (!result.OutputAlreadyRendered)
                {
                    SummaryRenderer.Render(result);
                }

                return result.ExitCode;
            }
            catch (Exception ex)
            {
                result = ProbeResult.Create(options);
                result.State = CardMonitorState.ReaderError.ToString();
                result.ExitCode = ExitReaderError;
                result.Errors.Add(ProbeError.FromException("UnhandledException", "未処理例外が発生しました。", ex));

                if (options.Json)
                {
                    JsonExporter.Write(result, options.JsonPath);
                }
                else
                {
                    Console.Error.WriteLine("エラー: " + ex.Message);
                }

                return ExitReaderError;
            }
        }

        private static void TrySetUtf8Console()
        {
            try
            {
                Console.OutputEncoding = Encoding.UTF8;
                Console.InputEncoding = Encoding.UTF8;
            }
            catch
            {
                // 出力先がリダイレクトされている場合などは既定エンコーディングに任せる。
            }
        }
    }

    internal sealed class AppRunner
    {
        private const int ExitReaderError = 2;
        private const int ExitNoCard = 3;
        private readonly TraceSink _trace;

        public AppRunner(TraceSink trace)
        {
            _trace = trace;
        }

        public ProbeResult Run(CliOptions options)
        {
            ProbeResult result = ProbeResult.Create(options);
            bool singleRun = options.Once || options.Json;
            result.Mode = singleRun ? "once" : "watch";

            try
            {
                using (PcscContext context = PcscContext.Establish(_trace))
                {
                    List<string> readers = context.ListReaders();
                    result.ReaderNames.AddRange(readers);

                    if (readers.Count == 0)
                    {
                        result.State = CardMonitorState.ReaderMissing.ToString();
                        result.ExitCode = ExitReaderError;
                        result.Errors.Add(new ProbeError("ReaderNotFound", "PC/SC リーダーが見つかりません。ACR122U の接続と Smart Card サービスを確認してください。"));
                        result.ProbeItems.Add(ProbeItem.Failed("Reader.List", "PC/SC リーダー一覧", "リーダーが 0 件でした。"));
                        return result;
                    }

                    ReaderSelection selection = ReaderCatalog.Select(readers, options.ReaderName);
                    if (!selection.Success)
                    {
                        result.State = CardMonitorState.ReaderMissing.ToString();
                        result.ExitCode = ExitReaderError;
                        result.Errors.Add(new ProbeError("ReaderSelectionFailed", selection.ErrorMessage));
                        result.ProbeItems.Add(ProbeItem.Failed("Reader.Select", "リーダー選択", selection.ErrorMessage));
                        return result;
                    }

                    result.SelectedReader = selection.ReaderName;
                    result.ReaderInfo = ReaderInfo.FromReaderName(selection.ReaderName, selection.Warning);

                    using (ReaderSession readerSession = new ReaderSession(context, selection.ReaderName, _trace))
                    {
                        readerSession.ProbeReader(result);

                        if (singleRun)
                        {
                            ProbeOnce(context, readerSession, result, options.IdentityKey);
                            return result;
                        }

                        result.OutputAlreadyRendered = true;
                        SummaryRenderer.Render(result);
                        Console.WriteLine();
                        Console.WriteLine("カード監視を開始します。終了するには Ctrl+C を押してください。");
                        new CardStateMachine(context, readerSession, _trace, options.IdentityKey).Watch();
                        result.ExitCode = 0;
                        return result;
                    }
                }
            }
            catch (PcscException ex)
            {
                result.State = CardMonitorState.ReaderError.ToString();
                result.ExitCode = ExitReaderError;
                result.Errors.Add(ProbeError.FromPcsc("Pcsc", "PC/SC 操作に失敗しました。", ex));
                return result;
            }
        }

        private void ProbeOnce(PcscContext context, ReaderSession readerSession, ProbeResult result, string identityKey)
        {
            CardStateSnapshot snapshot = CardStateMachine.GetStableSnapshot(context, readerSession.ReaderName, _trace);
            result.State = snapshot.State.ToString();

            if (!snapshot.CardPresent)
            {
                result.ExitCode = ExitNoCard;
                result.Errors.Add(new ProbeError("CardNotPresent", "カードが検出されませんでした。カードを 1 枚だけリーダーにかざして再実行してください。"));
                result.ProbeItems.Add(ProbeItem.NotApplicable("Card.Basic", "カード基本情報", "カード未検出のため ATR / UID / ATS は取得していません。"));
                return;
            }

            result.State = CardMonitorState.CardProcessing.ToString();
            using (CardSession cardSession = CardSession.Connect(readerSession.Context, readerSession.ReaderName, _trace))
            {
                new CardProbe(_trace, identityKey).Probe(cardSession, result);
            }

            result.State = CardMonitorState.CardDisplayed.ToString();
            result.ExitCode = 0;
        }
    }

    internal sealed class CliOptions
    {
        public const string DefaultIdentityKey = "0000";

        public bool ShowHelp { get; private set; }
        public bool ShowVersion { get; private set; }
        public bool Trace { get; private set; }
        public bool Json { get; private set; }
        public bool Once { get; private set; }
        public string JsonPath { get; private set; }
        public string ReaderName { get; private set; }
        public string IdentityKey { get; private set; }

        public static CliParseResult Parse(string[] args)
        {
            CliOptions options = new CliOptions { IdentityKey = DefaultIdentityKey };

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg == "--help" || arg == "-h")
                {
                    options.ShowHelp = true;
                }
                else if (arg == "--version")
                {
                    options.ShowVersion = true;
                }
                else if (arg == "--trace")
                {
                    options.Trace = true;
                }
                else if (arg == "--once")
                {
                    options.Once = true;
                }
                else if (arg == "--json")
                {
                    options.Json = true;
                    if (i + 1 < args.Length && !IsOption(args[i + 1]))
                    {
                        options.JsonPath = args[++i];
                    }
                }
                else if (arg == "--reader")
                {
                    if (i + 1 >= args.Length || IsOption(args[i + 1]))
                    {
                        return CliParseResult.Invalid("--reader にはリーダー名を指定してください。");
                    }

                    options.ReaderName = args[++i];
                }
                else if (arg == "--identity-key")
                {
                    if (i + 1 >= args.Length || IsOption(args[i + 1]))
                    {
                        return CliParseResult.Invalid("--identity-key には識別コード生成用キーを指定してください。");
                    }

                    options.IdentityKey = args[++i];
                }
                else
                {
                    return CliParseResult.Invalid("不明なオプションです: " + arg);
                }
            }

            if (options.ShowHelp && args.Length > 1)
            {
                return CliParseResult.Invalid("--help は単独で指定してください。");
            }

            if (options.ShowVersion && args.Length > 1)
            {
                return CliParseResult.Invalid("--version は単独で指定してください。");
            }

            return CliParseResult.Valid(options);
        }

        private static bool IsOption(string value)
        {
            return value != null && value.StartsWith("--", StringComparison.Ordinal);
        }
    }

    internal sealed class CliParseResult
    {
        public bool IsValid { get; private set; }
        public CliOptions Options { get; private set; }
        public string ErrorMessage { get; private set; }

        public static CliParseResult Valid(CliOptions options)
        {
            return new CliParseResult { IsValid = true, Options = options };
        }

        public static CliParseResult Invalid(string message)
        {
            return new CliParseResult { IsValid = false, Options = new CliOptions(), ErrorMessage = message };
        }
    }

    internal enum CardMonitorState
    {
        ReaderMissing,
        ReaderReadyEmpty,
        CardCandidate,
        CardPresentStable,
        CardProcessing,
        CardDisplayed,
        RemovalCandidate,
        CardRemovedStable,
        ReaderError
    }

    internal sealed class CardStateMachine
    {
        private readonly PcscContext _context;
        private readonly ReaderSession _readerSession;
        private readonly TraceSink _trace;
        private readonly string _identityKey;

        public CardStateMachine(PcscContext context, ReaderSession readerSession, TraceSink trace, string identityKey)
        {
            _context = context;
            _readerSession = readerSession;
            _trace = trace;
            _identityKey = identityKey;
        }

        public void Watch()
        {
            bool cancel = false;
            string displayedCardKey = null;
            ConsoleCancelEventHandler handler = delegate(object sender, ConsoleCancelEventArgs e)
            {
                e.Cancel = true;
                cancel = true;
            };

            Console.CancelKeyPress += handler;
            try
            {
                while (!cancel)
                {
                    CardStateSnapshot snapshot = GetStableSnapshot(_context, _readerSession.ReaderName, _trace);
                    if (snapshot.CardPresent && displayedCardKey == null)
                    {
                        ProbeResult cardResult = ProbeResult.Create(CliOptions.Parse(new string[0]).Options);
                        cardResult.Mode = "watch";
                        cardResult.ReaderNames.AddRange(_context.ListReaders());
                        cardResult.SelectedReader = _readerSession.ReaderName;
                        cardResult.ReaderInfo = ReaderInfo.FromReaderName(_readerSession.ReaderName, null);
                        cardResult.State = CardMonitorState.CardProcessing.ToString();
                        _readerSession.ProbeReader(cardResult);

                        using (CardSession cardSession = CardSession.Connect(_context, _readerSession.ReaderName, _trace))
                        {
                            new CardProbe(_trace, _identityKey).Probe(cardSession, cardResult);
                        }

                        displayedCardKey = cardResult.Card == null ? DateTimeOffset.Now.ToString("O") : cardResult.Card.IdentityKey;
                        cardResult.State = CardMonitorState.CardDisplayed.ToString();
                        cardResult.ExitCode = 0;
                        SummaryRenderer.Render(cardResult);
                    }
                    else if (!snapshot.CardPresent && displayedCardKey != null)
                    {
                        _trace.Event(CardMonitorState.RemovalCandidate.ToString(), "カード取り外し候補を検出しました。");
                        Thread.Sleep(200);
                        CardStateSnapshot removalCheck = _context.GetReaderState(_readerSession.ReaderName, 250);
                        if (!removalCheck.CardPresent)
                        {
                            displayedCardKey = null;
                            _trace.Event(CardMonitorState.CardRemovedStable.ToString(), "カード取り外しを安定検出しました。");
                            Console.WriteLine();
                            Console.WriteLine("カードが取り外されました。次のカードを待機します。");
                        }
                    }

                    Thread.Sleep(500);
                }
            }
            finally
            {
                Console.CancelKeyPress -= handler;
            }
        }

        public static CardStateSnapshot GetStableSnapshot(PcscContext context, string readerName, TraceSink trace)
        {
            CardStateSnapshot initial = context.GetReaderState(readerName, 250);
            if (!initial.CardPresent)
            {
                trace.Event(CardMonitorState.ReaderReadyEmpty.ToString(), "カード未検出です。");
                initial.State = CardMonitorState.ReaderReadyEmpty;
                return initial;
            }

            trace.Event(CardMonitorState.CardCandidate.ToString(), "カード候補を検出しました。");
            Thread.Sleep(200);

            CardStateSnapshot stable = context.GetReaderState(readerName, 250);
            if (stable.CardPresent)
            {
                trace.Event(CardMonitorState.CardPresentStable.ToString(), "カードあり状態を安定検出しました。");
                stable.State = CardMonitorState.CardPresentStable;
                return stable;
            }

            stable.State = CardMonitorState.ReaderReadyEmpty;
            return stable;
        }
    }

    internal sealed class PcscContext : IDisposable
    {
        private readonly TraceSink _trace;
        private bool _disposed;

        private PcscContext(IntPtr handle, TraceSink trace)
        {
            Handle = handle;
            _trace = trace;
        }

        public IntPtr Handle { get; private set; }

        public static PcscContext Establish(TraceSink trace)
        {
            IntPtr handle;
            int rc = PcscNative.SCardEstablishContext(PcscNative.SCARD_SCOPE_SYSTEM, IntPtr.Zero, IntPtr.Zero, out handle);
            if (rc != PcscNative.SCARD_S_SUCCESS)
            {
                throw new PcscException("SCardEstablishContext", rc);
            }

            trace.Event("PcscContext", "PC/SC コンテキストを作成しました。");
            return new PcscContext(handle, trace);
        }

        public List<string> ListReaders()
        {
            int length = 0;
            int rc = PcscNative.SCardListReaders(Handle, null, null, ref length);
            if (rc == PcscNative.SCARD_E_NO_READERS_AVAILABLE)
            {
                return new List<string>();
            }

            if (rc != PcscNative.SCARD_S_SUCCESS)
            {
                throw new PcscException("SCardListReaders", rc);
            }

            StringBuilder buffer = new StringBuilder(length);
            rc = PcscNative.SCardListReaders(Handle, null, buffer, ref length);
            if (rc != PcscNative.SCARD_S_SUCCESS)
            {
                throw new PcscException("SCardListReaders", rc);
            }

            return buffer.ToString()
                .Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries)
                .ToList();
        }

        public CardStateSnapshot GetReaderState(string readerName, int timeoutMs)
        {
            PcscNative.SCARD_READERSTATE[] states = new PcscNative.SCARD_READERSTATE[1];
            states[0] = new PcscNative.SCARD_READERSTATE
            {
                szReader = readerName,
                dwCurrentState = PcscNative.SCARD_STATE_UNAWARE,
                rgbAtr = new byte[36]
            };

            int rc = PcscNative.SCardGetStatusChange(Handle, (uint)timeoutMs, states, states.Length);
            if (rc == PcscNative.SCARD_E_TIMEOUT)
            {
                return new CardStateSnapshot(readerName, false, CardMonitorState.ReaderReadyEmpty, 0, new byte[0]);
            }

            if (rc != PcscNative.SCARD_S_SUCCESS)
            {
                throw new PcscException("SCardGetStatusChange", rc);
            }

            byte[] atr = new byte[states[0].cbAtr];
            if (states[0].cbAtr > 0)
            {
                Array.Copy(states[0].rgbAtr, atr, atr.Length);
            }

            bool present = (states[0].dwEventState & PcscNative.SCARD_STATE_PRESENT) == PcscNative.SCARD_STATE_PRESENT;
            bool empty = (states[0].dwEventState & PcscNative.SCARD_STATE_EMPTY) == PcscNative.SCARD_STATE_EMPTY;
            CardMonitorState state = present ? CardMonitorState.CardCandidate : CardMonitorState.ReaderReadyEmpty;
            if (empty)
            {
                state = CardMonitorState.ReaderReadyEmpty;
            }

            return new CardStateSnapshot(readerName, present, state, states[0].dwEventState, atr);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (Handle != IntPtr.Zero)
            {
                PcscNative.SCardReleaseContext(Handle);
                _trace.Event("PcscContext", "PC/SC コンテキストを解放しました。");
                Handle = IntPtr.Zero;
            }
        }
    }

    internal sealed class ReaderSession : IDisposable
    {
        private readonly TraceSink _trace;

        public ReaderSession(PcscContext context, string readerName, TraceSink trace)
        {
            Context = context;
            ReaderName = readerName;
            _trace = trace;
        }

        public PcscContext Context { get; private set; }
        public string ReaderName { get; private set; }

        public void ProbeReader(ProbeResult result)
        {
            result.ProbeItems.Add(ProbeItem.Success("Reader.Name", "リーダー名", ReaderName));
            if (result.ReaderInfo != null && !result.ReaderInfo.IsAcr122Candidate)
            {
                result.ProbeItems.Add(ProbeItem.Failed("Reader.Acr122uCandidate", "ACR122U 候補判定", "リーダー名に ACR122 が含まれていません。"));
            }
            else
            {
                result.ProbeItems.Add(ProbeItem.Success("Reader.Acr122uCandidate", "ACR122U 候補判定", "ACR122U 候補として扱います。"));
            }

            ApduResponse firmware = TransmitReaderControl("Firmware", new byte[] { 0xFF, 0x00, 0x48, 0x00, 0x00 });
            if (firmware.Success)
            {
                byte[] data = firmware.GetReaderPayload();
                string ascii = HexUtil.ToPrintableAscii(data);
                result.ReaderInfo.Firmware = ascii;
                result.ReaderInfo.FirmwareHex = HexUtil.ToHex(data);
                result.ProbeItems.Add(ProbeItem.Success("Reader.Firmware", "ファームウェア", string.IsNullOrEmpty(ascii) ? firmware.ResponseHex : ascii));
            }
            else
            {
                result.ProbeItems.Add(ProbeItem.Failed("Reader.Firmware", "ファームウェア", firmware.FailureMessage));
            }

            ApduResponse picc = TransmitReaderControl("PICC Operating Parameter", new byte[] { 0xFF, 0x00, 0x50, 0x00, 0x00 });
            byte piccParameter;
            if (TryGetPiccParameter(picc, out piccParameter))
            {
                result.ReaderInfo.PiccOperatingParameter = "0x" + piccParameter.ToString("X2");
                result.ReaderInfo.PiccOperatingParameterBits = PiccParameterInfo.FromByte(piccParameter);
                result.ProbeItems.Add(ProbeItem.Success("Reader.PiccOperatingParameter", "PICC operating parameter", result.ReaderInfo.PiccOperatingParameter));
            }
            else
            {
                result.ProbeItems.Add(ProbeItem.Failed("Reader.PiccOperatingParameter", "PICC operating parameter", picc.FailureMessage));
            }

            result.ProbeItems.Add(ProbeItem.SkippedByPolicy("Reader.SettingsWrite", "リーダー設定変更", "Phase 1 の既定動作は read-only のため、PICC parameter / buzzer / timeout / LED / antenna は変更しません。"));
        }

        private ApduResponse TransmitReaderControl(string name, byte[] command)
        {
            Stopwatch sw = Stopwatch.StartNew();
            IntPtr card = IntPtr.Zero;
            try
            {
                uint activeProtocol;
                int rc = PcscNative.SCardConnect(Context.Handle, ReaderName, PcscNative.SCARD_SHARE_DIRECT, PcscNative.SCARD_PROTOCOL_UNDEFINED, out card, out activeProtocol);
                if (rc != PcscNative.SCARD_S_SUCCESS)
                {
                    return ApduResponse.FromPcscError(name, command, "SCardConnect(SHARE_DIRECT)", rc, sw.Elapsed);
                }

                byte[] recv = new byte[258];
                int recvLength = recv.Length;
                rc = PcscNative.SCardControl(card, PcscNative.IoctlCcidEscape, command, command.Length, recv, recv.Length, out recvLength);
                if (rc != PcscNative.SCARD_S_SUCCESS)
                {
                    return ApduResponse.FromPcscError(name, command, "SCardControl(IOCTL_CCID_ESCAPE)", rc, sw.Elapsed);
                }

                byte[] response = new byte[recvLength];
                Array.Copy(recv, response, recvLength);
                _trace.Apdu(name, command, response, sw.Elapsed);
                return ApduResponse.FromReaderControl(name, command, response, sw.Elapsed);
            }
            finally
            {
                if (card != IntPtr.Zero)
                {
                    PcscNative.SCardDisconnect(card, PcscNative.SCARD_LEAVE_CARD);
                }
            }
        }

        private static bool TryGetPiccParameter(ApduResponse response, out byte parameter)
        {
            parameter = 0;
            if (!response.Success || response.RawResponse.Length == 0)
            {
                return false;
            }

            if (response.RawResponse.Length >= 2 && response.RawResponse[0] == 0x90)
            {
                parameter = response.RawResponse[1];
                return true;
            }

            byte[] data = response.GetReaderPayload();
            if (data.Length > 0)
            {
                parameter = data[data.Length - 1];
                return true;
            }

            return false;
        }

        public void Dispose()
        {
        }
    }

    internal sealed class CardSession : IDisposable
    {
        private readonly TraceSink _trace;
        private bool _disposed;

        private CardSession(PcscContext context, string readerName, IntPtr handle, uint activeProtocol, TraceSink trace)
        {
            Context = context;
            ReaderName = readerName;
            Handle = handle;
            ActiveProtocol = activeProtocol;
            _trace = trace;
            RefreshStatus();
        }

        public PcscContext Context { get; private set; }
        public string ReaderName { get; private set; }
        public IntPtr Handle { get; private set; }
        public uint ActiveProtocol { get; private set; }
        public byte[] Atr { get; private set; }
        public uint State { get; private set; }

        public string ProtocolName
        {
            get { return ErrorCatalog.ProtocolName(ActiveProtocol); }
        }

        public static CardSession Connect(PcscContext context, string readerName, TraceSink trace)
        {
            IntPtr handle;
            uint activeProtocol;
            int rc = PcscNative.SCardConnect(context.Handle, readerName, PcscNative.SCARD_SHARE_SHARED, PcscNative.SCARD_PROTOCOL_T0 | PcscNative.SCARD_PROTOCOL_T1, out handle, out activeProtocol);
            if (rc != PcscNative.SCARD_S_SUCCESS)
            {
                throw new PcscException("SCardConnect(SHARE_SHARED)", rc);
            }

            trace.Event("CardSession", "カードへ共有・読み取り用に接続しました。");
            return new CardSession(context, readerName, handle, activeProtocol, trace);
        }

        public ApduResponse Transmit(string name, byte[] command)
        {
            Stopwatch sw = Stopwatch.StartNew();
            PcscNative.SCARD_IO_REQUEST sendPci = new PcscNative.SCARD_IO_REQUEST
            {
                dwProtocol = ActiveProtocol,
                cbPciLength = (uint)Marshal.SizeOf(typeof(PcscNative.SCARD_IO_REQUEST))
            };
            byte[] recv = new byte[258];
            int recvLength = recv.Length;
            int rc = PcscNative.SCardTransmit(Handle, ref sendPci, command, command.Length, IntPtr.Zero, recv, ref recvLength);
            if (rc != PcscNative.SCARD_S_SUCCESS)
            {
                return ApduResponse.FromPcscError(name, command, "SCardTransmit", rc, sw.Elapsed);
            }

            byte[] response = new byte[recvLength];
            Array.Copy(recv, response, recvLength);
            _trace.Apdu(name, command, response, sw.Elapsed);
            return ApduResponse.FromIsoApdu(name, command, response, sw.Elapsed);
        }

        private void RefreshStatus()
        {
            StringBuilder readerName = new StringBuilder(256);
            int readerNameLength = readerName.Capacity;
            byte[] atr = new byte[36];
            int atrLength = atr.Length;
            uint state;
            uint protocol;
            int rc = PcscNative.SCardStatus(Handle, readerName, ref readerNameLength, out state, out protocol, atr, ref atrLength);
            if (rc != PcscNative.SCARD_S_SUCCESS)
            {
                throw new PcscException("SCardStatus", rc);
            }

            State = state;
            ActiveProtocol = protocol;
            Atr = new byte[atrLength];
            Array.Copy(atr, Atr, atrLength);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (Handle != IntPtr.Zero)
            {
                PcscNative.SCardDisconnect(Handle, PcscNative.SCARD_LEAVE_CARD);
                _trace.Event("CardSession", "カード接続を SCARD_LEAVE_CARD で解放しました。");
                Handle = IntPtr.Zero;
            }
        }
    }

    internal sealed class CardProbe
    {
        private readonly TraceSink _trace;
        private readonly string _identityKey;

        public CardProbe(TraceSink trace, string identityKey)
        {
            _trace = trace;
            _identityKey = identityKey;
        }

        public void Probe(CardSession session, ProbeResult result)
        {
            result.Card = new CardInfo();
            result.Card.Atr = HexUtil.ToHex(session.Atr);
            result.Card.AtrLength = session.Atr.Length;
            result.Card.PcscProtocol = session.ProtocolName;
            result.Card.PcscProtocolRaw = "0x" + session.ActiveProtocol.ToString("X");
            result.ProbeItems.Add(ProbeItem.Success("Card.ATR", "ATR", result.Card.Atr));
            result.ProbeItems.Add(ProbeItem.Success("Card.Protocol", "PC/SC protocol", result.Card.PcscProtocol));

            AtrInfo atrInfo = AtrParser.Parse(session.Atr);
            result.Card.HistoricalBytes = HexUtil.ToHex(atrInfo.HistoricalBytes);
            result.Card.AtrParseWarnings.AddRange(atrInfo.Warnings);
            result.Card.CardNameCode = atrInfo.CardNameCode;
            result.Card.EstimatedCardName = atrInfo.CardNameText;
            result.ProbeItems.Add(ProbeItem.Success("Card.HistoricalBytes", "Historical bytes", string.IsNullOrEmpty(result.Card.HistoricalBytes) ? "なし" : result.Card.HistoricalBytes));

            ApduTransceiver transceiver = new ApduTransceiver(session);

            ApduResponse uid = transceiver.Transmit("UID", new byte[] { 0xFF, 0xCA, 0x00, 0x00, 0x00 });
            result.Card.Uid = uid.Success ? uid.ResponseDataHex : null;
            result.Card.UidStatus = ProbeItem.FromApdu("Card.UID", "UID / NFC ID", uid);
            result.ProbeItems.Add(result.Card.UidStatus);

            ApduResponse ats = transceiver.Transmit("ATS", new byte[] { 0xFF, 0xCA, 0x01, 0x00, 0x00 });
            result.Card.Ats = ats.Success ? ats.ResponseDataHex : null;
            result.Card.AtsStatus = ProbeItem.FromApdu("Card.ATS", "ATS", ats);
            result.ProbeItems.Add(result.Card.AtsStatus);

            CardClassification classification = CardClassifier.Classify(atrInfo, uid, ats);
            result.Card.EstimatedStandard = classification.EstimatedStandard;
            result.Card.EstimatedCardName = classification.EstimatedCardName;
            result.Card.ClassificationReason = classification.Reason;
            result.Card.IdentityKey = session.ReaderName + "|" + result.Card.Atr + "|" + (result.Card.Uid ?? "") + "|" + result.Card.PcscProtocolRaw;
            result.ProbeItems.Add(ProbeItem.Success("Card.Classification", "カード分類", classification.EstimatedStandard + " / " + classification.EstimatedCardName));

            IdentityCodeResult identity = IdentityCodeGenerator.Generate(result.Card, _identityKey);
            result.Card.IdentityCode = identity.Code;
            result.Card.IdentitySource = identity.Source;
            result.ProbeItems.Add(ProbeItem.Success("Card.IdentityCode", "識別コード", result.Card.IdentityCode));
            _trace.Event("CardIdentity", "識別コードを生成しました。");

            AddSafetyBoundaryItems(result);
            _trace.Event("CardProbe", "カード基本情報の取得を完了しました。");
        }

        private static void AddSafetyBoundaryItems(ProbeResult result)
        {
            result.ProbeItems.Add(ProbeItem.SkippedByPolicy("Policy.ProtectedBlocks", "保護領域", "鍵が必要な領域、残高、履歴、個人情報は読み取りません。"));
            result.ProbeItems.Add(ProbeItem.SkippedByPolicy("Policy.MifareAuthentication", "MIFARE 認証", "Phase 1 ではキー入力、認証、ブロック読み取りを実装しません。"));
            result.ProbeItems.Add(ProbeItem.SkippedByPolicy("Policy.FelicaServiceScan", "FeliCa サービス探索", "サービスコードの総当たりや交通系カードの残高・履歴抽出は行いません。"));
            result.ProbeItems.Add(ProbeItem.SkippedByPolicy("Policy.WriteCommands", "書き込み系 APDU", "Phase 1 は読み取り専用で、カード/リーダー設定を書き換えません。"));
        }
    }

    internal sealed class ApduTransceiver
    {
        private readonly CardSession _session;

        public ApduTransceiver(CardSession session)
        {
            _session = session;
        }

        public ApduResponse Transmit(string name, byte[] command)
        {
            return _session.Transmit(name, command);
        }
    }

    internal sealed class ProbeResult
    {
        public string Application { get; set; }
        public string Version { get; set; }
        public string Timestamp { get; set; }
        public string Mode { get; set; }
        public string State { get; set; }
        public int ExitCode { get; set; }
        public bool TraceEnabled { get; set; }
        public bool JsonOutput { get; set; }
        public string RequestedReader { get; set; }
        public string SelectedReader { get; set; }
        public bool OutputAlreadyRendered { get; set; }
        public List<string> ReaderNames { get; private set; }
        public ReaderInfo ReaderInfo { get; set; }
        public CardInfo Card { get; set; }
        public List<ProbeItem> ProbeItems { get; private set; }
        public List<ProbeError> Errors { get; private set; }

        public ProbeResult()
        {
            ReaderNames = new List<string>();
            ProbeItems = new List<ProbeItem>();
            Errors = new List<ProbeError>();
        }

        public static ProbeResult Create(CliOptions options)
        {
            return new ProbeResult
            {
                Application = "Acr122uCardInspector",
                Version = VersionInfo.InformationalVersion,
                Timestamp = DateTimeOffset.Now.ToString("O"),
                State = CardMonitorState.ReaderMissing.ToString(),
                Mode = "once",
                ExitCode = 0,
                TraceEnabled = options.Trace,
                JsonOutput = options.Json,
                RequestedReader = options.ReaderName
            };
        }
    }

    internal sealed class ReaderInfo
    {
        public string ReaderName { get; set; }
        public bool IsAcr122Candidate { get; set; }
        public string SelectionWarning { get; set; }
        public string Firmware { get; set; }
        public string FirmwareHex { get; set; }
        public string PiccOperatingParameter { get; set; }
        public PiccParameterInfo PiccOperatingParameterBits { get; set; }

        public static ReaderInfo FromReaderName(string readerName, string warning)
        {
            return new ReaderInfo
            {
                ReaderName = readerName,
                IsAcr122Candidate = readerName != null && readerName.IndexOf("ACR122", StringComparison.OrdinalIgnoreCase) >= 0,
                SelectionWarning = warning
            };
        }
    }

    internal sealed class PiccParameterInfo
    {
        public bool AutoPiccPolling { get; set; }
        public bool AutoAtsGeneration { get; set; }
        public string PollingInterval { get; set; }
        public List<string> Targets { get; private set; }

        public PiccParameterInfo()
        {
            Targets = new List<string>();
        }

        public static PiccParameterInfo FromByte(byte value)
        {
            PiccParameterInfo info = new PiccParameterInfo
            {
                AutoPiccPolling = (value & 0x80) != 0,
                AutoAtsGeneration = (value & 0x40) != 0,
                PollingInterval = (value & 0x20) != 0 ? "250 ms" : "500 ms"
            };

            if ((value & 0x01) != 0) info.Targets.Add("ISO 14443 Type A");
            if ((value & 0x02) != 0) info.Targets.Add("ISO 14443 Type B");
            if ((value & 0x04) != 0) info.Targets.Add("Topaz / Jewel");
            if ((value & 0x08) != 0) info.Targets.Add("FeliCa 212K");
            if ((value & 0x10) != 0) info.Targets.Add("FeliCa 424K");
            return info;
        }
    }

    internal sealed class CardInfo
    {
        public string Atr { get; set; }
        public int AtrLength { get; set; }
        public string HistoricalBytes { get; set; }
        public string Uid { get; set; }
        public string Ats { get; set; }
        public string PcscProtocol { get; set; }
        public string PcscProtocolRaw { get; set; }
        public string EstimatedStandard { get; set; }
        public string EstimatedCardName { get; set; }
        public string CardNameCode { get; set; }
        public string ClassificationReason { get; set; }
        public string IdentityKey { get; set; }
        public string IdentityCode { get; set; }
        public IdentitySourceInfo IdentitySource { get; set; }
        public ProbeItem UidStatus { get; set; }
        public ProbeItem AtsStatus { get; set; }
        public List<string> AtrParseWarnings { get; private set; }

        public CardInfo()
        {
            AtrParseWarnings = new List<string>();
        }
    }

    internal sealed class IdentitySourceInfo
    {
        public string Uid { get; set; }
        public string Atr { get; set; }
        public string Ats { get; set; }
        public string Pmm { get; set; }
        public string Type { get; set; }
        public bool KeyConfigured { get; set; }
        public string Algorithm { get; set; }
        public string HashFormat { get; set; }
        public string NormalizedInputWithoutKey { get; set; }
    }

    internal sealed class IdentityCodeResult
    {
        public string Code { get; set; }
        public IdentitySourceInfo Source { get; set; }
    }

    internal static class IdentityCodeGenerator
    {
        private const string Unknown = "unknown";

        public static IdentityCodeResult Generate(CardInfo card, string identityKey)
        {
            IdentitySourceInfo source = new IdentitySourceInfo
            {
                Uid = NormalizeHexOrUnknown(card == null ? null : card.Uid),
                Atr = NormalizeHexOrUnknown(card == null ? null : card.Atr),
                Ats = NormalizeHexOrUnknown(card == null ? null : card.Ats),
                Pmm = Unknown,
                Type = NormalizeTextOrUnknown(BuildType(card)),
                KeyConfigured = !string.IsNullOrEmpty(identityKey),
                Algorithm = "MD5",
                HashFormat = "lowercase-hex-32"
            };
            source.NormalizedInputWithoutKey = BuildNormalizedInput(source, null);

            string normalizedInput = BuildNormalizedInput(source, identityKey ?? "");
            return new IdentityCodeResult
            {
                Code = ComputeMd5LowerHex(normalizedInput),
                Source = source
            };
        }

        private static string BuildType(CardInfo card)
        {
            if (card == null)
            {
                return null;
            }

            string standard = NormalizeTextOrUnknown(card.EstimatedStandard);
            string name = NormalizeTextOrUnknown(card.EstimatedCardName);
            return standard + "/" + name;
        }

        private static string BuildNormalizedInput(IdentitySourceInfo source, string key)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("UID=").Append(source.Uid);
            builder.Append("|ATR=").Append(source.Atr);
            builder.Append("|ATS=").Append(source.Ats);
            builder.Append("|PMM=").Append(source.Pmm);
            builder.Append("|TYPE=").Append(source.Type);
            if (key != null)
            {
                builder.Append("|KEY=").Append(NormalizeTextOrUnknown(key));
            }

            return builder.ToString();
        }

        private static string ComputeMd5LowerHex(string value)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] input = Encoding.UTF8.GetBytes(value);
                byte[] hash = md5.ComputeHash(input);
                return HexUtil.ToCompactLowerHex(hash);
            }
        }

        private static string NormalizeHexOrUnknown(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Unknown;
            }

            StringBuilder builder = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f'))
                {
                    builder.Append(char.ToUpperInvariant(c));
                }
            }

            return builder.Length == 0 ? Unknown : builder.ToString();
        }

        private static string NormalizeTextOrUnknown(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Unknown;
            }

            StringBuilder builder = new StringBuilder(value.Length);
            bool previousWasSpace = false;
            foreach (char c in value.Trim())
            {
                if (c == '|' || c == '=' || char.IsWhiteSpace(c))
                {
                    AppendSingleSpace(builder, ref previousWasSpace);
                }
                else
                {
                    builder.Append(c);
                    previousWasSpace = false;
                }
            }

            return builder.Length == 0 ? Unknown : builder.ToString();
        }

        private static void AppendSingleSpace(StringBuilder builder, ref bool previousWasSpace)
        {
            if (builder.Length == 0 || previousWasSpace)
            {
                return;
            }

            builder.Append(' ');
            previousWasSpace = true;
        }
    }

    internal sealed class ProbeItem
    {
        public string Key { get; set; }
        public string Label { get; set; }
        public string Status { get; set; }
        public string Value { get; set; }
        public string Reason { get; set; }
        public string StatusCode { get; set; }
        public string Detail { get; set; }

        public static ProbeItem Success(string key, string label, string value)
        {
            return new ProbeItem { Key = key, Label = label, Status = "Success", Value = value };
        }

        public static ProbeItem Failed(string key, string label, string reason)
        {
            return new ProbeItem { Key = key, Label = label, Status = "Failed", Reason = reason };
        }

        public static ProbeItem SkippedByPolicy(string key, string label, string reason)
        {
            return new ProbeItem { Key = key, Label = label, Status = "SkippedByPolicy", Reason = reason };
        }

        public static ProbeItem NotApplicable(string key, string label, string reason)
        {
            return new ProbeItem { Key = key, Label = label, Status = "NotApplicable", Reason = reason };
        }

        public static ProbeItem FromApdu(string key, string label, ApduResponse response)
        {
            if (response.Success)
            {
                return new ProbeItem
                {
                    Key = key,
                    Label = label,
                    Status = "Success",
                    Value = response.ResponseDataHex,
                    StatusCode = response.StatusCode,
                    Detail = response.StatusDescription
                };
            }

            return new ProbeItem
            {
                Key = key,
                Label = label,
                Status = "Failed",
                Reason = response.FailureMessage,
                StatusCode = response.StatusCode,
                Detail = response.StatusDescription
            };
        }
    }

    internal sealed class ProbeError
    {
        public string Code { get; set; }
        public string Message { get; set; }
        public string Detail { get; set; }

        public ProbeError(string code, string message)
        {
            Code = code;
            Message = message;
        }

        public static ProbeError FromPcsc(string code, string message, PcscException ex)
        {
            return new ProbeError(code, message)
            {
                Detail = ex.ApiName + ": " + ex.ErrorCodeHex + " " + ErrorCatalog.PcscError(ex.ErrorCode)
            };
        }

        public static ProbeError FromException(string code, string message, Exception ex)
        {
            return new ProbeError(code, message)
            {
                Detail = ex.GetType().Name + ": " + ex.Message
            };
        }
    }

    internal sealed class ReaderSelection
    {
        public bool Success { get; set; }
        public string ReaderName { get; set; }
        public string Warning { get; set; }
        public string ErrorMessage { get; set; }
    }

    internal static class ReaderCatalog
    {
        public static ReaderSelection Select(List<string> readers, string requestedReader)
        {
            if (!string.IsNullOrEmpty(requestedReader))
            {
                string exact = readers.FirstOrDefault(r => string.Equals(r, requestedReader, StringComparison.OrdinalIgnoreCase));
                if (exact != null)
                {
                    return new ReaderSelection { Success = true, ReaderName = exact };
                }

                string partial = readers.FirstOrDefault(r => r.IndexOf(requestedReader, StringComparison.OrdinalIgnoreCase) >= 0);
                if (partial != null)
                {
                    return new ReaderSelection { Success = true, ReaderName = partial };
                }

                return new ReaderSelection
                {
                    Success = false,
                    ErrorMessage = "指定されたリーダーが見つかりません: " + requestedReader
                };
            }

            string acr122 = readers.FirstOrDefault(r => r.IndexOf("ACR122", StringComparison.OrdinalIgnoreCase) >= 0);
            if (acr122 != null)
            {
                return new ReaderSelection { Success = true, ReaderName = acr122 };
            }

            return new ReaderSelection
            {
                Success = true,
                ReaderName = readers[0],
                Warning = "ACR122U 候補が見つからないため、最初の PC/SC リーダーを選択しました。"
            };
        }
    }

    internal sealed class CardStateSnapshot
    {
        public CardStateSnapshot(string readerName, bool cardPresent, CardMonitorState state, uint rawState, byte[] atr)
        {
            ReaderName = readerName;
            CardPresent = cardPresent;
            State = state;
            RawState = rawState;
            Atr = atr ?? new byte[0];
        }

        public string ReaderName { get; private set; }
        public bool CardPresent { get; private set; }
        public CardMonitorState State { get; set; }
        public uint RawState { get; private set; }
        public byte[] Atr { get; private set; }
    }

    internal sealed class ApduResponse
    {
        public string Name { get; set; }
        public byte[] Command { get; set; }
        public byte[] RawResponse { get; set; }
        public byte[] ResponseData { get; set; }
        public string StatusCode { get; set; }
        public string StatusDescription { get; set; }
        public bool Success { get; set; }
        public string FailureMessage { get; set; }
        public long ElapsedMilliseconds { get; set; }

        public string ResponseHex
        {
            get { return HexUtil.ToHex(RawResponse); }
        }

        public string ResponseDataHex
        {
            get { return HexUtil.ToHex(ResponseData); }
        }

        public static ApduResponse FromIsoApdu(string name, byte[] command, byte[] response, TimeSpan elapsed)
        {
            ApduResponse result = CreateBase(name, command, response, elapsed);
            if (response.Length >= 2)
            {
                byte sw1 = response[response.Length - 2];
                byte sw2 = response[response.Length - 1];
                result.StatusCode = sw1.ToString("X2") + " " + sw2.ToString("X2");
                result.StatusDescription = ErrorCatalog.StatusWord(result.StatusCode);
                result.Success = sw1 == 0x90 && sw2 == 0x00;
                result.ResponseData = new byte[response.Length - 2];
                Array.Copy(response, result.ResponseData, result.ResponseData.Length);
            }
            else
            {
                result.StatusCode = "Unknown";
                result.StatusDescription = "ステータスワードが含まれていません。";
                result.Success = false;
                result.ResponseData = new byte[0];
            }

            if (!result.Success)
            {
                result.FailureMessage = "APDU が失敗しました: " + result.StatusCode + " " + result.StatusDescription;
            }

            return result;
        }

        public static ApduResponse FromReaderControl(string name, byte[] command, byte[] response, TimeSpan elapsed)
        {
            ApduResponse result = CreateBase(name, command, response, elapsed);
            if (response.Length >= 2 && response[0] == 0x90)
            {
                result.Success = true;
                result.StatusCode = "90 " + response[1].ToString("X2");
                result.StatusDescription = "ACR122U reader command success";
                result.ResponseData = response.Skip(1).ToArray();
                return result;
            }

            if (response.Length >= 2 && response[response.Length - 2] == 0x90 && response[response.Length - 1] == 0x00)
            {
                result.Success = true;
                result.StatusCode = "90 00";
                result.StatusDescription = ErrorCatalog.StatusWord(result.StatusCode);
                result.ResponseData = response.Take(response.Length - 2).ToArray();
                return result;
            }

            if (response.Length > 0 && response.All(HexUtil.IsPrintableAscii))
            {
                result.Success = true;
                result.StatusCode = "Raw";
                result.StatusDescription = "ASCII 応答";
                result.ResponseData = response;
                return result;
            }

            result.Success = response.Length > 0;
            result.StatusCode = response.Length == 0 ? "Empty" : HexUtil.ToHex(response);
            result.StatusDescription = result.Success ? "Reader control raw response" : "応答が空です。";
            result.ResponseData = response;
            result.FailureMessage = result.Success ? null : "リーダーコマンドの応答が空でした。";
            return result;
        }

        public static ApduResponse FromPcscError(string name, byte[] command, string apiName, int errorCode, TimeSpan elapsed)
        {
            ApduResponse result = CreateBase(name, command, new byte[0], elapsed);
            result.Success = false;
            result.ResponseData = new byte[0];
            result.StatusCode = "0x" + unchecked((uint)errorCode).ToString("X8");
            result.StatusDescription = ErrorCatalog.PcscError(errorCode);
            result.FailureMessage = apiName + " が失敗しました: " + result.StatusCode + " " + result.StatusDescription;
            return result;
        }

        public byte[] GetReaderPayload()
        {
            if (RawResponse.Length >= 2 && RawResponse[0] == 0x90)
            {
                return RawResponse.Skip(1).ToArray();
            }

            if (RawResponse.Length >= 2 && RawResponse[RawResponse.Length - 2] == 0x90 && RawResponse[RawResponse.Length - 1] == 0x00)
            {
                return RawResponse.Take(RawResponse.Length - 2).ToArray();
            }

            return RawResponse;
        }

        private static ApduResponse CreateBase(string name, byte[] command, byte[] response, TimeSpan elapsed)
        {
            return new ApduResponse
            {
                Name = name,
                Command = command,
                RawResponse = response ?? new byte[0],
                ResponseData = new byte[0],
                ElapsedMilliseconds = (long)elapsed.TotalMilliseconds
            };
        }
    }

    internal sealed class AtrInfo
    {
        public byte[] HistoricalBytes { get; set; }
        public string CardNameCode { get; set; }
        public string CardNameText { get; set; }
        public byte? StandardByte { get; set; }
        public List<string> Warnings { get; private set; }

        public AtrInfo()
        {
            HistoricalBytes = new byte[0];
            CardNameText = "不明";
            Warnings = new List<string>();
        }
    }

    internal static class AtrParser
    {
        public static AtrInfo Parse(byte[] atr)
        {
            AtrInfo info = new AtrInfo();
            if (atr == null || atr.Length < 2)
            {
                info.Warnings.Add("ATR が短すぎるため解析できません。");
                return info;
            }

            int historicalLength = atr[1] & 0x0F;
            int index = 2;
            int y = (atr[1] & 0xF0) >> 4;
            while (y != 0 && index < atr.Length)
            {
                if ((y & 0x01) != 0) index++;
                if ((y & 0x02) != 0) index++;
                if ((y & 0x04) != 0) index++;
                if ((y & 0x08) != 0 && index < atr.Length)
                {
                    y = (atr[index] & 0xF0) >> 4;
                    index++;
                }
                else
                {
                    y = 0;
                }
            }

            if (index + historicalLength <= atr.Length)
            {
                info.HistoricalBytes = new byte[historicalLength];
                Array.Copy(atr, index, info.HistoricalBytes, 0, historicalLength);
            }
            else
            {
                info.Warnings.Add("ATR の historical bytes 長が実データ長を超えています。");
                info.HistoricalBytes = atr.Skip(index).ToArray();
            }

            ApplyPcscCardName(info);
            return info;
        }

        private static void ApplyPcscCardName(AtrInfo info)
        {
            byte[] h = info.HistoricalBytes;
            byte[] rid = new byte[] { 0xA0, 0x00, 0x00, 0x03, 0x06 };
            int ridIndex = IndexOf(h, rid);
            if (ridIndex < 0)
            {
                info.Warnings.Add("PC/SC RID が historical bytes 内に見つかりません。");
                return;
            }

            int standardIndex = ridIndex + rid.Length;
            int cardNameIndex = standardIndex + 1;
            if (cardNameIndex + 1 >= h.Length)
            {
                info.Warnings.Add("Card Name フィールドを取得できません。");
                return;
            }

            info.StandardByte = h[standardIndex];
            string code = h[cardNameIndex].ToString("X2") + " " + h[cardNameIndex + 1].ToString("X2");
            info.CardNameCode = code;
            info.CardNameText = ErrorCatalog.CardName(code);
        }

        private static int IndexOf(byte[] source, byte[] pattern)
        {
            for (int i = 0; i <= source.Length - pattern.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (source[i + j] != pattern[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    return i;
                }
            }

            return -1;
        }
    }

    internal sealed class CardClassification
    {
        public string EstimatedStandard { get; set; }
        public string EstimatedCardName { get; set; }
        public string Reason { get; set; }
    }

    internal static class CardClassifier
    {
        public static CardClassification Classify(AtrInfo atrInfo, ApduResponse uid, ApduResponse ats)
        {
            string cardName = string.IsNullOrEmpty(atrInfo.CardNameText) ? "不明" : atrInfo.CardNameText;
            string standard = "不明";
            string reason = "ATR historical bytes を基準に推定しました。";

            if (atrInfo.CardNameCode == "F0 11" || atrInfo.CardNameCode == "F0 12")
            {
                standard = "FeliCa";
            }
            else if (atrInfo.CardNameCode == "F0 04")
            {
                standard = "Topaz / Jewel";
            }
            else if (atrInfo.StandardByte.HasValue && atrInfo.StandardByte.Value == 0x03)
            {
                standard = ats.Success ? "ISO 14443-4 / Type A 系" : "ISO 14443 Type A / MIFARE 系";
            }
            else if (ats.Success)
            {
                standard = "ISO 14443-4 互換";
                reason = "ATS 取得に成功したため ISO 14443-4 互換として推定しました。";
            }

            if ((string.IsNullOrEmpty(cardName) || cardName == "不明") && uid.Success)
            {
                cardName = "UID 取得可能な非接触カード";
            }

            return new CardClassification
            {
                EstimatedStandard = standard,
                EstimatedCardName = cardName,
                Reason = reason
            };
        }
    }

    internal static class SummaryRenderer
    {
        public static void RenderHelp()
        {
            Console.WriteLine("ACR122U Card Inspector");
            Console.WriteLine();
            Console.WriteLine("使い方:");
            Console.WriteLine("  Acr122uCardInspector [options]");
            Console.WriteLine();
            Console.WriteLine("オプション:");
            Console.WriteLine("  --help              ヘルプを表示して終了します。");
            Console.WriteLine("  --version           バージョンを表示して終了します。");
            Console.WriteLine("  --once              1 回だけリーダー/カード状態を確認して終了します。");
            Console.WriteLine("  --reader <name>     使用する PC/SC リーダー名を指定します。部分一致も許可します。");
            Console.WriteLine("  --identity-key <v>  識別コード生成用キーを指定します。既定値は暫定仕様の 0000 です。");
            Console.WriteLine("  --trace             logs/trace-*.log に診断トレースを出力します。");
            Console.WriteLine("  --json [path]       ProbeResult を JSON 出力します。path 省略時は標準出力へ出します。");
            Console.WriteLine();
            Console.WriteLine("安全方針:");
            Console.WriteLine("  既定では reader 設定変更、書き込み APDU、鍵探索、残高/履歴/個人情報の読み取りを行いません。");
            Console.WriteLine("  識別コードは簡易識別用です。強い認証や改ざん耐性は保証しません。");
        }

        public static void Render(ProbeResult result)
        {
            Console.WriteLine("ACR122U Card Inspector");
            Console.WriteLine("Version: " + result.Version);
            Console.WriteLine("Mode: " + result.Mode);
            Console.WriteLine("State: " + ToJapaneseState(result.State));
            Console.WriteLine();

            Console.WriteLine("PC/SC リーダー一覧:");
            if (result.ReaderNames.Count == 0)
            {
                Console.WriteLine("  なし");
            }
            else
            {
                foreach (string reader in result.ReaderNames)
                {
                    Console.WriteLine("  - " + reader + (reader == result.SelectedReader ? " (選択中)" : ""));
                }
            }

            if (result.ReaderInfo != null)
            {
                Console.WriteLine();
                Console.WriteLine("リーダー概要:");
                Console.WriteLine("  Reader: " + result.ReaderInfo.ReaderName);
                Console.WriteLine("  ACR122U 候補: " + (result.ReaderInfo.IsAcr122Candidate ? "はい" : "いいえ"));
                if (!string.IsNullOrEmpty(result.ReaderInfo.SelectionWarning))
                {
                    Console.WriteLine("  注意: " + result.ReaderInfo.SelectionWarning);
                }
                Console.WriteLine("  Firmware: " + ValueOrUnavailable(result.ReaderInfo.Firmware));
                Console.WriteLine("  Firmware Hex: " + ValueOrUnavailable(result.ReaderInfo.FirmwareHex));
                Console.WriteLine("  PICC Parameter: " + ValueOrUnavailable(result.ReaderInfo.PiccOperatingParameter));
                RenderPiccParameter(result.ReaderInfo.PiccOperatingParameterBits);
            }

            if (result.Card != null)
            {
                Console.WriteLine();
                Console.WriteLine("カード概要:");
                Console.WriteLine("  ATR: " + ValueOrUnavailable(result.Card.Atr));
                Console.WriteLine("  ATR Length: " + result.Card.AtrLength);
                Console.WriteLine("  UID/NFC ID: " + ValueOrUnavailable(result.Card.Uid));
                Console.WriteLine("  ATS: " + ValueOrUnavailable(result.Card.Ats));
                Console.WriteLine("  識別コード: " + ValueOrUnavailable(result.Card.IdentityCode));
                Console.WriteLine("  Estimated Standard: " + ValueOrUnavailable(result.Card.EstimatedStandard));
                Console.WriteLine("  Estimated Card: " + ValueOrUnavailable(result.Card.EstimatedCardName));
                Console.WriteLine("  Card Name Code: " + ValueOrUnavailable(result.Card.CardNameCode));
                Console.WriteLine("  PC/SC Protocol: " + ValueOrUnavailable(result.Card.PcscProtocol));
                Console.WriteLine("  Historical Bytes: " + ValueOrUnavailable(result.Card.HistoricalBytes));
                if (result.Card.AtrParseWarnings.Count > 0)
                {
                    Console.WriteLine("  ATR 解析注意:");
                    foreach (string warning in result.Card.AtrParseWarnings)
                    {
                        Console.WriteLine("    - " + warning);
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine("取得結果:");
            foreach (ProbeItem item in result.ProbeItems)
            {
                Console.WriteLine("  - " + item.Label + ": " + ToJapaneseProbeStatus(item.Status) + FormatProbeItem(item));
            }

            if (result.Errors.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("エラー:");
                foreach (ProbeError error in result.Errors)
                {
                    Console.WriteLine("  - " + error.Code + ": " + error.Message);
                    if (!string.IsNullOrEmpty(error.Detail))
                    {
                        Console.WriteLine("    " + error.Detail);
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine("安全境界:");
            Console.WriteLine("  保護領域、暗号化領域、残高、履歴、個人情報、権限のないデータは読み取りません。");
            Console.WriteLine("  Phase 1 では reader 設定変更、カード書き込み、MIFARE 認証、FeliCa サービス探索を行いません。");
        }

        private static void RenderPiccParameter(PiccParameterInfo info)
        {
            if (info == null)
            {
                return;
            }

            Console.WriteLine("    Auto PICC Polling: " + (info.AutoPiccPolling ? "Enabled" : "Disabled"));
            Console.WriteLine("    Auto ATS Generation: " + (info.AutoAtsGeneration ? "Enabled" : "Disabled"));
            Console.WriteLine("    Polling Interval: " + info.PollingInterval);
            Console.WriteLine("    Targets: " + (info.Targets.Count == 0 ? "なし" : string.Join(", ", info.Targets.ToArray())));
        }

        private static string FormatProbeItem(ProbeItem item)
        {
            if (!string.IsNullOrEmpty(item.Value))
            {
                return " - " + item.Value;
            }

            if (!string.IsNullOrEmpty(item.Reason))
            {
                return " - " + item.Reason;
            }

            return "";
        }

        private static string ToJapaneseProbeStatus(string status)
        {
            if (status == "Success") return "取得成功";
            if (status == "Failed") return "取得失敗";
            if (status == "SkippedByPolicy") return "安全方針で未取得";
            if (status == "NotApplicable") return "対象外";
            if (status == "Unsupported") return "未サポート";
            return status;
        }

        private static string ToJapaneseState(string state)
        {
            if (state == CardMonitorState.ReaderMissing.ToString()) return "リーダー未検出";
            if (state == CardMonitorState.ReaderReadyEmpty.ToString()) return "リーダーあり / カードなし";
            if (state == CardMonitorState.CardCandidate.ToString()) return "カード候補検出";
            if (state == CardMonitorState.CardPresentStable.ToString()) return "カード安定検出";
            if (state == CardMonitorState.CardProcessing.ToString()) return "カード処理中";
            if (state == CardMonitorState.CardDisplayed.ToString()) return "カード表示済み";
            if (state == CardMonitorState.CardRemovedStable.ToString()) return "カード取り外し済み";
            if (state == CardMonitorState.ReaderError.ToString()) return "リーダーエラー";
            return state;
        }

        private static string ValueOrUnavailable(string value)
        {
            return string.IsNullOrEmpty(value) ? "未取得" : value;
        }
    }

    internal static class JsonExporter
    {
        public static void Write(ProbeResult result, string path)
        {
            string json = ToJson(result);
            if (string.IsNullOrEmpty(path))
            {
                Console.WriteLine(json);
                return;
            }

            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, json, new UTF8Encoding(false));
        }

        public static string ToJson(ProbeResult result)
        {
            JsonBuilder json = new JsonBuilder();
            json.BeginObject();
            json.Property("application", result.Application);
            json.Property("version", result.Version);
            json.Property("timestamp", result.Timestamp);
            json.Property("mode", result.Mode);
            json.Property("state", result.State);
            json.Property("exitCode", result.ExitCode);
            json.Property("traceEnabled", result.TraceEnabled);
            json.Property("jsonOutput", result.JsonOutput);
            json.Property("requestedReader", result.RequestedReader);
            json.Property("selectedReader", result.SelectedReader);
            json.PropertyArray("readerNames", result.ReaderNames);
            WriteReaderInfo(json, result.ReaderInfo);
            WriteCardInfo(json, result.Card);
            WriteProbeItems(json, result.ProbeItems);
            WriteErrors(json, result.Errors);
            json.EndObject();
            return json.ToString();
        }

        private static void WriteReaderInfo(JsonBuilder json, ReaderInfo info)
        {
            json.PropertyName("readerInfo");
            if (info == null)
            {
                json.NullValue();
                return;
            }

            json.BeginObject();
            json.Property("readerName", info.ReaderName);
            json.Property("isAcr122Candidate", info.IsAcr122Candidate);
            json.Property("selectionWarning", info.SelectionWarning);
            json.Property("firmware", info.Firmware);
            json.Property("firmwareHex", info.FirmwareHex);
            json.Property("piccOperatingParameter", info.PiccOperatingParameter);
            json.PropertyName("piccOperatingParameterBits");
            if (info.PiccOperatingParameterBits == null)
            {
                json.NullValue();
            }
            else
            {
                json.BeginObject();
                json.Property("autoPiccPolling", info.PiccOperatingParameterBits.AutoPiccPolling);
                json.Property("autoAtsGeneration", info.PiccOperatingParameterBits.AutoAtsGeneration);
                json.Property("pollingInterval", info.PiccOperatingParameterBits.PollingInterval);
                json.PropertyArray("targets", info.PiccOperatingParameterBits.Targets);
                json.EndObject();
            }
            json.EndObject();
        }

        private static void WriteCardInfo(JsonBuilder json, CardInfo card)
        {
            json.PropertyName("card");
            if (card == null)
            {
                json.NullValue();
                return;
            }

            json.BeginObject();
            json.Property("atr", card.Atr);
            json.Property("atrLength", card.AtrLength);
            json.Property("historicalBytes", card.HistoricalBytes);
            json.Property("uid", card.Uid);
            json.Property("ats", card.Ats);
            json.Property("pcscProtocol", card.PcscProtocol);
            json.Property("pcscProtocolRaw", card.PcscProtocolRaw);
            json.Property("estimatedStandard", card.EstimatedStandard);
            json.Property("estimatedCardName", card.EstimatedCardName);
            json.Property("cardNameCode", card.CardNameCode);
            json.Property("classificationReason", card.ClassificationReason);
            json.Property("identityCode", card.IdentityCode);
            WriteIdentitySource(json, card.IdentitySource);
            json.PropertyArray("atrParseWarnings", card.AtrParseWarnings);
            json.EndObject();
        }

        private static void WriteIdentitySource(JsonBuilder json, IdentitySourceInfo source)
        {
            json.PropertyName("identitySource");
            if (source == null)
            {
                json.NullValue();
                return;
            }

            json.BeginObject();
            json.Property("uid", source.Uid);
            json.Property("atr", source.Atr);
            json.Property("ats", source.Ats);
            json.Property("pmm", source.Pmm);
            json.Property("type", source.Type);
            json.Property("keyConfigured", source.KeyConfigured);
            json.Property("algorithm", source.Algorithm);
            json.Property("hashFormat", source.HashFormat);
            json.Property("normalizedInputWithoutKey", source.NormalizedInputWithoutKey);
            json.EndObject();
        }

        private static void WriteProbeItems(JsonBuilder json, List<ProbeItem> items)
        {
            json.PropertyName("probeItems");
            json.BeginArray();
            foreach (ProbeItem item in items)
            {
                json.BeginObject();
                json.Property("key", item.Key);
                json.Property("label", item.Label);
                json.Property("status", item.Status);
                json.Property("value", item.Value);
                json.Property("reason", item.Reason);
                json.Property("statusCode", item.StatusCode);
                json.Property("detail", item.Detail);
                json.EndObject();
            }
            json.EndArray();
        }

        private static void WriteErrors(JsonBuilder json, List<ProbeError> errors)
        {
            json.PropertyName("errors");
            json.BeginArray();
            foreach (ProbeError error in errors)
            {
                json.BeginObject();
                json.Property("code", error.Code);
                json.Property("message", error.Message);
                json.Property("detail", error.Detail);
                json.EndObject();
            }
            json.EndArray();
        }
    }

    internal sealed class JsonBuilder
    {
        private readonly StringBuilder _builder = new StringBuilder();
        private readonly Stack<bool> _needsComma = new Stack<bool>();

        public void BeginObject()
        {
            BeforeValue();
            _builder.Append('{');
            _needsComma.Push(false);
        }

        public void EndObject()
        {
            _builder.Append('}');
            _needsComma.Pop();
            AfterValue();
        }

        public void BeginArray()
        {
            BeforeValue();
            _builder.Append('[');
            _needsComma.Push(false);
        }

        public void EndArray()
        {
            _builder.Append(']');
            _needsComma.Pop();
            AfterValue();
        }

        public void PropertyName(string name)
        {
            BeforeProperty();
            WriteQuoted(name);
            _builder.Append(':');
        }

        public void Property(string name, string value)
        {
            PropertyName(name);
            if (value == null)
            {
                NullValue();
            }
            else
            {
                WriteQuoted(value);
                AfterValue();
            }
        }

        public void Property(string name, int value)
        {
            PropertyName(name);
            _builder.Append(value);
            AfterValue();
        }

        public void Property(string name, bool value)
        {
            PropertyName(name);
            _builder.Append(value ? "true" : "false");
            AfterValue();
        }

        public void PropertyArray(string name, IEnumerable<string> values)
        {
            PropertyName(name);
            BeginArray();
            foreach (string value in values)
            {
                if (value == null)
                {
                    NullValue();
                }
                else
                {
                    BeforeValue();
                    WriteQuoted(value);
                    AfterValue();
                }
            }
            EndArray();
        }

        public void NullValue()
        {
            BeforeValue();
            _builder.Append("null");
            AfterValue();
        }

        public override string ToString()
        {
            return _builder.ToString();
        }

        private void BeforeProperty()
        {
            if (_needsComma.Count > 0 && _needsComma.Peek())
            {
                _builder.Append(',');
                _needsComma.Pop();
                _needsComma.Push(false);
            }
        }

        private void BeforeValue()
        {
            if (_needsComma.Count > 0 && _needsComma.Peek())
            {
                _builder.Append(',');
                _needsComma.Pop();
                _needsComma.Push(false);
            }
        }

        private void AfterValue()
        {
            if (_needsComma.Count > 0)
            {
                _needsComma.Pop();
                _needsComma.Push(true);
            }
        }

        private void WriteQuoted(string value)
        {
            _builder.Append('"');
            foreach (char c in value)
            {
                if (c == '\\') _builder.Append("\\\\");
                else if (c == '"') _builder.Append("\\\"");
                else if (c == '\n') _builder.Append("\\n");
                else if (c == '\r') _builder.Append("\\r");
                else if (c == '\t') _builder.Append("\\t");
                else if (char.IsControl(c)) _builder.Append("\\u" + ((int)c).ToString("X4"));
                else _builder.Append(c);
            }
            _builder.Append('"');
        }
    }

    internal sealed class TraceSink : IDisposable
    {
        private readonly object _gate = new object();
        private readonly StreamWriter _writer;
        private int _sequence;

        public TraceSink(bool enabled)
        {
            Enabled = enabled;
            if (!enabled)
            {
                return;
            }

            Directory.CreateDirectory("logs");
            string path = Path.Combine("logs", "trace-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log");
            _writer = new StreamWriter(path, false, new UTF8Encoding(false));
            Event("Trace", "トレースを開始しました: " + path);
        }

        public bool Enabled { get; private set; }

        public void Event(string name, string message)
        {
            if (!Enabled)
            {
                return;
            }

            Write(name, message);
        }

        public void Apdu(string name, byte[] command, byte[] response, TimeSpan elapsed)
        {
            if (!Enabled)
            {
                return;
            }

            Write("APDU " + name, "send=" + HexUtil.ToHex(command) + " recv=" + HexUtil.ToHex(response) + " elapsedMs=" + (long)elapsed.TotalMilliseconds);
        }

        private void Write(string name, string message)
        {
            lock (_gate)
            {
                _sequence++;
                _writer.WriteLine(DateTimeOffset.Now.ToString("O") + " #" + _sequence + " [" + Thread.CurrentThread.ManagedThreadId + "] " + name + " " + message);
                _writer.Flush();
            }
        }

        public void Dispose()
        {
            if (_writer != null)
            {
                Event("Trace", "トレースを終了しました。");
                _writer.Dispose();
            }
        }
    }

    internal sealed class PcscException : Exception
    {
        public PcscException(string apiName, int errorCode)
            : base(apiName + " failed: 0x" + unchecked((uint)errorCode).ToString("X8") + " " + ErrorCatalog.PcscError(errorCode))
        {
            ApiName = apiName;
            ErrorCode = errorCode;
        }

        public string ApiName { get; private set; }
        public int ErrorCode { get; private set; }
        public string ErrorCodeHex
        {
            get { return "0x" + unchecked((uint)ErrorCode).ToString("X8"); }
        }
    }

    internal static class ErrorCatalog
    {
        public static string PcscError(int code)
        {
            uint unsigned = unchecked((uint)code);
            switch (unsigned)
            {
                case 0x8010000A: return "タイムアウトしました。";
                case 0x8010000B: return "スマートカードサービスが応答しません。";
                case 0x8010000C: return "カードが挿入されていません。";
                case 0x8010000D: return "不明なリーダーです。";
                case 0x80100016: return "リーダーまたはカードが利用中です。";
                case 0x80100017: return "共有違反です。";
                case 0x8010001D: return "カードが取り外されました。";
                case 0x8010002E: return "利用可能なスマートカードリーダーがありません。";
                default: return "PC/SC エラーです。";
            }
        }

        public static string StatusWord(string statusWord)
        {
            switch (statusWord)
            {
                case "90 00": return "成功";
                case "62 82": return "読み取りデータが短い、または EOF 前に警告が返りました。";
                case "63 00": return "認証または検証に失敗しました。";
                case "67 00": return "長さが正しくありません。";
                case "69 82": return "セキュリティ条件を満たしていません。";
                case "6A 81": return "機能がサポートされていません。";
                case "6A 82": return "ファイルまたは対象が見つかりません。";
                case "6A 86": return "P1/P2 が正しくありません。";
                case "6D 00": return "INS がサポートされていません。";
                case "6E 00": return "CLA がサポートされていません。";
                default: return "未登録のステータスです。";
            }
        }

        public static string ProtocolName(uint protocol)
        {
            if (protocol == PcscNative.SCARD_PROTOCOL_T0) return "T=0";
            if (protocol == PcscNative.SCARD_PROTOCOL_T1) return "T=1";
            if (protocol == (PcscNative.SCARD_PROTOCOL_T0 | PcscNative.SCARD_PROTOCOL_T1)) return "T=0/T=1";
            return "Unknown(" + protocol + ")";
        }

        public static string CardName(string code)
        {
            switch (code)
            {
                case "00 01": return "MIFARE Classic 1K";
                case "00 02": return "MIFARE Classic 4K";
                case "00 03": return "MIFARE Ultralight";
                case "00 26": return "MIFARE Mini";
                case "F0 04": return "Topaz / Jewel";
                case "F0 11": return "FeliCa 212K";
                case "F0 12": return "FeliCa 424K";
                default:
                    if (code != null && code.StartsWith("FF ", StringComparison.Ordinal))
                    {
                        return "Undefined / SAK " + code.Substring(3);
                    }
                    return "不明";
            }
        }
    }

    internal static class HexUtil
    {
        public static string ToHex(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return "";
            }

            StringBuilder builder = new StringBuilder(bytes.Length * 3);
            for (int i = 0; i < bytes.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(' ');
                }
                builder.Append(bytes[i].ToString("X2"));
            }
            return builder.ToString();
        }

        public static string ToCompactLowerHex(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return "";
            }

            StringBuilder builder = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("x2"));
            }

            return builder.ToString();
        }

        public static string ToPrintableAscii(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return "";
            }

            StringBuilder builder = new StringBuilder(bytes.Length);
            foreach (byte b in bytes)
            {
                if (IsPrintableAscii(b))
                {
                    builder.Append((char)b);
                }
            }

            return builder.ToString();
        }

        public static bool IsPrintableAscii(byte value)
        {
            return value >= 0x20 && value <= 0x7E;
        }
    }

    internal static class VersionInfo
    {
        public static string InformationalVersion
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false);
                if (attributes.Length == 0)
                {
                    return Assembly.GetExecutingAssembly().GetName().Version.ToString();
                }

                return ((AssemblyInformationalVersionAttribute)attributes[0]).InformationalVersion;
            }
        }
    }

    internal static class PcscNative
    {
        public const int SCARD_S_SUCCESS = 0;
        public const int SCARD_E_TIMEOUT = unchecked((int)0x8010000A);
        public const int SCARD_E_NO_READERS_AVAILABLE = unchecked((int)0x8010002E);
        public const uint SCARD_SCOPE_SYSTEM = 2;
        public const uint SCARD_SHARE_SHARED = 2;
        public const uint SCARD_SHARE_DIRECT = 3;
        public const uint SCARD_PROTOCOL_UNDEFINED = 0;
        public const uint SCARD_PROTOCOL_T0 = 1;
        public const uint SCARD_PROTOCOL_T1 = 2;
        public const uint SCARD_LEAVE_CARD = 0;
        public const uint SCARD_STATE_UNAWARE = 0x00000000;
        public const uint SCARD_STATE_EMPTY = 0x00000010;
        public const uint SCARD_STATE_PRESENT = 0x00000020;
        public static readonly uint IoctlCcidEscape = SCardCtlCode(3500);

        [StructLayout(LayoutKind.Sequential)]
        public struct SCARD_IO_REQUEST
        {
            public uint dwProtocol;
            public uint cbPciLength;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct SCARD_READERSTATE
        {
            public string szReader;
            public IntPtr pvUserData;
            public uint dwCurrentState;
            public uint dwEventState;
            public uint cbAtr;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 36)]
            public byte[] rgbAtr;
        }

        [DllImport("winscard.dll")]
        public static extern int SCardEstablishContext(uint dwScope, IntPtr pvReserved1, IntPtr pvReserved2, out IntPtr phContext);

        [DllImport("winscard.dll")]
        public static extern int SCardReleaseContext(IntPtr hContext);

        [DllImport("winscard.dll", CharSet = CharSet.Unicode)]
        public static extern int SCardListReaders(IntPtr hContext, string mszGroups, StringBuilder mszReaders, ref int pcchReaders);

        [DllImport("winscard.dll", CharSet = CharSet.Unicode)]
        public static extern int SCardConnect(IntPtr hContext, string szReader, uint dwShareMode, uint dwPreferredProtocols, out IntPtr phCard, out uint pdwActiveProtocol);

        [DllImport("winscard.dll")]
        public static extern int SCardDisconnect(IntPtr hCard, uint dwDisposition);

        [DllImport("winscard.dll", CharSet = CharSet.Unicode)]
        public static extern int SCardStatus(IntPtr hCard, StringBuilder mszReaderNames, ref int pcchReaderLen, out uint pdwState, out uint pdwProtocol, byte[] pbAtr, ref int pcbAtrLen);

        [DllImport("winscard.dll")]
        public static extern int SCardTransmit(IntPtr hCard, ref SCARD_IO_REQUEST pioSendPci, byte[] pbSendBuffer, int cbSendLength, IntPtr pioRecvPci, byte[] pbRecvBuffer, ref int pcbRecvLength);

        [DllImport("winscard.dll")]
        public static extern int SCardControl(IntPtr hCard, uint dwControlCode, byte[] lpInBuffer, int nInBufferSize, byte[] lpOutBuffer, int nOutBufferSize, out int lpBytesReturned);

        [DllImport("winscard.dll", CharSet = CharSet.Unicode)]
        public static extern int SCardGetStatusChange(IntPtr hContext, uint dwTimeout, [In, Out] SCARD_READERSTATE[] rgReaderStates, int cReaders);

        private static uint SCardCtlCode(uint code)
        {
            return (0x31u << 16) | (code << 2);
        }
    }
}
