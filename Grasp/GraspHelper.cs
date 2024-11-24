﻿using Illusion.Component.Correct;
using KK.RootMotion.FinalIK;
using KK_VR.Camera;
using KK_VR.Fixes;
using KK_VR.Handlers;
using KK_VR.IK;
using KK_VR.Interpreters;
using KK_VR.Settings;
using KK_VR.Trackers;
using KK_VR.Grasp;
using RootMotion.FinalIK;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using VRGIN.Core;
using static KK_VR.Grasp.GraspController;
using static KK_VR.Grasp.TouchReaction;
using BodyPart = KK_VR.Grasp.GraspController.BodyPart;
using KK_VR.Holders;
using Valve.VR;
using static KK.RootMotion.FinalIK.IKSolverVR;

namespace KK_VR.Grasp
{
    internal class GraspHelper : MonoBehaviour
    {
        internal static GraspHelper Instance => _instance;
        private static GraspHelper _instance;
        //private bool _transition;
        private bool _animChange;
        private bool _handChange;
        //private readonly List<OffsetPlay> _transitionList = [];
        private readonly Dictionary<ChaControl, string> _animChangeDic = [];
        private static Dictionary<ChaControl, List<BodyPart>> _bodyPartsDic;
        private static Dictionary<ChaControl, IKStuff> _auxDic = [];
        private readonly List<HandScroll> _handScrollList = [];
        private bool _baseHold;
        private readonly List<BaseHold> _baseHoldList = [];

        // Switch from chara root to objAnim.
        private static readonly List<OrigOrient> _origOrientList = [];

        private class OrigOrient
        {
            internal OrigOrient(ChaControl chara)
            {
                _chara = chara.transform;
                _position = _chara.position;
                _rotation = _chara.rotation;
            }
            private readonly Transform _chara;
            private readonly Vector3 _position;
            private readonly Quaternion _rotation;

            internal void Restore() => _chara.SetPositionAndRotation(_position, _rotation);
        }
        private class IKStuff
        {
            internal KK.RootMotion.FinalIK.FullBodyBipedIK newFbik;
            internal RootMotion.FinalIK.FullBodyBipedIK oldFbik;
            internal LookAtController lookAt;
            internal TouchReaction reaction;
        }
        internal void Init(IEnumerable<ChaControl> charas, Dictionary<ChaControl, List<BodyPart>> bodyPartsDic)
        {
            _instance = this;
            _auxDic.Clear();
            _bodyPartsDic = bodyPartsDic;
            foreach (var chara in charas)
            {
                // Dude will have VRIK. Guess we'll need both and hot swap option for all of them. 
                // hot swap can't be pretty though, but if it happens on the time of pov exit, then it should be fine.
                if (chara.sex == 1)
                {
                    AddChara(chara);
                    _origOrientList.Add(new(chara));
                }
            }
        }

