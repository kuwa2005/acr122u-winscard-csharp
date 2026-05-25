using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NFC_CardReader;
using NFC_CardReader.ACR122U;
using NFC_CardReader.ACR122U.CardTypes.MifareClassic;
using NFC_CardReader.ACR122UManager;
using System.IO;
using NFC_CardReader.WinSCard;

#region BaseTesting
//#region WincardAPIImportTesting

//#region ContextGetReaders
//Console.WriteLine("Currently connected readers: ");
//int MaxNumber = 0;
//List<string> Names = WinSmartCardContext.ListReadersAsStringsStatic();
//foreach (string Reader in Names)
//{
//    Console.WriteLine("\t" + MaxNumber + ":" + Reader);
//    MaxNumber += 1;
//}
//Console.WriteLine("Filtering to usable test readers. Note: driver update adds ACS ACR122U PICC Interface that are not usable.");
//Console.WriteLine("Currently connected ACR122U readers: ");
//Names = WinSmartCardContext.ListReadersAsStringsStatic();
//Names = Names.Where(x=> x.Contains("ACS ACR122") && !x.Contains("ACS ACR122U PICC Interface")).ToList();
//MaxNumber = 0;
//foreach (string Reader in Names)
//{
//    Console.WriteLine("\t" + MaxNumber + ":" + Reader);
//    MaxNumber += 1;
//}
//#endregion

//#region ContextConnect
//int Selection = 0;
////Console.WriteLine("Please try and select one.\nNote do not pick ACS ACR122U PICC Interface.\nDriver update added them and it doesnt work.\nA ACS ACR122, will work however.");
//Console.WriteLine("Please try and select one.");
//while (!(int.TryParse(Console.ReadLine(), out Selection) && -1 < Selection && Selection < MaxNumber))
//{
//    Console.WriteLine("Oops thats not a valid selection number. Try again.");
//    //"ACS ACR122U PICC Interface Interface 0"
//}

//#region ContextInIt
//WinSmartCardContext Context = new WinSmartCardContext(OperationScopes.SCARD_SCOPE_SYSTEM, Names[Selection]);

//#region CardLessStaticFuncs/Methods
//ACR122U_PICCOperatingParametersControl Settings;
//NFC_CardReader.ACR122U.ACR122U_SmartCard.GetPICCOperatingParameterStateStatic(Context, out Settings);
//Console.WriteLine("Getting PICC: " + Settings);
//#endregion

//#endregion

//#region WinscardStatusChange
//Console.WriteLine("Testing polling/blocking calls");
//Console.WriteLine("Please change state");
//ReadersCurrentState[] States = new ReadersCurrentState[] { new ReadersCurrentState() { ReaderName = Names[Selection] } };
//Context.GetStatusChange(5000, ref States);
//Console.WriteLine("Test 1 Results (Init)");
//Console.WriteLine("\t\tStates ATR: " + BitConverter.ToString(States.Last().ATR));
//Console.WriteLine("\t\tStates Event State: " + States[0].EventState);
//Console.WriteLine("\t\tStates Current State: " + States[0].CurrentState);
//Console.WriteLine("\t\tStates Changed Reader: " + States[0].ReaderName);

//States[0].CurrentState = States[0].EventState;
//Context.GetStatusChange(5000, ref States);
//Console.WriteLine("Test 2 Results (with Timeout)");
//Console.WriteLine("\t\tStates ATR: " + BitConverter.ToString(States.Last().ATR));
//Console.WriteLine("\t\tStates Event State: " + States[0].EventState);
//Console.WriteLine("\t\tStates Current State: " + States[0].CurrentState);
//Console.WriteLine("\t\tStates Changed Reader: " + States[0].ReaderName);

//States[0].CurrentState = States[0].EventState;
//Context.GetStatusChange(5000, ref States);
//Console.WriteLine("Test 3 Results (with Timeout)");
//Console.WriteLine("\t\tStates ATR: " + BitConverter.ToString(States.Last().ATR));
//Console.WriteLine("\t\tStates Event State: " + States[0].EventState);
//Console.WriteLine("\t\tStates Current State: " + States[0].CurrentState);
//Console.WriteLine("\t\tStates Changed Reader: " + States[0].ReaderName);

//ReadersCurrentState LastState;

/////Again but this time for ever
//States[0].CurrentState = States[0].EventState;
//LastState = States[0];
//while (LastState.EventState == States[0].EventState)
//{
//    Context.GetStatusChange(5000, ref States);
//}
//Console.WriteLine("Test 4 Results (forever)");
//Console.WriteLine("\t\tStates ATR: " + BitConverter.ToString(States.Last().ATR));
//Console.WriteLine("\t\tStates Event State: " + States[0].EventState);
//Console.WriteLine("\t\tStates Current State: " + States[0].CurrentState);
//Console.WriteLine("\t\tStates Changed Reader: " + States[0].ReaderName);


//States[0].CurrentState = States[0].EventState;
//LastState = States[0];
//while (LastState.EventState == States[0].EventState)
//{
//    Context.GetStatusChange(5000, ref States);
//}
//Console.WriteLine("Test 5 Results (forever)");
//Console.WriteLine("\t\tStates ATR: " + BitConverter.ToString(States.Last().ATR));
//Console.WriteLine("\t\tStates Event State: " + States[0].EventState);
//Console.WriteLine("\t\tStates Current State: " + States[0].CurrentState);
//Console.WriteLine("\t\tStates Changed Reader: " + States[0].ReaderName);

//#endregion

//#endregion

//WinSmartCard WSC = Context.CardConnect(SmartCardShareTypes.SCARD_SHARE_SHARED);
//Console.WriteLine("Connected to card as winscard.\n\tProperties are");

//Console.WriteLine("\t\tIsAliveWithAContext: " + WSC.IsAliveWithContext);
//Console.WriteLine("\t\tATRString: " + WSC.ATRString);
//Console.WriteLine("\t\tATR(ConvertedFromBytes): " + BitConverter.ToString(WSC.ATR.ToArray()));
//Console.WriteLine("\t\tProtocol: " + WSC.Protocol);
//Console.WriteLine("\t\tReaderName: " + WSC.Parent.ConnectedReaderName);

