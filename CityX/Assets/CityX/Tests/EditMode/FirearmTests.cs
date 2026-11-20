using System.Collections.Generic;
using System.Linq;
using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CityX.Tests
{
    /// <summary>
    /// The guns' rules and data (2026-10-14): the Glock and the AK-47 as two profiles on one firearm component, the AK's learnable spray, the
    /// one pistol-grip hold, the reload motions made in Blender (their parts, their events, in order), the grip of the fingers on a gun, and
    /// the built prefabs carrying the parts the motions move.
    /// </summary>
    public sealed class FirearmTests
    {
        [Test]
        public void TheGlockAndTheAK47AreTwoProfilesOfOneGun()
        {
            FirearmProfile glock = FirearmProfile.Glock();
            FirearmProfile ak = FirearmProfile.AK47();
            Assert.AreEqual(ItemRegistry.Pistol, glock.ItemId);
            Assert.AreEqual(ItemRegistry.AK47, ak.ItemId);
            Assert.IsTrue(ItemRegistry.TryGet(ItemRegistry.AK47, out _), "the AK-47 is not an item");
            Assert.AreEqual(15, glock.MagazineSize);
            Assert.IsFalse(glock.Automatic, "a Glock fires once per press");
            Assert.AreEqual(FirearmKind.Pistol, glock.Kind);
            Assert.AreEqual(30, ak.MagazineSize);
            Assert.IsTrue(ak.Automatic, "an AK fires while the trigger is held");
            Assert.AreEqual(FirearmKind.Rifle, ak.Kind);
            Assert.AreEqual(10.0f, ak.FireRatePerSecond, 0.5f, "about 600 rounds a minute");
            Assert.Greater(ak.Damage, glock.Damage, "a rifle round hits harder than a pistol's");
            Assert.Greater(ak.EmptyReloadSeconds, ak.ReloadSeconds, "an empty AK is racked after the new magazine: slower");
            Assert.IsFalse(ak.SupportHandOnlyWhenAiming, "a rifle is held two-handed always");
            Assert.AreEqual(PistolWeapon.MagazineSize, glock.MagazineSize, "the pistol's old constants and its profile agree");
        }

        [Test]
        public void TheAKsSprayClimbsStraightUpFirstThenWanders()
        {
            FirearmProfile ak = FirearmProfile.AK47();
            for (int i = 0; i < 5; i++)
            {
                Vector2 kick = ak.RecoilFor(i);
                Assert.Less(Mathf.Abs(kick.x), 0.1f, $"shot {i + 1} pulls sideways: the first shots climb straight up");
                Assert.Greater(kick.y, 0.5f, $"shot {i + 1} does not climb");
            }
            float climb = Enumerable.Range(0, 10).Sum(i => ak.RecoilFor(i).y);
            Assert.Greater(climb, 6.0f, "ten shots should climb several degrees: something to pull down against");
            var later = Enumerable.Range(10, 20).Select(i => ak.RecoilFor(i).x).ToList();
            Assert.IsTrue(later.Any(x => x > 0.3f) && later.Any(x => x < -0.3f), "after the climb the spray wanders both ways");
            Assert.AreEqual(ak.RecoilFor(29), ak.RecoilFor(200), "the last entry repeats");
            Assert.AreEqual(ak.RecoilFor(0), ak.RecoilFor(-3), "a negative shot index is the first shot");
        }

        [Test]
        public void EveryRoundOfTheAKAndThePistolDrawsATracer()
        {
            var akObject = new GameObject("AK");
            akObject.AddComponent<SphereCollider>();
            var glockObject = new GameObject("Glock");
            glockObject.AddComponent<SphereCollider>();
            try
            {
                var ak = akObject.AddComponent<PistolWeapon>();
                ak.Profile = FirearmProfile.AK47();
                var glock = glockObject.AddComponent<PistolWeapon>();
                for (int shot = 0; shot < 9; shot++)
                {
                    Assert.IsTrue(ak.ShowsTracer(shot), $"AK round {shot + 1}");
                    Assert.IsTrue(glock.ShowsTracer(shot), $"Glock round {shot + 1}");
                }
                Assert.AreEqual(600.0f, FirearmProfile.AK47().FireRatePerSecond * 60.0f, 0.01f, "Counter-Strike 2's AK-47: 600 rounds a minute");
            }
            finally
            {
                Object.DestroyImmediate(akObject);
                Object.DestroyImmediate(glockObject);
            }
        }

        [Test]
        public void ThePistolGripHoldPutsTheBarrelDownTheFingersAndTheTopAtTheThumb()
        {
            Quaternion hold = FirearmProfile.Glock().HoldRotation;
            float dip = FirearmProfile.Glock().HoldDipDegrees * Mathf.Deg2Rad;
            // The hand bone's frame (HandGrip): +x down the fingers, +y the back of the hand, +z the thumb's side.
            Assert.Less(Vector3.Distance(hold * Vector3.forward, new Vector3(Mathf.Cos(dip), 0.0f, -Mathf.Sin(dip))), 1e-4f, "the barrel");
            Assert.Less(Vector3.Distance(hold * Vector3.up, new Vector3(Mathf.Sin(dip), 0.0f, Mathf.Cos(dip))), 1e-4f, "the top of the gun");
            Assert.Less(Vector3.Distance(hold * Vector3.right, Vector3.up), 1e-4f, "the gun's right is the back of the hand");
        }

        [Test]
        public void ReloadNamesAndEventsAreReadBack()
        {
            Assert.IsTrue(GunReload.Has(GunReload.TacticalName));
            Assert.IsTrue(GunReload.TryGetStage(GunReload.EmptyName, out GunReload.Stage empty) && empty.Empty);
            Assert.IsTrue(GunReload.TryGetStage(GunReload.TacticalName, out GunReload.Stage tactical) && !tactical.Empty);
            Assert.IsFalse(GunReload.Has(GunReload.FireName), "a shot is not a reload");
            Assert.AreEqual(GunReload.SeatEvent, GunReload.EventKind("seat2"));
            Assert.AreEqual(GunReload.RackEvent, GunReload.EventKind("rack"));
            Assert.AreEqual(GunReload.EmptyName, GunReload.NameFor(true));
            Assert.AreEqual(GunReload.EmptySegment, GunReload.SegmentFor(true));
        }

        [TestCase("Glock/GlockReload", "Slide")]
        [TestCase("AK47/AK47Reload", "Bolt")]
        public void AReloadMotionHasItsPartsFingersAndEventsInOrder(string resource, string action)
        {
            BakedMotion motion = BakedMotion.Load(resource);
            Assert.IsNotNull(motion, $"no reload motion at Resources/{resource} -- run its Blender build script");
            Assert.IsTrue(motion.HasSegment(GunReload.TacticalSegment) && motion.HasSegment(GunReload.EmptySegment));
            Assert.Less(motion.SegmentSeconds(GunReload.TacticalSegment), motion.SegmentSeconds(GunReload.EmptySegment),
                "the empty reload racks the action after the magazine: it is the longer one");
            Assert.IsTrue(motion.HasFingers, "the fingers are posed through the reload");
            CollectionAssert.IsSupersetOf(motion.Props, new[] { action, "Magazine", "Trigger" });

            foreach (string segment in new[] { GunReload.TacticalSegment, GunReload.EmptySegment })
            {
                List<string> kinds = motion.EventsIn(segment).Select(e => GunReload.EventKind(e.Key)).ToList();
                float Seconds(string kind) => motion.EventsIn(segment).First(e => GunReload.EventKind(e.Key) == kind).Value;
                Assert.Contains(GunReload.DropEvent, kinds, segment);
                Assert.Contains(GunReload.SeatEvent, kinds, segment);
                Assert.Less(Seconds(GunReload.DropEvent), Seconds(GunReload.GrabEvent), segment + ": the old magazine goes before the new one is fetched");
                Assert.Less(Seconds(GunReload.GrabEvent), Seconds(GunReload.SeatEvent), segment);
                if (segment == GunReload.EmptySegment)
                {
                    Assert.Less(Seconds(GunReload.SeatEvent), Seconds(GunReload.RackEvent), "an empty gun is racked once the magazine is in");
                }
                else
                {
                    Assert.IsFalse(kinds.Contains(GunReload.RackEvent), "a tactical reload keeps its chambered round: no rack");
                }

                // The magazine is gone between the drop and the grab, and back in the gun at the end.
                int magazine = motion.Props.ToList().IndexOf("Magazine");
                int start = motion.SegmentStart(segment);
                int between = start + Mathf.RoundToInt((Seconds(GunReload.DropEvent) + Seconds(GunReload.GrabEvent)) * 0.5f * motion.FramesPerSecond);
                Assert.IsFalse(motion.PropVisibleAt(between, magazine), segment + ": the dropped magazine still shows in the gun");
                Assert.IsTrue(motion.PropVisibleAt(start + motion.SegmentFrames(segment), magazine), segment + ": no magazine at the end");
            }
        }

        [Test]
        public void AMotionWithAPartMissingFramesIsRefused()
        {
            const string json = "{\"fps\":60,\"frames\":2,\"bones\":[\"hips\"],\"segments\":[],\"hips\":[0,0,0,0,0,0]," +
                                "\"rotations\":[0,0,0,1,0,0,0,1],\"props\":[{\"name\":\"Slide\",\"positions\":[0,0,0],\"rotations\":[0,0,0,1,0,0,0,1]}]}";
            Assert.Throws<System.ArgumentException>(() => BakedMotion.FromJson(json));
            const string late = "{\"fps\":60,\"frames\":2,\"bones\":[\"hips\"],\"segments\":[],\"hips\":[0,0,0,0,0,0]," +
                                "\"rotations\":[0,0,0,1,0,0,0,1],\"events\":[{\"name\":\"seat\",\"frame\":5}]}";
            Assert.Throws<System.ArgumentException>(() => BakedMotion.FromJson(late));
        }

        [Test]
        public void ARiflesTriggerFingerLiesAlongTheFrameUntilItFiresAndAPistolsRestsOnTheTrigger()
        {
            float IndexFold(FingerAmounts a) => Quaternion.Angle(Quaternion.identity, HandGrip.CurlFor(0, true, a));
            float IndexTipFold(FingerAmounts a) => Quaternion.Angle(Quaternion.identity, HandGrip.CurlFor(1, true, a));
            float MiddleFold(FingerAmounts a) => Quaternion.Angle(Quaternion.identity, HandGrip.CurlFor(2, true, a));
            FingerAmounts rifle = FingerAmounts.RifleStrong(0.0f);
            Assert.Less(IndexFold(rifle), 10.0f, "a rifle's trigger finger is curled round the grip while it is only carried");
            Assert.AreEqual(HandGrip.FingerProximalDegrees, MiddleFold(rifle), 0.01f, "a rifle's other fingers wrap the grip");
            Assert.Greater(IndexFold(FingerAmounts.RifleStrong(1.0f)), IndexFold(rifle) + 20.0f, "pulling the trigger folds a rifle's trigger finger");

            // The pistol's are authored against its grip (PistolHoldPlayTests has them on the real hands): through the guard even while only carried.
            FingerAmounts carried = FingerAmounts.PistolStrong(0.0f);
            FingerAmounts pulled = FingerAmounts.PistolStrong(1.0f);
            Assert.Greater(IndexFold(carried), 20.0f, "a pistol's trigger finger is not curled in through the guard");
            Assert.Greater(MiddleFold(carried), IndexFold(carried), "a pistol's other fingers do not close further than the trigger finger");
            Assert.Greater(IndexFold(pulled), IndexFold(carried) + 4.0f, "pulling the trigger folds a pistol's trigger finger");
            Assert.Greater(IndexTipFold(pulled), IndexTipFold(carried) + 10.0f, "pulling the trigger folds a pistol's trigger finger's tip");
            Quaternion thumbForward = HandGrip.CurlFor(8, true, carried);
            Quaternion thumbWrapped = HandGrip.CurlFor(8, true, FingerAmounts.Wrap(1.0f));
            Assert.Greater(Quaternion.Angle(thumbForward, thumbWrapped), 30.0f, "the thumb lies forward along a gun's frame, not across it");
            // The old wrap is unchanged for everything else a hand holds.
            Assert.AreEqual(HandGrip.CurlFor(3, false, 0.7f), HandGrip.CurlFor(3, false, FingerAmounts.Wrap(0.7f)));
        }

#if UNITY_EDITOR
        [TestCase("Assets/CityX/Weapons/Pistol.prefab")]
        [TestCase("Assets/CityX/Weapons/AK47.prefab")]
        public void TheBuiltGunHasThePartsItsReloadMoves(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, path + " is missing -- CityX > Rebuild Firearms");
            var gun = prefab.GetComponent<PistolWeapon>();
            Assert.IsNotNull(gun);
            Assert.IsNotNull(gun.ActionPart, "no slide or bolt");
            Assert.IsNotNull(gun.MagazinePart, "no magazine");
            Assert.IsNotNull(gun.TriggerPart, "no trigger");
            Assert.AreEqual(gun.Profile.ActionPartName, gun.ActionPart.name);
            Assert.AreEqual(gun.Profile.MagazinePartName, gun.MagazinePart.name);
            BakedMotion motion = gun.Motion;
            Assert.IsNotNull(motion, "the gun's reload motion is missing");
            CollectionAssert.Contains(motion.Props, gun.ActionPart.name);
            CollectionAssert.Contains(motion.Props, gun.MagazinePart.name);
            Assert.Less(Vector3.Distance(gun.MuzzleFlashObject.transform.localPosition, gun.Profile.MuzzleLocal), 1e-4f, "the muzzle flash is not at the muzzle");
            Assert.AreEqual(Quaternion.identity, gun.ActionPart.localRotation, "a part rests unturned, as the motions were exported");
            // The motion's first frame of each segment has the parts where the model has them: nothing jumps when a reload starts.
            int magazine = motion.Props.ToList().IndexOf(gun.MagazinePart.name);
            int start = motion.SegmentStart(GunReload.TacticalSegment);
            Assert.Less(Vector3.Distance(motion.PropPositionAt(start, magazine), gun.MagazinePart.localPosition), 0.005f,
                "the reload's magazine does not start where the model's is");
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    Assert.IsNotNull(material, renderer.name + " has a missing material (it would render pink)");
                    Assert.IsNull(material.mainTexture, renderer.name + ": guns are flat-coloured, no textures");
                }
            }
        }