        private void AddChara(ChaControl chara)
        {
            _auxDic.Add(chara, new IKStuff
            {
                newFbik = FBBIK.UpdateFBIK(chara),
                oldFbik = chara.objAnim.GetComponent<RootMotion.FinalIK.FullBodyBipedIK>(),
                lookAt = LookAt.SetupLookAtIK(chara),
                reaction = chara.objAnim.AddComponent<TouchReaction>()
            });
            AnimLoaderHelper.FindMissingBones(_auxDic[chara].oldFbik);
            var ik = _auxDic[chara].newFbik;
            if (ik == null) return;
            _bodyPartsDic.Add(chara,
            [

                new (
                    _name:       PartName.Spine,
                    _effector:   ik.solver.bodyEffector,
                    _afterIK:    ik.solver.bodyEffector.bone,
                    _beforeIK:   BeforeIK.CreateObj("spine", chara, ik.solver.bodyEffector.bone),
                    _chain:      ik.solver.chain[0]
                    ),

                new (
                    _name:       PartName.ShoulderL,
                    _effector:   ik.solver.leftShoulderEffector,
                    _afterIK:    ik.solver.leftShoulderEffector.bone,
                    _beforeIK:   BeforeIK.CreateObj("shoulderL", chara, ik.solver.leftShoulderEffector.bone)
                    ),

                new (
                    _name:       PartName.ShoulderR,
                    _effector:   ik.solver.rightShoulderEffector,
                    _afterIK:    ik.solver.rightShoulderEffector.bone,
                    _beforeIK:   BeforeIK.CreateObj("shoulderR", chara, ik.solver.rightShoulderEffector.bone)
                    ),

                new (
                    _name:       PartName.ThighL,
                    _effector:   ik.solver.leftThighEffector,
                    _afterIK:    ik.solver.leftThighEffector.bone,
                    _beforeIK:   BeforeIK.CreateObj("thighL", chara, ik.solver.leftThighEffector.bone)
                    ),

                new (
                    _name:       PartName.ThighR,
                    _effector:   ik.solver.rightThighEffector,
                    _afterIK:    ik.solver.rightThighEffector.bone,
                    _beforeIK:   BeforeIK.CreateObj("thighR", chara, ik.solver.rightThighEffector.bone)
                    ),

                new (
                    _name:       PartName.HandL,
                    _effector:   ik.solver.leftHandEffector,
                    _afterIK:    ik.solver.leftHandEffector.bone,
                    _beforeIK:   ik.solver.leftHandEffector.target,
                    _chain:      ik.solver.leftArmChain
                    ),

                new (
                    _name:       PartName.HandR,
                    _effector:   ik.solver.rightHandEffector,
                    _afterIK:    ik.solver.rightHandEffector.bone,
                    _beforeIK:   ik.solver.rightHandEffector.target,
                    _chain:      ik.solver.rightArmChain
                    ),

                new (
                    _name:       PartName.FootL,
                    _effector:   ik.solver.leftFootEffector,
                    _afterIK:    ik.solver.leftFootEffector.bone,
                    _beforeIK:   ik.solver.leftFootEffector.target,
                    _chain:      ik.solver.leftLegChain
                    ),

                new (
                    _name:       PartName.FootR,
                    _effector:   ik.solver.rightFootEffector,
                    _afterIK:    ik.solver.rightFootEffector.bone,
                    _beforeIK:   ik.solver.rightFootEffector.target,
                    _chain:      ik.solver.rightLegChain
                    ),

                new BodyPartHead(
                    _name:       PartName.Head,
                    _chara:      chara,
                    _afterIK:    ik.references.head,
                    _beforeIK:   BeforeIK.CreateObj("head", chara, ik.references.head)
                    ),
            ]);

            AddExtraColliders(chara);
            foreach (var bodyPart in _bodyPartsDic[chara])
            {
                bodyPart.anchor.SetParent(bodyPart.beforeIK, false);
                if (bodyPart.name == PartName.Head)
                {
                    ((BodyPartHead)bodyPart).headEffector.enabled = KoikatuInterpreter.settings.IKHeadEffector == KoikatuSettings.HeadEffector.Always;
                }
                bodyPart.guide.Init(bodyPart);

                if (KoikatuInterpreter.settings.IKShowDebug)
                {
                    Util.CreatePrimitive(PrimitiveType.Sphere, new Vector3(0.06f, 0.06f, 0.06f), bodyPart.anchor, Color.yellow, 0.5f);
                    //Util.CreatePrimitive(PrimitiveType.Sphere, new Vector3(0.12f, 0.12f, 0.12f), bodyPart.afterIK, Color.yellow, 0.4f);
                }
                if (bodyPart.name > PartName.ThighR && bodyPart.name != PartName.Head)
                {
                    bodyPart.colliders = FindColliders(chara, bodyPart.name);
                }
                else
                {
                    bodyPart.colliders = [];
                }

            }
            SetWorkingState(chara);

            // MonoBehavior will get sad if we won't let it get Start().
            StartCoroutine(InitCo(_bodyPartsDic[chara]));
        }

        private IEnumerator InitCo(IEnumerable<BodyPart> bodyParts)
        {
            yield return null;
            foreach (var bodyPart in bodyParts)
            {
                bodyPart.anchor.gameObject.SetActive(bodyPart.GetDefaultState());
            }
        }

