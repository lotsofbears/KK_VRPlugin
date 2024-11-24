﻿using System;
using System.Collections;
using System.Runtime.InteropServices;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using KKAPI;
using KKAPI.MainGame;
using KK_VR.Features;
using KK_VR.Fixes;
using KK_VR.Interpreters;
using KK_VR.Settings;
using UnityEngine;
using Valve.VR;
using VRGIN.Core;
using VRGIN.Helpers;

namespace KK_VR
{
    [BepInPlugin(GUID, Name, Version)]
    [BepInProcess(KoikatuAPI.GameProcessName)]
    [BepInDependency(KoikatuAPI.GUID, KoikatuAPI.VersionConst)]
    [BepInIncompatibility("bero.crossfadervr")]
    public class VRPlugin : BaseUnityPlugin
    {
        public const string GUID = "kk.vr.game";
        public const string Name = "MainGameVR";
        public const string Version = Constants.Version;

        internal static new ManualLogSource Logger;

        private void Awake()
        {
            Logger = base.Logger;

            var settings = SettingsManager.Create(Config);
            if (SteamVRDetector.IsRunning)
            {
                BepInExVrLogBackend.ApplyYourself();
                StartCoroutine(LoadDevice(settings));
            }
            CrossFader.Initialize(Config, enabled);
        }

        private const string DeviceOpenVR = "OpenVR";
        private IEnumerator LoadDevice(KoikatuSettings settings)
        {
            //yield return new WaitUntil(() => Manager.Scene. initialized);
            //yield return new WaitUntil(() => Manager.Scene.initialized && Manager.Scene.LoadSceneName == "Title");
            if (UnityEngine.VR.VRSettings.loadedDeviceName != DeviceOpenVR)
            {
                UnityEngine.VR.VRSettings.LoadDeviceByName(DeviceOpenVR);
                yield return null;
            }
            UnityEngine.VR.VRSettings.enabled = true; 
            while (UnityEngine.VR.VRSettings.loadedDeviceName != DeviceOpenVR)
            {
                yield return null;
            }
            while (true)
            {
                var rect = VRGIN.Native.WindowManager.GetClientRect();
                if (rect.Right - rect.Left > 0)
                {
                    break;
                }
                //VRLog.Info("waiting for the window rect to be non-empty");
                yield return null;
            }

            new Harmony(GUID).PatchAll(typeof(VRPlugin).Assembly);
            VRManager.Create<Interpreters.KoikatuInterpreter>(new KoikatuContext(settings));

            // VRGIN doesn't update the near clip plane until a first "main" camera is created, so we set it here.
            UpdateNearClipPlane(settings);
            UpdateIPD(settings);
            settings.AddListener("NearClipPlane", (_, _1) => UpdateNearClipPlane(settings));
            settings.AddListener("IPDScale", (_, _1) => UpdateIPD(settings));

            VR.Manager.SetMode<GameStandingMode>();


            VRFade.Create();
            PrivacyScreen.Initialize();
            GraphicRaycasterPatches.Initialize();

            // It's been reported in #28 that the game window defocues when
            // the game is under heavy load. We disable window ghosting in
            // an attempt to counter this.
            NativeMethods.DisableProcessWindowsGhosting();

            //TweakShadowSettings();

            //DontDestroyOnLoad(VRCamera.Instance.gameObject);

            // Probably unnecessary, but just to be safe
            //VR.Mode.MoveToPosition(Vector3.zero, Quaternion.Euler(Vector3.zero), true);


            if (SettingsManager.EnableBoop.Value)
                VRBoop.Initialize();
            GameAPI.RegisterExtraBehaviour<InterpreterHooks>(GUID);
            Logger.LogInfo("Finished loading into VR mode!");
        }

        private void UpdateNearClipPlane(KoikatuSettings settings)
        {
            VR.Camera.gameObject.GetComponent<UnityEngine.Camera>().nearClipPlane = settings.NearClipPlane;
        }
        private void UpdateIPD(KoikatuSettings settings)
        {
            VRCamera.Instance.SteamCam.origin.localScale = Vector3.one * settings.IPDScale;
        }
        private static void TweakShadowSettings()
        {
            // This helps environment shadows a bit, and brings flickering chara shadows.
            // Grab "KKS_BetterShadowQualitySettings.dll" from HongFire patch, it does the job much better.
            // Goes without saying, configuration according to the taste is due, as the stock setting is bad, and there is no golden middle for vr.

            // Default shadows look too wobbly in VR.
            //QualitySettings.shadowProjection = ShadowProjection.StableFit;
            //QualitySettings.shadowCascades = 4;
            //QualitySettings.shadowCascade4Split = new Vector4(0.05f, 0.1f, 0.2f);
        }

        private static class NativeMethods
        {
            [DllImport("user32.dll")]
            public static extern void DisableProcessWindowsGhosting();
        }
    }
}
