using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace GorillaDrive
{
    /// <summary>Mesh data read from Gorilla Tag (sheet row gorilla_mesh). Plain arrays, no Unity types.</summary>
    public class GtMeshData
    {
        public string Name;
        public int VertexCount;
        public float[] Positions, Normals, Uv0;   // xyz, xyz, uv
        public int[] Triangles;                   // all submeshes, base vertex applied
        public int[] DominantBone;                // per vertex, -1 when unskinned
        public float[][] BindPoses;               // per bone, 16 floats row-major (e00, e01, ...)
    }

    /// <summary>Texture read from Gorilla Tag (sheet row gorilla_fur).</summary>
    public class GtTexture
    {
        public string Name;
        public int Width, Height, Format, MipCount;
        public byte[] Data;
    }

    /// <summary>The player gorilla rig found in Gorilla Tag (sheet row gorilla_skin).</summary>
    public class GtRig
    {
        public string SourceFile;
        public int Score;
        public float[] Tint; // rgb, from the gorilla material's base colour
        public string GameObjectName;
        public GtMeshData Mesh;
        public GtTexture Fur;
        public string[] BoneNames;
        public int[] BoneParents; // index into BoneNames, -1 for root/outside
    }

    /// <summary>One piece of the gorilla rebuilt for The Long Drive (rows left_hand_part, right_hand_part, body_part).</summary>
    public class GtPart
    {
        public string Id;
        public int Bone = -1;          // bone whose bind space the part is expressed in; -1 = mesh root
        public float[] Positions, Normals, Uv0;
        public int[] Triangles;
    }

    /// <summary>Reads Gorilla Tag's own asset files from the player's install. Runs off the main thread.</summary>
    public class GtReader
    {
        readonly string _gtDir;
        readonly string _classDataPath;
        readonly Action<string> _log;

        public GtReader(string gtDir, string classDataPath, Action<string> log)
        {
            _gtDir = gtDir; _classDataPath = classDataPath; _log = log;
        }

        public static string FindDataDir(string gtDir)
        {
            var named = Path.Combine(gtDir, "Gorilla Tag_Data");
            if (Directory.Exists(named)) return named;
            return Directory.GetDirectories(gtDir, "*_Data").FirstOrDefault();
        }

        IEnumerable<string> CandidateFiles(string dataDir)
        {
            foreach (var f in Directory.GetFiles(dataDir))
            {
                var n = Path.GetFileName(f);
                if (n.StartsWith("level") && !n.Contains(".") || n.EndsWith(".assets")) yield return f;
            }
            var streaming = Path.Combine(dataDir, "StreamingAssets");
            if (Directory.Exists(streaming))
                foreach (var f in Directory.GetFiles(streaming, "*.bundle", SearchOption.AllDirectories)) yield return f;
        }

        int _logged;
        int _assetErrors;
        readonly List<string> _candidates = new List<string>();

        static string Describe(Exception e)
        {
            var frame = (e.StackTrace ?? "").Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) ?? "";
            return e.GetType().Name + ": " + e.Message + " @ " + frame;
        }

        public GtRig FindGorilla()
        {
            var dataDir = FindDataDir(_gtDir);
            if (dataDir == null) { _log("Gorilla Tag data folder not found in " + _gtDir); return null; }

            var am = new AssetsManager();
            am.LoadClassPackage(_classDataPath);
            GtRig best = null;
            try
            {
                foreach (var path in CandidateFiles(dataDir))
                {
                    try
                    {
                        foreach (var inst in Open(am, path))
                        {
                            var md = inst.file.Metadata;
                            if (!md.TypeTreeEnabled)
                                am.LoadClassDatabaseFromPackage(md.UnityVersion);
                            if (_logged++ < 3)
                                _log($"{Path.GetFileName(path)}: Unity {md.UnityVersion}, format {inst.file.Header.Version}, type trees {(md.TypeTreeEnabled ? "yes" : "no (class data)")}, {md.AssetInfos.Count} assets");
                            var rig = ScanFile(am, inst, dataDir, path);
                            if (rig != null && rig.Fur == null) FindFurInFile(am, inst, dataDir, path, rig);
                            if (rig != null && (best == null || rig.Score > best.Score || rig.Score == best.Score && rig.Mesh.VertexCount > best.Mesh.VertexCount))
                                best = rig;
                        }
                    }
                    catch (Exception e) { _log("skip " + Path.GetFileName(path) + ": " + Describe(e)); }
                    finally { am.UnloadAll(); }
                    if (best != null && best.Score >= 70 && (best.Fur != null || best.Tint != null)) break;
                }
            }
            finally { am.UnloadAll(true); }
            _log($"gorilla candidates ({_candidates.Count}, {_assetErrors} unreadable renderers):");
            foreach (var c in _candidates.Take(40)) _log("  " + c);
            return best;
        }

        IEnumerable<AssetsFileInstance> Open(AssetsManager am, string path)
        {
            if (path.EndsWith(".bundle"))
            {
                var b = am.LoadBundleFile(path, true);
                int n = b.file.BlockAndDirInfo.DirectoryInfos.Count;
                for (int i = 0; i < n; i++)
                {
                    AssetsFileInstance inst = null;
                    try { inst = am.LoadAssetsFileFromBundle(b, i, false); } catch { }
                    if (inst != null) yield return inst;
                }
            }
            else yield return am.LoadAssetsFile(path, false);
        }

        static readonly Regex HandBone = new Regex("hand", RegexOptions.IgnoreCase);

        GtRig ScanFile(AssetsManager am, AssetsFileInstance inst, string dataDir, string path)
        {
            GtRig best = null;
            foreach (var info in inst.file.GetAssetsOfType(AssetClassID.SkinnedMeshRenderer))
            {
                try
                {
                    var rig = ReadRenderer(am, inst, info, dataDir, path);
                    if (rig != null && (best == null || rig.Score > best.Score || rig.Score == best.Score && rig.Mesh.VertexCount > best.Mesh.VertexCount))
                        best = rig;
                }
                catch (Exception e)
                {
                    if (_assetErrors++ < 5) _log($"  renderer {info.PathId} in {Path.GetFileName(path)}: {Describe(e)}");
                }
            }
            return best;
        }

        static readonly Regex Cosmetic = new Regex("glove|hat|badge|cosmetic|holdable|face|cape|shirt|pants|fur_?overlay|ragdoll", RegexOptions.IgnoreCase);

        static int Score(string goName, string meshName, string[] bones)
        {
            bool l = bones.Any(n => HandBone.IsMatch(n) && n.IndexOf("L", StringComparison.Ordinal) >= 0);
            bool head = bones.Any(n => n.IndexOf("head", StringComparison.OrdinalIgnoreCase) >= 0);
            int score = 0;
            if (Regex.IsMatch(goName + " " + meshName, "gorilla", RegexOptions.IgnoreCase)) score += 40;
            if (Regex.IsMatch(goName + " " + meshName, "body|player|^gorilla$", RegexOptions.IgnoreCase)) score += 40;
            if (head) score += 20;
            if (l) score += 10;
            if (Cosmetic.IsMatch(goName + " " + meshName)) score -= 100;
            return score;
        }

        GtRig ReadRenderer(AssetsManager am, AssetsFileInstance inst, AssetFileInfo info, string dataDir, string path)
        {
            {
                var smr = am.GetBaseField(inst, info);
                var go = am.GetExtAsset(inst, smr["m_GameObject"]);
                if (go.baseField == null) return null;
                var goName = go.baseField["m_Name"].AsString;

                var bones = smr["m_Bones.Array"].Children;
                var boneNames = new string[bones.Count];
                var boneTransformIds = new long[bones.Count];
                var boneFathers = new long[bones.Count];
                for (int i = 0; i < bones.Count; i++)
                {
                    var tr = am.GetExtAsset(inst, bones[i]);
                    if (tr.baseField == null) { boneNames[i] = ""; boneFathers[i] = -1; continue; }
                    boneTransformIds[i] = tr.info.PathId;
                    boneFathers[i] = tr.baseField["m_Father.m_PathID"].AsLong;
                    var bgo = am.GetExtAsset(tr.file, tr.baseField["m_GameObject"]);
                    boneNames[i] = bgo.baseField?["m_Name"].AsString ?? "";
                }
                if (!boneNames.Any(n => HandBone.IsMatch(n))) return null;

                var meshAsset = am.GetExtAsset(inst, smr["m_Mesh"]);
                if (meshAsset.baseField == null) return null;
                var meshName = meshAsset.baseField["m_Name"].AsString;
                int score = Score(goName, meshName, boneNames);
                _candidates.Add($"score {score}: '{goName}' mesh '{meshName}', {boneNames.Length} bones, in {Path.GetFileName(path)}");
                if (score < 30) return null;
                var mesh = ReadMesh(meshAsset.baseField, dataDir, path, meshAsset.file);
                if (mesh == null) return null;

                var parents = new int[bones.Count];
                for (int i = 0; i < bones.Count; i++)
                    parents[i] = Array.IndexOf(boneTransformIds, boneFathers[i]);

                return new GtRig
                {
                    Score = score,
                    SourceFile = Path.GetFileName(path),
                    GameObjectName = goName,
                    Mesh = mesh,
                    BoneNames = boneNames,
                    BoneParents = parents,
                    Fur = ReadMainTexture(am, inst, smr, dataDir, path),
                };
            }
        }

        static readonly string[] FurMaterials = { "gorilla", "lightfur", "darkfur", "gorilla_body" };

        /// <summary>
        /// Gorilla Tag's player shader takes its fur from an atlas, so the rig's own material often has no plain
        /// texture. Fall back to the game's fur materials in the same file, and keep the body colour as a tint.
        /// </summary>
        void FindFurInFile(AssetsManager am, AssetsFileInstance inst, string dataDir, string path, GtRig rig)
        {
            var mats = new Dictionary<string, AssetTypeValueField>();
            foreach (var info in inst.file.GetAssetsOfType(AssetClassID.Material))
            {
                try
                {
                    var m = am.GetBaseField(inst, info);
                    var name = m["m_Name"].AsString;
                    if (Array.IndexOf(FurMaterials, name) >= 0 && !mats.ContainsKey(name)) mats[name] = m;
                }
                catch { }
            }
            foreach (var name in FurMaterials)
            {
                if (!mats.TryGetValue(name, out var m)) continue;
                if (rig.Tint == null)
                    foreach (var c in m["m_SavedProperties.m_Colors.Array"].Children)
                    {
                        var cn = c["first"].AsString;
                        if (cn != "_BaseColor" && cn != "_Color") continue;
                        rig.Tint = new[] { c["second.r"].AsFloat, c["second.g"].AsFloat, c["second.b"].AsFloat };
                        _log($"fur tint from '{name}' {cn}: {rig.Tint[0]:0.00} {rig.Tint[1]:0.00} {rig.Tint[2]:0.00}");
                        break;
                    }
                if (rig.Fur != null) continue;
                foreach (var pair in m["m_SavedProperties.m_TexEnvs.Array"].Children)
                {
                    var tn = pair["first"].AsString;
                    if (tn != "_MainTex" && tn != "_BaseMap") continue;
                    if (pair["second.m_Texture.m_PathID"].AsLong == 0) continue;
                    try
                    {
                        var tex = am.GetExtAsset(inst, pair["second.m_Texture"]);
                        if (tex.baseField == null || tex.info.TypeId != (int)AssetClassID.Texture2D) continue;
                        var t = ReadTexture(tex.baseField, dataDir, path, tex.file);
                        if (t != null) { rig.Fur = t; _log($"fur texture from material '{name}' {tn}: {t.Name} ({t.Width}x{t.Height}, format {t.Format})"); break; }
                    }
                    catch (Exception e) { _log($"fur '{name}' {tn}: {Describe(e)}"); }
                }
            }
        }

        readonly HashSet<string> _loggedMaterials = new HashSet<string>();

        GtTexture ReadMainTexture(AssetsManager am, AssetsFileInstance inst, AssetTypeValueField smr, string dataDir, string path)
        {
            var preferred = new[] { "_MainTex", "_BaseMap", "_BaseColorMap", "_Albedo", "_MainTexture" };
            foreach (var pass in new[] { 0, 1 })
            foreach (var matPtr in smr["m_Materials.Array"].Children)
            {
                var mat = am.GetExtAsset(inst, matPtr);
                if (mat.baseField == null) continue;
                var pairs = mat.baseField["m_SavedProperties.m_TexEnvs.Array"].Children;
                var matName = mat.baseField["m_Name"].AsString;
                if (pass == 0 && _loggedMaterials.Add(matName) && _loggedMaterials.Count <= 12)
                    _log($"material '{matName}' textures: " + string.Join(", ", pairs.Select(p => p["first"].AsString).ToArray()));
                foreach (var pair in pairs)
                {
                    var name = pair["first"].AsString;
                    if (pass == 0 && Array.IndexOf(preferred, name) < 0) continue;
                    if (pass == 1 && Regex.IsMatch(name, "normal|bump|mask|emission|occlusion|detail|metallic|reflect|depth|gradient|weather|lightmap|deform|mouth|atlas|parallax|spec", RegexOptions.IgnoreCase)) continue;
                    if (pair["second.m_Texture.m_PathID"].AsLong == 0) continue;
                    var tex = am.GetExtAsset(mat.file, pair["second.m_Texture"]);
                    if (tex.baseField == null || tex.info.TypeId != (int)AssetClassID.Texture2D) continue;
                    var t = ReadTexture(tex.baseField, dataDir, path, tex.file);
                    if (t != null) { _log($"fur texture: {name} = {t.Name} ({t.Width}x{t.Height}, format {t.Format})"); return t; }
                }
            }
            return null;
        }

        static byte[] Bytes(AssetTypeValueField f)
        {
            if (f == null || f.IsDummy) return null;
            if (f.Value != null && f.Value.ValueType == AssetValueType.ByteArray) return f.AsByteArray;
            var arr = f["Array"];
            if (!arr.IsDummy)
            {
                if (arr.Value != null && arr.Value.ValueType == AssetValueType.ByteArray) return arr.AsByteArray;
                return arr.Children.Select(c => (byte)c.AsUInt).ToArray();
            }
            return f.AsByteArray;
        }

        byte[] ReadStream(AssetTypeValueField streamData, string dataDir, string assetsPath, AssetsFileInstance owner)
        {
            if (streamData.IsDummy) return null;
            var p = streamData["path"].AsString;
            if (string.IsNullOrEmpty(p)) return null;
            long offset = streamData["offset"].AsLong;
            int size = (int)streamData["size"].AsUInt;
            if (size == 0) return null;
            string file;
            if (p.StartsWith("archive:/"))
            {
                // The data sits in a .resS entry of the bundle the asset came from.
                var bun = owner?.parentBundle;
                if (bun == null) return null;
                int idx = bun.file.GetFileIndex(p.Substring(p.LastIndexOf('/') + 1));
                if (idx < 0) return null;
                bun.file.GetFileRange(idx, out long entryOffset, out long entryLength);
                if (offset + size > entryLength) return null;
                lock (bun.file.DataReader)
                {
                    bun.file.DataReader.Position = entryOffset + offset;
                    return bun.file.DataReader.ReadBytes(size);
                }
            }
            file = Path.Combine(dataDir, p);
            if (!File.Exists(file)) file = Path.Combine(Path.GetDirectoryName(assetsPath), Path.GetFileName(p));
            using (var fs = File.OpenRead(file))
            {
                fs.Seek(offset, SeekOrigin.Begin);
                var buf = new byte[size];
                int read = 0;
                while (read < size) { int n = fs.Read(buf, read, size - read); if (n <= 0) break; read += n; }
                return buf;
            }
        }

        GtMeshData ReadMesh(AssetTypeValueField m, string dataDir, string path, AssetsFileInstance owner)
        {
            var vd = m["m_VertexData"];
            int vertexCount = (int)vd["m_VertexCount"].AsUInt;
            var channels = vd["m_Channels.Array"].Children.Select(c => new MeshDecode.Channel
            {
                Stream = c["stream"].AsInt, Offset = c["offset"].AsInt,
                Format = c["format"].AsInt, Dimension = c["dimension"].AsInt,
            }).ToList();
            var data = Bytes(vd["m_DataSize"]);
            if (data == null || data.Length == 0) data = ReadStream(m["m_StreamData"], dataDir, path, owner);
            if (data == null || vertexCount == 0) { _log("mesh " + m["m_Name"].AsString + " has no readable vertex data (compressed?)"); return null; }

            var mesh = new GtMeshData
            {
                Name = m["m_Name"].AsString,
                VertexCount = vertexCount,
                Positions = MeshDecode.ReadChannel(data, channels, 0, vertexCount),
                Normals = MeshDecode.ReadChannel(data, channels, 1, vertexCount),
                Uv0 = MeshDecode.ReadChannel(data, channels, 4, vertexCount),
            };
            var weights = MeshDecode.ReadChannel(data, channels, 12, vertexCount);
            var indices = MeshDecode.ReadChannel(data, channels, 13, vertexCount);
            mesh.DominantBone = new int[vertexCount];
            int wd = channels.Count > 12 ? channels[12].Dimension & 0xF : 0;
            int id = channels.Count > 13 ? channels[13].Dimension & 0xF : 0;
            for (int v = 0; v < vertexCount; v++)
            {
                mesh.DominantBone[v] = -1;
                if (indices == null) continue;
                int bestK = 0; float bestW = -1;
                for (int k = 0; k < id; k++)
                {
                    float w = weights != null && k < wd ? weights[v * wd + k] : (k == 0 ? 1 : 0);
                    if (weights != null && k == wd && wd < id) w = 1 - Enumerable.Range(0, wd).Sum(j => weights[v * wd + j]);
                    if (w > bestW) { bestW = w; bestK = k; }
                }
                mesh.DominantBone[v] = (int)indices[v * id + bestK];
            }

            var ib = Bytes(m["m_IndexBuffer"]);
            bool is32 = m["m_IndexFormat"].IsDummy ? false : m["m_IndexFormat"].AsInt == 1;
            var tris = new List<int>();
            foreach (var sm in m["m_SubMeshes.Array"].Children)
            {
                if (sm["topology"].AsInt != 0) continue; // triangles only
                var idx = MeshDecode.ReadIndices(ib, is32, (int)sm["firstByte"].AsUInt, (int)sm["indexCount"].AsUInt);
                int baseVertex = (int)sm["baseVertex"].AsUInt;
                foreach (var i in idx) tris.Add(i + baseVertex);
            }
            mesh.Triangles = tris.ToArray();

            mesh.BindPoses = m["m_BindPose.Array"].Children.Select(bp =>
            {
                var a = new float[16];
                for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++) a[r * 4 + c] = bp["e" + r + c].AsFloat;
                return a;
            }).ToArray();
            return mesh;
        }

        GtTexture ReadTexture(AssetTypeValueField t, string dataDir, string path, AssetsFileInstance owner)
        {
            var tex = new GtTexture
            {
                Name = t["m_Name"].AsString,
                Width = t["m_Width"].AsInt,
                Height = t["m_Height"].AsInt,
                Format = t["m_TextureFormat"].AsInt,
                MipCount = t["m_MipCount"].IsDummy ? 1 : t["m_MipCount"].AsInt,
            };
            tex.Data = Bytes(t["image data"]);
            if (tex.Data == null || tex.Data.Length == 0) tex.Data = ReadStream(t["m_StreamData"], dataDir, path, owner);
            if (tex.Data == null)
            {
                var sd = t["m_StreamData"];
                _log($"texture {tex.Name} data not readable (format {tex.Format}, stream '{(sd.IsDummy ? "-" : sd["path"].AsString)}' size {(sd.IsDummy ? 0 : sd["size"].AsUInt)})");
                return null;
            }
            return tex;
        }

        /// <summary>Splits the rig into hands + body (rows left_hand_part, right_hand_part, body_part).</summary>
        public static GtPart[] SplitParts(GtRig rig, Action<string> log)
        {
            int Find(Regex side) => Array.FindIndex(rig.BoneNames, n => HandBone.IsMatch(n) && side.IsMatch(n));
            int left = Find(new Regex(@"(\.l\b|_l\b|left|\bl_)", RegexOptions.IgnoreCase));
            int right = Find(new Regex(@"(\.r\b|_r\b|right|\br_)", RegexOptions.IgnoreCase));
            log($"hand bones: left={(left >= 0 ? rig.BoneNames[left] : "none")} right={(right >= 0 ? rig.BoneNames[right] : "none")}");

            int n = rig.BoneNames.Length;
            var partOfBone = new int[n];
            for (int b = 0; b < n; b++)
            {
                for (int a = b; a >= 0; a = rig.BoneParents[a])
                {
                    if (a == left) { partOfBone[b] = 1; break; }
                    if (a == right) { partOfBone[b] = 2; break; }
                    if (rig.BoneParents[a] == a) break;
                }
            }
            var split = MeshDecode.SplitByBone(rig.Mesh.Triangles, rig.Mesh.DominantBone, partOfBone, 3);
            var ids = new[] { "body_part", "left_hand_part", "right_hand_part" };
            var boneOf = new[] { -1, left, right };
            var parts = new GtPart[3];
            for (int p = 0; p < 3; p++)
                parts[p] = Compact(ids[p], split[p], rig.Mesh, boneOf[p] >= 0 && boneOf[p] < rig.Mesh.BindPoses.Length ? boneOf[p] : -1);
            return parts;
        }

        static GtPart Compact(string id, List<int> tris, GtMeshData m, int bone)
        {
            var map = new Dictionary<int, int>();
            var pos = new List<float>(); var nor = new List<float>(); var uv = new List<float>();
            var outTris = new int[tris.Count];
            float[] bp = bone >= 0 ? m.BindPoses[bone] : null;
            for (int i = 0; i < tris.Count; i++)
            {
                int v = tris[i];
                if (!map.TryGetValue(v, out var nv))
                {
                    nv = map.Count; map[v] = nv;
                    float x = m.Positions[v * 3], y = m.Positions[v * 3 + 1], z = m.Positions[v * 3 + 2];
                    float nx = 0, ny = 1, nz = 0;
                    if (m.Normals != null) { nx = m.Normals[v * 3]; ny = m.Normals[v * 3 + 1]; nz = m.Normals[v * 3 + 2]; }
                    if (bp != null)
                    {
                        // bind pose maps mesh space -> bone space
                        float tx = bp[0] * x + bp[1] * y + bp[2] * z + bp[3];
                        float ty = bp[4] * x + bp[5] * y + bp[6] * z + bp[7];
                        float tz = bp[8] * x + bp[9] * y + bp[10] * z + bp[11];
                        float tnx = bp[0] * nx + bp[1] * ny + bp[2] * nz;
                        float tny = bp[4] * nx + bp[5] * ny + bp[6] * nz;
                        float tnz = bp[8] * nx + bp[9] * ny + bp[10] * nz;
                        x = tx; y = ty; z = tz; nx = tnx; ny = tny; nz = tnz;
                    }
                    pos.Add(x); pos.Add(y); pos.Add(z);
                    nor.Add(nx); nor.Add(ny); nor.Add(nz);
                    if (m.Uv0 != null) { uv.Add(m.Uv0[v * 2]); uv.Add(m.Uv0[v * 2 + 1]); } else { uv.Add(0); uv.Add(0); }
                }
                outTris[i] = nv;
            }
            return new GtPart { Id = id, Bone = bone, Positions = pos.ToArray(), Normals = nor.ToArray(), Uv0 = uv.ToArray(), Triangles = outTris };
        }
    }
}
