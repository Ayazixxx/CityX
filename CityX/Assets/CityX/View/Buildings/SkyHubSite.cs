using CityX.World.Plan;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// The sky hub in the world (docs/SKY_HUB.md): stood by the streamer where the plan keeps it (SkyHubPlan), built whole when the world is made and
    /// kept, as Old Guy's house is. A pure function of the world seed, so nothing about it is sent between machines.
    /// </summary>
    public sealed class SkyHubSite : MonoBehaviour
    {
        [Tooltip("The city's wall material (CityConcrete): every surface of the hub is tinted in it.")]
        public Material Structure;

        public GameObject Built { get; private set; }

        /// <summary>Puts the hub where the plan says, built afresh: the island's middle, level with the stair's foot.</summary>
        public void PlaceOn(in SkyHubPlan hub)
        {
            Clear();
            transform.SetPositionAndRotation(new Vector3((float)hub.CentreX, (float)hub.FootHeight, (float)hub.CentreZ), Quaternion.identity);
            Built = SkyHubBuilder.Build(transform, Structure, hub);
            Physics.SyncTransforms();
        }

        private void OnDestroy()
        {
            Clear();
        }

        private void Clear()
        {
            if (Built == null)
            {
                return;
            }
            foreach (MeshFilter filter in Built.GetComponentsInChildren<MeshFilter>(true))
            {
                if (Application.isPlaying) Destroy(filter.sharedMesh); else DestroyImmediate(filter.sharedMesh);
            }
            if (Application.isPlaying) Destroy(Built); else DestroyImmediate(Built);
            Built = null;
        }
    }
}
