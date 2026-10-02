using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Walks a real body into a real building.
    ///
    /// THE GEOMETRY TESTS CANNOT ANSWER THIS. They prove the doorway is a hole
    /// of the right area and the slab is the footprint less its stairwell, and
    /// every one of those could be true of a building you still cannot get
    /// into: a doorway too low for the capsule, a lobby floor wound so the
    /// controller falls through it, a flight too steep for the step offset.
    /// "Enterable" is a claim about a body moving, so it is checked with one.
    ///
    /// The chunk is built and collided exactly as CityStreamer builds it --
    /// one MeshCollider over the same triangles the renderer draws -- because a
    /// purpose-built collision proxy would be testing something the game does
    /// not ship.
    ///
    /// EVERY BUILDING IS TURNED TO FACE ITS STREET, so none of this is along a world axis any more: the door is on the building's
    /// own +Z wall, and "in" is whichever way that is. Positions are worked out in the building's own frame and converted
    /// (BuildingSite.LocalToWorld / WorldToLocal).
    /// </summary>
    public class BuildingInteriorPlayTests
    {
        private const int Seed = 20260810;

        /// <summary>See PlayerLocomotionPlayTests: batch mode runs uncapped.</summary>
        private const float Step = 1.0f / 60.0f;

        private GameObject _chunks;
        private GameObject _player;
        private PlayerLocomotion _locomotion;
        private BuildingSite _site;
        private float _storey;

        [SetUp]
        public void SetUp()
        {
            var world = new CityWorld(Seed);
            _site = PickBuilding(world);
            Assert.IsTrue(_site.Exists, "no building near the spawn with level ground at its door");

            _storey = BuildingInterior.StoreyHeight(_site.Params.Height);

            // The chunk that builds the building, and the ones around it (they carry the street you walk up from).
            _chunks = new GameObject("Chunks");
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dy = -2; dy <= 2; dy++)
                {
                    BuildChunk(world, _site.OwnerChunkX + dx, _site.OwnerChunkY + dy);
                }
            }

            _player = new GameObject("Player");
            _player.AddComponent<CharacterController>();
            _locomotion = _player.AddComponent<PlayerLocomotion>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.Destroy(_player);
            if (_chunks != null)
            {
                foreach (MeshFilter filter in _chunks.GetComponentsInChildren<MeshFilter>())
                {
                    Object.Destroy(filter.sharedMesh);
                }
                Object.Destroy(_chunks);
            }
        }

        private Vector3 InBuilding(double localX, float y, double localZ)
        {
            _site.LocalToWorld(localX, localZ, out double wx, out double wz);
            return new Vector3((float)wx, y, (float)wz);
        }

        /// <summary>The way into the building, in the world: the opposite of the front wall's outward direction.</summary>
        private Vector3 WorldIn => new Vector3((float)_site.AxisZ, 0.0f, (float)-_site.AxisX);

        /// <summary>
        /// Walks in off the street. The doorway is on the building's front (+Z) wall, so getting inside means travelling
        /// the other way.
        /// </summary>
        [UnityTest]
        public IEnumerator WalkingAtTheDoorwayGetsYouInside()
        {
            PlaceAt(InBuilding(0.0, _site.BaseHeight + 0.2f, _site.Params.Depth * 0.5 + 1.2));
            yield return Settle();

            _site.WorldToLocal(_player.transform.position.x, _player.transform.position.z, out _, out double startZ);
            yield return Drive(WorldIn, 180);

            Vector3 at = _player.transform.position;
            _site.WorldToLocal(at.x, at.z, out double localX, out double localZ);
            Assert.Less(localZ, _site.Params.Depth * 0.5 - 0.5,
                $"walked at the doorway for three seconds and got from {startZ:0.00} to "
                + $"{localZ:0.00}, with the wall at {_site.Params.Depth * 0.5:0.00} -- the door is not open");
            Assert.Less(System.Math.Abs(localX), _site.Params.Width * 0.5, "left the building sideways");
            Assert.AreEqual(_site.BaseHeight, at.y, 0.15f,
                "inside the building but not standing on its lobby floor");
        }

        /// <summary>
        /// The lobby holds a body up. A slab wound the wrong way is solid to
        /// the renderer's silhouette and invisible to nothing -- but a floor
        /// that is simply missing drops you down the inside of the podium, and
        /// from the street the two look identical.
        /// </summary>
        [UnityTest]
        public IEnumerator TheLobbyFloorHoldsABodyUp()
        {
            PlaceAt(new Vector3((float)_site.CentreX, _site.BaseHeight + 2.5f, (float)_site.CentreZ));
            yield return Settle();

            Assert.IsTrue(_locomotion.IsGrounded, "never landed on anything");
            Assert.AreEqual(_site.BaseHeight, _player.transform.position.y, 0.15f,
                $"came to rest at {_player.transform.position.y:0.00} rather than the lobby "
                + $"floor at {_site.BaseHeight:0.00}");
        }

        /// <summary>
        /// THE FLIGHT IS CLIMBABLE, which is the one thing the port genuinely
        /// changed rather than carried across. city_chunk.gd collides a
        /// staircase as a single sloped ramp box and draws the steps on top of
        /// it; here the chunk's own MeshCollider means the steps ARE the
        /// collision, so whether a body gets up them is a question about the
        /// riser height against the controller's step offset, and it has an
        /// answer only when something walks it.
        /// </summary>
        [UnityTest]
        public IEnumerator TheStairsCarryABodyUpAStorey()
        {
            StairPlan plan = BuildingStairs.PlanFor(_site).Floors[0];
            Assert.IsNotNull(plan, "the lobby has a stair");
            Vector2[] route = plan.Route();

            // Start in the cell you step on from.
            PlaceAt(InBuilding(route[0].x, _site.BaseHeight + 0.3f, route[0].y));
            yield return Settle();

            float startY = _player.transform.position.y;
            int next = 1;
            for (int tick = 0; tick < 2400 && next < route.Length; tick++)
            {
                Vector3 here = _player.transform.position;
                Vector3 target = InBuilding(route[next].x, here.y, route[next].y);
                Vector3 toward = target - here;
                toward.y = 0.0f;
                if (toward.magnitude < 0.6f)
                {
                    next++;
                    continue;
                }
                var input = new LocomotionInput
                {
                    Move = new Vector2(0.0f, 1.0f),
                    YawDegrees = Mathf.Atan2(toward.x, toward.z) * Mathf.Rad2Deg,
                };
                _locomotion.Tick(input, Step);
                yield return null;
            }

            float climbed = _player.transform.position.y - startY;
            Assert.Greater(climbed, _storey - 0.4f,
                $"walked up the flight for eight seconds and gained {climbed:0.00}m of a "
                + $"{_storey:0.00}m storey -- the steps are catching the capsule");
            Assert.Less(climbed, _storey * 2.0f,
                "gained more than a storey, so this did not stop at the floor above");
        }

        /// <summary>
        /// Puts the body somewhere, WITH THE CONTROLLER SWITCHED OFF AROUND IT.
        ///
        /// A CharacterController keeps its own copy of where it is and only
        /// reads the transform back when it is enabled, so writing
        /// transform.position under a live controller is quietly ignored: the
        /// first attempt at these tests placed the body 143m away and then
        /// watched it walk, and fall, from the origin instead.
        /// </summary>
        private void PlaceAt(Vector3 position)
        {
            var controller = _player.GetComponent<CharacterController>();
            controller.enabled = false;
            _player.transform.position = position;
            controller.enabled = true;
        }

        /// <summary>
        /// Lets gravity do its work before anything is measured.
        /// </summary>
        private IEnumerator Settle()
        {
            for (int i = 0; i < 90; i++)
            {
                _locomotion.Tick(default, Step);
                yield return null;
            }
        }

        private IEnumerator Drive(Vector3 worldDirection, int steps)
        {
            // Move is relative to YawDegrees, so facing the body the way it
            // should travel and pushing straight forward keeps this readable.
            var input = new LocomotionInput
            {
                Move = new Vector2(0.0f, 1.0f),
                YawDegrees = Mathf.Atan2(worldDirection.x, worldDirection.z) * Mathf.Rad2Deg,
            };
            for (int i = 0; i < steps; i++)
            {
                _locomotion.Tick(input, Step);
                yield return null;
            }
        }

        /// <summary>
        /// A building with room to walk about in and level ground at its door. A step outside the entrance would make this
        /// a test of whether a body can climb a kerb.
        /// </summary>
        private static BuildingSite PickBuilding(CityWorld world)
        {
            foreach (BuildingSite site in TestSites.NearSpawn(world))
            {
                if (site.Params.Width < 14.0f || site.Params.Depth < 14.0f)
                {
                    continue;
                }
                if (Mathf.Abs(site.BaseHeight - site.DoorGroundHeight) > 0.12f)
                {
                    continue;
                }
                return site;
            }
            return BuildingSite.None;
        }

        /// <summary>
        /// One chunk, in world coordinates, collided the way CityStreamer
        /// collides it: a single MeshCollider over every triangle drawn.
        /// </summary>
        private void BuildChunk(CityWorld world, int chunkX, int chunkY)
        {
            var ground = new MeshBuffer();
            var pavement = new MeshBuffer();
            var structures = new MeshBuffer();
            CityMeshBuilder.BuildChunk(world, chunkX, chunkY, Vector3.zero,
                ground, pavement, structures);

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            foreach (MeshBuffer buffer in new[] { ground, pavement, structures })
            {
                int offset = vertices.Count;
                vertices.AddRange(buffer.Vertices);
                foreach (int index in buffer.Triangles)
                {
                    triangles.Add(index + offset);
                }
            }
            if (triangles.Count == 0)
            {
                return;
            }

            var mesh = new Mesh
            {
                name = $"chunk-{chunkX}-{chunkY}",
                indexFormat = vertices.Count > 65000
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16,
            };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            var go = new GameObject(mesh.name);
            go.transform.SetParent(_chunks.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }
    }
}