//#region WinscardGetStatus
//SmartCardStatus Status;
//SmartCardProtocols Protocol;
//string ATR;
//string ReaderName;
//WSC.GetStatus(out ReaderName, out Status, out Protocol, out ATR);
//Console.WriteLine("Getting Status");
//Console.WriteLine("\tReaderName: " + ReaderName);
//Console.WriteLine("\tStatus: " + Status);
//Console.WriteLine("\tProtocol: " + Protocol);
//Console.WriteLine("\tATR: " + ATR);
//#endregion

//#region WinscardGetATR(NotSupportedByMyReader)
////WSC.GetAttrib((SmartCardATR)400100, out ATR);
//Console.WriteLine("Getting ATR");
//Console.WriteLine("\tATR: Get ATR is not supported by this device");
//#endregion

//#endregion

//#region ACR122U_ADU_API_Testing

//#region InIt
//ACR122U_SmartCard ACR122U_SmartCard = new ACR122U_SmartCard(WSC);
//Console.WriteLine("Upgraded connection to card to ACR122U_SmartCard for API");
//#endregion

//#region ACRGetStatus
//bool CardPresent;
//ACR122U_StatusErrorCodes ACRError;
//bool FieldPresent;
//byte NumberOfTargets;
//byte LogicalNumber;
//ACR122U_StatusBitRateInReception BitRateInReception;
//ACR122U_StatusBitsRateInTransmiton BitsRateInTransmiton;
//ACR122U_StatusModulationType ModulationType;
//ACR122U_SmartCard.GetStatus(out CardPresent, out ACRError, out FieldPresent, out NumberOfTargets, out LogicalNumber, out BitRateInReception, out BitsRateInTransmiton, out ModulationType);
//Console.WriteLine("Getting ACR122u Status");
//Console.WriteLine("\tCard Present: " + CardPresent);
//Console.WriteLine("\tACR Error: " + ACRError);
//Console.WriteLine("\tFields Present: " + FieldPresent);
//Console.WriteLine("\tNumber Of Targets: " + NumberOfTargets);
//Console.WriteLine("\tLogical Number: " + LogicalNumber);
//Console.WriteLine("\tBit Rate In Reception: " + BitRateInReception);
//Console.WriteLine("\tBit Rate In Transmiton: " + BitsRateInTransmiton);
//Console.WriteLine("\tModulation Type: " + ModulationType);
////PrintACRError(ACR122U_SmartCard);
//#endregion

//#region ACRGet/SetPICC
//ACR122U_SmartCard.GetPICCOperatingParameterState(out Settings);
//Console.WriteLine("Getting PICC: " + Settings);
//Settings = ACR122U_PICCOperatingParametersControl.AllOff;
//ACR122U_SmartCard.SetPICCOperatingParameterState(ref Settings);
//Console.WriteLine("Setting PICC");
//Console.WriteLine("\tPICC Setting Return: " + Settings);
//ACR122U_SmartCard.GetPICCOperatingParameterState(out Settings);
//Console.WriteLine("Getting PICC: " + Settings);
//Settings = ACR122U_PICCOperatingParametersControl.AllOn;
//ACR122U_SmartCard.SetPICCOperatingParameterState(ref Settings);
//Console.WriteLine("Setting PICC");
//Console.WriteLine("\tPICC Setting Return: " + Settings);
//ACR122U_SmartCard.GetPICCOperatingParameterState(out Settings);
//Console.WriteLine("Getting PICC: " + Settings);
////PrintACRError(ACR122U_SmartCard);
//#endregion

//#region GetUDI
//Console.WriteLine("UDI string: " + ACR122U_SmartCard.GetcardUID());
//#endregion

//#region GetATS(NotSupportedByMyParticACRorItsFirmware)
//try
//{
//    Console.WriteLine("ATS string: " + ACR122U_SmartCard.GetcardATS());
//}
//catch (ACR122U_SmartCardException Ex)
//{
//    if (Ex.ACRErrorOnException == ACR122U_ResposeErrorCodes.FuctionNotSupported)
//        Console.WriteLine("ATS string: " + ACR122U_SmartCard.GetACRErrMsg(Ex.ACRErrorOnException));
//    else
//        throw Ex;
//}
//PrintACRError(ACR122U_SmartCard);
//#endregion

//#region LoadAthenticationAndAthenticate+ReadWrite
//Console.WriteLine("Loading athentication Keys to 1: " + ACR122U_SmartCard.LoadAthenticationKeys(ACR122U_KeyMemories.Key1, new byte[6] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }));

//byte[] Data;
//Console.WriteLine("Attempting to write block 4 (sector 1, block 1) expected fail(not athenticated): " + ACR122U_SmartCard.WriteBlock(new byte[16] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }, 5));
//Console.WriteLine("Attempting to read block 4 (sector 1, block 1) expected fail(not athenticated): " + ACR122U_SmartCard.ReadBlock(out Data,  5));
////PrintACRError(ACR122U_SmartCard);

//Console.WriteLine("Athentication Key A to 1: " + ACR122U_SmartCard.Athentication(5, ACR122U_Keys.KeyA, ACR122U_KeyMemories.Key1));
////PrintACRError(ACR122U_SmartCard);
////seems to return true if keys are the same?
//Console.WriteLine("Attempting to write block 5 (sector 1, block 1) expected fail(not athenticated(A is read)): " + ACR122U_SmartCard.WriteBlock(new byte[16] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }, 5));
//Console.WriteLine("Attempting to read block 5 (sector 1, block 1): " + ACR122U_SmartCard.ReadBlock(out Data, 5));
//Console.WriteLine("\tData: " + BitConverter.ToString(Data));
////PrintACRError(ACR122U_SmartCard);
//Console.WriteLine("Athentication Key B to 1: " + ACR122U_SmartCard.Athentication(5, ACR122U_Keys.KeyB, ACR122U_KeyMemories.Key1));

