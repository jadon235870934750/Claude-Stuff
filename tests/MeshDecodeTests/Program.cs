using System;
using System.Collections.Generic;
using System.IO;
using GorillaDrive;

// Builds a two-stream vertex buffer the way Unity lays it out and checks it decodes back.
int fails = 0;
void Check(bool ok, string what) { Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

var ch = new List<MeshDecode.Channel>();
for (int i = 0; i < 14; i++) ch.Add(new MeshDecode.Channel());
ch[0] = new MeshDecode.Channel { Stream = 0, Offset = 0, Format = 0, Dimension = 3 };   // position f32x3
ch[1] = new MeshDecode.Channel { Stream = 0, Offset = 12, Format = 0, Dimension = 3 };  // normal f32x3
ch[4] = new MeshDecode.Channel { Stream = 1, Offset = 0, Format = 1, Dimension = 2 };   // uv0 f16x2
ch[12] = new MeshDecode.Channel { Stream = 2, Offset = 0, Format = 0, Dimension = 4 };  // weights
ch[13] = new MeshDecode.Channel { Stream = 2, Offset = 16, Format = 6, Dimension = 4 }; // indices u8x4
int vc = 3;
var ms = new MemoryStream(); var w = new BinaryWriter(ms);
for (int v = 0; v < vc; v++) { w.Write(v + 0.5f); w.Write(1f); w.Write(-2f); w.Write(0f); w.Write(1f); w.Write(0f); }
while (ms.Length % 16 != 0) w.Write((byte)0);
ushort Half(float f) => (ushort)BitConverter.HalfToInt16Bits((Half)f);
for (int v = 0; v < vc; v++) { w.Write(Half(0.25f * v)); w.Write(Half(0.75f)); }
while (ms.Length % 16 != 0) w.Write((byte)0);
for (int v = 0; v < vc; v++) { w.Write(0.2f); w.Write(0.8f); w.Write(0f); w.Write(0f); w.Write((byte)3); w.Write((byte)(7 + v)); w.Write((byte)0); w.Write((byte)0); }
var data = ms.ToArray();

var pos = MeshDecode.ReadChannel(data, ch, 0, vc);
Check(pos[3] == 1.5f && pos[4] == 1f && pos[5] == -2f, "positions decode across stream 0");
var nor = MeshDecode.ReadChannel(data, ch, 1, vc);
Check(nor[7] == 1f, "normals decode");
var uv = MeshDecode.ReadChannel(data, ch, 4, vc);
Check(Math.Abs(uv[4] - 0.5f) < 1e-3 && Math.Abs(uv[5] - 0.75f) < 1e-3, "half-float uv decode in 16-byte aligned stream 1");
var idx = MeshDecode.ReadChannel(data, ch, 13, vc);
Check(idx[4] == 3 && idx[5] == 8, "bone indices decode in stream 2");
Check(MeshDecode.ReadChannel(data, ch, 2, vc) == null, "absent tangent channel returns null");
Check(MeshDecode.HalfToFloat(0x3C00) == 1f && MeshDecode.HalfToFloat(0xC000) == -2f, "half to float");
var tris = MeshDecode.ReadIndices(new byte[] { 0, 0, 1, 0, 2, 0 }, false, 0, 3);
Check(tris[2] == 2, "16-bit indices");
var parts = MeshDecode.SplitByBone(new[] { 0, 1, 2, 2, 1, 0 }, new[] { 5, 9, 9 }, new[] { 0, 0, 0, 0, 0, 1, 0, 0, 0, 2 }, 3);
Check(parts[1].Count == 3 && parts[2].Count == 3, "triangles split by dominant bone");
Console.WriteLine(fails == 0 ? "ALL PASS" : fails + " FAILED");
return fails;
