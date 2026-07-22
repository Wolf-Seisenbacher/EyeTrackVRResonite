using System;
using Elements.Core;
using FrooxEngine;
using ResoniteModLoader;

namespace EyeTrackVRResonite
{
    public class EyeTrackVR : ResoniteMod
    {
        internal const string VERSION_CONSTANT = "3.0.0";
        public override string Name => "EyeTrackVRResonite";
        public override string Author => "qualia + Wolf-Seisenbacher + PLYSHKA + dfgHiatus + Noble";
        public override string Version => VERSION_CONSTANT;
        public override string Link => "https://github.com/WolfSeisenbacher/EyeTrackVRResonite";

        public override void OnEngineInit()
        {
            _config = GetConfiguration();
            Engine.Current.OnShutdown += () => ETVROSC.Teardown();

            Engine.Current.RunPostInit(() =>
            {
                try
                {
                    _etvr = new ETVROSC(_config.GetValue(OscPort));
                    var gen = new EyeTrackVRInterface();
                    Engine.Current.InputInterface.RegisterInputDriver(gen);
                }
                catch (Exception e)
                {
                    Warn("Module failed to initialize.");
                    Warn(e.ToString());
                }
            });

        }
        internal enum TrackingType
        {
            VRCFTv1,
            VRCFTv2
        }

        private static ETVROSC _etvr;
        private static ModConfiguration _config;

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> ModEnabled = new("enabled", "Mod Enabled", () => true);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<float> Alpha = new("alpha", "Eye Swing Multiplier X", () => 1.0f);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<float> Beta = new("beta", "Eye Swing Multiplier Y", () => 1.0f);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<float> DilationScale = new("dilation_scale", "Dilation Scale Divider", () => 100.0f);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<int> OscPort = new("osc_port", "EyeTrackVR OSC port", () => 9000);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<TrackingType> OscTrackingType = new("tracking_type", "EyeTrackVR Tracking Type", () => TrackingType.VRCFTv1);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> UseFakeWiden = new("fake_widen", "Use Fake Widen (for v1 only)", () => true);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> RemapOpennessToWiden = new("remap_openness", "Remap openness to also control eye widen (like in VRCFT's spec)", () => false);

        private class EyeTrackVRInterface : IInputDriver
        {
            private Eyes _eyes;
            private const float DefaultPupilSize = 0.0035f;
            public int UpdateOrder => 100;

            public void CollectDeviceInfos(DataTreeList list)
            {
                var eyeDataTreeDictionary = new DataTreeDictionary();
                eyeDataTreeDictionary.Add("Name", "EyeTrackVR Eye Tracking");
                eyeDataTreeDictionary.Add("Type", "Eye Tracking");
                eyeDataTreeDictionary.Add("Model", "ETVR Module");
                list.Add(eyeDataTreeDictionary);
            }

            public void RegisterInputs(InputInterface inputInterface)
            {
                _eyes = new Eyes(inputInterface, "EyeTrackVR Eye Tracking", true);
            }

            private float OpennessRemap(float openness, bool allowWidenRemap, bool isWiden)
            {
                return allowWidenRemap ? (isWiden ? MathX.Remap(openness, 0.75f, 1f, 0f, 1f) : MathX.Remap(openness, 0f, 0.75f, 0f, 1f)) : (isWiden ? 0f : openness);
            }

