using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;

using Windows.ApplicationModel.Background;
using Windows.Networking.Sockets;

namespace LastSliceOven
{
    public sealed class StartupTask : IBackgroundTask
    {

        // this is just to conveniently store a sequence of inputs
        // chars aren't related to the pin mapping (abstracted by OvenManager)

        private static readonly Dictionary<char, OvenManager.eBtn> s_rLetterToBtnMapping =
            new Dictionary<char, OvenManager.eBtn>() {
                {'A', OvenManager.eBtn.A },
                {'B', OvenManager.eBtn.B } ,
                {'D', OvenManager.eBtn.DN }, // DOWN
                {'L', OvenManager.eBtn.LT }, // LEFT
                {'R', OvenManager.eBtn.RT }, // RIGHT
                {'U', OvenManager.eBtn.UP }  // UP
            };

        // the unlock sequence
        const string KONAMI_CODE = "UUDDLRLRBA";

        // start -> jalapeno
        const string INGR_1 = "RRRD";

        // jalapeno -> pepperoni
        const string INGR_2 = "DLLDDDDDDR";

        // pepperoni -> mushrooms
        const string INGR_3 = "LUUUUUURRRRRRRRRR";

        // mushrooms -> sausage
        const string INGR_4 = "DDDRDDDL";

        // delay settings (msec)
        private const int iDELAY_AFTER_BTN_PRESS = 200;
        private const int iDELAY_AFTER_BTN_RELEASE = 300;
        private const int iDELAY_AFTER_INPUT_SEQ = 2000;

        // handles the low level details
        private OvenManager m_rOvenManager;

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            m_rOvenManager = new OvenManager();
            BackgroundTaskDeferral deferral = taskInstance.GetDeferral();

            // used while trying to solve the challenge (remote input from pc)
            //await allowRemoteControl();

            // performs all the required steps to solve the challenge
            await solveChallenge();

            deferral.Complete();
        }

        private async Task sendInputSequence(string sInputSequence)
        {
            for (int i = 0; i < sInputSequence.Length; ++i) {
                char cInputLetter = sInputSequence[i];
                OvenManager.eBtn input = s_rLetterToBtnMapping[cInputLetter];
                m_rOvenManager.setBtn(input, OvenManager.eBtnState.PRESSED);
                await (Task.Delay(iDELAY_AFTER_BTN_PRESS));
                m_rOvenManager.setBtn(input, OvenManager.eBtnState.UNPRESSED);
                await (Task.Delay(iDELAY_AFTER_BTN_RELEASE));
            }
        }

        private async Task solveChallenge()
        {
            string[] inputSequences = new string[] {
                KONAMI_CODE, INGR_1, INGR_2, INGR_3, INGR_4
            };
            foreach (string sInputSeq in inputSequences) {
                await sendInputSequence(sInputSeq);
                await Task.Delay(iDELAY_AFTER_INPUT_SEQ);
            }

            string sFromUART = await m_rOvenManager.readFromUART();
            Debug.WriteLine("UART <- " + sFromUART);
        }


        // ugly but somewhat functional
        private async Task allowRemoteControl()
        {
            TaskCompletionSource<bool> canStopListening = new TaskCompletionSource<bool>();
            try {
                var streamSocketListener = new StreamSocketListener();
                streamSocketListener.ConnectionReceived += async
                    (StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args) => {
                        Debug.WriteLine("TCP: connection received");
                        string request;
                        using (var streamReader = new StreamReader(args.Socket.InputStream.AsStreamForRead())) {
                            while (true) {
                                request = await streamReader.ReadLineAsync();
                                Debug.WriteLine("TCP <- " + request);
                                if (request.StartsWith("close")) {
                                    canStopListening.SetResult(true);
                                    args.Socket.Dispose();
                                }
                                if (request.StartsWith("serial")) {
                                    string sFromUART = await m_rOvenManager.readFromUART();
                                    Debug.WriteLine("UART <- " + sFromUART);
                                } else if (request.StartsWith("konami")) {
                                    await sendInputSequence(KONAMI_CODE); ;
                                } else if (request.StartsWith("JP")) {
                                    await sendInputSequence(INGR_1);
                                } else if (request.StartsWith("PP")) {
                                    await sendInputSequence(INGR_2);
                                } else if (request.StartsWith("MR")) {
                                    await sendInputSequence(INGR_3);
                                } else if (request.StartsWith("SA")) {
                                    await sendInputSequence(INGR_4);
                                } else { // key press/release - request is one of [U|D|L|R][0|1]
                                    OvenManager.eBtn btn = s_rLetterToBtnMapping[request[0]];
                                    OvenManager.eBtnState btnState = request[1] == '0' ?
                                        OvenManager.eBtnState.PRESSED : OvenManager.eBtnState.UNPRESSED;
                                    m_rOvenManager.setBtn(btn, btnState);
                                }
                            }
                        }
                    };
                await streamSocketListener.BindServiceNameAsync("12345");
                Debug.WriteLine("TCP: waiting for connections");
                await canStopListening.Task;
                Debug.WriteLine("TCP: closing");
                streamSocketListener.Dispose();
            } catch (Exception ex) {
                SocketErrorStatus webErrorStatus = SocketError.GetStatus(ex.GetBaseException().HResult);
                Debug.WriteLine(webErrorStatus.ToString() != "Unknown" ? webErrorStatus.ToString() : ex.Message);
            }
        }
    }
}
