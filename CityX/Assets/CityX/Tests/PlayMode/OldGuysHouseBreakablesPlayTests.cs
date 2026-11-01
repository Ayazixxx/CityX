using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.View;
using CityX.Weapons;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using House = CityX.World.OldGuysHouse;

namespace CityX.Tests
{
    /// <summary>
    /// What breaks in Old Guy's house (docs/OLD_GUYS_HOUSE.md; owner, 2026-10-31: "make the windows shatterable", "make it so you have to break the
    /// wood planks"): a pane cracks under a fist and shatters at the next, a blade shatters it at once, a bullet cracks it and goes on through; the
    /// front door's planks are torn off by blows and with all three gone the door swings open and a body walks in from the porch; a piece broken on
    /// another machine breaks here, and a house built afterwards finds it broken.
    /// </summary>
    public sealed class OldGuysHouseBreakablesPlayTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            TutorialHouseDamage.Shared.Clear();
            TutorialHouseDamage.Shared.Relay = null;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made) if (go != null) Object.Destroy(go);
            _made.Clear();
            foreach (BoardedPlank plank in Object.FindObjectsByType<BoardedPlank>(FindObjectsSortMode.None)) Object.Destroy(plank.gameObject);
            TutorialHouseDamage.Shared.Clear();
        }

        private OldGuysHouseSite MakeHouse() => OldGuysHousePlayTests.MakeHouse(_made, false, false);

        [UnityTest]
        public IEnumerator APaneCracksAtTheFirstPunchAndShattersAtTheSecond()
        {
            HousePane pane = MakeHouse().Built.GetComponentsInChildren<HousePane>()[0];
            yield return null;
            Assert.IsTrue(pane.TakePunch(pane.transform.position), "a punch at the pane did not connect");
            Assert.IsTrue(pane.Cracked && !pane.Shattered, "the first punch did not just crack the pane");
            pane.TakePunch(pane.transform.position);
            Assert.IsTrue(pane.Shattered, "the second punch did not shatter the pane");
            Assert.IsFalse(pane.GetComponent<Collider>().enabled, "a shattered pane still blocks the window");
            Assert.IsTrue(TutorialHouseDamage.Shared.IsBroken(pane.Piece), "the shattering was not recorded for the other players");
        }

        [UnityTest]
        public IEnumerator ABladeShattersAPaneAtOnceAndABulletCracksItAndGoesOnThrough()
        {
            HousePane[] panes = MakeHouse().Built.GetComponentsInChildren<HousePane>();
            yield return null;
            panes[1].TakeBlow(panes[1].transform.position, Vector3.forward, BlowStrength.Heavy);
            Assert.IsTrue(panes[1].Shattered, "a blade did not shatter the pane at once");

            // A real shot's line through a pane: the pane takes the bullet, cracks, stays, and the shot carries on past it.
            HousePane target = panes[2];
            Vector3 outward = target.AlongLocalX ? target.transform.forward : target.transform.right;
            Vector3 across = target.AlongLocalX ? target.transform.right : target.transform.forward;
            Vector3 from = target.transform.position + across * (target.Size.x * 0.25f) + outward * 3.0f, direction = -outward;   // beside the mullion
            Physics.SyncTransforms();
            Assert.IsTrue(Physics.Raycast(from, direction, out RaycastHit hit, 10.0f), "the shot hit nothing");
            Assert.AreEqual(target.gameObject, hit.collider.gameObject, "the shot did not meet the pane first");
            GlassPenetration.Follow(ref hit, direction, 50.0f, true, (Vector3 o, float d, out RaycastHit next) => Physics.Raycast(o, direction, out next, d), out int panesThrough, out _);
            Assert.AreEqual(1, panesThrough, "the bullet did not go on through the pane");
            Assert.IsTrue(target.Cracked && !target.Shattered, "a bullet should crack the pane and leave it standing");
        }

        [UnityTest]
        public IEnumerator BlowsTearThePlanksOffAndWithAllThreeGoneTheFrontDoorOpensAndABodyWalksIn()
        {
            OldGuysHouseSite site = MakeHouse();
            BoardedPlank[] planks = site.Built.GetComponentsInChildren<BoardedPlank>();
            BoardedDoor door = site.Built.GetComponentInChildren<BoardedDoor>();
            Assert.AreEqual(House.FrontDoorPlanks, planks.Length, "the front door is not boarded with its planks");
            yield return null;

            // A sweep that meets a plank on several frames counts once.
            planks[0].TakeBlow(planks[0].transform.position, Vector3.zero, BlowStrength.Heavy);
            planks[0].TakeBlow(planks[0].transform.position, Vector3.zero, BlowStrength.Heavy);
            Assert.AreEqual(BoardedPlank.Health - 2, planks[0].Left, "one sweep of a blade counted twice");

            foreach (BoardedPlank plank in planks)
            {
                for (int blow = 0; blow < BoardedPlank.Health && !plank.Broken; blow++)
                {
                    Assert.IsFalse(door.Open, "the door opened with a plank still nailed across it");
                    Assert.IsTrue(plank.TakePunch(plank.transform.position), "a punch at a plank did not connect");
                    yield return new WaitForSeconds(BoardedPlank.BlowSeconds + 0.05f);
                }
                Assert.IsTrue(plank.Broken, "three punches did not tear a plank off");
                // Fallen, it is debris a body walks through, wherever it lands (one on its edge in the doorway used to stop the walk in).
                Assert.AreEqual(CityX.World.GlassFracture.ShardLayer, plank.gameObject.layer, "a torn-off plank still blocks bodies");
            }
            Assert.IsTrue(door.Open, "the door did not open once the planks were gone");
            yield return new WaitForSeconds(BoardedDoor.OpenSeconds + 0.2f);

            // In off the porch, through the doorway, into the entry hall.
            float l1 = House.LevelY(1);
            var body = new GameObject("Body");
            _made.Add(body);
            body.transform.position = site.transform.TransformPoint(new Vector3(31.0f, l1 + 0.1f, 9.7f));
            var controller = body.AddComponent<CharacterController>();
            controller.height = BodyRig.StandingHeight;
            controller.radius = FusedBodyShape.TorsoRadius;
            controller.center = new Vector3(0, BodyRig.StandingHeight * 0.5f, 0);
            controller.stepOffset = 0.3f;
            Physics.SyncTransforms();
            Vector3 inside = site.transform.TransformPoint(new Vector3(24.0f, l1, 9.7f));
            for (float t = 0; t < 6.0f; t += 1.0f / 60.0f)
            {
                Vector3 to = inside - body.transform.position;
                to.y = 0;
                if (to.magnitude < 0.4f) break;
                controller.Move((to.normalized * 2.0f + Vector3.down * 3.0f) * (1.0f / 60.0f));
                yield return null;
            }
            Vector3 p = body.transform.position;
            Assert.Less(new Vector2(p.x - inside.x, p.z - inside.z).magnitude, 0.8f, $"the body did not get in through the front door, it stopped at {site.transform.InverseTransformPoint(p)}");
            Assert.AreEqual("Entry hall", House.RoomAt(1, site.transform.InverseTransformPoint(p).x, site.transform.InverseTransformPoint(p).z)?.Name);
        }

        [UnityTest]
        public IEnumerator APieceBrokenOnAnotherMachineBreaksHereAndAHouseBuiltLaterFindsItBroken()
        {
            OldGuysHouseSite site = MakeHouse();
            HousePane pane = site.Built.GetComponentsInChildren<HousePane>()[3];
            BoardedDoor door = site.Built.GetComponentInChildren<BoardedDoor>();
            yield return null;
            TutorialHouseDamage.Shared.Told(pane.Piece);
            Assert.IsTrue(pane.Shattered, "a pane broken on another machine stayed whole here");
            for (int i = 0; i < House.FrontDoorPlanks; i++) TutorialHouseDamage.Shared.Told(i);
            Assert.IsTrue(door.Open, "the planks torn off on another machine left the door shut here");

            // A player who joins now builds the house with those pieces already gone.
            OldGuysHouseSite later = MakeHouse();
            yield return null;
            yield return null;
            Assert.IsTrue(later.Built.GetComponentsInChildren<HousePane>()[3].Shattered, "a later house has the broken pane whole");
            Assert.AreEqual(0, later.Built.GetComponentsInChildren<BoardedPlank>().Length, "a later house has the torn-off planks back");
            Assert.IsTrue(later.Built.GetComponentInChildren<BoardedDoor>().Open, "a later house has the front door shut");
        }
    }
}