        internal IKCaress StartRoughCaress(HandCtrl.AibuColliderKind colliderKind, ChaControl chara, HandHolder hand)
        {
            var rough = hand.Anchor.gameObject.AddComponent<IKCaress>();
            rough.Init(_auxDic[chara].newFbik, colliderKind, _bodyPartsDic[chara], chara, hand.Anchor);
            return rough;
        }
        //internal void OnSpotChangePre()
        //{
        //    //foreach (var orient in _origOrientList)
        //    //{
        //    //    orient.Restore();
        //    //}
        //    //_origOrientList.Clear();
        //}
        //internal void OnSpotChangePost()
        //{
        //    //VRPlugin.Logger.LogDebug($"Helper:Grasp:OnSpotChange");
        //    //foreach (var kv in _bodyPartsDic)
        //    //{
        //    //    _origOrientList.Add(new(kv.Key));
        //    //}
        //}
        private void SetRelativePosition()
        {

        }
        private readonly Dictionary<string, float[]> _poseRelPos = new Dictionary<string, float[]>
        {
            { "kha_f_00", [ 1f, 0f ] },
            { "kha_f_01", [ 0f, 0f ] },
            { "kha_f_02", [ 0f, 0f ] },
            { "kha_f_03", [ 1f, 1f ] },
            { "kha_f_04", [ 0f, 0f ] },
            { "kha_f_05", [ 0f, 0f ] },
            { "kha_f_06", [ 0f, 0f ] },
            { "kha_f_07", [ 0f, 1f ] },


            { "khs_f_00", [ 1f, 1f ] },
        };
        private readonly List<string> _extraColliders =
        [
            "cf_n_height/cf_j_hips/cf_j_waist01/cf_j_waist02/cf_j_thigh00_L/cf_j_leg01_L/cf_j_leg03_L/cf_j_foot_L/cf_hit_leg02_L",
            "cf_n_height/cf_j_hips/cf_j_waist01/cf_j_waist02/cf_j_thigh00_R/cf_j_leg01_R/cf_j_leg03_R/cf_j_foot_R/cf_hit_leg02_R",
        ];

        private void AddFeetCollider(Transform bone)
        {
            var collider = bone.gameObject.AddComponent<CapsuleCollider>();
            collider.radius = 0.1f;
            collider.height = 0.5f;
            collider.direction = 2;
            bone.localPosition = new Vector3(bone.localPosition.x, 0f, 0.06f);
        }
        private void AddExtraColliders(ChaControl chara)
        {
            foreach (var path in _extraColliders)
            {
                AddFeetCollider(chara.objBodyBone.transform.Find(path));
            }
        }
        private Dictionary<Collider, bool> FindColliders(ChaControl chara, PartName partName)
        {
            var dic = new Dictionary<Collider, bool>();
            foreach (var str in _limbColliders[partName])
            {
                var target = chara.objBodyBone.transform.Find(str);
#if KK
                if (target != null)
                {
                    var col = target.GetComponent<Collider>();
                    if (col != null)
                    {
                        dic.Add(col, col.isTrigger);
                    }
                }
#else
                if (target != null && target.TryGetComponent<Collider>(out var col))
                {
                    dic.Add(col, col.isTrigger);
                }
#endif
            }
            return dic;
        }

        // With new ik mostly obsolete, only want hook from prefix for anim change.
        internal static void SetWorkingState(ChaControl chara)
        {
            // By default only limbs are used, the rest is limited to offset play by hitReaction.
            VRPlugin.Logger.LogDebug($"Helper:Grasp:SetWorkingState:{chara}");
            if (_bodyPartsDic != null && _bodyPartsDic.ContainsKey(chara))
            {
                foreach (var bodyPart in _bodyPartsDic[chara])
                {
                    if (bodyPart.effector != null)
                    {
                        //if (!bodyPart.IsLimb())
                        //    bodyPart.targetBaseData.bone = bodyPart.effector.bone;
                        bodyPart.effector.target = bodyPart.anchor;
                        if (bodyPart.chain != null)
                        {
                            bodyPart.chain.bendConstraint.weight = bodyPart.state == State.Default ? 1f : KoikatuInterpreter.settings.IKDefaultBendConstraint;
                        }
                    }
                }
                _auxDic[chara].oldFbik.enabled = false;
            }
        }