//Console.WriteLine("Attempting to write block 5 (sector 1, block 1) All 0xFF: " + ACR122U_SmartCard.WriteBlock(new byte[16] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }, 5));
//Console.WriteLine("Attempting to read block 5 (sector 1, block 1): " + ACR122U_SmartCard.ReadBlock(out Data, 5));
//Console.WriteLine("\tData: " + BitConverter.ToString(Data));

//Console.WriteLine("Attempting to write block 5 (sector 1, block 1) All 0x00: " + ACR122U_SmartCard.WriteBlock(new byte[16] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, 5));
//Console.WriteLine("Attempting to read block 5 (sector 1, block 1): " + ACR122U_SmartCard.ReadBlock(out Data, 5));
//Console.WriteLine("\tData: " + BitConverter.ToString(Data));
//#endregion

//#region Values
//Int32 Data2;
//Console.WriteLine("Attempting to write value to block 5 (sector 1, block 1) Value = 5: " + ACR122U_SmartCard.WriteValueToBlock(5, 5));
//Console.WriteLine("Attempting to read value from block 5 (sector 1, block 1) Value ?= 5: " + ACR122U_SmartCard.ReadValueFromBlock(out Data2, 5));
//Console.WriteLine("\tData: " + Data2);

//Console.WriteLine("Attempting to write value to block 5 (sector 1, block 1) Value = 0: " + ACR122U_SmartCard.WriteValueToBlock(0, 5));
//Console.WriteLine("Attempting to read value from block 5 (sector 1, block 1) Value ?= 0: " + ACR122U_SmartCard.ReadValueFromBlock(out Data2, 5));
//Console.WriteLine("\tData: " + Data2);

//Console.WriteLine("Attempting to increment value at block 5 (sector 1, block 1): " + ACR122U_SmartCard.IncrementValue(1, 5));
//Console.WriteLine("Attempting to read value from block 5 (sector 1, block 1) Value ?= 1: " + ACR122U_SmartCard.ReadValueFromBlock(out Data2, 5));
//Console.WriteLine("\tData: " + Data2);

//Console.WriteLine("Attempting to decrement value at block 5 block 5 (sector 1, block 1): " + ACR122U_SmartCard.DecrementValue(1, 5));
//Console.WriteLine("Attempting to read value from block 5 (sector 1, block 1) Value ?= 0: " + ACR122U_SmartCard.ReadValueFromBlock(out Data2, 5));
//Console.WriteLine("\tData: " + Data2);

//Console.WriteLine("Attempting to increment value at block 5 (sector 1, block 1): " + ACR122U_SmartCard.IncrementValue(1, 5));
//Console.WriteLine("Attempting to read value from block 5 (sector 1, block 1) Value ?= 1: " + ACR122U_SmartCard.ReadValueFromBlock(out Data2, 5));
//Console.WriteLine("\tData: " + Data2);

//Console.WriteLine("Attempting to copy value at block 5 to block 4 (sector 1, block 1 => sector 1, block 0): " + ACR122U_SmartCard.Copy(5, 4));
//Console.WriteLine("Attempting to read value from block 5 (sector 1, block 1) Value: " + ACR122U_SmartCard.ReadValueFromBlock(out Data2, 5));
//Console.WriteLine("\tData: " + Data2);
//Console.WriteLine("Attempting to read value from block 4 (sector 1, block 1) Value[4] ?= Value[5]: " + ACR122U_SmartCard.ReadValueFromBlock(out Data2, 4));
//Console.WriteLine("\tData: " + Data2);

//#endregion

//#region SetLEDandBuzzerControl
////Console.WriteLine("Testing LED/BuzzerControl\n\tUnit shoud T1 & T2 Buzz for 2000ms. This should happen 2 times with blinks between.\n\tPay close attention with the break points first time through.\n\tAfter it is a physical test.");
////byte OddData;
////ACR122U_SmartCard.SetLEDandBuzzerControl(ACR122U_LEDControl.InitialRedBlinkingState | ACR122U_LEDControl.RedBlinkingMask | ACR122U_LEDControl.RedLEDStateMask | ACR122U_LEDControl.GreenFinalState, 20, 20, 2, ACR122U_BuzzerControl.BuzzerOnT1Cycle, out OddData);
////Console.WriteLine("\tDone.\n\tAdditional odd Data(some times shows with no expanation): " + OddData);
//#endregion

//#region CardLessStaticFuncs/Methods
////NFC_CardReader.ACR122U.ACR122U_SmartCard.GetPICCOperatingParameterStateStatic(Context, out Settings);
////Console.WriteLine("Getting PICC: " + Settings);
//#endregion

////ACR122U_SmartCard.Dispose();
////Context.Dispose();
//#endregion
//Console.WriteLine("Athentication Key A to 1: " + ACR122U_SmartCard.Athentication(5, ACR122U_Keys.KeyA, ACR122U_KeyMemories.Key1));

//Console.ReadKey();
#endregion
#region NFCMaifareClassicTesting
//namespace CardReader_TestFileLogger
//{
//    class Program
//    {
//        static void Main(string[] args)
//        {
//            byte[] AcceptedATR = new byte[] { 0x3B, 0x8F, 0x80, 0x01, 0x80, 0x4F, 0x0C, 0xA0, 0x00, 0x00, 0x03, 0x06, 0x03, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x6A };
//            ACR122UManager Manager = new ACR122UManager(ACR122UManager.GetACR122UReaders().FirstOrDefault());
//            //
//            ACR122U_MifareClassic_Status Status;
//            Manager.GetStatus(out Status);
//            //
//            ACR122U_PICCOperatingParametersControl ControlOptions = ACR122U_PICCOperatingParametersControl.AllOn;
//            Manager.SetPICCOperatingParameterState(ref ControlOptions);
//            //
//            Console.WriteLine("PIC options:\n" + ControlOptions);
//            Console.WriteLine("Starting Status:\n\tCard: " + Status.Card + "\n\tError: " + Status.ErrorCode);
//            //
//            ACR122UManager.GlobalCardCheck = (e) =>
//            {
//                bool CeckSuccess = false;
//                if (e.ATR.Length == AcceptedATR.Length)
//                {
//                    CeckSuccess = true;
//                    for (int i = 0; i < e.ATR.Length; i++)
//                    {
//                        if (e.ATR[i] != AcceptedATR[i])
//                        {
//                            CeckSuccess = false;
//                            break;
//                        }
//                    }
//                }
//                return CeckSuccess;
//            };

