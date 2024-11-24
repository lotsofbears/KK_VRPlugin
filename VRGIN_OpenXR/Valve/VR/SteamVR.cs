using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;
using Valve.Newtonsoft.Json;

namespace Valve.VR
{
    public class SteamVR : IDisposable
    {
        public enum InitializedStates
        {
            None = 0,
            Initializing = 1,
            InitializeSuccess = 2,
            InitializeFailure = 3
        }

        private static bool _enabled = true;

        private static SteamVR _instance;

        public static InitializedStates initializedState = InitializedStates.None;

        public static bool[] connected = new bool[64];

        public ETextureType textureType;

        private static bool runningTemporarySession = false;

        public const string defaultUnityAppKeyTemplate = "application.generated.unity.{0}.exe";

        public const string defaultAppKeyTemplate = "application.generated.{0}";

        public static bool active => _instance != null;

        public static bool enabled
        {
            get
            {
                if (XRSettings.supportedDevices.Length == 0) enabled = false;
                return _enabled;
            }
            set
            {
                _enabled = value;
                if (_enabled)
                    Initialize(false);
                else
                    SafeDispose();
            }
        }

        public static SteamVR instance
        {
            get
            {
                if (!enabled) return null;
                if (_instance == null)
                {
                    _instance = CreateInstance();
                    if (_instance == null) _enabled = false;
                }

                return _instance;
            }
        }

        public static bool usingNativeSupport => XRDevice.GetNativePtr() != IntPtr.Zero;

        public static SteamVR_Settings settings { get; private set; }

        public CVRSystem hmd { get; private set; }

        public CVRCompositor compositor { get; private set; }

        public CVROverlay overlay { get; private set; }

        public static bool initializing { get; private set; }

        public static bool calibrating { get; private set; }

        public static bool outOfRange { get; private set; }

        public float sceneWidth { get; private set; }

        public float sceneHeight { get; private set; }

        public float aspect { get; private set; }

        public float fieldOfView { get; private set; }

        public Vector2 tanHalfFov { get; private set; }

        public VRTextureBounds_t[] textureBounds { get; private set; }

        public SteamVR_Utils.RigidTransform[] eyes { get; private set; }

        public string hmd_TrackingSystemName => GetStringProperty(ETrackedDeviceProperty.Prop_TrackingSystemName_String);

        public string hmd_ModelNumber => GetStringProperty(ETrackedDeviceProperty.Prop_ModelNumber_String);

        public string hmd_SerialNumber => GetStringProperty(ETrackedDeviceProperty.Prop_SerialNumber_String);

        public string hmd_Type => GetStringProperty(ETrackedDeviceProperty.Prop_ControllerType_String);

        public float hmd_SecondsFromVsyncToPhotons => GetFloatProperty(ETrackedDeviceProperty.Prop_SecondsFromVsyncToPhotons_Float);

        public float hmd_DisplayFrequency => GetFloatProperty(ETrackedDeviceProperty.Prop_DisplayFrequency_Float);

        public static void Initialize(bool forceUnityVRMode = false)
        {
            if (forceUnityVRMode)
            {
                SteamVR_Behaviour.instance.InitializeSteamVR(true);
                return;
            }

            if (_instance == null)
            {
                _instance = CreateInstance();
                if (_instance == null) _enabled = false;
            }

            if (_enabled) SteamVR_Behaviour.Initialize(forceUnityVRMode);
        }

        private static void ReportGeneralErrors()
        {
            UnityEngine.Debug.LogWarning("<b>[SteamVR]</b> Initialization failed. " +
                                         "Please verify that you have SteamVR installed, your hmd is functioning, and OpenVR Loader is checked in the XR Plugin Management section of Project Settings.");
        }

