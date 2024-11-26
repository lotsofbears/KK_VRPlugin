using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Valve.VR;
using VRGIN.Controls;
using VRGIN.Core;
using VRGIN.Helpers;
using UnityEngine;
using KK_VR.Handlers;

namespace KK_VR.Controls
{
    // Placed directly in KK part for comfy use, and easy new features.
    public class GripMove
    {
        private readonly Controller _owner;
        private readonly Controller.Lock _ownerLock;
        private readonly Controller _other;
        //private readonly TravelDistanceRumble _travelRumble;


        /// <summary>
        /// If present, orbiting it instead of changing Yaw around controller.
        /// </summary>
        private Transform _attachPoint;
        private Vector3 _prevAttachVec;
        private Vector3 _prevAttachPos;
        //private Quaternion _prevAttachRot;

        private GripMoveLag _moveLag;

        private bool _main;
        private bool _otherLock;
        private bool _alterYaw;
        private bool _alterRotation;


        private Vector3 _prevPos;
        private Quaternion _prevRot;

        public GripMove(Controller owner)
        {
            _owner = owner;
            _owner.TryAcquireFocus(out _ownerLock, keepTool: true);
            _other = owner.Other;

            _main = true;
            _otherLock = !_other.CanAcquireFocus();


            // Feels too much with those forced vibrations on trigger/grip - press/release.
            //_travelRumble = new TravelDistanceRumble(500, 0.1f, _owner.transform)
            //{
            //    UseLocalPosition = true
            //};

            // _travelRumble.Reset();
            //_owner.StartRumble(_travelRumble);

            _prevPos = _owner.transform.position;
            _prevRot = _owner.transform.rotation;
        }

        /// <summary>
        /// Used as means to acquire highly precise offsets. Adds extra source to evaluate movements and provides the point we start to "orbit".
        /// </summary>
        internal void AttachGripMove(Transform attachPoint)
        {
            // All calculations are done through deltas due to saturated input.
            _attachPoint = attachPoint;
            _prevAttachPos = _attachPoint.position;
            _moveLag.ResetPositions(Vector3.zero);

            // Necessary is we started with trigger already. 
            _prevAttachVec = VR.Camera.Head.TransformPoint(new Vector3(0f, 0.05f, 0f)) - attachPoint.position;
            // With full trigger + touchpad.
            //_prevAttachRot = _attachPoint.rotation;
        }
        public void Destroy()
        {
            //_owner.StopRumble(_travelRumble);
            _ownerLock?.Release();
        }

        public void HandleGrabbing()
        {
            // We check if other controller wants to joint us, or override control if other has ended gripMove.
            // Then we use deltas of orientation to setup origin orientation directly, or through evaluation of multiple frames and "averaging it out" if current action requests it.
            if (_main)
            {
                if (_otherLock && _other.CanAcquireFocus())
                {
                    _otherLock = false;
                }
                if (!_otherLock && !_other.CanAcquireFocus())
                {
                    _main = false;
                }
                else
                {
                    var origin = VR.Camera.SteamCam.origin;
                    if (_alterYaw)
                    {
                        var deltaRot = _prevRot * Quaternion.Inverse(_owner.transform.rotation);
                        //var invRot = Quaternion.Inverse(_prevRot) * _owner.transform.rotation;
                        if (_moveLag == null)
                        {
                            if (_alterRotation)
                            {
                                origin.rotation = deltaRot * origin.rotation;
                            }
                            else
                            {
                                origin.RotateAround(_owner.transform.position, Vector3.up, deltaRot.eulerAngles.y);
                            }
                            origin.position += _prevPos - _owner.transform.position;
                        }
                        else
                        {
                            if (_alterRotation)
                            {
                                if (_attachPoint == null)
                                {
                                    _moveLag.SetPositionAndRotation(deltaRot);
                                }
                                else
                                {
                                    _moveLag.SetDeltaPositionAndRotation(
                                        (_attachPoint.position - _prevAttachPos),
                                        deltaRot
                                        );
                                    _prevAttachPos = _attachPoint.position;
                                    //_prevAttachRot = _attachPoint.rotation;
                                }
                            }
                            else
                            {
                                if (_attachPoint == null)
                                {
                                    var deltaRotY = Quaternion.Euler(0f, deltaRot.eulerAngles.y, 0f);
                                    _moveLag.SetPositionAndRotation(
                                        _owner.transform.position +
                                        deltaRotY * (origin.position - new Vector3(_owner.transform.position.x, origin.position.y, _owner.transform.position.z)),
                                        deltaRotY);
                                }
                                else
                                {
                                    var newAttachVec = deltaRot * _prevAttachVec;
                                    _moveLag.SetDeltaPositionAndRotation(
                                        (newAttachVec - _prevAttachVec) + (_prevPos - _owner.transform.position) + (_attachPoint.position - _prevAttachPos),
                                        deltaRot
                                        );
                                    _prevAttachVec = newAttachVec;
                                    _prevAttachPos = _attachPoint.position;
                                }

                            }

                        }
                    }
                    else
                    {
                        if (_moveLag == null)
                        {
                            origin.position += _prevPos - _owner.transform.position;
                        }
                        else
                        {
                            if (_attachPoint == null)
                            {
                                _moveLag.SetPosition();
                            }
                            else
                            {
                                _moveLag.SetDeltaPosition(_attachPoint.position - _prevAttachPos + (_prevPos - _owner.transform.position));
                                _prevAttachPos = _attachPoint.position;
                            }
                        }
                    }
                }
                _prevPos = _owner.transform.position;
                _prevRot = _owner.transform.rotation;
            }
            else
            {
                if (_other.CanAcquireFocus())
                {
                    _main = true;
                    _prevPos = _owner.transform.position;
                    _prevRot = _owner.transform.rotation;
                }
            }
        }

        internal void StartLag(int avgFrame)
        {
            _moveLag = new GripMoveLag(_owner.transform, avgFrame);
        }
        internal void StopLag()
        {
            _moveLag = null;
            _attachPoint = null;
        }
        internal void OnTrigger(bool press)
        {
            _alterYaw = press;
            if (press)
            {
                UpdateAttachVec();
            }
        }

        internal void OnTouchpad(bool press)
        {
            _alterRotation = press;
            if (!press)
            {
                UpdateAttachVec();
            }
        }
        private void UpdateAttachVec()
        {
            // Due to vec being utilized only by Trigger-mode, other modes don't update it, so we do it on button input.

            if (_moveLag != null && _attachPoint != null)
            {
                _prevAttachVec = VR.Camera.Head.TransformPoint(new Vector3(0f, 0.05f, 0f)) - _attachPoint.position;
            }
        }
    }
}