        /// <summary>
        /// We put IKEffector.target to theirs ~default states.
        /// MotionIK.Calc() requires original stuff, without it we won't get body size offsets or effector's supposed targets.
        /// </summary>
        internal static void SetDefaultState(ChaControl chara, string stateName)
        {
            VRPlugin.Logger.LogDebug($"Helper:Grasp:SetDefaultState:{chara}");
            if (_bodyPartsDic != null && _bodyPartsDic.ContainsKey(chara))
            {
                if (stateName != null && chara.objTop.activeSelf && chara.visibleAll)
                {
                    _instance.StartAnimChange(chara, stateName);
                }
                foreach (var bodyPart in _bodyPartsDic[chara])
                {
                    if (bodyPart.effector != null)
                    {
                        bodyPart.effector.target = bodyPart.origTarget;
                        if (bodyPart.chain != null)
                        {
                            bodyPart.chain.bendConstraint.weight = 1f;
                        }
                    }
                }
            }

        }
        /// <summary>
        /// We hold anchors of currently modified Hand bodyParts while animation crossfades, and return back afterwards.
        /// </summary>
        private void StartAnimChange(ChaControl chara, string stateName)
        {
            VRPlugin.Logger.LogDebug($"Helper:Grasp:StartAnimChange:{chara}");
            for (var i = 6; i < 8; i++)
            {
                var bodyPart = _bodyPartsDic[chara][i];
                if (bodyPart.state == State.Active)
                {
                    //var parent = GetParent(bodyPart.name);
                    //VRPlugin.Logger.LogDebug($"AnimChange:Add:{bodyPart.name} -> {parent} -> {_bodyPartsDic[chara][(int)parent].origTarget}");
                    if (!_animChangeDic.ContainsKey(chara))
                    {
                        _animChangeDic.Add(chara, stateName);
                        _animChange = true;
                    }
                    bodyPart.anchor.parent = _bodyPartsDic[chara][(int)GetParent(bodyPart.name)].anchor;
                }
            }
        }
        private PartName GetParent(PartName partName)
        {
            return partName switch
            {
                PartName.HandL => PartName.ShoulderL,
                PartName.HandR => PartName.ShoulderR,
                PartName.FootL => PartName.ThighL,
                PartName.FootR => PartName.ThighR,
                _ => PartName.Spine
            };
        }
        private void DoAnimChange()
        {
            foreach (var kv in _animChangeDic)
            {
                VRPlugin.Logger.LogDebug($"AnimChangeWait:{kv.Key}:{kv.Value}");
                if (kv.Key.animBody.GetCurrentAnimatorStateInfo(0).IsName(kv.Value))
                {
                    OnAnimChangeEnd(kv.Key);
                    return;
                }
            }
        }
        internal void ScrollHand(PartName partName, ChaControl chara, bool increase)
        {
            _handChange = true;
            _handScrollList.Add(new HandScroll(partName, chara, increase));
        }
        internal void StopScroll()
        {
            _handChange = false;
            _handScrollList.Clear();
        }
        internal void OnPoseChange()
        {
            //VRPlugin.Logger.LogDebug($"Helper:Grasp:OnPoseChange");
            //StopTransition();
            StopAnimChange();
            //foreach (var orig in _origOrientList)
            //{
            //    orig.Restore();
            //}
            RetargetEffectors();
            foreach (var kv in _bodyPartsDic)
            {
                foreach (var bodyPart in kv.Value)
                {
                    bodyPart.guide.Sleep(true);
                }
                AnimLoaderHelper.FindMissingBones(kv.Key.objAnim.GetComponent<RootMotion.FinalIK.FullBodyBipedIK>());
            }
        }
        internal void RetargetEffectors()
        {
            // Limbs only
            foreach (var kv in _auxDic)
            {
                for (var i = 5; i < 9; i++)
                {
                    if (kv.Value.oldFbik.solver.effectors[i].rotationWeight == 0f)
                    {
                        _bodyPartsDic[kv.Key][i].targetBaseData.Reset();
                        _bodyPartsDic[kv.Key][i].targetBaseData.bone = _bodyPartsDic[kv.Key][i].effector.bone;
                    }
                }
            }
        }
        internal void TouchReaction(ChaControl chara, Vector3 handPosition, Tracker.Body body)
        {
            if (_auxDic.ContainsKey(chara))
            {
                foreach (var bodyPart in _bodyPartsDic[chara])
                {
                    if (bodyPart.IsLimb())
                    {
                        ((BodyPartGuide)bodyPart.guide).StartRelativeRotation();
                    }
                }
                var index = ConvertToTouch(body);
                var vec = (GetClosestBone(chara, index).position - handPosition);
                vec.y = 0f;
                _auxDic[chara].reaction.React(index, vec.normalized);
            }
        }
        private int ConvertToTouch(Tracker.Body part)
        {
            return part switch
            {
                Tracker.Body.LowerBody => 0,
                Tracker.Body.ArmL or Tracker.Body.MuneL => 1,
                Tracker.Body.ArmR  or Tracker.Body.MuneR => 2,
                Tracker.Body.ThighL => 3,
                Tracker.Body.ThighR => 4,
                Tracker.Body.HandL or Tracker.Body.ForearmL => 5,
                Tracker.Body.HandR or Tracker.Body.ForearmR => 6,
                Tracker.Body.FootL => 7,
                Tracker.Body.FootR => 8,
                Tracker.Body.UpperBody or Tracker.Body.Head => 9,
                Tracker.Body.Groin or Tracker.Body.Asoko => 10,
                Tracker.Body.LegL => 11,
                Tracker.Body.LegR => 12,
                _ => 0
            };
        }