            public void UpdateInputs(float deltaTime)
            {
                if (!_config.GetValue(ModEnabled))
                {
                    _eyes.IsEyeTrackingActive = false;
                    return;
                }

                _eyes.IsEyeTrackingActive = true;

                bool remapOpenness = _config.GetValue(RemapOpennessToWiden);

                if (_config.GetValue(OscTrackingType) == TrackingType.VRCFTv2)
                {   
                    var pupilDiameter = MathX.Clamp01((ETVROSC.EyeDataWithAddress["/avatar/parameters/v2/PupilDilation"] / _config.GetValue(DilationScale)));

                    if (ETVROSC._isSingleEye) // need to compute left and right from combined eye
                    {
                        var eyeDirection = Project2DTo3D(ETVROSC.EyeDataWithAddress["/avatar/parameters/v2/EyeX"], ETVROSC.EyeDataWithAddress["/avatar/parameters/v2/EyeY"]);

                        UpdateEye(
                            eyeDirection,
                            float3.Zero,
                            true,
                            pupilDiameter,
                            OpennessRemap(ETVROSC.EyeDataWithAddress["/avatar/parameters/v2/EyeLid"], remapOpenness, false), // 0 to 0.75 is actual openness
                            OpennessRemap(ETVROSC.EyeDataWithAddress["/avatar/parameters/v2/EyeLid"], remapOpenness, true), // 0.75 to 1 is widen. thanks vrcft!
                            ETVROSC.EyeDataWithAddress["/avatar/parameters/v2/EyeSquint"],
                            ETVROSC.EyeDataWithAddress["/avatar/parameters/v2/BrowExpression"],
                            deltaTime,
                            _eyes.CombinedEye
                        );

                        UpdateEye(
                            eyeDirection,
                            float3.Zero,
                            true,
                            pupilDiameter,
                            OpennessRemap(ETVROSC.EyeDataWithAddress["/avatar/parameters/v2/EyeLid"], remapOpenness, false), // 0 to 0.75 is actual openness
                            OpennessRemap(ETVROSC.EyeDataWithAddress["/avatar/parameters/v2/EyeLid"], remapOpenness, true), // 0.75 to 1 is widen. thanks vrcft!
                            ETVROSC.EyeDataWithAddress["/avatar/parameters/v2/EyeSquint"],
                            ETVROSC.EyeDataWithAddress["/avatar/parameters/v2/BrowExpression"],
                            deltaTime,
                            _eyes.LeftEye
                        );

                        UpdateEye(
                            eyeDirection,
                            float3.Zero,
                            true,
                            pupilDiameter,
                            OpennessRemap(ETVROSC.EyeDataWithAddress["/avatar/parameters/v2/EyeLid"], remapOpenness, false), // 0 to 0.75 is actual openness
                            OpennessRemap(ETVROSC.EyeDataWithAddress["/avatar/parameters/v2/EyeLid"], remapOpenness, true), // 0.75 to 1 is widen. thanks vrcft!
                            ETVROSC.EyeDataWithAddress["/avatar/parameters/v2/EyeSquint"],
                            ETVROSC.EyeDataWithAddress["/avatar/parameters/v2/BrowExpression"],
                            deltaTime,
                            _eyes.RightEye
                        );
                    } 
                    else // need to compute combined parameters from both eyes
                    {
                        var leftEyeDirection = Project2DTo3D(ETVROSC.EyeDataWithAddress["/avatar/parameters/v2/EyeLeftX"], ETVROSC.EyeDataWithAddress["/avatar/parameters/v2/EyeLeftY"]);

                        UpdateEye(
                            leftEyeDirection,
                            float3.Zero,
                            true,
                            pupilDiameter,
                            OpennessRemap(ETVROSC.EyeDataWithAddress["/avatar/parameters/v2/EyeLidLeft"], remapOpenness, false), // 0 to 0.75 is actual openness
                            OpennessRemap(ETVROSC.EyeDataWithAddress["/avatar/parameters/v2/EyeLidLeft"], remapOpenness, true), // 0.75 to 1 is widen. thanks vrcft!
                            ETVROSC.EyeDataWithAddress["/avatar/parameters/v2/EyeSquintLeft"],
                            ETVROSC.EyeDataWithAddress["/avatar/parameters/v2/BrowExpressionLeft"],
                            deltaTime,
                            _eyes.LeftEye
                        );

                        var rightEyeDirection = Project2DTo3D(ETVROSC.EyeDataWithAddress["/avatar/parameters/v2/EyeRightX"], ETVROSC.EyeDataWithAddress["/avatar/parameters/v2/EyeRightY"]);

                        UpdateEye(
                            rightEyeDirection,
                            float3.Zero,
                            true,
                            pupilDiameter,
                            OpennessRemap(ETVROSC.EyeDataWithAddress["/avatar/parameters/v2/EyeLidRight"], remapOpenness, false), // 0 to 0.75 is actual openness
                            OpennessRemap(ETVROSC.EyeDataWithAddress["/avatar/parameters/v2/EyeLidRight"], remapOpenness, true), // 0.75 to 1 is widen. thanks vrcft!
                            ETVROSC.EyeDataWithAddress["/avatar/parameters/v2/EyeSquintRight"],
                            ETVROSC.EyeDataWithAddress["/avatar/parameters/v2/BrowExpressionRight"],
                            deltaTime,
                            _eyes.RightEye
                        );

                        var combinedDirection = MathX.Average(leftEyeDirection, rightEyeDirection);
                        var combinedOpenness = OpennessRemap(MathX.Average(ETVROSC.EyeDataWithAddress["/avatar/parameters/v2/EyeLidLeft"], ETVROSC.EyeDataWithAddress["/avatar/parameters/v2/EyeLidRight"]), remapOpenness, true);

                        UpdateEye(
                            combinedDirection,
                            float3.Zero,
                            true,
                            pupilDiameter,
                            combinedOpenness,
                            0f, // These get calculated immediately after
                            0f,
                            0f,
                            deltaTime,
                            _eyes.CombinedEye
                        );

                        _eyes.ComputeCombinedEyeParameters();
                    }
                }
                else
                {
                    var fakeWiden = _config.GetValue(UseFakeWiden) ? MathX.Remap(MathX.Clamp01(ETVROSC.EyeDataWithAddress["/avatar/parameters/EyesY"]), 0f,
                        1f, 0f, 0.33f) : 0;

                    var pupilDiameter = MathX.Clamp01((ETVROSC.EyeDataWithAddress["/avatar/parameters/EyesDilation"] / _config.GetValue(DilationScale)));

                    var leftEyeDirection = Project2DTo3D(ETVROSC.EyeDataWithAddress["/avatar/parameters/LeftEyeX"],
                        ETVROSC.EyeDataWithAddress["/avatar/parameters/EyesY"]);
                    UpdateEye(leftEyeDirection, float3.Zero, true, pupilDiameter,
                        ETVROSC.EyeDataWithAddress["/avatar/parameters/LeftEyeLidExpandedSqueeze"],
                        fakeWiden, 0f, 0f, deltaTime, _eyes.LeftEye);

                    var rightEyeDirection = Project2DTo3D(ETVROSC.EyeDataWithAddress["/avatar/parameters/RightEyeX"],
                        ETVROSC.EyeDataWithAddress["/avatar/parameters/EyesY"]);
                    UpdateEye(rightEyeDirection, float3.Zero, true, pupilDiameter,
                        ETVROSC.EyeDataWithAddress["/avatar/parameters/RightEyeLidExpandedSqueeze"],
                        fakeWiden, 0f, 0f, deltaTime, _eyes.RightEye);

                    var combinedDirection = MathX.Average(leftEyeDirection, rightEyeDirection);
                    var combinedOpenness =
                        MathX.Average(ETVROSC.EyeDataWithAddress["/avatar/parameters/LeftEyeLidExpandedSqueeze"],
                            ETVROSC.EyeDataWithAddress["/avatar/parameters/RightEyeLidExpandedSqueeze"]);
                    UpdateEye(combinedDirection, float3.Zero, true, pupilDiameter, combinedOpenness,
                        fakeWiden, 0f, 0f, deltaTime, _eyes.CombinedEye);

                    _eyes.ComputeCombinedEyeParameters();
                }
                _eyes.ConvergenceDistance = 0f;
                _eyes.Timestamp += deltaTime;
                _eyes.FinishUpdate();
            }

            private static void UpdateEye(float3 gazeDirection, float3 gazeOrigin, bool status, float pupilSize,
                float openness, float widen, float squeeze, float brow, float deltaTime, Eye eye)
            {
                eye.IsDeviceActive = Engine.Current.InputInterface.VR_Active;
                eye.IsTracking = status;

                if (eye.IsTracking)
                {
                    eye.UpdateWithDirection(gazeDirection);
                    eye.RawPosition = gazeOrigin;
                    eye.PupilDiameter = pupilSize != 0f ? pupilSize : DefaultPupilSize;
                }

                eye.Openness = openness;
                eye.Widen = widen;
                eye.Squeeze = squeeze;
                eye.OuterBrowVertical = brow;
                eye.InnerBrowVertical = brow;
            }

            private static float3 Project2DTo3D(float x, float y)
            {
                return new float3(MathX.Tan(_config.GetValue(Alpha) * x),
                    MathX.Tan(_config.GetValue(Beta) * y),
                    1f).Normalized;
            }
        }
    }
}
