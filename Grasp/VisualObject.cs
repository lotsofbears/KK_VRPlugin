﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static KK_VR.Grasp.GraspController;
using UnityEngine;
using KK_VR.Fixes;

namespace KK_VR.Grasp
{
    internal class VisualObject
    {
        internal readonly GameObject gameObject;
        private readonly Renderer _renderer;
        private readonly static List<Color> _colors =
        [
            new(1f, 0f, 0f, 0.2f), // Red
            new(0f, 1f, 0f, 0.2f), // Green
            new(0f, 0f, 1f, 0.2f), // Blue
            new(1f, 1f, 1f, 0.2f)  // Gray
        ];
        internal VisualObject(BodyPart bodyPart)
        {
            gameObject = Util.CreatePrimitive(
                    PrimitiveType.Sphere,
                    GetGuideObjectSize(bodyPart.name),
                    bodyPart.afterIK,
                    _colors[3],
                    removeCollider: false);
            //gameObject.name = "ik_visual";
            gameObject.name = "ik_vl_" + bodyPart.GetLowerCaseName();
            _renderer = gameObject.GetComponent<Renderer>();
            _renderer.enabled = false;
        }
        internal void Show() => _renderer.enabled = true;
        internal void Hide() => _renderer.enabled = false;
        internal void SetColor(bool active)
        {
            _renderer.material.color = active ? _colors[1] : _colors[3];
        }
        private Vector3 GetGuideObjectSize(PartName partName)
        {
            return partName switch
            {
                PartName.ShoulderL or PartName.ShoulderR => new Vector3(0.14f, 0.14f, 0.14f),
                PartName.HandL or PartName.HandR => new Vector3(0.11f, 0.11f, 0.11f),
                _ => new Vector3(0.2f, 0.2f, 0.2f),
            };
        }
    }
}
