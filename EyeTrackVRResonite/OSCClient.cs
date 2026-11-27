using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using EyeTrackVR;
using Rug.Osc;

namespace EyeTrackVRResonite
{
    // Credit to yewnyx on the VRC OSC Discord for this

    public class ETVROSC
    {
        public static readonly Dictionary<string, float> EyeDataWithAddress = new();

        private static OscReceiver? _receiver;
        private static Task? _task;

        private const int DefaultPort = 9000;

        public static bool _isSingleEye = false;

        public ETVROSC(int? port = null)
        {
            if (_receiver != null)
            {
                return;
            }

            var activePort = port ?? DefaultPort;

            _receiver = new OscReceiver(IPAddress.Loopback, activePort);
            _receiver.Connect();

            foreach (var shape in ETVRExpressions.EyeDataWithAddress)
                EyeDataWithAddress.Add(shape, 0f);
            
            _task = Task.Run(ListenLoop);
        }

        private static void ListenLoop()
        {
            EyeTrackVR.Msg($"EyeTrackVR loop now listening on port {_receiver?.Port}");
            while (_receiver.State == OscSocketState.Connected)
            {
                var packet = _receiver.Receive();
                if (packet != null)
                    ProcessOscMessage(packet);
            }
        }

        private static void ProcessOscMessage(OscPacket packet)
        {
            if (packet is OscBundle bundle)
            {
                foreach (var subpacket in bundle)
                    ProcessOscMessage(subpacket);
            }

            if (packet is not OscMessage message)
                return;

            if (!EyeDataWithAddress.ContainsKey(message.Address))
            {
                EyeTrackVR.Warn($"Unknown OSC Address: {message.Address}");
                return;
            }

            var arg = message[0];

            switch (arg)
            {
                case float floatValue:
                    EyeDataWithAddress[message.Address] = floatValue;
                    _isSingleEye = message.Address switch
                    {
                        "/avatar/parameters/v2/EyeX" or "/avatar/parameters/v2/EyeY" => true,
                        "/avatar/parameters/v2/EyeLeftX" or "/avatar/parameters/v2/EyeRightX" => false,
                        _ => _isSingleEye
                    };

                    break;
                
                case int intValue:
                    EyeDataWithAddress[message.Address] = intValue;
                    break;
                default:
                    EyeTrackVR.Warn($"Unknown OSC type: {arg.GetType()}");
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
            _receiver.Close();
            _task.Wait();
            EyeTrackVR.Msg("EyeTrackVR teardown completed");
        }
    }
}