//            Manager.CheckCard = true;

//            ManagerTest Test = new ManagerTest(Manager);

//            Manager.AcceptedCardScaned += Test.TestAccept;
//            Manager.CardStateChanged += Test.TestStateChange;
//            Manager.RejectedCardScaned += Test.TestRejected;
//            Manager.CardDetected += Test.TestCardDetected;
//            Manager.CardRemoved += Test.TestCardRemoved;
//            List<string> Names = WinSmartCardContext.ListReadersAsStringsStatic();
//            Console.ReadKey();

//        }

//        static class FileLogger
//        {
//            static readonly string Location = Environment.CurrentDirectory + "\\CardReaderOutput.txt";

//            public static void WriteLine(string Write)
//            {
//                using (StreamWriter SW = new StreamWriter(File.Open(Location, FileMode.Append)))
//                {
//                    SW.WriteLine(Write);
//                }
//            }

//            public static void WriteLine(string Write, params object[] obj)
//            {
//                using (StreamWriter SW = new StreamWriter(File.Open(Location, FileMode.Append)))
//                {
//                    SW.WriteLine(string.Format(Write, obj));
//                }
//            }

//        }

//        public class ManagerTest
//        {

//            ACR122UManager Manager;

//            public ManagerTest(ACR122UManager M)
//            {
//                Manager = M;
//            }

//            public void TestStateChange(object sender, ACRCardStateChangeEventArg e)
//            {
//                Console.WriteLine("CardReaders state has changed");
//                Console.WriteLine("State Enum : {0}", e.EventState);
//                Console.WriteLine("State as Hex : {0:x}", (int)e.EventState);
//                Console.WriteLine("ATR : {0}", e.ATRString);
//            }

//            public void TestAccept(object sender, ACRCardAcceptedCardScanEventArg e)
//            {
//                Console.WriteLine("CardReader has accepted Card");
//                Console.WriteLine("State Enum : {0}", e.EventState);
//                Console.WriteLine("State as Hex : {0:x}", (int)e.EventState);
//                Console.WriteLine("ATR : {0}", e.ATRString);

//                if (Manager.Card == null)
//                {
//                    #region BasicConnect
//                    ACR122U_MifareClassic_SmartCard Card = Manager.ConnectToMifareClassicCard();
//                    Console.WriteLine("\tCard Conneted");
//                    Console.WriteLine("\tUDI: " + Card.GetcardUID());
//                    #endregion

//                    #region ValueTesting
//                    byte[] Data;
//                    Console.WriteLine("\tLoading athentication Keys to Key Memory 1: 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF");
//                    Card.LoadAthenticationKeys(ACR122U_KeyMemories.Key1, new byte[6] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF });
//                    Console.WriteLine("\tAthentication Key B (Read/Write Key) to Key Memory 1: ");
//                    Card.Athentication(5, ACR122U_Keys.KeyB, ACR122U_KeyMemories.Key1);
//                    Console.WriteLine("\tAttempting to write block 5 (sector 1, block 1) All 0xFF: ");
//                    Card.WriteBlock(new byte[16] { 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }, 5);
//                    Console.WriteLine("\tAttempting to read block 5 (sector 1, block 1): ");
//                    Card.ReadBlock(out Data, 5);
//                    Console.WriteLine("\tData: " + BitConverter.ToString(Data));
//                    Console.WriteLine("\tAttempting to write block 5 (sector 1, block 1) All 0x00: ");
//                    Card.WriteBlock(new byte[16] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, 5);
//                    Console.WriteLine("\tAttempting to read block 5 (sector 1, block 1): ");
//                    Card.ReadBlock(out Data, 5);
//                    Console.WriteLine("\tData: " + BitConverter.ToString(Data));
//                    #endregion

//                    #region Values
//                    Int32 Data2;
//                    Console.WriteLine("\tAttempting to write value to block 5 (sector 1, block 1) Value = 5: ");
//                    Card.WriteValueToBlock(5, 5);
//                    Console.WriteLine("\tAttempting to read value from block 5 (sector 1, block 1) Value ?= 5: ");
//                    Card.ReadValueFromBlock(out Data2, 5);
//                    Console.WriteLine("\t\tData: " + Data2);

//                    Console.WriteLine("\tAttempting to write value to block 5 (sector 1, block 1) Value = 0: ");
//                    Card.WriteValueToBlock(0, 5);
//                    Console.WriteLine("\tAttempting to read value from block 5 (sector 1, block 1) Value ?= 0: ");
//                    Card.ReadValueFromBlock(out Data2, 5);
//                    Console.WriteLine("\t\tData: " + Data2);

//                    Console.WriteLine("\tAttempting to increment value at block 5 (sector 1, block 1): ");
//                    Card.IncrementValue(1, 5);
//                    Console.WriteLine("\tAttempting to read value from block 5 (sector 1, block 1) Value ?= 1: ");
//                    Card.ReadValueFromBlock(out Data2, 5);
//                    Console.WriteLine("\t\tData: " + Data2);

//                    Console.WriteLine("\tAttempting to decrement value at block 5 block 5 (sector 1, block 1): ");
//                    Card.DecrementValue(1, 5);
//                    Console.WriteLine("\tAttempting to read value from block 5 (sector 1, block 1) Value ?= 0: ");
//                    Card.ReadValueFromBlock(out Data2, 5);
//                    Console.WriteLine("\t\tData: " + Data2);

