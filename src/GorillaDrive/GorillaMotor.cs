using System.Collections.Generic;
using UnityEngine;

namespace GorillaDrive
{
    /// <summary>Live values of the locomotion sheet (defaults generated in Sheets.g.cs, overridable in config).</summary>
    public class Locomotion
    {
        public float maxArmLength = LocomotionRules.maxArmLengthDefault;
        public float unStickDistance = LocomotionRules.unStickDistanceDefault;
        public float velocityLimit = LocomotionRules.velocityLimitDefault;
        public float maxJumpSpeed = LocomotionRules.maxJumpSpeedDefault;
        public float jumpMultiplier = LocomotionRules.jumpMultiplierDefault;
        public int velocityHistorySize = LocomotionRules.velocityHistorySizeDefault;
        public float handRadius = LocomotionRules.handRadiusDefault;
        public float gravity = LocomotionRules.gravityDefault;
    }

    /// <summary>
    /// Gorilla Tag arm locomotion for a mouse: a gripping hand sticks to the surface it reached, the mouse moves
    /// the virtual hand, and the body follows so the stuck hand stays put. Letting go flings you with your
    /// recent velocity, like Gorilla Tag's GorillaLocomotion.Player.
    /// </summary>
    public class GorillaMotor
    {
        public class Hand
        {
            public Vector3 RestOffset;     // in head space
            public Vector3 Offset;         // virtual hand, head space
            public bool Gripping, Stuck;
            public Vector3 Anchor;         // world, when stuck
            public Vector3 VisualWorld;    // where the hand is drawn
        }

        public readonly Hand Left = new Hand { RestOffset = new Vector3(-0.25f, -0.35f, 0.45f) };
        public readonly Hand Right = new Hand { RestOffset = new Vector3(0.25f, -0.35f, 0.45f) };
        public Locomotion Rules = new Locomotion();
        public float ArmSensitivity = 0.004f;
        public Vector3 AirVelocity;
        public bool Airborne;
        readonly Queue<Vector3> _history = new Queue<Vector3>();
        Vector3 _lastHead;
        bool _wasStuck;

        public GorillaMotor() { Left.Offset = Left.RestOffset; Right.Offset = Right.RestOffset; }

        public void Reset(Vector3 head)
        {
            Left.Gripping = Left.Stuck = Right.Gripping = Right.Stuck = false;
            Left.Offset = Left.RestOffset; Right.Offset = Right.RestOffset;
            _history.Clear(); _lastHead = head; AirVelocity = Vector3.zero; Airborne = false; _wasStuck = false;
        }

        /// <summary>One frame. Returns the world displacement the player's body should make.</summary>
        public Vector3 Step(Transform head, bool leftDown, bool rightDown, Vector2 mouse, float dt, bool grounded, int ignoreLayers)
        {
            var headPos = head.position;
            var rot = head.rotation;
            Grip(Left, leftDown, head, ignoreLayers, -1);
            Grip(Right, rightDown, head, ignoreLayers, 1);

            // Mouse moves whichever hands are gripping (head looks are frozen while gripping).
            var mouseLocal = new Vector3(mouse.x, mouse.y, 0) * ArmSensitivity;
            foreach (var h in new[] { Left, Right })
            {
                if (h.Gripping) h.Offset = Vector3.ClampMagnitude(h.Offset + mouseLocal, Rules.maxArmLength);
                else h.Offset = Vector3.Lerp(h.Offset, h.RestOffset, 1 - Mathf.Exp(-12f * dt));
            }

            Vector3 move = Vector3.zero;
            int stuck = 0;
            foreach (var h in new[] { Left, Right })
            {
                if (!h.Stuck) continue;
                var wanted = h.Anchor - rot * h.Offset; // head position that keeps the stuck hand on its anchor
                var d = wanted - headPos;
                if (d.magnitude > Rules.unStickDistance) { h.Stuck = false; continue; }
                move += d; stuck++;
            }

            if (stuck > 0)
            {
                move /= stuck;
                Airborne = false; AirVelocity = Vector3.zero;
            }
            else
            {
                if (_wasStuck)
                {
                    var v = AverageVelocity();
                    if (v.magnitude > Rules.velocityLimit)
                    {
                        AirVelocity = Vector3.ClampMagnitude(v * Rules.jumpMultiplier, Rules.maxJumpSpeed);
                        Airborne = true;
                    }
                }
                if (Airborne || !grounded) { AirVelocity += Vector3.down * Rules.gravity * dt; Airborne = true; }
                if (grounded && AirVelocity.y <= 0) { Airborne = false; AirVelocity = Vector3.zero; }
                move = AirVelocity * dt;
            }
            _wasStuck = stuck > 0;

            // velocity history (GorillaLocomotion averages the last N frames)
            if (dt > 0) _history.Enqueue((headPos + move - _lastHead) / dt);
            while (_history.Count > Mathf.Max(1, Rules.velocityHistorySize)) _history.Dequeue();
            _lastHead = headPos + move;

            foreach (var h in new[] { Left, Right })
                h.VisualWorld = h.Stuck ? h.Anchor : headPos + move + rot * h.Offset;
            return move;
        }

        void Grip(Hand h, bool down, Transform head, int ignoreLayers, int side)
        {
            if (down && !h.Gripping)
            {
                h.Gripping = true;
                var dir = (head.forward + head.right * 0.25f * side - head.up * 0.15f).normalized;
                if (Physics.SphereCast(head.position, Rules.handRadius, dir, out var hit, Rules.maxArmLength, ~ignoreLayers, QueryTriggerInteraction.Ignore))
                {
                    h.Stuck = true;
                    h.Anchor = hit.point + hit.normal * Rules.handRadius;
                    h.Offset = Quaternion.Inverse(head.rotation) * (h.Anchor - head.position);
                }
                else h.Offset = Quaternion.Inverse(head.rotation) * (dir * Rules.maxArmLength * 0.8f);
            }
            else if (!down && h.Gripping) { h.Gripping = false; h.Stuck = false; }
        }

        Vector3 AverageVelocity()
        {
            if (_history.Count == 0) return Vector3.zero;
            var s = Vector3.zero; foreach (var v in _history) s += v;
            return s / _history.Count;
        }
    }
}