        private static SteamVR CreateInstance()
        {
            initializedState = InitializedStates.Initializing;
            try
            {
                var peError = EVRInitError.None;
                OpenVR.GetGenericInterface("IVRCompositor_026", ref peError);
                if (peError != 0)
                {
                    initializedState = InitializedStates.InitializeFailure;
                    ReportError(peError);
                    ReportGeneralErrors();
                    SteamVR_Events.Initialized.Send(false);
                    return null;
                }

                OpenVR.GetGenericInterface("IVROverlay_024", ref peError);
                if (peError != 0)
                {
                    initializedState = InitializedStates.InitializeFailure;
                    ReportError(peError);
                    SteamVR_Events.Initialized.Send(false);
                    return null;
                }

                OpenVR.GetGenericInterface("IVRInput_010", ref peError);
                if (peError != 0)
                {
                    initializedState = InitializedStates.InitializeFailure;
                    ReportError(peError);
                    SteamVR_Events.Initialized.Send(false);
                    return null;
                }

                settings = SteamVR_Settings.instance;
                if (SteamVR_Settings.instance.inputUpdateMode != SteamVR_UpdateModes.Nothing || SteamVR_Settings.instance.poseUpdateMode != SteamVR_UpdateModes.Nothing) SteamVR_Input.Initialize(false);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("<b>[SteamVR]</b> " + ex);
                SteamVR_Events.Initialized.Send(false);
                return null;
            }

            _enabled = true;
            initializedState = InitializedStates.InitializeSuccess;
            SteamVR_Events.Initialized.Send(true);
            return new SteamVR();
        }

        private static void ReportError(EVRInitError error)
        {
            switch (error)
            {
                case EVRInitError.VendorSpecific_UnableToConnectToOculusRuntime:
                    UnityEngine.Debug.LogWarning("<b>[SteamVR]</b> Initialization Failed!  Make sure device is on, Oculus runtime is installed, and OVRService_*.exe is running.");
                    break;
                case EVRInitError.Init_VRClientDLLNotFound:
                    UnityEngine.Debug.LogWarning("<b>[SteamVR]</b> Drivers not found!  They can be installed via Steam under Library > Tools.  Visit http://steampowered.com to install Steam.");
                    break;
                case EVRInitError.Driver_RuntimeOutOfDate:
                    UnityEngine.Debug.LogWarning("<b>[SteamVR]</b> Initialization Failed!  Make sure device's runtime is up to date.");
                    break;
                default:
                    UnityEngine.Debug.LogWarning("<b>[SteamVR]</b> " + OpenVR.GetStringForHmdError(error));
                    break;
                case EVRInitError.None:
                    break;
            }
        }

        public EDeviceActivityLevel GetHeadsetActivityLevel()
        {
            return OpenVR.System.GetTrackedDeviceActivityLevel(0u);
        }

        public string GetTrackedDeviceString(uint deviceId)
        {
            var pError = ETrackedPropertyError.TrackedProp_Success;
            var stringTrackedDeviceProperty = hmd.GetStringTrackedDeviceProperty(deviceId, ETrackedDeviceProperty.Prop_AttachedDeviceId_String, null, 0u, ref pError);
            if (stringTrackedDeviceProperty > 1)
            {
                var stringBuilder = new StringBuilder((int)stringTrackedDeviceProperty);
                hmd.GetStringTrackedDeviceProperty(deviceId, ETrackedDeviceProperty.Prop_AttachedDeviceId_String, stringBuilder, stringTrackedDeviceProperty, ref pError);
                return stringBuilder.ToString();
            }

            return null;
        }

        public string GetStringProperty(ETrackedDeviceProperty prop, uint deviceId = 0u)
        {
            var pError = ETrackedPropertyError.TrackedProp_Success;
            var stringTrackedDeviceProperty = hmd.GetStringTrackedDeviceProperty(deviceId, prop, null, 0u, ref pError);
            if (stringTrackedDeviceProperty > 1)
            {
                var stringBuilder = new StringBuilder((int)stringTrackedDeviceProperty);
                hmd.GetStringTrackedDeviceProperty(deviceId, prop, stringBuilder, stringTrackedDeviceProperty, ref pError);
                return stringBuilder.ToString();
            }

            if (pError == ETrackedPropertyError.TrackedProp_Success) return "<unknown>";
            return pError.ToString();
        }

        public float GetFloatProperty(ETrackedDeviceProperty prop, uint deviceId = 0u)
        {
            var pError = ETrackedPropertyError.TrackedProp_Success;
            return hmd.GetFloatTrackedDeviceProperty(deviceId, prop, ref pError);
        }

