using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GorillaDrive
{
    /// <summary>
    /// Runtime lookups into The Long Drive (sheet tld_hooks). No patches: each hook finds a live object.
    /// Lookup ids match the sheet rows: "player_camera", "player_root", "player_walk_script", "in_vehicle".
    /// </summary>
    public class TldHooks
    {
        readonly Action<string> _log;
        readonly HashSet<string> _warned = new HashSet<string>();
        public string[] WalkScriptNames = { "fpscontroller" };

        public TldHooks(Action<string> log) { _log = log; }

        void WarnOnce(string id, string msg) { if (_warned.Add(id)) _log($"hook {id}: {msg}"); }

        public Camera PlayerCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                // Not every game tags its camera MainCamera: take the enabled camera drawn last.
                foreach (var c in Camera.allCameras)
                    if (c.enabled && c.targetTexture == null && (cam == null || c.depth > cam.depth)) cam = c;
            }
            if (cam == null) WarnOnce("player_camera", "no camera yet");
            return cam;
        }

        public Transform PlayerRoot(Camera cam)
        {
            if (cam == null) return null;
            // Walk up from the camera to the first object that moves the player.
            for (var t = cam.transform; t != null; t = t.parent)
            {
                if (t.GetComponent<CharacterController>() != null) return t;
                var rb = t.GetComponent<Rigidbody>();
                if (rb != null && !rb.isKinematic && t.GetComponentInChildren<WheelCollider>() == null) return t;
            }
            WarnOnce("player_root", "no CharacterController/Rigidbody above the camera");
            return null;
        }

        public Behaviour[] PlayerWalkScripts(Transform root)
        {
            if (root == null) return new Behaviour[0];
            var found = root.GetComponentsInChildren<MonoBehaviour>(true)
                .Where(b => b != null && WalkScriptNames.Any(n => string.Equals(b.GetType().Name, n, StringComparison.OrdinalIgnoreCase)))
                .Cast<Behaviour>().ToArray();
            if (found.Length == 0)
                WarnOnce("player_walk_script", "none of [" + string.Join(", ", WalkScriptNames) + "] on " + root.name +
                    "; scripts there: " + string.Join(", ", root.GetComponents<MonoBehaviour>().Select(b => b.GetType().Name).ToArray()));
            return found;
        }

        public bool InVehicle(Camera cam, Transform root)
        {
            if (cam == null) return false;
            var cc = root != null ? root.GetComponent<CharacterController>() : null;
            if (cc != null && !cc.enabled) return true;
            for (var t = cam.transform.parent; t != null; t = t.parent)
                if (t.GetComponent<WheelCollider>() != null || t.GetComponentInChildren<WheelCollider>() != null && t != root)
                    return true;
            return false;
        }
    }
}
