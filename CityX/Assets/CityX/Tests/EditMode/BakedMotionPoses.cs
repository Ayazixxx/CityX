using System.Collections.Generic;
using CityX.Character;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Reads a Blender move (BakedMotion) the way the skeleton plays it, without a scene: each bone's world turn and each joint's place on a
    /// frame, the blade's direction, and how far any bone turns between frames. Shared by the tests of every baked move.
    /// </summary>
    internal static class BakedMotionPoses
    {
        /// <summary>World turns of the body's bones on one frame, from the root down, the way the skeleton applies them.</summary>
        public static Dictionary<string, Quaternion> WorldTurns(BakedMotion motion, int frame)
        {
            var parent = new Dictionary<string, string>();
            foreach (BodyRig.Bone bone in BodyRig.Bones())
            {
                parent[bone.Name] = bone.Parent;
            }
            var world = new Dictionary<string, Quaternion>();
            for (int b = 0; b < motion.Bones.Count; b++)
            {
                string name = motion.Bones[b];
                Quaternion local = motion.RotationAt(frame, b);
                world[name] = parent[name] == null ? local : world[parent[name]] * local;
            }
            return world;
        }

        /// <summary>Where every joint is on one frame: the skeleton's T-pose rest offsets turned by the frame's rotations, from its hips down.</summary>
        public static Dictionary<string, Vector3> Joints(BakedMotion motion, int frame, Dictionary<string, Quaternion> world)
        {
            var bones = new Dictionary<string, BodyRig.Bone>();
            foreach (BodyRig.Bone bone in BodyRig.Bones())
            {
                bones[bone.Name] = bone;
            }
            var at = new Dictionary<string, Vector3>();
            foreach (string name in motion.Bones)
            {
                BodyRig.Bone bone = bones[name];
                at[name] = bone.Parent == null
                    ? motion.HipsAt(frame)
                    : at[bone.Parent] + world[bone.Parent] * (bone.Position - bones[bone.Parent].Position);
            }
            return at;
        }

        /// <summary>The blade's direction on a frame: the hand's turn applied to the sword's default hold (the moves were posed with it).</summary>
        public static Vector3 Blade(Dictionary<string, Quaternion> world)
        {
            return world["hand_r"] * CityX.Weapons.SwordWeapon.DefaultGripRotation * Vector3.up;
        }

        /// <summary>The most any bone turns from one frame to the next across <paramref name="from"/>..<paramref name="to"/> (and from the last back to the first if it loops).</summary>
        public static float WorstStep(BakedMotion motion, int from, int to, bool loops, out string where)
        {
            float worst = 0.0f;
            where = "";
            for (int f = from; f < to + (loops ? 1 : 0); f++)
            {
                int next = f + 1 > to ? from : f + 1;
                for (int b = 0; b < motion.Bones.Count; b++)
                {
                    float step = Quaternion.Angle(motion.RotationAt(f, b), motion.RotationAt(next, b));
                    if (step > worst)
                    {
                        worst = step;
                        where = $"{motion.Bones[b]}, frame {f} -> {next}";
                    }
                }
            }
            return worst;
        }

        /// <summary>The bones a baked move poses: BodyRig's, all but the fingers (HandGrip poses those; the Blender rig has none).</summary>
        public static List<string> BodyBonesWithoutFingers()
        {
            var names = new List<string>();
            foreach (BodyRig.Bone bone in BodyRig.Bones())
            {
                if (!BodyRig.IsFinger(bone.Human))
                {
                    names.Add(bone.Name);
                }
            }
            return names;
        }
    }
}
