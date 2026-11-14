using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE SWORD ATTACKS, APPLIED TO THE RIG (see <see cref="SwordStrikes"/> for what they are and <see cref="SwordPoseDriver"/> for their timing).
    /// After the Animator, each frame: the HIPS are turned first (<see cref="ApplySwordHips"/>, before the foot IK, so the legs are re-solved to the
    /// planted feet under them), then the torso is turned the rest of the way, leaned and tilted into the cut; the sword arm is solved (two-bone IK)
    /// to put the wrist where the frame says; the hand is turned so the BLADE points the way the frame says with its EDGE leading, which is what
    /// makes a cut read as a cut and not a wave; the twist that needs goes into the forearm, not the wrist (<see cref="WristTwist"/>); with a
    /// one-handed sword the free hand is placed too; with a two-handed one the support hand follows the grip as it always has (ApplySupportHand
    /// runs right after). The hips' drop and the stance are handed to the foot IK, which bends the legs and steps the feet (SwordFootwork).
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
        private SwordFrame _swordFrame;
        private Transform _posedSword;
        private ISwordBlade _posedBlade;

        /// <summary>The sword's hold in the hand as it was when adjusting it in the editor began (WeaponGripSettings.HoldBeingEdited).</summary>
        private bool _holdEditFrozen;

        private Vector3 _frozenBladeInHand;

        private Vector3 _frozenEdgeInHand;

        /// <summary>The sword wrist's and the support wrist's twists last frame (WristTwist's continuity: no forearm flip through the half turn).</summary>
        private float _swordWristTwist;

        private float _supportWristTwist;

        /// <summary>The blade's tip is kept at least this far above the ground under the body, metres: a low follow-through pitches up rather than through the floor.</summary>
        public const float TipFloorClearance = 0.06f;

        /// <summary>
        /// In a one-handed held stance the wrist is tipped this far toward the little finger, degrees, and the forearm comes into the hand at
        /// that angle: a fist holding a sword up and forward cocks the wrist so the point rises while the forearm stays level and the elbow low
        /// at the side. With the forearm run straight along the knuckles instead, the elbow went up by the face (owner, 2026-10-17, of the stance
        /// after a swing: "its like bent at the wrong part of the arm").
        /// </summary>
        public const float HeldWristCockDegrees = 35.0f;

        /// <summary>
        /// The most a one-handed sword hand bends from its forearm, degrees (a wrist goes about 70 one way, less two ways at once). Past it the
        /// hand rolls round the handle (<see cref="WithinAWristsBend"/>).
        /// </summary>
        public const float MostWristBendDegrees = 55.0f;


        /// <summary>The tries at rolling the hand round the handle, from none to the whole roll that bends the wrist least.</summary>
        private const int WristRollSteps = 10;

        /// <summary>
        /// A one-handed sword hand turned <paramref name="wanted"/>, rolled round the blade's own line (the blade keeps pointing the same way)
        /// as little as brings the wrist within <see cref="MostWristBendDegrees"/> of the <paramref name="forearm"/>. The poses keep the edge
        /// leading the blade's motion, and where the blade swings on round behind after a hit (the second cut's follow-through) that turned the
        /// knuckles back against a forearm reaching forward: the hand folded 154 degrees back on the wrist (owner, 2026-11-14: "make it more
        /// human like animation the arm bends strangly"). A wrist gives instead, by at most <paramref name="mostRollDegrees"/>: while the blade
        /// can hit only a little, so the edge still leads (owner, 2026-10-16); outside it as far as it needs (<see cref="SwordStrikes.WristRollDegrees"/>).
        /// </summary>
        private static Quaternion WithinAWristsBend(Quaternion wanted, Vector3 forearm, Vector3 blade, float mostRollDegrees)
        {
            Vector3 fingers = wanted * Vector3.right;   // the hand bone's +X runs to the knuckles
            if (Vector3.Angle(forearm, fingers) <= MostWristBendDegrees)
            {
                return wanted;
            }
            Vector3 fromFingers = Vector3.ProjectOnPlane(fingers, blade), toForearm = Vector3.ProjectOnPlane(forearm, blade);
            if (fromFingers.sqrMagnitude < 1e-6f || toForearm.sqrMagnitude < 1e-6f)
            {
                return wanted;
            }
            float roll = Mathf.Clamp(Vector3.SignedAngle(fromFingers, toForearm, blade), -mostRollDegrees, mostRollDegrees);
            Quaternion best = wanted;
            for (int step = 1; step <= WristRollSteps; step++)
            {
                best = Quaternion.AngleAxis(roll * step / WristRollSteps, blade) * wanted;
                if (Vector3.Angle(forearm, best * Vector3.right) <= MostWristBendDegrees)
                {
                    break;
                }
            }
            return best;
        }

        /// <summary>
        /// How far a one-handed held stance's hand gives way, 0 to 1, toward where an arm with that cocked wrist can actually put it: the stance
        /// keys place the hand without the arm's lengths in mind, and pinned there the elbow could not get low enough for the wrist to stay at
        /// HeldWristCockDegrees (it was bent 57). The blade still points where the stance says; only the hand moves, a few centimetres.
        /// </summary>
        public const float HeldHandYield = 1.0f;

        /// <summary>How much of the left arm's full length a two-handed grip may ask of it (a straight arm locks and pops; a hair short of it reads as reaching).</summary>
        public const float SupportReachShare = 0.97f;

        public void SetSwordPose(in SwordFrame frame)
        {
            _swordFrame = frame;
        }

        public void ClearSwordPose()
        {
            _swordFrame = default;
        }

        /// <summary>True while a sword pose (an attack, a guard, the ready stance or the carry) is being applied.</summary>
        public bool SwordPoseActive => _swordFrame.Active && _swordFrame.Weight > 0.001f;

        /// <summary>The sword pose being applied this frame (for tests and captures).</summary>
        public SwordFrame CurrentSwordFrame => _swordFrame;

        /// <summary>How far the hips are sunk by the pose this frame (the sword's or the fists'), metres: the foot IK takes it off the root with the legs kept planted.</summary>
        private float PoseHipDrop => (_swordFrame.Active ? Mathf.Max(0.0f, _swordFrame.HipDrop) * Mathf.Clamp01(_swordFrame.Weight) : 0.0f) + FistHipDrop;

        /// <summary>How much the feet should stand in the sword's stance and step (SwordFootwork) this frame, 0 to 1.</summary>
        private float SwordFootworkWanted => _swordFrame.Active ? Mathf.Clamp01(_swordFrame.Footwork) * Mathf.Clamp01(_swordFrame.Weight) : 0.0f;

        /// <summary>The stance the footwork stands in this frame: the sword's while it has one, else the fists'.</summary>
        private void StanceWanted(out float footwork, out float lead, out float depth)
        {
            float sword = SwordFootworkWanted;
            float fists = FistFootworkWanted;
            footwork = Mathf.Max(sword, fists);
            if (sword >= fists)
            {
                lead = _swordFrame.StanceLead;
                depth = _swordFrame.StanceDepth;
            }
            else
            {
                lead = _fistFrame.StanceLead;
                depth = _fistFrame.StanceDepth;
            }
        }

        /// <summary>The sword in the right hand, if there is one: on this body or on another player's copy alike (the same model is mounted there).</summary>
        public ISwordBlade HeldSword
        {
            get
            {
                PosedSword();
                return _posedBlade;
            }
        }

        /// <summary>The blade's axis in the right hand bone's own frame when nothing says otherwise: out past the thumb.</summary>
        private static readonly Vector3 FallbackBladeInHand = new Vector3(0.26f, 0.0f, 0.966f).normalized;

        private static readonly Vector3 FallbackEdgeInHand = new Vector3(0.966f, 0.0f, -0.26f).normalized;

        /// <summary>The right hand's child count when it was last searched for a sword: asked every frame on every body (the carry), searched only when it changes.</summary>
        private int _posedSwordSearchedChildren = -1;

        /// <summary>The sword under the right hand, if one is mounted there (its own transform; the blade runs along its +Y and the edge faces its -X).</summary>
        private Transform PosedSword()
        {
            if (_handR == null)
            {
                return null;
            }
            if (_posedSword != null && _posedSword.parent == _handR)
            {
                return _posedSword;
            }
            if (_posedSword == null && _posedBlade == null && _handR.childCount == _posedSwordSearchedChildren)
            {
                return null;   // nothing mounted since the last look
            }
            _posedSwordSearchedChildren = _handR.childCount;
            _posedSword = null;
            _posedBlade = null;
            for (int i = 0; i < _handR.childCount; i++)
            {
                Transform child = _handR.GetChild(i);
                ISwordBlade blade = child.GetComponent<ISwordBlade>();
                if (blade != null)
                {
                    _posedSword = child;
                    _posedBlade = blade;
                    break;
                }
            }
            return _posedSword;
        }

        /// <summary>
        /// If the pose has put the blade's tip into the ground, the hand is pitched up about the axis across the blade until the tip clears it:
        /// a slam on a slope, or a follow-through authored a little too low, scrapes the floor instead of passing through it.
        /// </summary>
        private void KeepTipAboveGround(float weight)
        {
            if (_posedBlade == null || weight <= 0.001f)
            {
                return;
            }
            float floor = transform.position.y + TipFloorClearance;
            for (int pass = 0; pass < 2; pass++)
            {
                Vector3 root = _posedBlade.BladeBaseWorld;
                Vector3 tip = _posedBlade.BladeTipWorld;
                if (tip.y >= floor)
                {
                    return;
                }
                Vector3 blade = tip - root;
                float length = blade.magnitude;
                if (length < 1e-4f)
                {
                    return;
                }
                blade /= length;
                Vector3 axis = Vector3.Cross(blade, Vector3.up);
                if (axis.sqrMagnitude < 1e-6f)
                {
                    return;   // straight down: nothing across it to pitch about
                }
                axis.Normalize();
                float pitchNow = Mathf.Asin(Mathf.Clamp(blade.y, -1.0f, 1.0f));
                float pitchWanted = Mathf.Asin(Mathf.Clamp((floor - root.y) / length, -1.0f, 1.0f));
                float degrees = (pitchWanted - pitchNow) * Mathf.Rad2Deg * weight;
                Quaternion before = _handR.rotation;
                _handR.rotation = Quaternion.AngleAxis(degrees, axis) * before;
                if (_posedBlade.BladeTipWorld.y < tip.y)
                {
                    _handR.rotation = Quaternion.AngleAxis(-degrees, axis) * before;   // the other way round the axis
                }
            }
        }

        /// <summary>
        /// THE HIPS LEAD THE CUT (the kinetic chain: hips first, the shoulders after them). Turned here, before the foot IK, about the vertical; the
        /// thighs are given back their own heading so the legs do not swing round with the pelvis, and the foot IK then solves each leg from the
        /// turned hip down to its planted foot: the knees follow, the feet stay where they stand.
        /// </summary>
        private void ApplySwordHips()
        {
            if (!SwordPoseActive || _hips == null)
            {
                return;
            }
            float degrees = _swordFrame.HipTwistDegrees * Mathf.Clamp01(_swordFrame.Weight) * Mathf.Clamp01(TorsoFollowStrength);
            if (Mathf.Abs(degrees) < 0.01f)
            {
                return;
            }
            Quaternion leftThigh = _leftUpperLeg != null ? _leftUpperLeg.rotation : Quaternion.identity;
            Quaternion rightThigh = _rightUpperLeg != null ? _rightUpperLeg.rotation : Quaternion.identity;
            _hips.rotation = Quaternion.AngleAxis(degrees, transform.up) * _hips.rotation;
            if (_leftUpperLeg != null) _leftUpperLeg.rotation = leftThigh;
            if (_rightUpperLeg != null) _rightUpperLeg.rotation = rightThigh;
        }

        private void ApplySwordPose()
        {
            if (!SwordPoseActive || _rightUpperArm == null || _rightLowerArm == null || _handR == null)
            {
                _swordWristTwist = 0.0f;
                return;
            }
            float w = Mathf.Clamp01(_swordFrame.Weight);

            // The torso into the cut, before the arm is solved from the shoulder it moved. The hips have already turned their share
            // (ApplySwordHips), so the spine turns the rest. How much of the authored twist and lean the body does is the grip settings' torso
            // amount (1 as authored, 0 upright), read live so it can be tuned while the game runs. Under an attack's clip the torso is the
            // clip's (ApplyBakedTrunk, just before this), and the arm is solved from the shoulder IT has turned.
            float torso = w * Mathf.Clamp01(TorsoFollowStrength) * (1.0f - BakedTrunkOnlyWeight);
            if (_spine != null && torso > 0.0f)
            {
                Quaternion twist = Quaternion.AngleAxis((_swordFrame.TwistDegrees - _swordFrame.HipTwistDegrees) * torso, transform.up);
                Quaternion lean = Quaternion.AngleAxis(_swordFrame.LeanDegrees * torso, transform.right);
                Quaternion tilt = Quaternion.AngleAxis(_swordFrame.TiltDegrees * torso, transform.forward);
                _spine.rotation = twist * lean * tilt * _spine.rotation;
            }

            // The sword arm: the wrist to the frame's point, the elbow down or out as it says.
            Quaternion axes = ArmAxesUnderTheClip();
            Vector3 right = axes * Vector3.right, up = axes * Vector3.up, forward = axes * Vector3.forward;
            float armLength = Vector3.Distance(_rightUpperArm.position, _rightLowerArm.position) + Vector3.Distance(_rightLowerArm.position, _handR.position);
            Vector3 h = _swordFrame.Hand;
            Vector3 target = _rightUpperArm.position + (right * h.x + up * h.y + forward * h.z) * armLength;
            // Out to the right side (up to 1), or below 0 across the chest: a backhand's elbow leads the cut from the left.
            float elbowOut = Mathf.Clamp(_swordFrame.ElbowOut, -1.0f, 1.0f);
            Vector3 hint = -up * (1.0f - 0.8f * Mathf.Abs(elbowOut)) + right * (0.55f + 0.9f * elbowOut);
            TwoBoneIK.Solve(_rightUpperArm, _rightLowerArm, _handR, target, hint, w);

            // The hand turned so the blade points where the frame says, edge first.
            Vector3 bladeWorld = (right * _swordFrame.Blade.x + up * _swordFrame.Blade.y + forward * _swordFrame.Blade.z).normalized;
            Vector3 edgeWorld = right * _swordFrame.Edge.x + up * _swordFrame.Edge.y + forward * _swordFrame.Edge.z;
            edgeWorld -= bladeWorld * Vector3.Dot(edgeWorld, bladeWorld);
            if (edgeWorld.sqrMagnitude < 1e-6f)
            {
                edgeWorld = Vector3.Cross(bladeWorld, Vector3.up);
            }
            Vector3 bladeInHand = FallbackBladeInHand;
            Vector3 edgeInHand = FallbackEdgeInHand;
            Transform sword = PosedSword();
            if (sword != null)
            {
                if (WeaponGripSettings.HoldBeingEdited && _holdEditFrozen)
                {
                    // The hold is being adjusted in the editor: the hand keeps the turn it had when that began, so the sword turns in it.
                    bladeInHand = _frozenBladeInHand;
                    edgeInHand = _frozenEdgeInHand;
                }
                else
                {
                    bladeInHand = sword.localRotation * Vector3.up;
                    // The sword's own edge (most face their -X; the combat sword's faces +X), in the hand's frame.
                    Vector3 edgeAlong = _posedBlade != null ? _posedBlade.EdgeDirectionWorld : Vector3.zero;
                    edgeInHand = edgeAlong.sqrMagnitude > 1e-6f ? _handR.InverseTransformDirection(edgeAlong) : sword.localRotation * Vector3.left;
                    _frozenBladeInHand = bladeInHand;
                    _frozenEdgeInHand = edgeInHand;
                    _holdEditFrozen = WeaponGripSettings.HoldBeingEdited;
                }
            }
            if (!WeaponGripSettings.HoldBeingEdited)
            {
                _holdEditFrozen = false;
            }
            Quaternion wanted = Quaternion.LookRotation(bladeWorld, edgeWorld.normalized) * Quaternion.Inverse(Quaternion.LookRotation(bladeInHand, edgeInHand));

            // THE FOREARM RUNS INTO THE HAND (owner, 2026-10-16, holding the sword: "angle the arm properly so theres no that weird bend"): in the
            // held stances the elbow is put where the forearm comes in along the hand (the hand bone's +X runs to the fingertips), as near as the
            // arm's lengths allow, rather than where the key bows it -- which left the wrist bent to make up the difference. A one-handed grip
            // comes in at its cocked wrist (HeldWristCockDegrees: the hand tipped toward the little finger, so the forearm runs between the
            // knuckles and the thumb), which keeps the elbow low at the side.
            float align = Mathf.Clamp01(_swordFrame.ForearmAlign);
            if (align > 0.0f)
            {
                float forearm = Vector3.Distance(_rightLowerArm.position, _handR.position);
                Quaternion forearmInHand = _swordFrame.TwoHanded ? Quaternion.identity : Quaternion.AngleAxis(-HeldWristCockDegrees, Vector3.up);
                Vector3 forearmAlong = wanted * forearmInHand * Vector3.right;
                if (!_swordFrame.TwoHanded)
                {
                    // The hand where the upper arm can hang the elbow for that forearm: the elbow toward where it would be, the upper arm's length
                    // from the shoulder, and the wrist the forearm's length on from it.
                    float upperArm = Vector3.Distance(_rightUpperArm.position, _rightLowerArm.position);
                    Vector3 elbowToward = target - forearmAlong * forearm - _rightUpperArm.position;
                    if (elbowToward.sqrMagnitude > 1e-6f)
                    {
                        Vector3 reachable = _rightUpperArm.position + elbowToward.normalized * upperArm + forearmAlong * forearm;
                        target = Vector3.Lerp(target, reachable, HeldHandYield * align);   // (solved to below, with the elbow placed for it)
                    }
                }
                Vector3 idealElbow = target - forearmAlong * forearm;
                Vector3 toward = idealElbow - _rightUpperArm.position;
                if (toward.sqrMagnitude > 1e-6f)
                {
                    hint = Vector3.Lerp(hint.normalized, toward.normalized, align);
                    TwoBoneIK.Solve(_rightUpperArm, _rightLowerArm, _handR, target, hint, w);
                }
            }

            if (!_swordFrame.TwoHanded && _swordFrame.WristRollDegrees > 0.0f)
            {
                wanted = WithinAWristsBend(wanted, (_handR.position - _rightLowerArm.position).normalized, bladeWorld, _swordFrame.WristRollDegrees);
            }
            _handR.rotation = Quaternion.Slerp(_handR.rotation, wanted, w);

            // BOTH HANDS STAY ON A TWO-HANDED GRIP (owner, 2026-10-16: "use inverse kinematic so the hands reach the correct grip points on each
            // weapon"): the right hand carries the sword, the left reaches for its grip point (ApplySupportHand). Wherever a cut takes the sword
            // further from the LEFT shoulder than that arm can reach, the whole sword is drawn in toward it (the right arm re-solved, the blade
            // kept pointing the same way) until it can: the hands never come apart.
            if (_swordFrame.TwoHanded && _supportAnchor != null && _leftUpperArm != null && _leftLowerArm != null && _handL != null)
            {
                float leftReach = (Vector3.Distance(_leftUpperArm.position, _leftLowerArm.position) + Vector3.Distance(_leftLowerArm.position, _handL.position))
                    * SupportReachShare + PalmOffsetMetres;
                Vector3 grip = _supportAnchor.TransformPoint(_supportLocalPoint);
                Vector3 fromShoulder = grip - _leftUpperArm.position;
                float over = fromShoulder.magnitude - leftReach;
                if (over > 0.0f)
                {
                    target -= fromShoulder.normalized * over;
                    TwoBoneIK.Solve(_rightUpperArm, _rightLowerArm, _handR, target, hint, w);
                    _handR.rotation = Quaternion.Slerp(_handR.rotation, wanted, w);
                }
            }
            KeepTipAboveGround(w);
            WristTwist.ShareWithForearm(_rightLowerArm, _handR, ref _swordWristTwist);

            // The free hand, when the sword leaves it free (and the pose places it at all: the carry leaves it swinging with the walk).
            float free = w * Mathf.Clamp01(_swordFrame.FreeHandWeight);
            if (!_swordFrame.TwoHanded && free > 0.001f)
            {
                PlaceFreeHand(_leftUpperArm, _leftLowerArm, _handL, _swordFrame.FreeHand, free, _swordFrame.FreeElbowOut, -1.0f, axes);
            }
        }

        /// <summary>
        /// The axes a sword frame's arm is written in (x to the right, y up, z forward): the body's own, and under an attack's clip turned
        /// about the vertical by as far as the clip has the shoulders turned past the frame's own twist. A frame puts the hand at an offset
        /// from the shoulder that was worked out for the shoulders turned by its twist; a clip coils the shoulders further (seventy degrees
        /// where the frame has forty-five), and with the offset left in the body's axes the lower hand of a two-handed grip went 19 cm into
        /// the chest. Turned with the shoulders, the arms and the blade stay where the frame put them against the chest, and the wind-up is
        /// drawn back by as much as the body coils.
        /// </summary>
        private Quaternion ArmAxesUnderTheClip()
        {
            float clip = BakedTrunkOnlyWeight;
            if (clip <= 0.0f || _leftUpperArm == null || _rightUpperArm == null)
            {
                return transform.rotation;
            }
            Vector3 across = transform.InverseTransformDirection(_rightUpperArm.position - _leftUpperArm.position);
            float turned = Mathf.Atan2(-across.z, across.x) * Mathf.Rad2Deg;   // to the right, as a frame's twist is
            return Quaternion.AngleAxis(Mathf.DeltaAngle(_swordFrame.TwistDegrees, turned) * clip, transform.up) * transform.rotation;
        }

        /// <summary>
        /// A hand put <paramref name="offset"/> from its shoulder, in <paramref name="axes"/> (the body's own: x to its right, y up, z
        /// forward) and in arm lengths, by two-bone IK: the elbow goes down and out, further out to its <paramref name="side"/> with
        /// <paramref name="elbowOut"/>.
        /// </summary>
        private void PlaceFreeHand(Transform upper, Transform lower, Transform hand, Vector3 offset, float weight, float elbowOut, float side, Quaternion axes)
        {
            if (weight <= 0.001f || upper == null || lower == null || hand == null)
            {
                return;
            }
            float length = Vector3.Distance(upper.position, lower.position) + Vector3.Distance(lower.position, hand.position);
            Vector3 right = axes * Vector3.right, up = axes * Vector3.up;
            Vector3 target = upper.position + (right * offset.x + up * offset.y + axes * Vector3.forward * offset.z) * length;
            Vector3 hint = -up * (1.0f - 0.8f * elbowOut) + right * side * (0.55f + 0.9f * elbowOut);
            TwoBoneIK.Solve(upper, lower, hand, target, hint, weight);
        }
    }
}
