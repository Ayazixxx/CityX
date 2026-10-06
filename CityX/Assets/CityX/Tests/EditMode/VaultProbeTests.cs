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
    }
}