        internal void CatchHitReaction(RootMotion.FinalIK.IKSolverFullBodyBiped solver, Vector3 offset, int index)
        {
            foreach (var value in _auxDic.Values)
            {
                if (value.oldFbik.solver == solver)
                {
                    value.newFbik.solver.effectors[index].positionOffset += offset;
                }
            }
        }
        internal void OnTouchReactionStop(ChaControl chara)
        {
            foreach (var bodyPart in _bodyPartsDic[chara])
            {
                if (bodyPart.IsLimb())
                {
                    ((BodyPartGuide)bodyPart.guide).StopRelativeRotation();
                }
            }
            
        }
        private Transform GetClosestBone(ChaControl chara, int index)
        {
            // Normalize, not readable otherwise.
            return index switch
            {
                0 => _auxDic[chara].newFbik.solver.rootNode,   // chara.objBodyBone.transform.Find("cf_n_height/cf_j_hips/cf_j_spine01"),
                1 or 2 or 9 => chara.dictRefObj[ChaReference.RefObjKey.BUSTUP_TARGET].transform, //  chara.objBodyBone.transform.Find("cf_n_height/cf_j_hips/cf_j_spine01/cf_j_spine02/cf_j_spine03"),
                3 or 4 or > 9 => chara.objBodyBone.transform.Find("cf_n_height/cf_j_hips/cf_j_waist01/cf_j_waist02"),
                _ => _auxDic[chara].newFbik.solver.effectors[index].bone.transform
            };
        }
        private readonly List<PartName> _partNamesToHold =
            [
            PartName.HandL,
            PartName.HandR
            ];
        private void Update()
        {
            if (_baseHold) DoBaseHold();
            if (_animChange) DoAnimChange();
            if (_handChange) DoHandChange();
        }
        private void OnAnimChangeEnd(ChaControl chara)
        {
            VRPlugin.Logger.LogDebug($"Helper:Grasp:OnAnimChangeEnd");
            foreach (var part in _partNamesToHold)
            {
                var bodyPart = _bodyPartsDic[chara][(int)part];
                if (bodyPart.state == State.Active)
                {
                    bodyPart.anchor.parent = bodyPart.beforeIK;// SetParent(bodyPart.beforeIK, worldPositionStays: true);
                }
            }
            _animChangeDic.Remove(chara);
            _animChange = _animChangeDic.Count != 0;
        }

        internal class BaseHold
        {
            internal BaseHold(BodyPart _bodyPart, Transform _objAnim, Transform _attachPoint)
            {
                bodyPart = _bodyPart;
                objAnim = _objAnim;
                attachPoint = _attachPoint;
                offsetPos = _attachPoint.InverseTransformDirection(_objAnim.transform.position - _attachPoint.position);
                offsetRot = Quaternion.Inverse(_attachPoint.rotation) * _objAnim.transform.rotation;
            }
            internal BodyPart bodyPart;
            internal Transform objAnim;
            internal Transform attachPoint;
            internal Quaternion offsetRot;
            internal Vector3 offsetPos;
            internal int scrollDir;
            internal bool scrollInc;
        }
        internal BaseHold StartBaseHold(BodyPart bodyPart, Transform objAnim, Transform attachPoint)
        {
            _baseHold = true;
            var baseHold = new BaseHold(bodyPart, objAnim, attachPoint);
            _baseHoldList.Add(baseHold);
            return baseHold;
        }
        private void DoBaseHold()
        {
            foreach (var hold in _baseHoldList)
            {
                if (hold.scrollDir != 0)
                {
                    if (hold.scrollDir == 1)
                    {
                        DoBaseHoldVerticalScroll(hold, hold.scrollInc);
                    }
                    else
                    {
                        DoBaseHoldHorizontalScroll(hold, hold.scrollInc);
                    }
                }
                hold.objAnim.transform.SetPositionAndRotation(
                    hold.attachPoint.position + hold.attachPoint.TransformDirection(hold.offsetPos),
                    hold.attachPoint.rotation * hold.offsetRot
                    );
            }
        }
        internal void StopBaseHold(BaseHold baseHold)
        {
            _baseHoldList.Remove(baseHold);
            if (_baseHoldList.Count == 0)
            {
                _baseHold = false;
            }
        }
        private void DoHandChange()
        {
            foreach (var scroll in _handScrollList)
            {
                scroll.Scroll();
            }
        }
        internal void StartBaseHoldScroll(BaseHold baseHold, int direction, bool increase)
        {
            baseHold.scrollDir = direction;
            baseHold.scrollInc = increase;
        }

