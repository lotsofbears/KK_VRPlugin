using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Valve.VR
{
    public class SteamVR_TrackedCamera
    {
        public class VideoStreamTexture
        {
            private Texture2D _texture;

            private int prevFrameCount = -1;

            private uint glTextureId;

            private VideoStream videostream;

            private CameraVideoStreamFrameHeader_t header;

            public bool undistorted { get; private set; }

            public uint deviceIndex => videostream.deviceIndex;

            public bool hasCamera => videostream.hasCamera;

            public bool hasTracking
            {
                get
                {
                    Update();
                    return header.trackedDevicePose.bPoseIsValid;
                }
            }

            public uint frameId
            {
                get
                {
                    Update();
                    return header.nFrameSequence;
                }
            }

            public VRTextureBounds_t frameBounds { get; private set; }

            public EVRTrackedCameraFrameType frameType
            {
                get
                {
                    if (!undistorted) return EVRTrackedCameraFrameType.Distorted;
                    return EVRTrackedCameraFrameType.Undistorted;
                }
            }

            public Texture2D texture
            {
                get
                {
                    Update();
                    return _texture;
                }
            }

            public SteamVR_Utils.RigidTransform transform
            {
                get
                {
                    Update();
                    return new SteamVR_Utils.RigidTransform(header.trackedDevicePose.mDeviceToAbsoluteTracking);
                }
            }

            public Vector3 velocity
            {
                get
                {
                    Update();
                    var trackedDevicePose = header.trackedDevicePose;
                    return new Vector3(trackedDevicePose.vVelocity.v0, trackedDevicePose.vVelocity.v1, 0f - trackedDevicePose.vVelocity.v2);
                }
            }

            public Vector3 angularVelocity
            {
                get
                {
                    Update();
                    var trackedDevicePose = header.trackedDevicePose;
                    return new Vector3(0f - trackedDevicePose.vAngularVelocity.v0, 0f - trackedDevicePose.vAngularVelocity.v1, trackedDevicePose.vAngularVelocity.v2);
                }
            }

            public VideoStreamTexture(uint deviceIndex, bool undistorted)
            {
                this.undistorted = undistorted;
                videostream = Stream(deviceIndex);
            }

            public TrackedDevicePose_t GetPose()
            {
                Update();
                return header.trackedDevicePose;
            }

            public ulong Acquire()
            {
                return videostream.Acquire();
            }

            public ulong Release()
            {
                var result = videostream.Release();
                if (videostream.handle == 0L)
                {
                    UnityEngine.Object.Destroy(_texture);
                    _texture = null;
                }

                return result;
            }

            private void Update()
            {
                if (Time.frameCount == prevFrameCount) return;
                prevFrameCount = Time.frameCount;
                if (videostream.handle == 0L) return;
                var instance = SteamVR.instance;
                if (instance == null) return;
                var trackedCamera = OpenVR.TrackedCamera;
                if (trackedCamera == null) return;
                var ppD3D11ShaderResourceView = IntPtr.Zero;
                var texture2D = _texture != null ? _texture : new Texture2D(2, 2);
                var nFrameHeaderSize = (uint)Marshal.SizeOf(header.GetType());
                if (instance.textureType == ETextureType.OpenGL)
                {
                    if (glTextureId != 0) trackedCamera.ReleaseVideoStreamTextureGL(videostream.handle, glTextureId);
                    if (trackedCamera.GetVideoStreamTextureGL(videostream.handle, frameType, ref glTextureId, ref header, nFrameHeaderSize) != 0) return;
                    ppD3D11ShaderResourceView = (IntPtr)glTextureId;
                }
                else if (instance.textureType == ETextureType.DirectX &&
                         trackedCamera.GetVideoStreamTextureD3D11(videostream.handle, frameType, texture2D.GetNativeTexturePtr(), ref ppD3D11ShaderResourceView, ref header, nFrameHeaderSize) != 0) return;

                if (_texture == null)
                {
                    _texture = Texture2D.CreateExternalTexture((int)header.nWidth, (int)header.nHeight, TextureFormat.RGBA32, false, false, ppD3D11ShaderResourceView);
                    var pnWidth = 0u;
                    var pnHeight = 0u;
                    var pTextureBounds = default(VRTextureBounds_t);
                    if (trackedCamera.GetVideoStreamTextureSize(deviceIndex, frameType, ref pTextureBounds, ref pnWidth, ref pnHeight) == EVRTrackedCameraError.None)
                    {
                        pTextureBounds.vMin = 1f - pTextureBounds.vMin;
                        pTextureBounds.vMax = 1f - pTextureBounds.vMax;
                        frameBounds = pTextureBounds;
                    }
                }
                else
                    _texture.UpdateExternalTexture(ppD3D11ShaderResourceView);
            }
        }

        private class VideoStream
        {
            private ulong _handle;

            private bool _hasCamera;

            private ulong refCount;

            public uint deviceIndex { get; private set; }

            public ulong handle => _handle;

            public bool hasCamera => _hasCamera;

            public VideoStream(uint deviceIndex)
            {
                this.deviceIndex = deviceIndex;
                OpenVR.TrackedCamera?.HasCamera(deviceIndex, ref _hasCamera);
            }

            public ulong Acquire()
            {
                if (_handle == 0L && hasCamera) OpenVR.TrackedCamera?.AcquireVideoStreamingService(deviceIndex, ref _handle);
                return ++refCount;
            }

            public ulong Release()
            {
                if (refCount != 0 && --refCount == 0L && _handle != 0L)
                {
                    OpenVR.TrackedCamera?.ReleaseVideoStreamingService(_handle);
                    _handle = 0uL;
                }

                return refCount;
            }
        }

        private static VideoStreamTexture[] distorted;

        private static VideoStreamTexture[] undistorted;

        private static VideoStream[] videostreams;

        public static VideoStreamTexture Distorted(int deviceIndex = 0)
        {
            if (distorted == null) distorted = new VideoStreamTexture[64];
            if (distorted[deviceIndex] == null) distorted[deviceIndex] = new VideoStreamTexture((uint)deviceIndex, false);
            return distorted[deviceIndex];
        }

        public static VideoStreamTexture Undistorted(int deviceIndex = 0)
        {
            if (undistorted == null) undistorted = new VideoStreamTexture[64];
            if (undistorted[deviceIndex] == null) undistorted[deviceIndex] = new VideoStreamTexture((uint)deviceIndex, true);
            return undistorted[deviceIndex];
        }

        public static VideoStreamTexture Source(bool undistorted, int deviceIndex = 0)
        {
            if (!undistorted) return Distorted(deviceIndex);
            return Undistorted(deviceIndex);
        }

        private static VideoStream Stream(uint deviceIndex)
        {
            if (videostreams == null) videostreams = new VideoStream[64];
            if (videostreams[deviceIndex] == null) videostreams[deviceIndex] = new VideoStream(deviceIndex);
            return videostreams[deviceIndex];
        }
    }
}
