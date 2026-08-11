using System.Collections.Generic;
using CityX.World;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Keeps a crowd around the viewer as they walk the city.
    ///
    /// RECYCLES A FIXED POOL rather than spawning and destroying. A pedestrian
    /// is a full character rig -- skinned meshes, an animator, eleven rigid
    /// bodies and their joints -- and building one costs far more than moving
    /// one. In a streamed world the player is constantly leaving some behind
    /// and needing others ahead, so a spawn-and-destroy crowd would allocate a
    /// rig every few seconds forever. The pool is built once and its members
    /// are teleported.
    ///
    /// A civilian that dies is left where it fell until it is far enough away
    /// to recycle. Bodies that vanish while you are looking at them are worse
    /// than bodies that persist.
    /// </summary>
    public sealed class CivilianPopulation : MonoBehaviour
    {
        [Tooltip("The prefab built by CivilianBuilder.")]
        public GameObject CivilianPrefab;

        [Tooltip("Whoever the crowd forms around. Falls back to the main camera.")]
        public Transform Viewer;

        [Tooltip("How many exist at once. They are all built at startup.")]
        public int Count = 12;

        [Tooltip("Metres. Pedestrians are placed within this of the viewer.")]
        public float SpawnRadius = 60.0f;

        [Tooltip("Metres. Beyond this a pedestrian is recycled to somewhere ahead.")]
        public float RecycleDistance = 110.0f;

        public int WorldSeed = 20260810;

        [Tooltip("Ground height comes from the terrain, so nobody spawns inside a hill.")]
        public bool UseTerrainHeight = true;

        private readonly List<Civilian> _crowd = new List<Civilian>();
        private TerrainField _terrain;
        private int _nextSeed;

        public IReadOnlyList<Civilian> Crowd => _crowd;

        private void Start()
        {
            if (CivilianPrefab == null)
            {
                Debug.LogWarning("CityX: no civilian prefab -- the streets will be empty.");
                return;
            }

            _terrain = new TerrainField(WorldSeed);
            Transform viewer = ResolveViewer();
            Vector3 centre = viewer != null ? viewer.position : transform.position;

            for (int i = 0; i < Count; i++)
            {
                GameObject instance = Instantiate(CivilianPrefab, transform);
                instance.name = $"Civilian {i}";
                var civilian = instance.GetComponent<Civilian>();
                _crowd.Add(civilian);
                Place(civilian, centre, i);
            }
        }

        private void Update()
        {
            Transform viewer = ResolveViewer();
            if (viewer == null || _crowd.Count == 0)
            {
                return;
            }

            Vector3 centre = viewer.position;
            float recycleSquared = RecycleDistance * RecycleDistance;

            // ONE PER FRAME. Checking the whole crowd every frame is cheap now
            // and stops being cheap at a few hundred, and there is no reason
            // for a pedestrian to be recycled the exact frame they cross the
            // line -- they are, by definition, too far away to look at.
            int index = Time.frameCount % _crowd.Count;
            Civilian civilian = _crowd[index];
            if (civilian == null)
            {
                return;
            }

            Vector3 offset = civilian.transform.position - centre;
            offset.y = 0.0f;
            if (offset.sqrMagnitude > recycleSquared)
            {
                Place(civilian, centre, index);
            }
        }

        private void Place(Civilian civilian, Vector3 centre, int index)
        {
            Vector3 spot = StreetNetwork.SpawnPointNear(
                centre, _nextSeed + index, WorldSeed, SpawnRadius);

            if (UseTerrainHeight && _terrain != null)
            {
                int chunkX = Mathf.FloorToInt(spot.x / CityLayout.ChunkSize);
                int chunkY = Mathf.FloorToInt(spot.z / CityLayout.ChunkSize);
                // A little above the pad so the controller settles onto the
                // road rather than starting inside it.
                spot.y = _terrain.PadHeightFor(chunkX, chunkY) + 0.5f;
            }
            else
            {
                spot.y = centre.y + 0.5f;
            }

            civilian.Restart(_nextSeed + index, spot);
            _nextSeed += _crowd.Count;
        }

        private Transform ResolveViewer()
        {
            if (Viewer != null)
            {
                return Viewer;
            }
            return Camera.main != null ? Camera.main.transform : null;
        }
    }
}
