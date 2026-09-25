using CityX.CharacterEditor;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Builds the jetpack prefab -- two tanks on a backplate, a downward
    /// flame ParticleSystem, and a looping thrust AudioSource, all
    /// procedural except the audio clip itself (matching BottleBuilder/
    /// GrapplingHookBuilder's own reasoning: no glTF importer exists in
    /// this project for a hand-authored jetpack mesh, so nothing here waits
    /// on one). No Godot source to port numbers from -- see Jetpack.cs's
    /// own doc comment.
    /// </summary>
    public static class JetpackBuilder
    {
        private const string PrefabPath = "Assets/CityX/Weapons/Jetpack.prefab";

        /// <summary>
        /// WAV, not the original MP3 -- EXPLICIT BUG REPORT: "the jetpack
        /// has been sounding choppy for awhile." The original
        /// jetpack_thrust_loop.mp3 (0.86s) had no seamless loop point at
        /// all -- MP3 encoding also pads real, measured leading silence
        /// (LAME encoder delay, confirmed directly building the wind loop
        /// -- see WindClipPath's own doc comment), which on a clip this
        /// short repeats often enough for the resulting click/gap to read
        /// as constant "chugging" rather than a rare, easy-to-miss pop.
        /// Rebuilt with the exact same sample-aligned self-crossfade
        /// technique as the wind loop and re-exported as WAV -- no codec
        /// framing delay, so the loop point stays exactly what it was built
        /// to be. The old .mp3 is gone; nothing else in the project
        /// referenced it directly by path except this constant.
        /// </summary>
        private const string ThrustClipPath = "Assets/CityX/Weapons/Audio/jetpack_thrust_loop.wav";

        /// <summary>
        /// WAV, not MP3 -- see Jetpack.WindAudio's own doc comment for why:
        /// this clip was built as a seamless loop with an exact crossfade
        /// at the splice point, and MP3 encoding pads a real, measured gap
        /// of silence at the start of the file that would reintroduce a
        /// click on every loop.
        /// </summary>
        private const string WindClipPath = "Assets/CityX/Weapons/Audio/jetpack_wind_loop.wav";

        private static readonly Color TankColour = new Color(0.55f, 0.56f, 0.58f);
        private static readonly Color PlateColour = new Color(0.12f, 0.12f, 0.13f);

        private const float TankRadius = 0.09f;
        private const float TankHeight = 0.42f;
        private const float TankSpacing = 0.13f;
        private const float PlateWidth = 0.30f;
        private const float PlateHeight = 0.40f;
        private const float PlateThickness = 0.05f;

        [MenuItem("CityX/Rebuild Jetpack")]
        public static GameObject Build()
        {
            var root = new GameObject("Jetpack");
            try
            {
                Material plateMaterial = CharacterSkin.Ensure("JetpackPlate", PlateColour);
                plateMaterial.SetFloat("_Metallic", 0.6f);
                plateMaterial.SetFloat("_Smoothness", 0.35f);

                // A scaled Unity primitive, not a hand-copied mesh --
                // GrapplingHookBuilder/BottleBuilder's own BoxMesh/
                // CylinderMesh technique (new Mesh + copy the primitive's
                // vertices/triangles by hand) produced renderers with
                // exactly zero bounds here, confirmed directly by dumping
                // MeshRenderer.bounds after the build -- the jetpack was
                // parented correctly (worldPos matched expectations) but
                // rendered nothing, on every part, every time. Scaling the
                // primitive's own transform instead sidesteps whatever that
                // was: Unity's builtin cube/cylinder meshes are known-good,
                // so there is nothing left to copy wrong.
                var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plate.name = "Backplate";
                Object.DestroyImmediate(plate.GetComponent<Collider>());
                plate.transform.SetParent(root.transform, false);
                plate.transform.localScale = new Vector3(PlateWidth, PlateHeight, PlateThickness);
                plate.GetComponent<MeshRenderer>().sharedMaterial = plateMaterial;

                Material tankMaterial = CharacterSkin.Ensure("JetpackTank", TankColour);
                tankMaterial.SetFloat("_Metallic", 0.85f);
                tankMaterial.SetFloat("_Smoothness", 0.55f);

                Transform leftTank = BuildTank(root.transform, tankMaterial, -TankSpacing);
                Transform rightTank = BuildTank(root.transform, tankMaterial, TankSpacing);

                // Where the flame comes from -- the bottom of the tanks,
                // pointed straight down, matching the thrust direction
                // Jetpack.Tick actually applies (pure vertical velocity).
                var flameObject = new GameObject("Flame");
                flameObject.transform.SetParent(root.transform, false);
                flameObject.transform.localPosition = new Vector3(0.0f, -TankHeight * 0.5f - 0.02f, PlateThickness * 0.5f);
                flameObject.transform.localRotation = Quaternion.Euler(90.0f, 0.0f, 0.0f);
                ParticleSystem flame = BuildFlame(flameObject.transform);

                var pickupTrigger = root.AddComponent<SphereCollider>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = Jetpack.InteractRange;

                var jetpack = root.AddComponent<Jetpack>();
                jetpack.FlameEffect = flame;
                jetpack.ThrustAudio = BuildThrustAudio(root);
                jetpack.WindAudio = BuildWindAudio(root);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: jetpack written to {PrefabPath}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Transform BuildTank(Transform parent, Material material, float xOffset)
        {
            GameObject tank = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tank.name = xOffset < 0.0f ? "TankLeft" : "TankRight";
            Object.DestroyImmediate(tank.GetComponent<Collider>());
            tank.transform.SetParent(parent, false);
            tank.transform.localPosition = new Vector3(xOffset, 0.0f, 0.0f);
            tank.transform.localRotation = Quaternion.Euler(90.0f, 0.0f, 0.0f);
            // Unity's primitive cylinder is 2 units tall with radius 1.
            tank.transform.localScale = new Vector3(TankRadius * 2.0f, TankHeight * 0.5f, TankRadius * 2.0f);
            tank.GetComponent<MeshRenderer>().sharedMaterial = material;
            return tank.transform;
        }

        /// <summary>
        /// THE SAME FLAME FlamethrowerBuilder BUILDS, EXPLICIT ASK: "replace
        /// the flame effect... with the same one you used for the
        /// flamethrower it looks way better." The old jetpack flame was one
        /// flat, untextured cone -- Unity's Particles/Unlit shader with no
        /// texture assigned renders a hard-edged square per particle rather
        /// than a soft flame-like blob (see FlamethrowerBuilder's own
        /// BuildFlameMaterial doc comment, confirmed there directly by
        /// rendering without one first). Reuses FlamethrowerBuilder's own
        /// outer+core layers verbatim (promoted to internal for exactly
        /// this call) rather than copying their setup by hand, so the two
        /// stay identical for free if that tuning ever changes.
        ///
        /// NO SMOKE OR MUZZLE LIGHT, unlike the flamethrower's own build --
        /// those read as a WEAPON being fired, not a thrust exhaust the
        /// player flies around with continuously; "the flame effect" is the
        /// two fire layers themselves, which is what actually got the "way
        /// better" verdict.
        ///
        /// Parented under flameObject exactly as the old single-layer cone
        /// was -- flameObject's own transform (pointed straight down, see
        /// the call site) is what aims it, unchanged.
        /// </summary>
        private static ParticleSystem BuildFlame(Transform flameObject)
        {
            Texture2D particleTexture = FlamethrowerBuilder.BuildParticleTexture();
            Material flameMaterial = FlamethrowerBuilder.BuildFlameMaterial(particleTexture);

            ParticleSystem outer = FlamethrowerBuilder.BuildOuterFlame(flameObject, flameMaterial);
            FlamethrowerBuilder.BuildCoreFlame(outer.transform, flameMaterial);

            outer.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return outer;
        }

        /// <summary>
        /// Directly on the root, not a positioned child the way Flame is --
        /// spatialBlend 0 makes it non-positional (the sound of THIS BODY
        /// thrusting, heard from inside it), matching PlayerAudio's own
        /// AddLoop reasoning for footsteps/ambience, so there is no 3D
        /// position for it to need. Jetpack.Tick starts/stops this by
        /// calling Play()/Stop() directly, the same way it already does for
        /// FlameEffect.
        /// </summary>
        private static AudioSource BuildThrustAudio(GameObject root)
        {
            var source = root.AddComponent<AudioSource>();
            source.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ThrustClipPath);
            if (source.clip == null)
            {
                Debug.LogWarning($"CityX: {ThrustClipPath} missing -- jetpack will thrust silently.");
            }
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0.0f;
            return source;
        }

        /// <summary>
        /// A SEPARATE AudioSource from ThrustAudio, not the same one with a
        /// swapped clip -- Jetpack.Tick starts/stops each independently
        /// (ThrustAudio on thrustHeld alone, this on thrustHeld AND
        /// boostHeld), so an ordinary unboosted thrust needs ThrustAudio's
        /// own engine loop to keep playing uninterrupted while this one
        /// stays silent. EXPLICIT ASK: "add this wind noise as a looping
        /// sound when flying through the air at shift speed using
        /// jetpack." Same non-positional spatialBlend reasoning as
        /// BuildThrustAudio -- the sound of air rushing past THIS BODY,
        /// not a positioned effect.
        /// </summary>
        private static AudioSource BuildWindAudio(GameObject root)
        {
            var source = root.AddComponent<AudioSource>();
            source.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(WindClipPath);
            if (source.clip == null)
            {
                Debug.LogWarning($"CityX: {WindClipPath} missing -- boosted jetpack flight will have no wind.");
            }
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0.0f;
            return source;
        }
    }
}