//                    Console.WriteLine("\tAttempting to increment value at block 5 (sector 1, block 1): ");
//                    Card.IncrementValue(1, 5);
//                    Console.WriteLine("\tAttempting to read value from block 5 (sector 1, block 1) Value ?= 1: ");
//                    Card.ReadValueFromBlock(out Data2, 5);
//                    Console.WriteLine("\t\tData: " + Data2);

//                    Console.WriteLine("\tAttempting to copy value at block 5 to block 4 (sector 1, block 1 => sector 1, block 0): ");
//                    Card.Copy(5, 4);
//                    Console.WriteLine("\tAttempting to read value from block 5 (sector 1, block 1) Value: ");
//                    Card.ReadValueFromBlock(out Data2, 5);
//                    Console.WriteLine("\t\tData: " + Data2);

//                    Console.WriteLine("\tAttempting to read value from block 4 (sector 1, block 1) Value[4] ?= Value[5]: ");
//                    Card.ReadValueFromBlock(out Data2, 4);
//                    Console.WriteLine("\t\tData: " + Data2);
//                    #endregion

//                    Manager.DisconnectToCard();

//                }

//            }

//            public void TestRejected(object sender, ACRCardRejectedCardScanEventArg e)
//            {
//                Console.WriteLine("CardReader has rejected Card");
//                Console.WriteLine("State Enum : {0}", e.EventState);
//                Console.WriteLine("State as Hex : {0:x}", (int)e.EventState);
//                Console.WriteLine("ATR : {0}", e.ATRString);
//            }

//            public void TestCardDetected(object sender, ACRCardDetectedEventArg e)
//            {
//                Console.WriteLine("CardReader has detected Card");
//                Console.WriteLine("State Enum : {0}", e.EventState);
//                Console.WriteLine("State as Hex : {0:x}", (int)e.EventState);
//                Console.WriteLine("ATR : {0}", e.ATRString);
//            }

//            public void TestCardRemoved(object sender, ACRCardRemovedEventArg e)
//            {
//                Console.WriteLine("CardReader has removed Card");
//                Console.WriteLine("State Enum : {0}", e.EventState);
//                Console.WriteLine("State as Hex : {0:x}", (int)e.EventState);
//                Console.WriteLine("ATR : {0}", e.ATRString);

//                //Manager.DisconnectToCard();
//            }
//        }

