using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// SwordElbow (docs/SWORD_GRIP_SYSTEM.md): a sword arm's elbow goes where the forearm runs into the hand as the grip wants it, so the
    /// wrist stays near straight, over where a fixed rule would bow it.
    /// </summary>
    public sealed class SwordElbowTests
    {
        private const float Upper = 0.45f;
        private const float Lower = 0.40f;

        private static Vector3 ElbowAt(Vector3 shoulder, Vector3 wrist, Vector3 bow)
        {
            Vector3 axis = (wrist - shoulder).normalized;
            float reach = (wrist - shoulder).magnitude;
            float along = (Upper * Upper - Lower * Lower + reach * reach) / (2.0f * reach);
            return shoulder + axis * along + bow * Mathf.Sqrt(Upper * Upper - along * along);
        }

        [Test]
        public void TheElbowGoesWhereTheForearmRunsIntoTheHandAsTheGripWants()
        {
            // A backhand's hand out in front of the right shoulder, gripped so the forearm must come in from the left (across the chest): an
            // elbow bowed down and out, as a fixed rule had it, bends that wrist far; the chosen one does not.
            Vector3 shoulder = new Vector3(0.25f, 1.5f, 0.0f);
            Vector3 wrist = new Vector3(0.45f, 1.35f, 0.55f);
            Vector3 easyForearm = new Vector3(1.0f, -0.1f, 0.2f).normalized;
            Vector3 downAndOut = new Vector3(0.55f, -1.0f, 0.0f);
            Vector3 bow = SwordElbow.Bow(shoulder, wrist, Upper, Lower, easyForearm, downAndOut, Vector3.zero, out float bend);
            float fixedRule = Vector3.Angle(wrist - ElbowAt(shoulder, wrist, SwordElbowAcross(shoulder, wrist, downAndOut)), easyForearm);
            float chosen = Vector3.Angle(wrist - ElbowAt(shoulder, wrist, bow), easyForearm);
            Assert.AreEqual(chosen, bend, 0.5f, "the bend it reports is not the bend of the elbow it chose");
            Assert.Less(chosen, fixedRule - 20.0f, $"the chosen elbow bends the wrist {chosen:0} degrees, the fixed rule's {fixedRule:0}");
            Assert.AreEqual(0.0f, Vector3.Dot(bow, (wrist - shoulder).normalized), 1e-3f, "the bow is not across the arm's line");
        }

        [Test]
        public void WhereTheWristIsEasyEitherWayTheElbowHangsAsItEasilyDoes()
        {
            // The hand straight out in front and a grip happy with any elbow round it: the elbow stays where it hangs easily.
            Vector3 shoulder = new Vector3(0.25f, 1.5f, 0.0f);
            Vector3 wrist = new Vector3(0.25f, 1.5f, 0.84f);
            Vector3 easyForearm = Vector3.forward;
            Vector3 down = Vector3.down;
            Vector3 bow = SwordElbow.Bow(shoulder, wrist, Upper, Lower, easyForearm, down, Vector3.zero, out _);
            Assert.Greater(Vector3.Dot(bow, Vector3.down), 0.95f, "the elbow left where it hangs easily for nothing");
        }

        [Test]
        public void FromOneFrameToTheNextTheElbowDoesNotFlipRound()
        {
            // A hand swept across the front a little each frame, as a cut does, each frame's elbow handed on to the next: it moves with the
            // hand, never jumping round the arm between two frames.
            Vector3 shoulder = new Vector3(0.25f, 1.5f, 0.0f);
            Vector3 last = Vector3.zero;
            for (int frame = 0; frame <= 30; frame++)
            {
                float s = frame / 30.0f;
                Vector3 wrist = new Vector3(Mathf.Lerp(-0.2f, 0.7f, s), Mathf.Lerp(1.8f, 1.3f, s), 0.5f);
                Vector3 easyForearm = Quaternion.AngleAxis(Mathf.Lerp(-60.0f, 60.0f, s), Vector3.up) * Vector3.forward;
                Vector3 bow = SwordElbow.Bow(shoulder, wrist, Upper, Lower, easyForearm, new Vector3(0.55f, -1.0f, 0.0f), last, out _);
                if (last != Vector3.zero)
                {
                    Assert.Less(Vector3.Angle(bow, last), 30.0f, $"the elbow jumped round the arm at frame {frame}");
                }
                last = bow;
            }
        }

        private static Vector3 SwordElbowAcross(Vector3 shoulder, Vector3 wrist, Vector3 v)
        {
            Vector3 axis = (wrist - shoulder).normalized;
            return (v - axis * Vector3.Dot(v, axis)).normalized;
        }
    }
}
