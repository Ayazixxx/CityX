using System;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>One limp body's pose at one moment: the hips' place and turn in the world and every other bone's place and turn against its parent.</summary>
    public sealed class RagdollPose
    {
        public float Time;
        public Vector3 HipsPosition;
        public Quaternion HipsRotation = Quaternion.identity;
        public readonly Quaternion[] Local = new Quaternion[RagdollPoseCodec.MaxBones];
        public readonly Vector3[] LocalPositions = new Vector3[RagdollPoseCodec.MaxBones];
        public int Count;

        public void CopyFrom(RagdollPose other)
        {
            Time = other.Time;
            HipsPosition = other.HipsPosition;
            HipsRotation = other.HipsRotation;
            Count = other.Count;
            Array.Copy(other.Local, Local, other.Count);
            Array.Copy(other.LocalPositions, LocalPositions, other.Count);
        }
    }

    /// <summary>
    /// A LIMP BODY'S POSE ON THE WIRE (docs/MULTIPLAYER_STATE.md; owner, 2026-10-29: "make it so whatever ragdoll position you are in is the
    /// same on all multiplayer screens, whether it was ragdoll from the water jet or jetpack crash or grapple swinging"), with no scene. The
    /// hips' place and turn in the world and each other bone's place and turn against its parent: a turn packed into 32 bits (the three
    /// smallest parts of the quaternion, 10 bits each, and which part was left out: a tenth of a degree at worst), a place into three 16-bit
    /// numbers (a tenth of a millimetre). The places are sent as well as the turns because a ragdoll's joints give under load (hanging from the
    /// rope, lying twisted): sent turns alone, a copy kept its limbs at their rest length and lay up to 45 cm off its owner. Stamped with the
    /// owner's clock so a copy can play the poses back between two that arrived. Seventeen bones come to under two hundred bytes.
    /// </summary>
    public static class RagdollPoseCodec
    {
        public const int MaxBones = 40;

        /// <summary>The owner's clock, the hips' place (three floats), the hips' turn, the bone count; then a turn and a place per bone.</summary>
        public const int HeaderBytes = 4 + 12 + 4 + 1;

        public const int BytesPerBone = 4 + 6;

        /// <summary>The hips may be this far from the body's root (it stays where the body went limp; a crash can throw the body a long way).</summary>
        public const float MaxHipsMetres = 400.0f;

        /// <summary>A bone's place against its parent fits within this, metres, each way (the longest bone and then some).</summary>
        public const float MaxLocalMetres = 4.0f;

        private const float Range = 0.70710678f;
        private const float Steps = 1023.0f;

        public static int SizeFor(int bones) => HeaderBytes + bones * BytesPerBone;

        public static byte[] Pack(RagdollPose pose)
        {
            int count = Mathf.Clamp(pose.Count, 0, MaxBones);
            var data = new byte[SizeFor(count)];
            int at = 0;
            WriteFloat(data, ref at, pose.Time);
            WriteFloat(data, ref at, pose.HipsPosition.x);
            WriteFloat(data, ref at, pose.HipsPosition.y);
            WriteFloat(data, ref at, pose.HipsPosition.z);
            WriteUInt(data, ref at, PackRotation(pose.HipsRotation));
            data[at++] = (byte)count;
            for (int i = 0; i < count; i++)
            {
                WriteUInt(data, ref at, PackRotation(pose.Local[i]));
                WritePlace(data, ref at, pose.LocalPositions[i]);
            }
            return data;
        }

        /// <summary>
        /// Reads a pose off the wire, and refuses anything that is not one: the wrong length or bone count, a number that is not finite, hips
        /// further than <see cref="MaxHipsMetres"/> from <paramref name="root"/>. A stranger's packet is trusted no further than that.
        /// </summary>
        public static bool TryUnpack(byte[] data, int expectedBones, Vector3 root, RagdollPose into)
        {
            if (data == null || data.Length < HeaderBytes)
            {
                return false;
            }
            int at = 0;
            float time = ReadFloat(data, ref at);
            var hips = new Vector3(ReadFloat(data, ref at), ReadFloat(data, ref at), ReadFloat(data, ref at));
            uint hipsTurn = ReadUInt(data, ref at);
            int count = data[at++];
            if (count != expectedBones || count > MaxBones || data.Length != SizeFor(count))
            {
                return false;
            }
            if (!IsFinite(time) || !IsFinite(hips.x) || !IsFinite(hips.y) || !IsFinite(hips.z)
                || (hips - root).sqrMagnitude > MaxHipsMetres * MaxHipsMetres)
            {
                return false;
            }
            into.Time = time;
            into.HipsPosition = hips;
            into.HipsRotation = UnpackRotation(hipsTurn);
            into.Count = count;
            for (int i = 0; i < count; i++)
            {
                into.Local[i] = UnpackRotation(ReadUInt(data, ref at));
                into.LocalPositions[i] = ReadPlace(data, ref at);
            }
            return true;
        }

        /// <summary>A pose <paramref name="t"/> of the way from <paramref name="a"/> to <paramref name="b"/>, into <paramref name="into"/>.</summary>
        public static void Lerp(RagdollPose a, RagdollPose b, float t, RagdollPose into)
        {
            t = Mathf.Clamp01(t);
            into.Time = Mathf.Lerp(a.Time, b.Time, t);
            into.HipsPosition = Vector3.Lerp(a.HipsPosition, b.HipsPosition, t);
            into.HipsRotation = Quaternion.Slerp(a.HipsRotation, b.HipsRotation, t);
            into.Count = Mathf.Min(a.Count, b.Count);
            for (int i = 0; i < into.Count; i++)
            {
                into.Local[i] = Quaternion.Slerp(a.Local[i], b.Local[i], t);
                into.LocalPositions[i] = Vector3.Lerp(a.LocalPositions[i], b.LocalPositions[i], t);
            }
        }

        /// <summary>A bone's place against its parent in three 16-bit numbers, each within <see cref="MaxLocalMetres"/>.</summary>
        private static void WritePlace(byte[] data, ref int at, Vector3 place)
        {
            WriteShort(data, ref at, place.x);
            WriteShort(data, ref at, place.y);
            WriteShort(data, ref at, place.z);
        }

        private static Vector3 ReadPlace(byte[] data, ref int at)
        {
            return new Vector3(ReadShort(data, ref at), ReadShort(data, ref at), ReadShort(data, ref at));
        }

        private static void WriteShort(byte[] data, ref int at, float metres)
        {
            float value = float.IsNaN(metres) ? 0.0f : Mathf.Clamp(metres, -MaxLocalMetres, MaxLocalMetres);
            short packed = (short)Mathf.RoundToInt(value / MaxLocalMetres * short.MaxValue);
            data[at++] = (byte)packed;
            data[at++] = (byte)(packed >> 8);
        }

        private static float ReadShort(byte[] data, ref int at)
        {
            short packed = (short)(data[at] | (data[at + 1] << 8));
            at += 2;
            return packed / (float)short.MaxValue * MaxLocalMetres;
        }

        /// <summary>A turn in 32 bits: which of x, y, z, w was largest (2 bits, left out and rebuilt from the others), the other three 10 bits each.</summary>
        public static uint PackRotation(Quaternion q)
        {
            float length = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            if (length < 1e-6f || float.IsNaN(length))
            {
                q = Quaternion.identity;
                length = 1.0f;
            }
            float x = q.x / length, y = q.y / length, z = q.z / length, w = q.w / length;
            int largest = 0;
            float most = Mathf.Abs(x);
            if (Mathf.Abs(y) > most) { largest = 1; most = Mathf.Abs(y); }
            if (Mathf.Abs(z) > most) { largest = 2; most = Mathf.Abs(z); }
            if (Mathf.Abs(w) > most) { largest = 3; }
            float sign = Part(largest, x, y, z, w) < 0.0f ? -1.0f : 1.0f;   // q and -q are the same turn: keep the left-out part positive
            uint packed = (uint)largest << 30;
            int shift = 20;
            for (int i = 0; i < 4; i++)
            {
                if (i == largest)
                {
                    continue;
                }
                float value = Mathf.Clamp(Part(i, x, y, z, w) * sign, -Range, Range);
                packed |= (uint)Mathf.RoundToInt((value + Range) / (2.0f * Range) * Steps) << shift;
                shift -= 10;
            }
            return packed;
        }

        public static Quaternion UnpackRotation(uint packed)
        {
            int largest = (int)(packed >> 30);
            var parts = new Vector4();
            float sum = 0.0f;
            int shift = 20;
            for (int i = 0; i < 4; i++)
            {
                if (i == largest)
                {
                    continue;
                }
                float value = ((packed >> shift) & 1023u) / Steps * (2.0f * Range) - Range;
                parts[i] = value;
                sum += value * value;
                shift -= 10;
            }
            parts[largest] = Mathf.Sqrt(Mathf.Max(0.0f, 1.0f - sum));
            return new Quaternion(parts.x, parts.y, parts.z, parts.w).normalized;
        }

        private static float Part(int index, float x, float y, float z, float w)
        {
            switch (index)
            {
                case 0: return x;
                case 1: return y;
                case 2: return z;
                default: return w;
            }
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static void WriteFloat(byte[] data, ref int at, float value)
        {
            WriteUInt(data, ref at, (uint)BitConverter.SingleToInt32Bits(value));
        }

        private static float ReadFloat(byte[] data, ref int at)
        {
            return BitConverter.Int32BitsToSingle((int)ReadUInt(data, ref at));
        }

        private static void WriteUInt(byte[] data, ref int at, uint value)
        {
            data[at++] = (byte)value;
            data[at++] = (byte)(value >> 8);
            data[at++] = (byte)(value >> 16);
            data[at++] = (byte)(value >> 24);
        }

        private static uint ReadUInt(byte[] data, ref int at)
        {
            uint value = data[at] | ((uint)data[at + 1] << 8) | ((uint)data[at + 2] << 16) | ((uint)data[at + 3] << 24);
            at += 4;
            return value;
        }
    }
}