//    }
//}
#endregion
namespace CardReader_TestFileLogger
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            byte[] AcceptedATR = new byte[] { 0x3B, 0x8F, 0x80, 0x01, 0x80, 0x4F, 0x0C, 0xA0, 0x00, 0x00, 0x03, 0x06, 0x03, 0x00, 0x03, 0x00, 0x00, 0x00, 0x00, 0x68 };
            string readerName = ACR122UManager.GetACR122UReaders().FirstOrDefault();
            ACR122UManager Manager = new ACR122UManager(readerName);
            //
            ACR122U_MifareClassic_Status Status;
            Manager.GetStatus(out Status);
            //
            ACR122U_PICCOperatingParametersControl ControlOptions = ACR122U_PICCOperatingParametersControl.AllOn;
            Manager.SetPICCOperatingParameterState(ref ControlOptions);
            string firmwareVersion;
            TryGetFirmwareVersion(Manager.Context, out firmwareVersion);
            //
            WriteStartupStatus(readerName, firmwareVersion, ControlOptions, Status);
            //
            ACR122UManager.GlobalCardCheck = (e) =>
            {
                bool CeckSuccess = false;
                if (e.ATR.Length == AcceptedATR.Length)
                {
                    CeckSuccess = true;
                    for (int i = 0; i < e.ATR.Length; i++)
                    {
                        if (e.ATR[i] != AcceptedATR[i])
                        {
                            CeckSuccess = false;
                            break;
                        }
                    }
                }
                return CeckSuccess;
            };

            Manager.CheckCard = true;

            ManagerTest Test = new ManagerTest(Manager);

            Manager.AcceptedCardScaned += Test.TestAccept;
            Manager.CardStateChanged += Test.TestStateChange;
            Manager.RejectedCardScaned += Test.TestRejected;
            Manager.CardDetected += Test.TestCardDetected;
            Manager.CardRemoved += Test.TestCardRemoved;
            List<string> Names = WinSmartCardContext.ListReadersAsStringsStatic();
            WaitForExitOrClear(readerName, firmwareVersion, ControlOptions, Status);

        }

        private static void WaitForExitOrClear(string readerName, string firmwareVersion, ACR122U_PICCOperatingParametersControl controlOptions, ACR122U_MifareClassic_Status status)
        {
            while (true)
            {
                ConsoleKeyInfo keyInfo = Console.ReadKey(true);
                if (keyInfo.Key == ConsoleKey.C)
                {
                    Console.Clear();
                    WriteStartupStatus(readerName, firmwareVersion, controlOptions, status);
                    continue;
                }

                break;
            }
        }

        private static void WriteStartupStatus(string readerName, string firmwareVersion, ACR122U_PICCOperatingParametersControl controlOptions, ACR122U_MifareClassic_Status status)
        {
            WriteReaderSummary(readerName, firmwareVersion);
            Console.WriteLine("IC オプション:\n" + controlOptions);
            Console.WriteLine("起動時の状態:\n\tカード検出: " + status.Card + "\n\tエラー: " + status.ErrorCode);
            WriteControlGuide();
        }

        private static void WriteReaderSummary(string readerName, string firmwareVersion)
        {
            Console.WriteLine("=== ACR122U リーダー概要 ===");
            Console.WriteLine("[実機 / PC/SC から取得]");
            Console.WriteLine("\tリーダー名: " + GetDisplayValue(readerName));
            Console.WriteLine("\tファームウェアバージョン: " + GetDisplayValue(firmwareVersion) + (string.IsNullOrWhiteSpace(firmwareVersion) ? " (APDU: FF 00 48 00 00)" : ""));
            Console.WriteLine();
            Console.WriteLine("[資料ベースの既知仕様]");
            Console.WriteLine("\t対象機種: ACR122U USB NFC Reader");
            Console.WriteLine("\tインターフェース: USB 2.0 Full Speed (12 Mbps) / PC/SC / CCID / WinSCard");
            Console.WriteLine("\t対応カード/タグ: ISO 14443 Type A/B, MIFARE, FeliCa, ISO 18092 / NFC Forum Type 1-4");
            Console.WriteLine("\t周波数: 13.56 MHz");
            Console.WriteLine("\t通信速度: 106 Kbps / 212 Kbps / 最大 424 Kbps");
            Console.WriteLine("\t読取距離: 最大 50 mm (タグ種別に依存)");
            Console.WriteLine("\t注意: このサマリーでは保護領域、個人情報、残高などのカード内容は読み取りません。");
            Console.WriteLine();
        }

        private static string GetDisplayValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "取得できませんでした" : value;
        }

        private static bool TryGetFirmwareVersion(WinSmartCardContext context, out string firmwareVersion)
        {
            firmwareVersion = null;

            try
            {
                bool hasCard;
                byte[] response;
                byte[] command = new byte[] { 0xFF, 0x00, 0x48, 0x00, 0x00 };
                context.Control(command, out response, out hasCard);

                firmwareVersion = ParseFirmwareVersion(response);
                return !string.IsNullOrWhiteSpace(firmwareVersion);
            }
            catch
            {
                firmwareVersion = null;
                return false;
            }
        }

        private static string ParseFirmwareVersion(byte[] response)
        {
            if (response == null || response.Length == 0)
                return null;

            int payloadLength = response.Length;
            if (response.Length >= 2 && response[response.Length - 2] == 0x90 && response[response.Length - 1] == 0x00)
                payloadLength -= 2;

            while (payloadLength > 0 && response[payloadLength - 1] == 0x00)
                payloadLength--;

            if (payloadLength <= 0)
                return null;

            return Encoding.ASCII.GetString(response, 0, payloadLength).Trim();
        }

        private static void WriteControlGuide()
        {
            Console.WriteLine();
            Console.WriteLine("操作: C キーでコンソールをクリア / その他のキーで終了");
        }

        static class FileLogger
        {
            static readonly string Location = Environment.CurrentDirectory + "\\CardReaderOutput.txt";

            public static void WriteLine(string Write)
            {
                using (StreamWriter SW = new StreamWriter(File.Open(Location, FileMode.Append)))
                {
                    SW.WriteLine(Write);
                }
            }

            public static void WriteLine(string Write, params object[] obj)
            {
                using (StreamWriter SW = new StreamWriter(File.Open(Location, FileMode.Append)))
                {
                    SW.WriteLine(string.Format(Write, obj));
                }
            }

        }

        public class ManagerTest
        {

            ACR122UManager Manager;

            public ManagerTest(ACR122UManager M)
            {
                Manager = M;
            }

            public void TestStateChange(object sender, ACRCardStateChangeEventArg e)
            {
                Console.WriteLine("カードリーダーの状態が変化しました");
                Console.WriteLine("状態値: {0}", e.EventState);
                Console.WriteLine("状態値(16進): {0:x}", (int)e.EventState);
                Console.WriteLine("ATR: {0}", e.ATRString);
                WriteControlGuide();
            }

            public void TestAccept(object sender, ACRCardAcceptedCardScanEventArg e)
            {
                Console.WriteLine("カードが受理されました");
                Console.WriteLine("状態値: {0}", e.EventState);
                Console.WriteLine("状態値(16進): {0:x}", (int)e.EventState);
                Console.WriteLine("ATR: {0}", e.ATRString);
            }

            public void TestRejected(object sender, ACRCardRejectedCardScanEventArg e)
            {
                Console.WriteLine("カードが拒否されました");
                Console.WriteLine("状態値: {0}", e.EventState);
                Console.WriteLine("状態値(16進): {0:x}", (int)e.EventState);
                Console.WriteLine("ATR: {0}", e.ATRString);
            }

            public void TestCardDetected(object sender, ACRCardDetectedEventArg e)
            {
                Console.Clear();
                Console.WriteLine("カードを検出しました");
                Console.WriteLine("状態値: {0}", e.EventState);
                Console.WriteLine("状態値(16進): {0:x}", (int)e.EventState);
                Console.WriteLine("ATR: {0}", e.ATRString);
                CardSummaryWriter.Write(Manager, e.ATR);
            }

            public void TestCardRemoved(object sender, ACRCardRemovedEventArg e)
            {
                Console.WriteLine("カードが取り外されました");
                Console.WriteLine("状態値: {0}", e.EventState);
                Console.WriteLine("状態値(16進): {0:x}", (int)e.EventState);
                Console.WriteLine("ATR: {0}", e.ATRString);

                Manager.DisconnectToCard();
            }
        }

    }

    internal static class CardSummaryWriter
    {
        private static readonly byte[] GetUidCommand = new byte[] { 0xFF, 0xCA, 0x00, 0x00, 0x00 };
        private static readonly byte[] GetAtsCommand = new byte[] { 0xFF, 0xCA, 0x01, 0x00, 0x00 };

        public static void Write(ACR122UManager manager, byte[] atr)
        {
            atr = atr ?? new byte[0];
            AtrSummary atrSummary = AtrSummary.FromAtr(atr);
            CardConnectionSummary connectionSummary = ReadConnectionSummary(manager);

            Console.WriteLine("カードサマリー:");
            WriteLine("ATR", FormatBytes(atr));
            WriteLine("UID", connectionSummary.UidMessage);
            WriteLine("ATS", connectionSummary.AtsMessage);
            WriteLine("推定カード", atrSummary.CardName);
            WriteLine("推定規格", atrSummary.Standard);
            WriteLine("Historical bytes", atrSummary.HistoricalBytes);
            WriteLine("PC/SC プロトコル", connectionSummary.ProtocolMessage);
            WriteLine("ACR122U 状態", ReadReaderStatus(manager));
            WriteLine("注意", "UID/ATS/ATR などの公開情報のみを表示します。保護領域、残高、個人情報は読み取りません。");
            Console.WriteLine();
        }

        private static CardConnectionSummary ReadConnectionSummary(ACR122UManager manager)
        {
            WinSmartCard card = null;
            bool shouldDispose = false;
            CardConnectionSummary summary = new CardConnectionSummary();

            try
            {
                card = manager.Card ?? manager.Context.Card;
                if (card == null)
                {
                    card = manager.Context.CardConnect(SmartCardShareTypes.SCARD_SHARE_SHARED);
                    shouldDispose = true;
                }

                summary.ProtocolMessage = FormatProtocol(card.Protocol);
                summary.UidMessage = ReadPublicData(card, GetUidCommand, "UID").Message;
                summary.AtsMessage = ReadPublicData(card, GetAtsCommand, "ATS").Message;
            }
            catch (Exception ex)
            {
                string reason = CleanExceptionMessage(ex);
                summary.ProtocolMessage = "取得失敗 (" + reason + ")";
                summary.UidMessage = "取得失敗 (" + reason + ")";
                summary.AtsMessage = "取得失敗 (" + reason + ")";
            }
            finally
            {
                if (shouldDispose && card != null)
                    card.Dispose();
            }

            return summary;
        }

        private static ApduReadResult ReadPublicData(WinSmartCard card, byte[] command, string label)
        {
            try
            {
                byte[] response;
                card.TransmitData(command, out response);

                if (response == null || response.Length < 2)
                    return ApduReadResult.Failed("取得失敗 (応答が短すぎます)");

                int sw1Index = response.Length - 2;
                string statusWord = response[sw1Index].ToString("X2") + response[sw1Index + 1].ToString("X2");
                byte[] data = response.Take(sw1Index).ToArray();

                if (statusWord != "9000")
                    return ApduReadResult.Failed("取得失敗 (SW=" + statusWord + ")");

                if (data.Length == 0)
                    return ApduReadResult.Failed("取得失敗 (データなし)");

                return ApduReadResult.Success(FormatBytes(data));
            }
            catch (Exception ex)
            {
                return ApduReadResult.Failed("取得失敗 (" + label + ": " + CleanExceptionMessage(ex) + ")");
            }
        }

        private static string ReadReaderStatus(ACR122UManager manager)
        {
            try
            {
                bool cardPresent;
                ACR122U_StatusErrorCodes errorCode;
                bool fieldPresent;
                byte numberOfTargets;
                byte logicalNumber;
                ACR122U_StatusBitRateInReception bitRateInReception;
                ACR122U_StatusBitsRateInTransmiton bitRateInTransmition;
                ACR122U_StatusModulationType modulationType;

                manager.GetStatus(
                    out cardPresent,
                    out errorCode,
                    out fieldPresent,
                    out numberOfTargets,
                    out logicalNumber,
                    out bitRateInReception,
                    out bitRateInTransmition,
                    out modulationType);

                return "カード=" + FormatBool(cardPresent)
                    + ", RF フィールド=" + FormatBool(fieldPresent)
                    + ", ターゲット数=" + numberOfTargets
                    + ", 論理番号=" + logicalNumber
                    + ", 受信=" + FormatBitRate(bitRateInReception)
                    + ", 送信=" + FormatBitRate(bitRateInTransmition)
                    + ", 変調=" + FormatModulationType(modulationType)
                    + ", エラー=" + errorCode;
            }
            catch (Exception ex)
            {
                return "取得失敗 (" + CleanExceptionMessage(ex) + ")";
            }
        }

        private static void WriteLine(string label, string value)
        {
            Console.WriteLine("\t" + label + ": " + value);
        }

        private static string FormatProtocol(SmartCardProtocols protocol)
        {
            switch (protocol)
            {
                case SmartCardProtocols.SCARD_PROTOCOL_T0:
                    return "T=0";
                case SmartCardProtocols.SCARD_PROTOCOL_T1:
                    return "T=1";
                case SmartCardProtocols.SCARD_PROTOCOL_RAW:
                    return "RAW";
                case SmartCardProtocols.SCARD_PROTOCOL_UNDEFINED:
                    return "未確定";
                default:
                    return protocol.ToString();
            }
        }

        private static string FormatBitRate(ACR122U_StatusBitRateInReception bitRate)
        {
            switch (bitRate)
            {
                case ACR122U_StatusBitRateInReception.Is106kbps:
                    return "106 kbps";
                case ACR122U_StatusBitRateInReception.Is212kbps:
                    return "212 kbps";
                case ACR122U_StatusBitRateInReception.Is424kbps:
                    return "424 kbps";
                case ACR122U_StatusBitRateInReception.NoReception:
                    return "未受信";
                default:
                    return bitRate.ToString();
            }
        }

        private static string FormatBitRate(ACR122U_StatusBitsRateInTransmiton bitRate)
        {
            switch (bitRate)
            {
                case ACR122U_StatusBitsRateInTransmiton.Is106kbps:
                    return "106 kbps";
                case ACR122U_StatusBitsRateInTransmiton.Is212kbps:
                    return "212 kbps";
                case ACR122U_StatusBitsRateInTransmiton.Is424kbps:
                    return "424 kbps";
                case ACR122U_StatusBitsRateInTransmiton.NoTransmiton:
                    return "未送信";
                default:
                    return bitRate.ToString();
            }
        }

        private static string FormatModulationType(ACR122U_StatusModulationType modulationType)
        {
            switch (modulationType)
            {
                case ACR122U_StatusModulationType.ISO1443orMifare:
                    return "ISO 14443 / MIFARE";
                case ACR122U_StatusModulationType.ActiveMode:
                    return "Active mode";
                case ACR122U_StatusModulationType.InnovisionJewelTag:
                    return "Topaz / Jewel";
                case ACR122U_StatusModulationType.NoCardDetected:
                    return "カード未検出";
                default:
                    return modulationType.ToString();
            }
        }

        private static string FormatBool(bool value)
        {
            return value ? "あり" : "なし";
        }

        private static string FormatBytes(IEnumerable<byte> bytes)
        {
            if (bytes == null)
                return "なし";

            byte[] byteArray = bytes.ToArray();
            if (byteArray.Length == 0)
                return "なし";

            return BitConverter.ToString(byteArray);
        }

        private static string CleanExceptionMessage(Exception ex)
        {
            if (ex == null || string.IsNullOrWhiteSpace(ex.Message))
                return "詳細不明";

            string firstLine = ex.Message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(firstLine))
                return "詳細不明";

            return firstLine.Trim();
        }

        private sealed class CardConnectionSummary
        {
            public string UidMessage { get; set; } = "未取得";
            public string AtsMessage { get; set; } = "未取得";
            public string ProtocolMessage { get; set; } = "未取得";
        }

        private sealed class ApduReadResult
        {
            private ApduReadResult(string message)
            {
                Message = message;
            }

            public string Message { get; private set; }

            public static ApduReadResult Success(string data)
            {
                return new ApduReadResult(data);
            }

            public static ApduReadResult Failed(string reason)
            {
                return new ApduReadResult(reason);
            }
        }

        private sealed class AtrSummary
        {
            public string CardName { get; private set; }
            public string Standard { get; private set; }
            public string HistoricalBytes { get; private set; }

            private AtrSummary(string cardName, string standard, string historicalBytes)
            {
                CardName = cardName;
                Standard = standard;
                HistoricalBytes = historicalBytes;
            }

            public static AtrSummary FromAtr(byte[] atr)
            {
                if (atr == null || atr.Length == 0)
                    return Unknown("なし");

                byte[] historicalBytes = ExtractHistoricalBytes(atr);
                string historicalBytesText = FormatBytes(historicalBytes);

                int pcscRidIndex = IndexOf(atr, new byte[] { 0xA0, 0x00, 0x00, 0x03, 0x06 });
                if (pcscRidIndex >= 0 && pcscRidIndex + 7 < atr.Length)
                {
                    byte standardCode = atr[pcscRidIndex + 5];
                    byte cardNameHi = atr[pcscRidIndex + 6];
                    byte cardNameLo = atr[pcscRidIndex + 7];
                    return new AtrSummary(
                        GetCardName(cardNameHi, cardNameLo),
                        GetStandardName(standardCode, cardNameHi, cardNameLo),
                        historicalBytesText);
                }

                if (historicalBytes != null && historicalBytes.Length > 0)
                    return new AtrSummary("不明", "ISO 14443-4 Type A/B など (Historical bytes から詳細推定不可)", historicalBytesText);

                return Unknown(historicalBytesText);
            }

            private static AtrSummary Unknown(string historicalBytesText)
            {
                return new AtrSummary("不明", "不明", historicalBytesText);
            }

            private static byte[] ExtractHistoricalBytes(byte[] atr)
            {
                if (atr == null || atr.Length < 2)
                    return new byte[0];

                int historicalByteLength = atr[1] & 0x0F;
                int historicalByteStart = 4;
                if (historicalByteLength <= 0 || atr.Length <= historicalByteStart)
                    return new byte[0];

                int availableLength = Math.Min(historicalByteLength, atr.Length - historicalByteStart);
                byte[] historicalBytes = new byte[availableLength];
                Array.Copy(atr, historicalByteStart, historicalBytes, 0, availableLength);
                return historicalBytes;
            }

            private static int IndexOf(byte[] source, byte[] pattern)
            {
                if (source == null || pattern == null || pattern.Length == 0 || source.Length < pattern.Length)
                    return -1;

                for (int i = 0; i <= source.Length - pattern.Length; i++)
                {
                    bool matched = true;
                    for (int j = 0; j < pattern.Length; j++)
                    {
                        if (source[i + j] != pattern[j])
                        {
                            matched = false;
                            break;
                        }
                    }

                    if (matched)
                        return i;
                }

                return -1;
            }

            private static string GetStandardName(byte standardCode, byte cardNameHi, byte cardNameLo)
            {
                if (cardNameHi == 0xF0 && (cardNameLo == 0x11 || cardNameLo == 0x12))
                    return "FeliCa / ISO 18092";

                if (cardNameHi == 0xF0 && cardNameLo == 0x04)
                    return "NFC Forum Type 1 / Topaz / Jewel";

                switch (standardCode)
                {
                    case 0x03:
                        return "ISO 14443 Type A Part 3";
                    case 0x11:
                        return "ISO 14443 Type A Part 4";
                    case 0x12:
                        return "ISO 14443 Type B Part 4";
                    default:
                        return "不明 (Standard=0x" + standardCode.ToString("X2") + ")";
                }
            }

            private static string GetCardName(byte cardNameHi, byte cardNameLo)
            {
                if (cardNameHi == 0x00 && cardNameLo == 0x01)
                    return "MIFARE Classic 1K";
                if (cardNameHi == 0x00 && cardNameLo == 0x02)
                    return "MIFARE Classic 4K";
                if (cardNameHi == 0x00 && cardNameLo == 0x03)
                    return "MIFARE Ultralight";
                if (cardNameHi == 0x00 && cardNameLo == 0x26)
                    return "MIFARE Mini";
                if (cardNameHi == 0xF0 && cardNameLo == 0x04)
                    return "Topaz / Jewel";
                if (cardNameHi == 0xF0 && cardNameLo == 0x11)
                    return "FeliCa 212K";
                if (cardNameHi == 0xF0 && cardNameLo == 0x12)
                    return "FeliCa 424K";
                if (cardNameHi == 0xFF)
                    return "未定義 (SAK=0x" + cardNameLo.ToString("X2") + ")";

                return "不明 (Card Name=0x" + cardNameHi.ToString("X2") + cardNameLo.ToString("X2") + ")";
            }
        }
    }
}
