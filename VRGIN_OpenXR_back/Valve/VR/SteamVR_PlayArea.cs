using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using VRGIN.Helpers;

namespace Valve.VR
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
    public class SteamVR_PlayArea : MonoBehaviour
    {
        public enum Size
        {
            Calibrated = 0,
            _400x300 = 1,
            _300x225 = 2,
            _200x150 = 3
        }

        public float borderThickness = 0.15f;

        public float wireframeHeight = 2f;

        public bool drawWireframeWhenSelectedOnly;

        public bool drawInGame = true;

        public Size size;

        public Color color = Color.cyan;

        [HideInInspector] public Vector3[] vertices;

        public static bool GetBounds(Size size, ref HmdQuad_t pRect)
        {
            bool flag;
            int num;
            if (size == Size.Calibrated)
            {
                flag = false;
                if (Application.isEditor && !Application.isPlaying) flag = SteamVR.InitializeTemporarySession(false);
                var chaperone = OpenVR.Chaperone;
                if (chaperone != null)
                {
                    num = chaperone.GetPlayAreaRect(ref pRect) ? 1 : 0;
                    if (num != 0) goto IL_003a;
                }
                else
                    num = 0;

                Debug.LogWarning("<b>[SteamVR]</b> Failed to get Calibrated Play Area bounds!  Make sure you have tracking first, and that your space is calibrated.");
                goto IL_003a;
            }

            try
            {
                var array = size.ToString().Substring(1).Split(new char[1] { 'x' }, 2);
                var num2 = float.Parse(array[0]) / 200f;
                var num3 = float.Parse(array[1]) / 200f;
                pRect.vCorners0.v0 = num2;
                pRect.vCorners0.v1 = 0f;
                pRect.vCorners0.v2 = 0f - num3;
                pRect.vCorners1.v0 = 0f - num2;
                pRect.vCorners1.v1 = 0f;
                pRect.vCorners1.v2 = 0f - num3;
                pRect.vCorners2.v0 = 0f - num2;
                pRect.vCorners2.v1 = 0f;
                pRect.vCorners2.v2 = num3;
                pRect.vCorners3.v0 = num2;
                pRect.vCorners3.v1 = 0f;
                pRect.vCorners3.v2 = num3;
                return true;
            }
            catch { }

            return false;
        IL_003a:
            if (flag) SteamVR.ExitTemporarySession();
            return (byte)num != 0;
        }

        public void BuildMesh()
        {
            var pRect = default(HmdQuad_t);
            if (!GetBounds(size, ref pRect)) return;
            var array = new HmdVector3_t[4] { pRect.vCorners0, pRect.vCorners1, pRect.vCorners2, pRect.vCorners3 };
            vertices = new Vector3[array.Length * 2];
            for (var i = 0; i < array.Length; i++)
            {
                var hmdVector3_t = array[i];
                vertices[i] = new Vector3(hmdVector3_t.v0, 0.01f, hmdVector3_t.v2);
            }

            if (borderThickness == 0f)
            {
                GetComponent<MeshFilter>().mesh = null;
                return;
            }

            for (var j = 0; j < array.Length; j++)
            {
                var num = (j + 1) % array.Length;
                var num2 = (j + array.Length - 1) % array.Length;
                var normalized = (vertices[num] - vertices[j]).normalized;
                var normalized2 = (vertices[num2] - vertices[j]).normalized;
                var vector = vertices[j];
                vector += Vector3.Cross(normalized, Vector3.up) * borderThickness;
                vector += Vector3.Cross(normalized2, Vector3.down) * borderThickness;
                vertices[array.Length + j] = vector;
            }

            var triangles = new int[24]
            {
                0, 4, 1, 1, 4, 5, 1, 5, 2, 2,
                5, 6, 2, 6, 3, 3, 6, 7, 3, 7,
                0, 0, 7, 4
            };
            var uv = new Vector2[8]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };
            var colors = new Color[8]
            {
                color,
                color,
                color,
                color,
                new Color(color.r, color.g, color.b, 0f),
                new Color(color.r, color.g, color.b, 0f),
                new Color(color.r, color.g, color.b, 0f),
                new Color(color.r, color.g, color.b, 0f)
            };
            var mesh = new Mesh();
            GetComponent<MeshFilter>().mesh = mesh;
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.colors = colors;
            mesh.triangles = triangles;
            var component = GetComponent<MeshRenderer>();
            component.material = new Material(UnityHelper.GetShaderByMaterial("Sprites-Default"));
            component.reflectionProbeUsage = ReflectionProbeUsage.Off;
            component.shadowCastingMode = ShadowCastingMode.Off;
            component.receiveShadows = false;
            component.lightProbeUsage = LightProbeUsage.Off;
        }

        private void OnDrawGizmos()
        {
            if (!drawWireframeWhenSelectedOnly) DrawWireframe();
        }

        private void OnDrawGizmosSelected()
        {
            if (drawWireframeWhenSelectedOnly) DrawWireframe();
        }

        public void DrawWireframe()
        {
            if (vertices != null && vertices.Length != 0)
            {
                var vector = transform.TransformVector(Vector3.up * wireframeHeight);
                for (var i = 0; i < 4; i++)
                {
                    var num = (i + 1) % 4;
                    var vector2 = transform.TransformPoint(vertices[i]);
                    var vector3 = vector2 + vector;
                    var vector4 = transform.TransformPoint(vertices[num]);
                    var to = vector4 + vector;
                    Gizmos.DrawLine(vector2, vector3);
                    Gizmos.DrawLine(vector2, vector4);
                    Gizmos.DrawLine(vector3, to);
                }
            }
        }

        public void OnEnable()
        {
            if (Application.isPlaying)
            {
                GetComponent<MeshRenderer>().enabled = drawInGame;
                enabled = false;
                if (drawInGame && size == Size.Calibrated) StartCoroutine(UpdateBounds());
            }
        }

        private IEnumerator UpdateBounds()
        {
            GetComponent<MeshFilter>().mesh = null;
            var chaperone = OpenVR.Chaperone;
            if (chaperone != null)
            {
                while (chaperone.GetCalibrationState() != ChaperoneCalibrationState.OK) yield return null;
                BuildMesh();
            }
        }
    }
}
