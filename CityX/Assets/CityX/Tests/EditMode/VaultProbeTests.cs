using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The vault validator on plain colliders. The player stands at the origin facing +Z; an obstacle's near face is at z = 0.5.
    /// A vault needs a waist-to-chest-high, flat, thin top with clear ground beyond; every look-alike (a step, a climb, a building, a boxed-in gap)
    /// must be refused.
    /// </summary>
    public sealed class VaultProbeTests
    {
        private readonly System.Collections.Generic.List<GameObject> _made = new System.Collections.Generic.List<GameObject>();
        private const float Height = 2.6f;
        private const float Radius = 0.3f;

        [SetUp]
        public void SetUp()
        {
            Box(new Vector3(0.0f, -0.5f, 0.0f), new Vector3(100.0f, 1.0f, 100.0f));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made) Object.DestroyImmediate(go);
            _made.Clear();
        }

        private GameObject Box(Vector3 centre, Vector3 size)
        {
            var go = new GameObject("Box");
            go.transform.position = centre;
            go.AddComponent<BoxCollider>().size = size;
            _made.Add(go);
            Physics.SyncTransforms();
            return go;
        }

        /// <summary>An obstacle from the ground to <paramref name="top"/>, <paramref name="thickness"/> deep, 10 m wide, near face at z = 0.5.</summary>
        private void Obstacle(float top, float thickness)
        {
            Box(new Vector3(0.0f, top * 0.5f, 0.5f + thickness * 0.5f), new Vector3(10.0f, top, thickness));
        }

        private static bool Find(Vector3 forward, out VaultProbe.Vault vault)
        {
            return VaultProbe.TryFind(Vector3.zero, forward, Height, Radius, null, out vault);
        }

        /// <summary>A wall 0.4 m thick (near face z = 0.5) with a window in it: solid up to the sill and from the lintel up; the room's floor behind it at ground level.</summary>
        private void WindowWall(float sill, float lintel)
        {
            Box(new Vector3(0.0f, sill * 0.5f, 0.7f), new Vector3(10.0f, sill, 0.4f));
            Box(new Vector3(0.0f, lintel + 3.0f, 0.7f), new Vector3(10.0f, 6.0f, 0.4f));
        }

        [Test]
        public void AShatteredWindowIsVaultedThroughEvenUnderItsLintel()
        {
            // A punched window as the city builds them: the sill 0.9 m up, the opening 1.86 m tall. There is no room to stand on the sill, but there is room to dive.
            WindowWall(0.9f, 2.76f);
            Assert.IsTrue(Find(Vector3.forward, out VaultProbe.Vault vault), "a shattered window was not vaultable (the owner: 'you cant vault into a window')");
            Assert.AreEqual(0.9f, vault.Height, 0.05f, "the sill is the top");
            Assert.AreEqual(1.86f, vault.Headroom, 0.12f, "the free height under the lintel was not measured");
            Assert.IsTrue(VaultProbe.IsThroughAnOpening(vault, Height), "a window under its lintel is an opening");
            Assert.Greater(vault.Land.z, 0.9f, "the landing is inside the room, past the wall");
            Assert.AreEqual(0.0f, vault.Land.y, 0.05f);
            float dive = PlayerVaulting.DiveFor(vault.Headroom, Height, out float apexAboveTop);
            Assert.Greater(dive, 0.5f, "a window lower than the body should be mostly a dive");
            // The hips (half a body up the path) and the head over them fit under the lintel.
            float hips = vault.Height + apexAboveTop + Height * 0.5f;
            float lean = Mathf.Lerp(PlayerVaulting.LeanDegrees, PlayerVaulting.DiveLeanDegrees, dive);
            float headTop = hips + Height * 0.5f * Mathf.Cos(lean * Mathf.Deg2Rad) + FusedBodyShape.HeadRadius;
            Assert.Less(headTop, vault.Height + vault.Headroom + 0.02f, "the head would go through the lintel");
            Assert.Greater(hips - 0.35f, vault.Height, "the body would drag through the sill");
        }

        [Test]
        public void AnIntactWindowIsNotVaultedAndNeitherIsOneTooLowToDiveThrough()
        {
            WindowWall(0.9f, 2.76f);
            Box(new Vector3(0.0f, 1.83f, 0.7f), new Vector3(10.0f, 1.86f, 0.05f));   // the glass, still whole
            Assert.IsFalse(Find(Vector3.forward, out _), "a whole pane of glass was vaulted through");
            TearDown();
            SetUp();
            WindowWall(0.9f, 1.6f);   // a letterbox: 0.7 m of air over the sill
            Assert.IsFalse(Find(Vector3.forward, out _), "an opening too low to dive through was accepted");
        }

        [Test]
        public void AFenceHasAWholeBodysWorthOfAirOverItAndIsNoDive()
        {
            Obstacle(1.4f, 0.4f);
            Assert.IsTrue(Find(Vector3.forward, out VaultProbe.Vault vault));
            Assert.GreaterOrEqual(vault.Headroom, Height - 0.01f, "open sky over a fence should read as a whole body's worth of air");
            Assert.IsFalse(VaultProbe.IsThroughAnOpening(vault, Height), "a fence under open sky is not an opening");
            Assert.AreEqual(0.0f, PlayerVaulting.DiveFor(vault.Headroom, Height, out float apexAboveTop), 1e-4f, "a fence became a dive");
            Assert.AreEqual(PlayerVaulting.ClearanceMetres, apexAboveTop, 1e-4f);
        }

        [Test]
        public void AWaistHighThinWallIsAVaultWithLandingBeyondIt()
        {
            Obstacle(1.4f, 0.4f);
            Assert.IsTrue(Find(Vector3.forward, out VaultProbe.Vault vault), "a low thin wall was not vaultable");
            Assert.AreEqual(1.4f, vault.Height, 0.05f);
            Assert.Greater(vault.Land.z, 0.9f, "the landing must be past the far side");
            Assert.AreEqual(0.0f, vault.Land.y, 0.05f);
            Assert.AreEqual(0.5f, vault.NearEdge.z, 0.05f);
        }

        [Test]
        public void AKneeHighObstacleBelowTheWaistIsVaultable()
        {
            // The waist-height ray the first version used missed anything lower than the waist: "it only vaults objects this high sometimes".
            Obstacle(0.75f, 0.5f);
            Assert.IsTrue(Find(Vector3.forward, out VaultProbe.Vault vault), "a knee-high obstacle was not vaultable");
            Assert.AreEqual(0.75f, vault.Height, 0.05f);
        }

        [Test]
        public void ARoadsideBoxAsTallAsTheChestAndAMetreDeepIsVaultable()
        {
            Obstacle(1.9f, 1.0f);   // a utility box: 1.9 m tall, 1 m deep
            Assert.IsTrue(Find(Vector3.forward, out VaultProbe.Vault vault), "a 1.9 m, 1 m deep box was not vaultable");
            Assert.Greater(vault.Land.z, 1.5f);
        }

        [Test]
        public void ANarrowBoxIsVaultedEvenWhenRunningAtItsEdge()
        {
            // "It only vaults when I look right at the edge": a thin ray found a narrow obstacle only when aimed at it. A body-wide sweep finds it.
            Box(new Vector3(0.0f, 0.9f, 1.0f), new Vector3(0.8f, 1.8f, 0.6f));    // 0.8 m wide, 1.8 m tall, face at z = 0.7
            foreach (float x in new[] { -0.45f, -0.3f, 0.0f, 0.3f, 0.45f })
            {
                Assert.IsTrue(VaultProbe.TryFind(new Vector3(x, 0.0f, 0.0f), Vector3.forward, Height, Radius, null, out _),
                    $"running at the box from {x:F2} m to one side was not vaultable");
            }
        }

        [Test]
        public void AnObstacleBeingRunAtNotQuiteHeadOnIsStillVaulted()
        {
            Obstacle(1.5f, 0.5f);
            foreach (float degrees in new[] { -50.0f, -35.0f, -20.0f, 0.0f, 20.0f, 35.0f, 50.0f })
            {
                Vector3 direction = Quaternion.Euler(0.0f, degrees, 0.0f) * Vector3.forward;
                Assert.IsTrue(Find(direction, out _), $"running at the fence {degrees} degrees off square was not vaultable");
            }
        }

        [Test]
        public void AnAngledApproachStillFindsTheObstacle()
        {
            Obstacle(1.4f, 0.4f);
            Vector3 angled = Quaternion.Euler(0.0f, 35.0f, 0.0f) * Vector3.forward;
            Assert.IsTrue(Find(angled, out _), "running at the fence 35 degrees off square was not vaultable");
        }

        [Test]
        public void AStepTooLowToBotherWithIsNotAVault()
        {
            Obstacle(0.4f, 0.4f);
            Assert.IsFalse(Find(Vector3.forward, out _), "a kerb-height step was vaulted");
        }

        [Test]
        public void ATopTooHighIsAClimbNotAVault()
        {
            Obstacle(2.4f, 0.4f);
            Assert.IsFalse(Find(Vector3.forward, out _), "a head-high wall was vaulted");
        }

        [Test]
        public void AThickBlockIsNotVaulted()
        {
            Obstacle(1.4f, 4.0f);
            Assert.IsFalse(Find(Vector3.forward, out _), "a building-sized block was vaulted");
        }

        [Test]
        public void AnObstacleWithAWallRightBehindItHasNowhereToLand()
        {
            Obstacle(1.4f, 0.4f);
            Box(new Vector3(0.0f, 5.0f, 1.5f), new Vector3(10.0f, 10.0f, 0.4f));   // a tall wall 0.8 m behind the fence
            Assert.IsFalse(Find(Vector3.forward, out _), "vaulted into a boxed-in gap");
        }

        [Test]
        public void ARunningPastTheSideOfAnObstacleIsNotAVault()
        {
            Obstacle(1.4f, 0.4f);
            Assert.IsFalse(Find(Vector3.right, out _), "faced away from it but still vaulted");
        }

        [Test]
        public void AThinPoleIsNotVaultable()
        {
            Box(new Vector3(0.0f, 0.7f, 0.65f), new Vector3(0.05f, 1.4f, 0.05f));
            Assert.IsFalse(Find(Vector3.forward, out _), "a pole has no flat top to put hands on");
        }

        [Test]
        public void ThePathKeepsTheFeetAboveTheObstacleWhileTheyAreOverIt()
        {
            Obstacle(1.4f, 0.8f);
            Assert.IsTrue(Find(Vector3.forward, out VaultProbe.Vault vault));
            for (float u = 0.0f; u <= 1.0f; u += 0.01f)
            {
                Vector3 p = PlayerVaulting.Path(Vector3.zero, vault, u);
                // The capsule's radius counts: any part of the feet over the obstacle's footprint must be above its top.
                bool overObstacle = p.z + Radius > vault.NearEdge.z + 0.02f && p.z - Radius < vault.FarEdge.z - 0.02f;
                if (overObstacle)
                {
                    Assert.GreaterOrEqual(p.y, vault.Height - 0.02f, $"feet inside the obstacle at u={u:F2} (z={p.z:F2}, y={p.y:F2})");
                }
            }
            Assert.AreEqual(vault.Land.z, PlayerVaulting.Path(Vector3.zero, vault, 1.0f).z, 0.001f);
            Assert.AreEqual(vault.Land.y, PlayerVaulting.Path(Vector3.zero, vault, 1.0f).y, 0.001f);
        }

        /// <summary>
        /// A window as the city builds them (CityMeshBuilder.AddWindowReveals): a wall 0.3 m thick, near face at z = 0.5; the sill across its
        /// thickness with a stone ledge under it standing 8 cm proud of the wall (a sweep catches the ledge before the wall under it); jambs either
        /// side of a <paramref name="width"/> opening; the lintel <paramref name="opening"/> over the sill; the room's floor <paramref name="floor"/>
        /// over the street; with <paramref name="glass"/>, the pane 14 cm in from the face, whole.
        /// </summary>
        private void CityWindow(float sill, float opening, float width, float floor, bool glass = false)
        {
            const float wall = 0.3f;
            const float span = 12.0f;
            float lintel = sill + opening;
            float z = 0.5f + wall * 0.5f;
            Box(new Vector3(0.0f, sill * 0.5f, z), new Vector3(span, sill, wall));
            Box(new Vector3(0.0f, lintel + 3.0f, z), new Vector3(span, 6.0f, wall));
            float pier = (span - width) * 0.5f;
            Box(new Vector3(-(width + pier) * 0.5f, sill + opening * 0.5f, z), new Vector3(pier, opening, wall));
            Box(new Vector3((width + pier) * 0.5f, sill + opening * 0.5f, z), new Vector3(pier, opening, wall));
            Box(new Vector3(0.0f, sill - 0.035f, 0.46f), new Vector3(width + 0.1f, 0.07f, 0.08f));
            if (floor > 0.0f)
            {
                Box(new Vector3(0.0f, floor * 0.5f, 0.8f + 5.0f), new Vector3(span, floor, 10.0f));
            }
            if (glass)
            {
                Box(new Vector3(0.0f, sill + opening * 0.5f, 0.64f), new Vector3(width, opening, 0.02f));
            }
        }

        private static bool Find(Vector3 feet, Vector3 forward, out VaultProbe.Vault vault, out VaultProbe.Refusal why)
        {
            return VaultProbe.TryFind(feet, forward, Height, Radius, null, out vault, out why);
        }

        [Test]
        public void AWindowIntoARoomStandingHigherThanTheStreetIsVaulted()
        {
            // Many of the city's buildings stand higher than the street beside them, so past the sill the room's floor is inside the vault band too: it
            // read as the top stepping down part-way and the window was refused (owner, 2026-10-14: "isnt working on most windows").
            CityWindow(sill: 1.5f, opening: 1.86f, width: 1.15f, floor: 0.6f);
            Assert.IsTrue(Find(Vector3.zero, Vector3.forward, out VaultProbe.Vault vault, out VaultProbe.Refusal why), $"refused: {why}");
            Assert.AreEqual(1.5f, vault.Height, 0.05f, "the sill is the top");
            Assert.AreEqual(0.6f, vault.Land.y, 0.05f, "the feet land on the room's floor");
            Assert.Greater(vault.Land.z, 0.8f, "the landing is inside the room");
        }

        [Test]
        public void AWindowIsGoneThroughItsMiddleWhereverAlongItTheWallIsRunAt()
        {
            // The way through used to be checked wherever the body met the wall, a hand's width from a jamb as often as not.
            CityWindow(sill: 1.0f, opening: 1.86f, width: 1.15f, floor: 0.0f);
            float limit = 1.15f * 0.5f - VaultProbe.JambClearanceMetres + 0.01f;
            foreach (float x in new[] { -0.35f, -0.2f, 0.0f, 0.2f, 0.35f })
            foreach (float degrees in new[] { -25.0f, 0.0f, 25.0f })
            foreach (float back in new[] { 0.0f, -0.35f })
            {
                Vector3 run = Quaternion.Euler(0.0f, degrees, 0.0f) * Vector3.forward;
                Assert.IsTrue(Find(new Vector3(x, 0.0f, back), run, out VaultProbe.Vault vault, out VaultProbe.Refusal why),
                    $"running at the window {x:+0.00;-0.00} m off its middle, {degrees} degrees off square, from {0.5f - back:0.00} m out: {why}");
                Assert.LessOrEqual(Mathf.Abs(vault.NearEdge.x), limit, $"the way over at {vault.NearEdge.x:0.00} is not clear of the jambs");
                Assert.LessOrEqual(Mathf.Abs(vault.Land.x), limit, "the landing is not in line with the middle of the window");
            }
        }

        [Test]
        public void TheStoneLedgeUnderASillDoesNotHideTheWallUnderIt()
        {
            // The ledge sticks out over the wall: the sweep at knee height met its underside (an edge, its normal half down) before the wall, and threw the sighting away.
            CityWindow(sill: 0.9f, opening: 1.86f, width: 1.15f, floor: 0.0f);
            Assert.IsTrue(Find(Vector3.zero, Vector3.forward, out VaultProbe.Vault vault, out VaultProbe.Refusal why), $"refused: {why}");
            Assert.AreEqual(0.9f, vault.Height, 0.05f);
            Assert.Less(vault.Headroom, Height, "the lintel over the window was not seen");
        }

        [Test]
        public void ALowSillUnderATallOpeningIsVaulted()
        {
            // A ribbon window: the sill only half a metre up, so the lowest sweep used to pass over it into the room and find nothing to vault.
            CityWindow(sill: 0.5f, opening: 2.9f, width: 2.3f, floor: 0.0f);
            Assert.IsTrue(Find(Vector3.zero, Vector3.forward, out VaultProbe.Vault vault, out VaultProbe.Refusal why), $"refused: {why}");
            Assert.AreEqual(0.5f, vault.Height, 0.05f);
        }

        [Test]
        public void APlinthAlongTheFootOfTheWallDoesNotHideTheWindowAboveIt()
        {
            // The nearest face is the plinth, a step with the sill rising behind it: not a vault. The wall under the sill, further on, is.
            CityWindow(sill: 1.0f, opening: 1.86f, width: 1.15f, floor: 0.0f);
            Box(new Vector3(0.0f, 0.15f, 0.35f), new Vector3(12.0f, 0.3f, 0.3f));
            Assert.IsTrue(Find(Vector3.zero, Vector3.forward, out VaultProbe.Vault vault, out VaultProbe.Refusal why), $"refused: {why}");
            Assert.AreEqual(1.0f, vault.Height, 0.05f, "the sill is the top, not the plinth");
        }

        [Test]
        public void AWholeCityWindowIsNotVaultedFromAnyApproach()
        {
            foreach (float floor in new[] { 0.0f, 0.6f })
            {
                TearDown();
                SetUp();
                CityWindow(sill: 1.0f + floor, opening: 1.86f, width: 1.15f, floor: floor, glass: true);
                foreach (float x in new[] { -0.35f, 0.0f, 0.35f })
                foreach (float degrees in new[] { -25.0f, 0.0f, 25.0f })
                foreach (float back in new[] { 0.0f, -0.35f })
                {
                    Vector3 run = Quaternion.Euler(0.0f, degrees, 0.0f) * Vector3.forward;
                    Assert.IsFalse(Find(new Vector3(x, 0.0f, back), run, out _, out VaultProbe.Refusal why),
                        $"a WHOLE pane was vaulted through (floor {floor} m up, {x:+0.00;-0.00} m off its middle, {degrees} degrees off square)");
                    Assert.AreEqual(VaultProbe.Refusal.OpeningBlocked, why, "the pane is what should refuse it");
                }
            }
        }

        [Test]
        public void ARefusalSaysWhichStageTurnedItDown()
        {
            Assert.IsFalse(Find(Vector3.zero, Vector3.forward, out _, out VaultProbe.Refusal why));
            Assert.AreEqual(VaultProbe.Refusal.NoFace, why, "open ground");
            Obstacle(2.4f, 0.4f);
            Assert.IsFalse(Find(Vector3.zero, Vector3.forward, out _, out why));
            Assert.AreEqual(VaultProbe.Refusal.NoTop, why, "a wall too high to vault");
            TearDown();
            SetUp();
            Obstacle(1.4f, 0.4f);
            Box(new Vector3(0.0f, 5.0f, 1.5f), new Vector3(10.0f, 10.0f, 0.4f));
            Assert.IsFalse(Find(Vector3.zero, Vector3.forward, out _, out why));
            Assert.AreEqual(VaultProbe.Refusal.NoRoomToLand, why, "a fence with a wall right behind it");
        }
    }
}