        internal void StopBaseHoldScroll(BaseHold baseHold)
        {
            baseHold.scrollDir = 0;
        }

        private void DoBaseHoldVerticalScroll(BaseHold baseHold, bool increase)
        {
            baseHold.offsetPos += VR.Camera.Head.forward * (Time.deltaTime * (increase ? 10f : -10f));
        }

        private Quaternion _left = Quaternion.Euler(0f, 1f, 0f);
        private Quaternion _right = Quaternion.Euler(0f, -1f, 0f);

        private void DoBaseHoldHorizontalScroll(BaseHold baseHold, bool left)
        {
            baseHold.offsetRot *= (left ? _left : _right);
        }

        private void StopAnimChange()
        {
            _animChange = false;
            _animChangeDic.Clear();
        }

        private static readonly Dictionary<PartName, List<string>> _limbColliders = new()
        {
            {
                PartName.HandL, new List<string>()
                {
                    "cf_n_height/cf_j_hips/cf_j_spine01/cf_j_spine02/cf_j_spine03/cf_d_shoulder_L/cf_j_shoulder_L/" +
                    "cf_j_arm00_L/cf_j_forearm01_L/cf_d_forearm02_L/cf_s_forearm02_L/cf_hit_wrist_L",

                    "cf_n_height/cf_j_hips/cf_j_spine01/cf_j_spine02/cf_j_spine03/cf_d_shoulder_L/cf_j_shoulder_L/cf_j_arm00_L/cf_j_forearm01_L/cf_j_hand_L/com_hit_hand_L",
                }
            },
            {
                PartName.HandR, new List<string>()
                {
                    "cf_n_height/cf_j_hips/cf_j_spine01/cf_j_spine02/cf_j_spine03/cf_d_shoulder_R/cf_j_shoulder_R/" +
                    "cf_j_arm00_R/cf_j_forearm01_R/cf_d_forearm02_R/cf_s_forearm02_R/cf_hit_wrist_R",

                    "cf_n_height/cf_j_hips/cf_j_spine01/cf_j_spine02/cf_j_spine03/cf_d_shoulder_R/cf_j_shoulder_R/cf_j_arm00_R/cf_j_forearm01_R/cf_j_hand_R/com_hit_hand_R",
                }
            },
            {
                PartName.FootL, new List<string>()
                {
                    "cf_n_height/cf_j_hips/cf_j_waist01/cf_j_waist02/cf_j_thigh00_L/cf_j_leg01_L/cf_s_leg01_L/cf_hit_leg01_L/aibu_reaction_legL",
                    "cf_n_height/cf_j_hips/cf_j_waist01/cf_j_waist02/cf_j_thigh00_L/cf_j_leg01_L/cf_j_leg03_L/cf_j_foot_L/cf_hit_leg02_L",
                }
            },
            {
                PartName.FootR, new List<string>()
                {
                    "cf_n_height/cf_j_hips/cf_j_waist01/cf_j_waist02/cf_j_thigh00_R/cf_j_leg01_R/cf_s_leg01_R/cf_hit_leg01_R/aibu_reaction_legR",
                    "cf_n_height/cf_j_hips/cf_j_waist01/cf_j_waist02/cf_j_thigh00_R/cf_j_leg01_R/cf_j_leg03_R/cf_j_foot_R/cf_hit_leg02_R",
                }
            }
        };
        
    }
}
