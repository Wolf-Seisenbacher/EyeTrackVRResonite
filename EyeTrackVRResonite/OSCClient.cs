using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using EyeTrackVR;
using OscCore;
using OscCore.LowLevel;

namespace EyeTrackVRResonite
{
    // Credit to yewnyx on the VRC OSC Discord for this

    public class ETVROSC
    {
        private static bool _oscSocketState;
        public static readonly Dictionary<string, float> EyeDataWithAddress = new();

        private static UdpClient? _receiver;
        private static Task? _task;

        private const int DefaultPort = 9000;

        public static bool _isSingleEye = false;

        public ETVROSC(int? port = null)
        {
            if (_receiver != null)
            {
                return;
            }

            IPAddress.TryParse("127.0.0.1", out var candidate);

            _receiver = port.HasValue
                ? new UdpClient(new IPEndPoint(candidate, port.Value))
                : new UdpClient(new IPEndPoint(candidate, DefaultPort));

            foreach (var shape in ETVRExpressions.EyeDataWithAddress)
                EyeDataWithAddress.Add(shape, 0f);

            _oscSocketState = true;
            _task = Task.Run(ListenLoop);
        }

        private static async void ListenLoop()
        {
            EyeTrackVR.Msg($"EyeTrackVR loop now listening on port {((IPEndPoint?)_receiver?.Client.LocalEndPoint)?.Port}");
            while (_oscSocketState)
            {
                var result = await _receiver.ReceiveAsync();
                var bytes = new System.ArraySegment<byte>(result.Buffer, 0, result.Buffer.Length);
                if (IsBundle(bytes))
                {
                    var bundle = new OscBundleRaw(bytes);
                    foreach (var message in bundle)
                        ProcessOscMessage(message);
                }
                else
                {
                    var message = new OscMessageRaw(bytes);
                    ProcessOscMessage(message);
                }
            }
        }

        private static void ProcessOscMessage(OscMessageRaw message)
        {
            if (!EyeDataWithAddress.ContainsKey(message.Address))
            {
                EyeTrackVR.Warn($"Unknown OSC Address: {message.Address}");
                return;
            }

            var arg = message[0];

            switch (arg.Type)
            {
                case (OscToken.Float):
                    EyeDataWithAddress[message.Address] = message.ReadFloat(ref arg);

                    if (message.Address == "/avatar/parameters/v2/EyeX" || message.Address == "/avatar/parameters/v2/EyeY")
                        _isSingleEye = true;
                    else if (message.Address == "/avatar/parameters/v2/EyeLeftX" || message.Address == "/avatar/parameters/v2/EyeRightX")
                        _isSingleEye = false;

                    break;
                case (OscToken.Int):
                    EyeDataWithAddress[message.Address] = message.ReadInt(ref arg);
                    break;
                default:
                    EyeTrackVR.Warn($"Unknown OSC type: {arg.Type}");
                    break;
            }
        }

        private static readonly byte[] BundlePrefix = Encoding.ASCII.GetBytes("#bundle");
        private static bool IsBundle(System.ArraySegment<byte> bytes)
        {
            var prefix = BundlePrefix;
            if (bytes.Count < prefix.Length)
                return false;

            var i = 0;
            foreach (var b in bytes)
            {
                if (i < prefix.Length && b != prefix[i++])
                    return false;
                if (i == prefix.Length)
                    break;
            }
            return true;
        }


        public static void Teardown()
        {
            EyeTrackVR.Msg("EyeTrackVR teardown called");
            _oscSocketState = false;
            _receiver.Close();
            _task.Wait();
            EyeTrackVR.Msg("EyeTrackVR teardown completed");
        }
    }
}