        public static bool InitializeTemporarySession(bool initInput = false)
        {
            if (Application.isEditor)
            {
                var peError = EVRInitError.None;
                OpenVR.GetGenericInterface("IVRCompositor_026", ref peError);
                var flag = peError != EVRInitError.None;
                if (flag)
                {
                    var peError2 = EVRInitError.None;
                    OpenVR.Init(ref peError2, EVRApplicationType.VRApplication_Overlay);
                    if (peError2 != 0)
                    {
                        UnityEngine.Debug.LogError("<b>[SteamVR]</b> Error during OpenVR Init: " + peError2);
                        return false;
                    }

                    IdentifyEditorApplication(false);
                    SteamVR_Input.IdentifyActionsFile(false);
                    runningTemporarySession = true;
                }

                if (initInput) SteamVR_Input.Initialize(true);
                return flag;
            }

            return false;
        }

        public static void ExitTemporarySession()
        {
            if (runningTemporarySession)
            {
                OpenVR.Shutdown();
                runningTemporarySession = false;
            }
        }

        public static string GenerateAppKey()
        {
            var arg = GenerateCleanProductName();
            return $"application.generated.unity.{arg}.exe";
        }

        public static string GenerateCleanProductName()
        {
            var productName = Application.productName;
            if (string.IsNullOrEmpty(productName)) return "unnamed_product";
            productName = Regex.Replace(Application.productName, "[^\\w\\._]", "");
            return productName.ToLower();
        }

        private static string GetManifestFile()
        {
            var dataPath = Application.dataPath;
            var num = dataPath.LastIndexOf('/');
            dataPath = dataPath.Remove(num, dataPath.Length - num);
            var text = Path.Combine(dataPath, "unityProject.vrmanifest");
            var fileInfo = new FileInfo(SteamVR_Input.GetActionsFilePath(true));
            if (File.Exists(text))
            {
                var steamVR_Input_ManifestFile = JsonConvert.DeserializeObject<SteamVR_Input_ManifestFile>(File.ReadAllText(text));
                if (steamVR_Input_ManifestFile != null && steamVR_Input_ManifestFile.applications != null && steamVR_Input_ManifestFile.applications.Count > 0 &&
                    steamVR_Input_ManifestFile.applications[0].app_key != SteamVR_Settings.instance.editorAppKey)
                {
                    UnityEngine.Debug.Log("<b>[SteamVR]</b> Deleting existing VRManifest because it has a different app key.");
                    var fileInfo2 = new FileInfo(text);
                    if (fileInfo2.IsReadOnly) fileInfo2.IsReadOnly = false;
                    fileInfo2.Delete();
                }

                if (steamVR_Input_ManifestFile != null && steamVR_Input_ManifestFile.applications != null && steamVR_Input_ManifestFile.applications.Count > 0 &&
                    steamVR_Input_ManifestFile.applications[0].action_manifest_path != fileInfo.FullName)
                {
                    UnityEngine.Debug.Log("<b>[SteamVR]</b> Deleting existing VRManifest because it has a different action manifest path:\nExisting:" +
                                          steamVR_Input_ManifestFile.applications[0].action_manifest_path + "\nNew: " + fileInfo.FullName);
                    var fileInfo3 = new FileInfo(text);
                    if (fileInfo3.IsReadOnly) fileInfo3.IsReadOnly = false;
                    fileInfo3.Delete();
                }
            }

            if (!File.Exists(text))
            {
                var steamVR_Input_ManifestFile2 = new SteamVR_Input_ManifestFile();
                steamVR_Input_ManifestFile2.source = "Unity";
                var item = new SteamVR_Input_ManifestFile_Application
                {
                    app_key = SteamVR_Settings.instance.editorAppKey,
                    action_manifest_path = fileInfo.FullName,
                    launch_type = "url",
                    url = "steam://launch/",
                    strings =
                    {
                        {
                            "en_us",
                            new SteamVR_Input_ManifestFile_ApplicationString
                            {
                                name = $"{Application.productName} [Testing]"
                            }
                        }
                    }
                };
                steamVR_Input_ManifestFile2.applications = new List<SteamVR_Input_ManifestFile_Application>();
                steamVR_Input_ManifestFile2.applications.Add(item);
                var contents = JsonConvert.SerializeObject(steamVR_Input_ManifestFile2, Formatting.Indented, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });
                File.WriteAllText(text, contents);
            }

