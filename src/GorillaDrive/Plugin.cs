using System;
using System.IO;
using System.Linq;
using System.Threading;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.Rendering;

namespace GorillaDrive
{
    [BepInPlugin(Guid, "Gorilla Drive", Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "gg.melty.gorilladrive";
        public const string Version = "0.1.0";

        // controls sheet
        ConfigEntry<KeyCode> _toggle, _leftGrip, _rightGrip, _thirdPerson;
        ConfigEntry<float> _armSensitivity, _handScale, _mouseLook;
        ConfigEntry<string> _gtFolder, _walkScripts;
        ConfigEntry<bool> _startAsGorilla;

        TldHooks _hooks;
        readonly GorillaMotor _motor = new GorillaMotor();
        GtRig _rig; GtPart[] _parts; string _loadError;
        volatile bool _loaded;
        bool _built, _gorilla, _thirdPersonOn;
        Material _fur;
        Mesh _bodyMesh;
        GameObject _leftHand, _rightHand, _body;
        Camera _cam; Transform _root; Behaviour[] _walk = new Behaviour[0];
        float _pitch;
        Vector3 _camLocalHome;
        bool _haveCamHome;

        void Log(string m) => Logger.LogInfo(m);

        void Awake()
        {
            _toggle = Config.Bind("Controls", "toggle_gorilla", KeyCode.G, ControlText("toggle_gorilla"));
            _leftGrip = Config.Bind("Controls", "left_grip", KeyCode.Mouse0, ControlText("left_grip"));
            _rightGrip = Config.Bind("Controls", "right_grip", KeyCode.Mouse1, ControlText("right_grip"));
            _thirdPerson = Config.Bind("Controls", "third_person", KeyCode.V, ControlText("third_person"));
            _armSensitivity = Config.Bind("Controls", "mouse_arm_sensitivity", 0.004f, ControlText("mouse_arm_sensitivity"));
            _mouseLook = Config.Bind("Controls", "MouseLookSpeed", 2f, "Mouse look speed while in gorilla mode.");
            _handScale = Config.Bind("Gorilla", "HandScale", 1f, "Scale of the Gorilla Tag hands in view.");
            _startAsGorilla = Config.Bind("Gorilla", "StartAsGorilla", true, "Be a gorilla as soon as you are on foot.");
            _gtFolder = Config.Bind("Paths", "GorillaTagFolder", "", "Your Gorilla Tag folder. Empty = found automatically (Melty passes it).");
            _walkScripts = Config.Bind("Hooks", "WalkScriptNames", "fpscontroller", "The Long Drive scripts paused while you're a gorilla (comma-separated type names).");

            var loco = _motor.Rules;
            foreach (var row in LocomotionRules.All)
            {
                var e = Config.Bind("GorillaTagRules", row.id, row.value, row.meaning);
                var f = typeof(Locomotion).GetField(row.id);
                if (f.FieldType == typeof(int)) f.SetValue(loco, (int)e.Value); else f.SetValue(loco, e.Value);
            }
            _motor.ArmSensitivity = _armSensitivity.Value;

            _hooks = new TldHooks(Log) { WalkScriptNames = _walkScripts.Value.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray() };
            _gorilla = _startAsGorilla.Value;

            var gtDir = GtLocator.Find(_gtFolder.Value, Log);
            if (gtDir == null) { _loadError = "Gorilla Tag folder not found"; return; }
            Log("Reading the gorilla from your Gorilla Tag install: " + gtDir);
            var classData = Path.Combine(Path.GetDirectoryName(Info.Location), "classdata.tpk");
            new Thread(() =>
            {
                try
                {
                    var rig = new GtReader(gtDir, classData, Log).FindGorilla();
                    if (rig == null) { _loadError = "gorilla rig not found"; Log("GorillaDrive: gorilla rig not found"); return; }
                    Log($"Found gorilla '{rig.GameObjectName}' in {rig.SourceFile}: mesh {rig.Mesh.Name}, {rig.Mesh.VertexCount} vertices, {rig.BoneNames.Length} bones, fur {(rig.Fur != null ? rig.Fur.Name : "none")}");
                    _parts = GtReader.SplitParts(rig, Log);
                    _rig = rig;
                }
                catch (Exception e) { _loadError = e.Message; Log("GorillaDrive: reading Gorilla Tag failed: " + e); }
                finally { _loaded = true; }
            }) { IsBackground = true, Name = "GorillaDrive.GtReader" }.Start();
        }

        static string ControlText(string id) => ControlRows.All.First(r => r.id == id).action;

        void Build()
        {
            _built = true;
            _fur = GorillaBuilder.BuildMaterial(_rig?.Fur, Log);
            if (_parts != null)
            {
                var body = _parts.First(p => p.Id == "body_part");
                if (body.Triangles.Length > 0) _bodyMesh = GorillaBuilder.BuildMesh(body);
            }
            Log("Gorilla built" + (_rig == null ? " (fallback: " + _loadError + ")" : ""));
        }

        GameObject MakeHand(string id, Transform parent)
        {
            var part = _parts?.FirstOrDefault(p => p.Id == id);
            var holder = new GameObject("GorillaDrive_" + id);
            holder.transform.SetParent(parent, false);
            if (part != null && part.Triangles.Length > 0)
            {
                var mesh = GorillaBuilder.BuildMesh(part);
                var g = GorillaBuilder.Spawn("mesh", mesh, _fur, holder.transform, ShadowCastingMode.On);
                // Part vertices are in the hand bone's space; centre them on the holder.
                g.transform.localPosition = -mesh.bounds.center;
            }
            else GorillaBuilder.FallbackHand("mesh", _fur, holder.transform);
            holder.transform.localScale = Vector3.one * _handScale.Value;
            return holder;
        }

        void Update()
        {
            if (!_loaded && _loadError == null) return;
            if (!_built) Build();

            var cam = _hooks.PlayerCamera();
            if (cam != _cam) Attach(cam);
            if (_cam == null) return;

            if (Input.GetKeyDown(_toggle.Value)) SetGorilla(!_gorilla);
            if (Input.GetKeyDown(_thirdPerson.Value)) _thirdPersonOn = !_thirdPersonOn;

            bool seated = _hooks.InVehicle(_cam, _root);
            bool active = _gorilla && !seated && _root != null;
            foreach (var w in _walk) if (w != null) w.enabled = !active;
            if (_leftHand != null) _leftHand.SetActive(_gorilla);
            if (_rightHand != null) _rightHand.SetActive(_gorilla);
            if (_body != null) _body.SetActive(_gorilla && !seated);

            if (!active) { _motor.Reset(_cam.transform.position); RestoreCamera(); PlaceHandsIdle(); return; }

            bool l = Input.GetKey(_leftGrip.Value), r = Input.GetKey(_rightGrip.Value);
            var mouse = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 20f;
            if (!l && !r) Look(mouse);

            var cc = _root.GetComponent<CharacterController>();
            var rb = _root.GetComponent<Rigidbody>();
            bool grounded = cc != null ? cc.isGrounded : Physics.Raycast(_root.position + Vector3.up * 0.1f, Vector3.down, 0.25f);
            int playerLayer = 1 << _root.gameObject.layer;
            var move = _motor.Step(_cam.transform, l, r, l || r ? mouse : Vector2.zero, Time.deltaTime, grounded, playerLayer);

            if (cc != null) cc.Move(move);
            else if (rb != null) rb.MovePosition(rb.position + move);
            else _root.position += move;

            PlaceHands();
            ThirdPerson();
        }

        void Attach(Camera cam)
        {
            RestoreCamera();
            _cam = cam; _haveCamHome = false;
            if (cam == null) return;
            _root = _hooks.PlayerRoot(cam);
            _walk = _hooks.PlayerWalkScripts(_root);
            _pitch = cam.transform.localEulerAngles.x;
            if (_leftHand != null) Destroy(_leftHand);
            if (_rightHand != null) Destroy(_rightHand);
            if (_body != null) Destroy(_body);
            _leftHand = MakeHand("left_hand_part", null);
            _rightHand = MakeHand("right_hand_part", null);
            if (_bodyMesh != null && _root != null)
                _body = GorillaBuilder.Spawn("GorillaDrive_body", _bodyMesh, _fur, _root, ShadowCastingMode.ShadowsOnly);
            Log("Attached to player: camera " + cam.name + ", root " + (_root != null ? _root.name : "none") +
                ", walk scripts paused while gorilla: " + string.Join(", ", _walk.Select(w => w.GetType().Name).ToArray()));
            _motor.Reset(cam.transform.position);
        }

        void SetGorilla(bool on)
        {
            _gorilla = on;
            Log(on ? "Gorilla mode on" : "Gorilla mode off");
        }

        void Look(Vector2 mouse)
        {
            float s = _mouseLook.Value / 20f;
            _root.Rotate(0, mouse.x * s, 0, Space.World);
            _pitch = Mathf.Clamp(Mathf.DeltaAngle(0, _pitch) - mouse.y * s, -85, 85);
            var e = _cam.transform.localEulerAngles;
            _cam.transform.localEulerAngles = new Vector3(_pitch, e.y, e.z);
        }

        void PlaceHands()
        {
            var rot = _cam.transform.rotation;
            _leftHand.transform.SetPositionAndRotation(_motor.Left.VisualWorld, rot);
            _rightHand.transform.SetPositionAndRotation(_motor.Right.VisualWorld, rot);
        }

        void PlaceHandsIdle()
        {
            if (_leftHand == null || _cam == null) return;
            var t = _cam.transform;
            _leftHand.transform.SetPositionAndRotation(t.TransformPoint(_motor.Left.RestOffset), t.rotation);
            _rightHand.transform.SetPositionAndRotation(t.TransformPoint(_motor.Right.RestOffset), t.rotation);
        }

        void ThirdPerson()
        {
            if (_body != null)
            {
                var r = _body.GetComponent<Renderer>();
                r.shadowCastingMode = _thirdPersonOn ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly;
            }
            if (!_haveCamHome) { _camLocalHome = _cam.transform.localPosition; _haveCamHome = true; }
            _cam.transform.localPosition = _thirdPersonOn ? _camLocalHome + new Vector3(0, 0.6f, -2.5f) : _camLocalHome;
        }

        void RestoreCamera()
        {
            if (_cam != null && _haveCamHome) _cam.transform.localPosition = _camLocalHome;
        }
    }
}
