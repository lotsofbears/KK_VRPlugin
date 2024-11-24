﻿using IllusionUtility.GetUtility;
using KK_VR.Holders;
using KK_VR.Trackers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using VRGIN.Helpers;
using static ActionGame.VisibleController;
using BodyPart = KK_VR.Grasp.GraspController.BodyPart;

namespace KK_VR.Handlers
{
    // Component for actual character bone (bodyPart.afterIK). Can be repositioned at whim.
    // Controls collider tracker and movement of IK object. 
    // When IK object is being set and there are appropriate colliders within range of this component, 
    // IK object may be attached to intersecting collider. Due to nature of IK setup,
    // IK object is always somewhere not where you'd expect it to be,
    // thus we manage it through this component attached to the bone that would better represent particular IK point visually.
    internal abstract class PartGuide : Handler
    {
        protected class Translate
        {
            internal Translate(Transform anchor, Action onFinish)
            {
                _anchor = anchor;
                _offsetPos = anchor.localPosition;
                _offsetRot = anchor.localRotation;
                _onFinish = onFinish;
            }
            private float _lerp;
            private readonly Transform _anchor;
            private readonly Quaternion _offsetRot;
            private readonly Vector3 _offsetPos;
            private readonly Action _onFinish;

            internal void DoStep()
            {
                _lerp += Time.deltaTime;
                var step = Mathf.SmoothStep(0f, 1f, _lerp);
                _anchor.localPosition = Vector3.Lerp(_offsetPos, Vector3.zero, step);
                _anchor.localRotation = Quaternion.Lerp(_offsetRot, Quaternion.identity, step);
                if (_lerp >= 1f) _onFinish.Invoke();
                VRPlugin.Logger.LogDebug($"Translate:{_lerp}:{_anchor.localPosition}:{_anchor.localRotation.eulerAngles}");
            }
        }

        protected HandHolder _hand;
        protected virtual BodyPart BodyPart { get; set; }

        // Transform that guides IK, separate gameObject.
        protected Transform _anchor;

        // p_cf_body_bone (chara.objAnim) , doesn't move much, has all IKs in on place.
        // not actual anymore.
        //protected Transform _objAnim;
        protected Transform _target;
        // F it, non-penetrative behavior hardly worth it here. Maybe later.
        protected Rigidbody _rigidBody;

        //protected Vector3 _origScale;
        protected Vector3 _offsetPos;
        protected Quaternion _offsetRot;

        protected Translate _translate;

        //private float _timer;
        //private bool _unwind;
        protected bool _wasBusy;

        // If are not in default state.
        protected bool _follow;
        // If we follow particular transform.
        protected bool _attach;

        protected virtual void Awake()
        {
            //_origScale = transform.localScale;
            _rigidBody = gameObject.AddComponent<Rigidbody>();
            _rigidBody.isKinematic = true;
            _rigidBody.freezeRotation = true;

            // Implement accurate, controlled play with this.
            _rigidBody.useGravity = false;

            // Default primitive's collider-trigger.
            var colliderTrigger = gameObject.GetComponent<SphereCollider>();
            colliderTrigger.isTrigger = true;

            // RigidBody's slave.
            //var sphere = gameObject.AddComponent<SphereCollider>();
            //sphere.isTrigger = false;
            //sphere.radius = Mathf.Round(1000f * (colliderTrigger.radius * 0.7f)) * 0.001f;
            Tracker = new Tracker();
        }
        protected virtual void OnEnable()
        {
            _wasBusy = false;
        }

        internal virtual void Init(BodyPart bodyPart)
        {
            BodyPart = bodyPart;
            _anchor = bodyPart.anchor;
            //_objAnim = chara.objAnim.transform;
        }
        /// <summary>
        /// Sets the transform (controller) as parent of the part for further manipulation. 
        /// </summary>
        internal abstract void Follow(Transform target, HandHolder hand);
        /// <summary>
        /// Sets part's parent to itself before IK, to follow it with offset.
        /// </summary>
        internal abstract void Stay();
        /// <summary>
        /// Returns part to the default state.
        /// </summary>
        internal void Sleep(bool instant)
        {
            _hand = null;
            _follow = false;
            _attach = false;
            if (instant)
            {
                _anchor.localPosition = Vector3.zero;
                _anchor.localRotation = Quaternion.identity;
                Disable();
            }
            else
            {
                BodyPart.state = Grasp.GraspController.State.Translation;
                _translate = new(_anchor, Disable);
            }
            BodyPart.visual.Hide();
            //transform.localScale = _origScale;
            //transform.localPosition = Vector3.zero;
            //transform.localRotation = Quaternion.identity;
        }
        protected virtual void Disable()
        {
            _translate = null;
            if (BodyPart.chain != null)
            {
                BodyPart.chain.bendConstraint.weight = 1f;
            }
            BodyPart.state = Grasp.GraspController.State.Default;
            _anchor.gameObject.SetActive(BodyPart.GetDefaultState());
            //transform.parent = _bodyPart.beforeIK;
            //SetBodyPartCollidersToTrigger(false);
        }

        /// <summary>
        /// Sets the part to follow motion of particular transform.
        /// </summary>
        internal abstract void Attach(Transform target);

        //internal void SetBodyPartCollidersToTrigger(bool active)
        //{
        //    // To let rigidBody run free. Currently on hold, we use 'isKinematic' for a moment.
        //    foreach (var kv in _bodyPart.colliders)
        //    {
        //        kv.Key.isTrigger = active || kv.Value;
        //        //VRPlugin.Logger.LogDebug($"{_bodyPart.name} set {kv.Key.name}.Trigger = {kv.Key.isTrigger}[{kv.Value}]");
        //    }
        //}

        protected override void OnTriggerEnter(Collider other)
        {
            if (Tracker.AddCollider(other))
            {
                if (!_wasBusy)
                {
                    _wasBusy = true;
                    if (_hand != null)
                    {
                        _hand.Controller.StartRumble(new RumbleImpulse(500));
                        BodyPart.visual.SetColor(true);
                    }
                }
            }
        }

        protected override void OnTriggerExit(Collider other)
        {
            if (Tracker.RemoveCollider(other))
            {
                if (!IsBusy)
                {
                    _wasBusy = false;
                    //_unwind = true;
                    //_timer = 1f;
                    if (_hand != null)
                    {
                        BodyPart.visual.SetColor(false);
                    }
                }
            }
        }

    }
}