            return text;
        }

        private static void IdentifyEditorApplication(bool showLogs = true)
        {
            if (string.IsNullOrEmpty(SteamVR_Settings.instance.editorAppKey))
            {
                UnityEngine.Debug.LogError("<b>[SteamVR]</b> Critical Error identifying application. EditorAppKey is null or empty. Input may not work.");
                return;
            }

            var manifestFile = GetManifestFile();
            var eVRApplicationError = OpenVR.Applications.AddApplicationManifest(manifestFile, true);
            if (eVRApplicationError != 0)
                UnityEngine.Debug.LogError("<b>[SteamVR]</b> Error adding vr manifest file: " + eVRApplicationError);
            else if (showLogs) UnityEngine.Debug.Log("<b>[SteamVR]</b> Successfully added VR manifest to SteamVR");
            var id = Process.GetCurrentProcess().Id;
            var eVRApplicationError2 = OpenVR.Applications.IdentifyApplication((uint)id, SteamVR_Settings.instance.editorAppKey);
            if (eVRApplicationError2 != 0)
                UnityEngine.Debug.LogError("<b>[SteamVR]</b> Error identifying application: " + eVRApplicationError2);
            else if (showLogs) UnityEngine.Debug.Log($"<b>[SteamVR]</b> Successfully identified process as editor project to SteamVR ({SteamVR_Settings.instance.editorAppKey})");
        }

        private void OnInitializing(bool initializing)
        {
            SteamVR.initializing = initializing;
        }

        private void OnCalibrating(bool calibrating)
        {
            SteamVR.calibrating = calibrating;
        }

        private void OnOutOfRange(bool outOfRange)
        {
            SteamVR.outOfRange = outOfRange;
        }

        private void OnDeviceConnected(int i, bool connected)
        {
            SteamVR.connected[i] = connected;
        }

        private void OnNewPoses(TrackedDevicePose_t[] poses)
        {
            eyes[0] = new SteamVR_Utils.RigidTransform(hmd.GetEyeToHeadTransform(EVREye.Eye_Left));
            eyes[1] = new SteamVR_Utils.RigidTransform(hmd.GetEyeToHeadTransform(EVREye.Eye_Right));
            for (var i = 0; i < poses.Length; i++)
            {
                var bDeviceIsConnected = poses[i].bDeviceIsConnected;
                if (bDeviceIsConnected != connected[i]) SteamVR_Events.DeviceConnected.Send(i, bDeviceIsConnected);
            }

            if ((long)poses.Length > 0L)
            {
                var eTrackingResult = poses[0].eTrackingResult;
                var flag = eTrackingResult == ETrackingResult.Uninitialized;
                if (flag != initializing) SteamVR_Events.Initializing.Send(flag);
                var flag2 = eTrackingResult == ETrackingResult.Calibrating_InProgress || eTrackingResult == ETrackingResult.Calibrating_OutOfRange;
                if (flag2 != calibrating) SteamVR_Events.Calibrating.Send(flag2);
                var flag3 = eTrackingResult == ETrackingResult.Running_OutOfRange || eTrackingResult == ETrackingResult.Calibrating_OutOfRange;
                if (flag3 != outOfRange) SteamVR_Events.OutOfRange.Send(flag3);
            }
        }

