using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace GorillaDrive
{
    /// <summary>Turns parts read from Gorilla Tag into Unity objects inside The Long Drive (main thread only).</summary>
    public static class GorillaBuilder
    {
        static readonly Color FallbackFur = new Color(0x5a / 255f, 0x3a / 255f, 0x22 / 255f);

        public static Mesh BuildMesh(GtPart p)
        {
            var mesh = new Mesh { name = "GT_" + p.Id };
            int vc = p.Positions.Length / 3;
            if (vc > 65000) mesh.indexFormat = IndexFormat.UInt32;
            var v = new Vector3[vc]; var n = new Vector3[vc]; var uv = new Vector2[vc];
            for (int i = 0; i < vc; i++)
            {
                v[i] = new Vector3(p.Positions[i * 3], p.Positions[i * 3 + 1], p.Positions[i * 3 + 2]);
                n[i] = new Vector3(p.Normals[i * 3], p.Normals[i * 3 + 1], p.Normals[i * 3 + 2]);
                uv[i] = new Vector2(p.Uv0[i * 2], p.Uv0[i * 2 + 1]);
            }
            mesh.vertices = v; mesh.normals = n; mesh.uv = uv;
            mesh.triangles = p.Triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Material BuildMaterial(GtTexture fur, float[] tint, Action<string> log)
        {
            var shader = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse") ?? Shader.Find("Unlit/Texture");
            var mat = new Material(shader) { name = "GT_fur", color = Color.white };
            var tex = BuildTexture(fur, log);
            if (tex != null) mat.mainTexture = tex;
            else mat.color = tint != null ? new Color(tint[0], tint[1], tint[2]) : FallbackFur;
            return mat;
        }

        static Texture2D BuildTexture(GtTexture t, Action<string> log)
        {
            if (t == null) return null;
            try
            {
                var tex = new Texture2D(t.Width, t.Height, (TextureFormat)t.Format, t.MipCount > 1) { name = "GT_" + t.Name };
                tex.LoadRawTextureData(t.Data);
                tex.Apply(false, true);
                return tex;
            }
            catch (Exception e)
            {
                log($"fur texture {t.Name} (format {t.Format}) not loadable: {e.Message}; using flat fur colour");
                return null;
            }
        }

        public static GameObject Spawn(string name, Mesh mesh, Material mat, Transform parent, ShadowCastingMode shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows;
            return go;
        }

        /// <summary>Fallback hand when the rig's hand bones were not found (sheet fallback_if_missing).</summary>
        public static GameObject FallbackHand(string name, Material mat, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            UnityEngine.Object.Destroy(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * 0.12f;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }
    }
}
