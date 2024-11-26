﻿using KK.RootMotion.FinalIK;
using KK_VR.Fixes;
using KK_VR.Handlers;
using KK_VR.Holders;
using KK_VR.IK;
using KK_VR.Interactors;
using KK_VR.Interpreters;
using KK_VR.Trackers;
using RootMotion.FinalIK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using VRGIN.Controls;
using VRGIN.Helpers;
using static KK.RootMotion.FinalIK.IKSolverVR;
using static KK_VR.Grasp.GraspController;
using BodyPart = KK_VR.Grasp.GraspController.BodyPart;

namespace KK_VR.Handlers
{
    internal class BodyPartGuide : PartGuide
    {
        private Vector3 _translateExOffset;
        private bool _translateEx;
        private KK.RootMotion.FinalIK.IKEffector _effector;
        private bool _maintainRot;
        private BodyPart _bodyPart;
        private Quaternion _prevRotOffset;


        /// <summary>
        /// Return relative position weight over period of 1 second.
        /// </summary>
        private void TranslateOnFollow()
        {
            // Way too tricky to do it sneaky. Barely noticeable as it is, not worth it.
            _effector.maintainRelativePositionWeight = Mathf.Clamp01(_effector.maintainRelativePositionWeight + Time.deltaTime);
            if (_effector.maintainRelativePositionWeight == 1f)
            {
                _translateEx = false;
            }
        }
        ///////////////////////////////////////////
        ///                                     ///
        ///   ANCHOR -> IKObject                ///
        ///                                     ///
        ///   THIS.TRANSOFRM -> EFFECTOR.BONE   ///
        ///   THIS.TRANSOFRM != ANCHOR          ///
        ///                                     ///
        ///////////////////////////////////////////

        internal override void Init(BodyPart bodyPart)
        {
            base.Init(bodyPart);
            _effector = BodyPart.effector;
            _bodyPart = bodyPart;
        }
        internal override void Follow(Transform target, HandHolder hand)
        {
            _hand = hand;
            _attach = false;
            _follow = true;
            _target = target;
            if (!_anchor.gameObject.activeSelf)
            {
                //SetBodyPartCollidersToTrigger(true);
                _anchor.gameObject.SetActive(true);
                //transform.parent = _objAnim;
            }
            //_bodyPart.effector.rotationWeight = 1f;
            //_bodyPart.effector.target = _bodyPart.anchor;
            if (_bodyPart.chain != null)
            {
                _bodyPart.chain.bendConstraint.weight = KoikatuInterpreter.settings.IKDefaultBendConstraint;
            }
            if (_bodyPart.effector.maintainRelativePositionWeight != 1f && KoikatuInterpreter.settings.MaintainLimbOrientation)
            {
                _translateEx = true;
                //_translateOffset = _bodyPart.afterIK.position - transform.position;
            }
            else
            {
                // Turning it off just in case.
                _translateEx = false;
            }
            _offsetRot = Quaternion.Inverse(target.rotation) * _anchor.rotation;
            _offsetPos = target.InverseTransformPoint(_anchor.position);

            if (KoikatuInterpreter.settings.ShowGuideObjects) _bodyPart.visual.Show();
            _bodyPart.state = State.Grasped;

            Tracker.SetBlacklistDic(hand.Grasp.GetBlacklistDic);
            ClearBlacks();
            _bodyPart.visual.SetColor(IsBusy);
            _wasBusy = false;
        }

        internal override void Stay()
        {
            _hand = null;
            _follow = false;
            _attach = false;
            //SetBodyPartCollidersToTrigger(false);
            _bodyPart.visual.Hide();
        }

        internal void StartRelativeRotation()
        {
            if (!_maintainRot)
            {
                _maintainRot = true;
                _prevRotOffset = _offsetRot;
                _offsetRot = Quaternion.Inverse(_bodyPart.chain.nodes[1].transform.rotation) * _anchor.rotation;
                //_offsetRot = Quaternion.Inverse(Quaternion.LookRotation(_bodyPart.afterIK.position - _bodyPart.chain.nodes[1].transform.position)) * _anchor.rotation;
            }
        }

        internal void StopRelativeRotation()
        {
            _maintainRot = false;
            _offsetRot = _prevRotOffset;
            _anchor.rotation = _bodyPart.beforeIK.rotation * _offsetRot;
        }

        private void TranslateOnAttach()
        {
            // By default we want to have "effector.maintainRelativePositionWeight" in full weight, but on attached we want it at zero,
            // but doing so changes calculations of IK Solver quite a bit, thus we compensate over the course of 1 second.
            // We look at initial delta vector between OffsetEffector (this gameObject) and actual bone that we see rendered after IK,
            // then each frame we adjust our position based on change of this delta. As result there is only a miniscule offset(at least there should be, it's impossible to notice)
            // between desired position with full 'maintainRelativePositionWeight' and actual without 'maintainRelativePositionWeight'.

            _effector.maintainRelativePositionWeight = Mathf.Clamp01(_effector.maintainRelativePositionWeight - Time.deltaTime);
            _anchor.position = _target.TransformPoint(_offsetPos) - (_translateExOffset - (_anchor.position - _bodyPart.afterIK.position));
            if (_effector.maintainRelativePositionWeight == 0f)
            {
                _translateEx = false;
                _offsetRot = Quaternion.Inverse(_target.rotation) * _anchor.rotation;
                _offsetPos = _target.InverseTransformPoint(_anchor.position);
            }
        }
        internal override void Attach(Transform target)
        {
            _hand = null;
            _translateEx = true;
            _attach = true;
            _target = target;

            _translateExOffset = _anchor.position - _bodyPart.afterIK.position;

            _offsetRot = Quaternion.Inverse(_target.rotation) * _anchor.rotation;
            _offsetPos = _target.InverseTransformPoint(_anchor.position);
            //transform.parent = _objAnim;
        }

        private void Update()
        {
            if (_follow)
            {
                if (_translateEx)
                {
                    if (_follow)
                    {
                        if (KoikatuInterpreter.settings.MaintainLimbOrientation)
                        {
                            TranslateOnFollow();
                        }
                        _anchor.SetPositionAndRotation(
                            _target.TransformPoint(_offsetPos),
                            _target.rotation * _offsetRot
                            );
                    }
                    else
                    {
                        TranslateOnAttach();
                    }
                }
                else
                {
                    _anchor.SetPositionAndRotation(
                        _target.TransformPoint(_offsetPos),
                        _target.rotation * _offsetRot
                        );
                }
            }
            else
            {
                if (_maintainRot)
                {
                    _anchor.rotation = _bodyPart.chain.nodes[1].transform.rotation * _offsetRot;
                }
                else if (_translate != null)
                {
                    _translate.DoStep();
                }
            }

        }
        private void LateUpdate()
        {
            _effector.positionOffset += _anchor.position - _effector.bone.position;
        }

        //private void FixedUpdate()
        //{
        //    //if (_unwind)
        //    //{
        //    //    _timer = Mathf.Clamp01(_timer - Time.deltaTime);
        //    //    _rigidBody.velocity *= _timer;
        //    //    if (_timer == 0f)
        //    //    {
        //    //        _unwind = false;
        //    //    }
        //    //}
        //    if (_follow)
        //    {
        //        _rigidBody.MovePosition(_target.TransformPoint(_offsetPos));
        //        _rigidBody.MoveRotation(_target.rotation * _offsetRot);

        //        if (_translate)
        //        {
        //            TranslateOnFollow();
        //        }
        //    }
        //}
    }
}