        private SteamVR()
        {
            hmd = OpenVR.System;
            UnityEngine.Debug.LogFormat("<b>[SteamVR]</b> Initialized. Connected to {0} : {1} : {2} :: {3}", hmd_TrackingSystemName, hmd_ModelNumber, hmd_SerialNumber, hmd_Type);
            compositor = OpenVR.Compositor;
            overlay = OpenVR.Overlay;
            var pnWidth = 0u;
            var pnHeight = 0u;
            hmd.GetRecommendedRenderTargetSize(ref pnWidth, ref pnHeight);
            sceneWidth = pnWidth;
            sceneHeight = pnHeight;
            var pfLeft = 0f;
            var pfRight = 0f;
            var pfTop = 0f;
            var pfBottom = 0f;
            hmd.GetProjectionRaw(EVREye.Eye_Left, ref pfLeft, ref pfRight, ref pfTop, ref pfBottom);
            var pfLeft2 = 0f;
            var pfRight2 = 0f;
            var pfTop2 = 0f;
            var pfBottom2 = 0f;
            hmd.GetProjectionRaw(EVREye.Eye_Right, ref pfLeft2, ref pfRight2, ref pfTop2, ref pfBottom2);
            tanHalfFov = new Vector2(Mathf.Max(0f - pfLeft, pfRight, 0f - pfLeft2, pfRight2), Mathf.Max(0f - pfTop, pfBottom, 0f - pfTop2, pfBottom2));
            textureBounds = new VRTextureBounds_t[2];
            textureBounds[0].uMin = 0.5f + 0.5f * pfLeft / tanHalfFov.x;
            textureBounds[0].uMax = 0.5f + 0.5f * pfRight / tanHalfFov.x;
            textureBounds[0].vMin = 0.5f - 0.5f * pfBottom / tanHalfFov.y;
            textureBounds[0].vMax = 0.5f - 0.5f * pfTop / tanHalfFov.y;
            textureBounds[1].uMin = 0.5f + 0.5f * pfLeft2 / tanHalfFov.x;
            textureBounds[1].uMax = 0.5f + 0.5f * pfRight2 / tanHalfFov.x;
            textureBounds[1].vMin = 0.5f - 0.5f * pfBottom2 / tanHalfFov.y;
            textureBounds[1].vMax = 0.5f - 0.5f * pfTop2 / tanHalfFov.y;
            sceneWidth /= Mathf.Max(textureBounds[0].uMax - textureBounds[0].uMin, textureBounds[1].uMax - textureBounds[1].uMin);
            sceneHeight /= Mathf.Max(textureBounds[0].vMax - textureBounds[0].vMin, textureBounds[1].vMax - textureBounds[1].vMin);
            aspect = tanHalfFov.x / tanHalfFov.y;
            fieldOfView = 2f * Mathf.Atan(tanHalfFov.y) * 57.29578f;
            eyes = new SteamVR_Utils.RigidTransform[2]
            {
                new SteamVR_Utils.RigidTransform(hmd.GetEyeToHeadTransform(EVREye.Eye_Left)),
                new SteamVR_Utils.RigidTransform(hmd.GetEyeToHeadTransform(EVREye.Eye_Right))
            };
            switch (SystemInfo.graphicsDeviceType)
            {
                case GraphicsDeviceType.OpenGLES2:
                case GraphicsDeviceType.OpenGLES3:
                case GraphicsDeviceType.OpenGLCore:
                    textureType = ETextureType.OpenGL;
                    break;
                case GraphicsDeviceType.Vulkan:
                    textureType = ETextureType.Vulkan;
                    break;
                default:
                    textureType = ETextureType.DirectX;
                    break;
            }

            SteamVR_Events.Initializing.Listen(OnInitializing);
            SteamVR_Events.Calibrating.Listen(OnCalibrating);
            SteamVR_Events.OutOfRange.Listen(OnOutOfRange);
            SteamVR_Events.DeviceConnected.Listen(OnDeviceConnected);
            SteamVR_Events.NewPoses.Listen(OnNewPoses);
        }

        ~SteamVR()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            SteamVR_Events.Initializing.Remove(OnInitializing);
            SteamVR_Events.Calibrating.Remove(OnCalibrating);
            SteamVR_Events.OutOfRange.Remove(OnOutOfRange);
            SteamVR_Events.DeviceConnected.Remove(OnDeviceConnected);
            SteamVR_Events.NewPoses.Remove(OnNewPoses);
            _instance = null;
        }

        public static void SafeDispose()
        {
            if (_instance != null) _instance.Dispose();
        }
    }
}
