using CityX.Character;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// Builds the hanging ragdoll dummy: a real body hanging from a rope
    /// tied off on an actual nearby building's own FACADE, permanently
    /// limp from the moment the scene starts. See HangingRagdollDummy's
    /// own class doc comment for why this exists -- a live reference for
    /// whether Ragdoll.cs itself is stable, walkable up to in the actual
    /// game rather than only checkable through a diagnostic render.
    ///
    /// EXPLICIT ASK, after a first version anchored to a short 3.4m pole
    /// "didnt really come out in a way thats helpful": "make the string
    /// attached to the top of a building... make it feel the same as the
    /// string used on the grapple gun" -- and, once that first rewrite
    /// anchored straight down off a ROOFTOP instead, "like just have him
    /// hanging down the side of the building. feet not touching the
    /// ground". A HORIZONTAL SEARCH, NOT A DOWNWARD ONE -- the same
    /// GrapplingHookItem.FindAnchor a real grapple shot already uses (a
    /// forward raycast hitting a wall), not a rooftop search at all.
    /// Confirmed directly, live: hanging straight down from the CENTRE of
    /// a rooftop puts the whole rope span inside the building's own solid
    /// footprint, which is what "down the SIDE of the building" actually
    /// asked to avoid -- a wall anchor with the body offset outward along
    /// the hit surface's own normal keeps the entire hang in the open
    /// street instead, which is also the literal same shape a real
    /// grapple anchor already is.
    ///
    /// REUSES Civilian.prefab WHOLESALE for the body -- same rig, same
    /// animator, same already-built Ragdoll component CivilianBuilder
    /// already wires up. The Civilian script itself (the wander AI) and
    /// its CharacterController are disabled rather than removed, since
    /// removing either mid-build risks disturbing component references
    /// RagdollBuilder/Ragdoll already wired onto the same GameObject.
    /// </summary>
    public static class HangingRagdollDummyBuilder
    {
        private const string CivilianPrefabPath = "Assets/CityX/Character/Civilian.prefab";

        /// <summary>
        /// How far the search reaches outward, and how high above
        /// <paramref name="near"/>'s own local terrace it tries -- both
        /// wide enough to clear a whole street intersection, not just one
        /// building lot. THE PLAYER'S OWN SPAWN SITS EXACTLY ON A 4-WAY
        /// STREET INTERSECTION -- confirmed directly: CityLayout.IsStreetChunk
        /// is true whenever a chunk's X OR Z index is divisible by
        /// CityLayout.StreetPeriod, and chunk (0,0) (spawn's own chunk)
        /// satisfies BOTH, meaning the entire chunk ROW and COLUMN through
        /// spawn are street the whole way out, not just spawn's own one
        /// chunk. A ray fired due +X/-X/+Z/-Z from spawn never leaves that
        /// street at any distance; only a ray angled off-axis ever reaches
        /// a real buildable chunk, and even then not until it clears at
        /// least one full ChunkSize. A single fixed search height fares no
        /// better: this city's terrain is deliberately terraced (see
        /// TerrainField.TerraceStep/TotalRelief), so a neighbouring
        /// buildable chunk's own pad can sit well above or below spawn's
        /// own -- trying several heights is what keeps the search from
        /// simply flying over or under a real nearby facade.
        /// </summary>
        private const float SearchDistance = 80.0f;

        private static readonly float[] SearchHeightsAboveNear = { 3.0f, 8.0f, 15.0f, 24.0f, 35.0f };

        /// <summary>
        /// A full 36-direction ring (every 10 degrees), not just the 8
        /// cardinal/diagonal compass points -- confirmed necessary
        /// directly: at spawn's own street intersection, only the
        /// off-axis diagonals ever leave the street at all, so finer
        /// angular resolution is what actually finds the nearest real
        /// building rather than skipping past it between two coarse
        /// diagonals. Fine enough that the CLOSEST hit found across the
        /// whole ring (see FindWallAnchor) is genuinely the closest real
        /// building, not just whichever one a coarse sample happened to
        /// graze first.
        /// </summary>
        private static readonly float[] SearchAngles = BuildSearchAngles();

        private static float[] BuildSearchAngles()
        {
            var angles = new float[36];
            for (int i = 0; i < angles.Length; i++)
            {
                angles[i] = i * 10.0f;
            }
            return angles;
        }

        /// <summary>
        /// How far down the wall the dummy hangs -- explicit ask: "just
        /// have him hanging down the side of the building. feet not
        /// touching the ground".
        /// </summary>
        private const float RopeLength = 6.0f;

        /// <summary>
        /// How far out from the wall's own surface the body hangs, along
        /// the hit's own normal -- enough that a limp, swaying ragdoll
        /// clears the facade without needing to know its exact shape.
        /// </summary>
        private const float StandoffFromWall = 1.4f;

        /// <summary>
        /// Builds the whole prop near <paramref name="spawnPosition"/> (the
        /// player's own spawn point) and parents it under
        /// <paramref name="parent"/>. Returns null, having logged, if no
        /// real wall with clear space in front of it could be found nearby.
        /// </summary>
        public static GameObject Build(Vector3 spawnPosition, Transform parent)
        {
            GameObject civilianPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CivilianPrefabPath);
            if (civilianPrefab == null)
            {
                Debug.LogError("CityX: run CityX/Rebuild Civilian first -- the hanging dummy shares its body.");
                return null;
            }

            if (!FindWallAnchor(spawnPosition, out Vector3 anchorPoint, out Vector3 outward))
            {
                Debug.LogError("CityX: no real wall with clear space in front of it found near spawn.");
                return null;
            }

            var root = new GameObject("HangingRagdollDummy");
            root.transform.SetParent(parent, false);
            // RopeLength below the anchor, StandoffFromWall out from the
            // wall's own surface -- clear of the building's own solid
            // volume in both directions a straight-down hang from a
            // rooftop centre could not guarantee at all.
            root.transform.position = anchorPoint - Vector3.up * RopeLength + outward * StandoffFromWall;

            var anchor = new GameObject("RopeAnchor");
            anchor.transform.SetParent(root.transform, false);
            anchor.transform.position = anchorPoint;

            // --- the body. Civilian.prefab already carries a fully built
            // --- Ragdoll (RagdollBuilder.Build already ran when it was
            // --- last rebuilt) -- nothing here builds a rig from scratch.
            var body = (GameObject)PrefabUtility.InstantiatePrefab(civilianPrefab, root.transform);
            body.name = "Dummy";
            body.transform.localPosition = Vector3.zero;

            Civilian civilian = body.GetComponent<Civilian>();
            if (civilian != null) civilian.enabled = false;
            CharacterController controller = body.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;

            Ragdoll ragdoll = body.GetComponent<Ragdoll>();
            if (ragdoll == null)
            {
                Debug.LogError("CityX: Civilian.prefab has no Ragdoll -- run CityX/Rebuild Civilian first.");
                return root;
            }

            Animator animator = body.GetComponentInChildren<Animator>();
            Transform rightLowerArm = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightLowerArm) : null;
            Transform rightHand = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            Rigidbody handBody = rightLowerArm != null ? rightLowerArm.GetComponent<Rigidbody>() : null;

            if (handBody == null || rightHand == null)
            {
                Debug.LogError("CityX: couldn't find the dummy's own right lower arm rigidbody -- nothing to hang.");
                return root;
            }

            // THE SAME MATERIAL THE REAL GRAPPLE ROPE USES, not a
            // separately tuned look-alike -- explicit ask: "make it feel
            // the same as the string used on the grapple gun". Ensure
            // returns the SAME shared asset GrapplingHookBuilder itself
            // already builds/reuses by this exact name.
            Material ropeMaterial = CharacterSkin.Ensure("GrappleRope", GrapplingHookItem.RopeColour);
            ropeMaterial.SetFloat("_Smoothness", 0.1f);

            HangingRagdollDummy dummy = root.AddComponent<HangingRagdollDummy>();
            dummy.Ragdoll = ragdoll;
            dummy.HandBody = handBody;
            dummy.HandBone = rightHand;
            dummy.AnchorPoint = anchor.transform;
            // Just under the real hand-to-anchor distance, matching the
            // working short-pole tuning's own "settle just shy of the
            // exact distance" shape rather than exactly on it, which is
            // what kept that version's settle calm.
            dummy.RopeLength = Vector3.Distance(root.transform.position, anchorPoint) * 0.98f;
            dummy.RopeMaterial = ropeMaterial;

            return root;
        }

        /// <summary>
        /// Casts forward in a ring of directions from
        /// <paramref name="near"/>, at several real heights above it, and
        /// returns the CLOSEST real wall with real clear space in front of
        /// it -- the same "aim outward and see what a real shot would
        /// hit" shape GrapplingHookItem.FindAnchor already uses for an
        /// actual grapple shot, not a rooftop search.
        ///
        /// EXPLICIT ASK after the first version landed 41m from spawn on a
        /// building the player had to go hunting for: "make it the
        /// closest building to spawn facing toward the player." Scans the
        /// WHOLE ring/height grid and keeps the nearest valid hit rather
        /// than returning the first one the iteration order happens to
        /// reach -- a coarse "first hit wins" search has no reason to
        /// prefer near over far, and confirmed directly, it didn't
        /// (landed on a building 47m away on the very first candidate
        /// tried, while a much closer one existed). "Facing toward the
        /// player" falls out of the search shape for free: every ray
        /// fires OUTWARD from spawn, so whatever face it hits first is
        /// necessarily the one looking back at spawn, never the far side
        /// of the building.
        ///
        /// THE STANDOFF POSITION IS WHAT ACTUALLY GETS VALIDATED, not the
        /// wall hit itself -- confirmed directly, live, on an earlier
        /// rooftop-based version: a query that starts already inside a
        /// collider reports no hit for that collider at all, so checking
        /// "is the wall's own surface clear" proves nothing about whether
        /// the body standing StandoffFromWall out from it is.
        /// </summary>
        private static bool FindWallAnchor(Vector3 near, out Vector3 anchorPoint, out Vector3 outward)
        {
            bool found = false;
            float bestDistanceSqr = float.MaxValue;
            Vector3 bestAnchor = Vector3.zero;
            Vector3 bestOutward = Vector3.zero;

            foreach (float height in SearchHeightsAboveNear)
            {
                Vector3 origin = near + Vector3.up * height;
                foreach (float angle in SearchAngles)
                {
                    Vector3 direction = Quaternion.Euler(0.0f, angle, 0.0f) * Vector3.forward;
                    if (!Physics.Raycast(origin, direction, out RaycastHit wallHit,
                            SearchDistance, ~0, QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }

                    Vector3 normal = wallHit.normal.sqrMagnitude > 0.01f ? wallHit.normal.normalized : -direction;
                    Vector3 standoffPosition = wallHit.point - Vector3.up * RopeLength + normal * StandoffFromWall;
                    if (Physics.CheckSphere(standoffPosition, 0.6f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }

                    // Flattened, not the raw ray distance -- the search
                    // fires from several different heights above `near`,
                    // and comparing straight-line ray lengths across those
                    // would bias toward whichever height happened to be
                    // closest to the wall vertically rather than which
                    // building is actually nearest on the ground.
                    Vector3 flatOffset = wallHit.point - near;
                    flatOffset.y = 0.0f;
                    float distanceSqr = flatOffset.sqrMagnitude;
                    if (distanceSqr < bestDistanceSqr)
                    {
                        bestDistanceSqr = distanceSqr;
                        bestAnchor = wallHit.point;
                        bestOutward = normal;
                        found = true;
                    }
                }
            }

            anchorPoint = bestAnchor;
            outward = bestOutward;
            return found;
        }
    }
}