#endif

#if UNITY_EDITOR
        /// <summary>A two-handed weapon's triangles at most (the equippable budgets of 2026-10-18: 1-5k, 8k at the very most).</summary>
        private const int RifleTriangleBudget = 5000;

        /// <summary>
        /// Owner, 2026-11-19: "make the ak47 model look better keep it sort of cartoony ... but improve upon the model we have now ... keep the
        /// moving parts and reload animations". The remodelled rifle: rounded (smooth-shaded, more than the first one's 800 triangles of boxes)
        /// and inside a rifle's budget, wood and steel each a look of its own with none left on the fallback gunmetal, and what the hands hold
        /// where it was: the handguards inside the box the left hand's cradle was made on (FirearmPlayTests measures the hand against the same
        /// numbers), the muzzle, the butt and the charging handle where the reload and the profile have them.
        /// </summary>
        [Test]
        public void TheAK47IsRoundedInsideItsBudgetAndWhatTheHandsHoldIsWhereItWas()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/AK47.prefab");
            Assert.IsNotNull(prefab, "the AK-47 is missing -- CityX > Rebuild Firearms");
            var gun = prefab.GetComponent<PistolWeapon>();
            int triangles = 0;
            var looks = new HashSet<string>();
            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                triangles += filter.sharedMesh.triangles.Length / 3;
                foreach (Material material in filter.GetComponent<MeshRenderer>().sharedMaterials)
                {
                    looks.Add(material.name);
                }
            }
            Assert.Greater(triangles, 2500, "the AK-47 is the first model's boxes again");
            Assert.LessOrEqual(triangles, RifleTriangleBudget);
            CollectionAssert.IsSubsetOf(looks, CityX.WeaponsEditor.AK47Builder.Finishes.Keys, "a part of the AK-47 has no look of its own");
            CollectionAssert.Contains(looks, "AKWood");
            CollectionAssert.Contains(looks, "AKSteel");

            Mesh body = CityX.Core.TransformSearch.FindDeep(prefab.transform, "Body").GetComponent<MeshFilter>().sharedMesh;
            Vector3[] vertices = body.vertices;
            Vector3[] normals = body.normals;
            Assert.AreEqual(vertices.Length, normals.Length, "the body has no normals of its own: it would be shaded as flat facets");

            // The lower handguard's box and the gun's top over it (ak47_model.py's HANDGUARD, times its scale of 1.45): the cradle's numbers.
            const float halfWidth = 0.0334f, back = 0.370f, front = 0.587f, gunTop = 0.109f, slack = 0.0006f;
            float widest = 0.0f, highest = float.MinValue;
            foreach (Vector3 v in vertices)
            {
                if (v.z > back && v.z < front)
                {
                    widest = Mathf.Max(widest, Mathf.Abs(v.x));
                    highest = Mathf.Max(highest, v.y);
                }
            }
            Assert.LessOrEqual(widest, halfWidth + slack, "the handguard is wider than the box the left hand's hold was made on");
            Assert.LessOrEqual(highest, gunTop + slack, "the gun stands higher over the handguard than the left hand's fingers clear");

            Bounds bounds = body.bounds;
            Assert.AreEqual(gun.Profile.MuzzleLocal.z, bounds.max.z, 0.002f, "the barrel does not end at the muzzle");
            Assert.AreEqual(gun.Profile.ButtLocal.z, bounds.min.z, 0.002f, "the stock does not end at the butt");
            Assert.Less(Vector3.Distance(gun.ActionPart.localPosition, new Vector3(0.0225f, 0.0595f, 0.2349f)), 0.001f,
                "the charging handle is not where the reload's hand goes to it");
        }

        [Test]
        public void TheAK47ReloadsToItsOwnRecordingCutToEachReloadsLengthAndTheGlockKeepsItsClicks()
        {
            // Owner, 2026-10-14: "replace the ak47 reload sound with this. speed it up so its 2.8 seconds when magazine is empty and 2.17 seconds
            // with rounds left, but try to maintain the pitch". Each recording is exactly as long as the motion it plays with, so it ends as the hands do.
            var ak = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/AK47.prefab").GetComponent<PistolWeapon>();
            AudioClip tactical = ak.Profile.ReloadSoundFor(false);
            AudioClip empty = ak.Profile.ReloadSoundFor(true);
            Assert.IsNotNull(tactical, "the AK-47 has no recording for a reload with rounds left -- CityX > Rebuild Firearms");
            Assert.IsNotNull(empty, "the AK-47 has no recording for an empty reload -- CityX > Rebuild Firearms");
            Assert.AreNotSame(tactical, empty);
            Assert.AreEqual(2.17f, tactical.length, 0.01f, "the reload with rounds left");
            Assert.AreEqual(2.8f, empty.length, 0.01f, "the empty reload");
            Assert.AreEqual(ak.Motion.SegmentSeconds(GunReload.TacticalSegment), tactical.length, 0.01f, "the recording outlasts or falls short of its motion");
            Assert.AreEqual(ak.Motion.SegmentSeconds(GunReload.EmptySegment), empty.length, 0.01f, "the recording outlasts or falls short of its motion");
            Assert.AreEqual(ak.Profile.ReloadSeconds, tactical.length, 0.01f, "a body without the motion times its reload by ReloadSeconds");
            Assert.AreEqual(ak.Profile.EmptyReloadSeconds, empty.length, 0.01f);
            var glock = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/Pistol.prefab").GetComponent<PistolWeapon>();
            Assert.IsNull(glock.Profile.ReloadSoundFor(false), "the Glock keeps its synthesised reload clicks");
            Assert.IsNull(glock.Profile.ReloadSoundFor(true));
        }
#endif
    }
}
