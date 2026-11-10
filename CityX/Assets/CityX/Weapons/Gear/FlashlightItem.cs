using CityX.Character;
using CityX.Items;
using CityX.View;
using CityX.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace CityX.Weapons
{
    /// <summary>
    /// THE FLASHLIGHT (owner, 2026-10-29: "make a basic flashlight item. research video game flashlights and use what you learn to make it";
    /// docs/FLASHLIGHT.md). A black aluminium torch (drawn in code by FlashlightBuilder) in the right hand; left click switches it on and off
    /// with a click. Lit, the arm holds it out and points it where you look, a little late (PlayerLocomotion.Torch), and it throws its beam the
    /// way games light theirs: a spot light shaped by a cookie (<see cref="FlashlightRules"/>: a hot spot melting into a soft spill),
    /// warm white, casting soft shadows, so walls and bodies cut it; under the ground a dim light all round it, which is what shows the
    /// walls beside you in a tunnel; a faint shaft of light in the air, seen from the side; and a glow on its lens that flares when you
    /// look into the beam.
    ///
    /// Held like the jerry can (IEquippableFlamethrower: ticked every frame with the trigger and the camera's aim), equipped from the console
    /// (/equip flashlight), seen in other players' hands lit and pointing where they point (ICosmeticMountable and FlashlightSync, through
    /// <see cref="ITorch"/>).
    /// </summary>
    public sealed class FlashlightItem : MonoBehaviour, IEquippableItem, IEquippableFlamethrower, ICosmeticMountable, ITorch
    {
        public string ItemId => ItemRegistry.Flashlight;

        /// <summary>The torch in the fist, its head out past the thumb (the hand's +Z), as a staff's shaft is held (StaffItem).</summary>
        public static Vector3 GripPosition => new Vector3(PlayerLocomotion.PalmOffsetMetres, 0.0f, 0.0f);

        public static Quaternion GripRotation => Quaternion.FromToRotation(Vector3.up, Vector3.forward);

        [Tooltip("The lens: where the light comes out, facing along the torch (+Y). Set by FlashlightBuilder.")]
        public Transform Lens;

        private Light _light;
        private Light _glow;
        private Transform _beam;
        private Transform _shaft;
        private MeshRenderer _shaftRenderer;
        private Transform _glare;
        private MeshRenderer _glareRenderer;
        private AudioSource _click;
        private MaterialPropertyBlock _glareTint;
        private MaterialPropertyBlock _shaftTint;
        private PlayerLocomotion _locomotion;
        private bool _triggerWas;

        /// <summary>On: the beam, the shaft and the lens's glow shown, and under the ground the light round it.</summary>
        public bool IsLit { get; private set; }

        /// <summary>Where it points: what is under the crosshair, or the end of its reach (on a copy, its owner's, sent).</summary>
        public Vector3 AimPoint { get; private set; }

        /// <summary>The spot light it throws (for tests).</summary>
        public Light Beam => _light;

        /// <summary>The dim light all round it under the ground (for tests).</summary>
        public Light Glow => _glow;

        private MaterialPropertyBlock GlareTint => _glareTint ??= new MaterialPropertyBlock();

        private MaterialPropertyBlock ShaftTint => _shaftTint ??= new MaterialPropertyBlock();

        private void Awake()
        {
            Build();
            Switch(false, false);
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += FaceCamera;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= FaceCamera;
        }

        public void OnEquip(GameObject player, ItemInstanceData instanceData)
        {
            Hold(player);
            var input = player.GetComponent<PlayerInputReader>();
            if (input != null)
            {
                input.EquippedFlamethrower = this;
            }
        }

        public void OnUnequip(GameObject player)
        {
            var input = player != null ? player.GetComponent<PlayerInputReader>() : null;
            if (input != null && ReferenceEquals(input.EquippedFlamethrower, this))
            {
                input.EquippedFlamethrower = null;
            }
            Release();
            Destroy(gameObject);
        }

        public void MountCosmetic(GameObject body)
        {
            Hold(body);
        }

        public void UnmountCosmetic(GameObject body)
        {
            Release();
        }

        public void SetCosmeticActive(bool active)
        {
        }

        public ItemInstanceData GetInstanceData() => ItemInstanceData.None;

        private void OnDestroy()
        {
            Release();
        }

        /// <summary>One frame held: a fresh click switches it, and it points where the camera looks.</summary>
        public void Tick(bool triggerHeld, Vector3 origin, Vector3 direction, GameObject wielder, float deltaTime)
        {
            if (triggerHeld && !_triggerWas)
            {
                Switch(!IsLit, true);
            }
            _triggerWas = triggerHeld;
            float reach = FlashlightRules.Range * Scale;
            Transform body = wielder != null ? wielder.transform : null;
            AimPoint = BloodSplats.Surface(origin, direction, reach, body, out RaycastHit hit) ? hit.point : origin + direction * reach;
            Point();
        }

        public void Toggle()
        {
            Switch(!IsLit, true);
            Point();
        }

        /// <summary>On another player's copy: on or off, and pointed where its owner's points.</summary>
        public void ShowLit(bool lit, Vector3 point)
        {
            AimPoint = point;
            if (lit != IsLit)
            {
                Switch(lit, true);
            }
            Point();
        }

        /// <summary>Into the right hand, the body told it holds a torch (it poses the arm from here on).</summary>
        private void Hold(GameObject body)
        {
            var animator = body != null ? body.GetComponentInChildren<Animator>() : null;
            Transform hand = animator != null && animator.avatar != null ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (hand != null)
            {
                transform.SetParent(hand, false);
                transform.localPosition = GripPosition;
                transform.localRotation = GripRotation;
                transform.localScale = Vector3.one;
            }
            foreach (Collider c in GetComponentsInChildren<Collider>())
            {
                c.enabled = false;
            }
            _locomotion = body != null ? body.GetComponent<PlayerLocomotion>() : null;
            if (_locomotion != null)
            {
                _locomotion.Torch = transform;
                AimPoint = body.transform.position + body.transform.forward * FlashlightRules.Range;
                Point();
            }
        }

        private void Release()
        {
            if (_locomotion != null && _locomotion.Torch == transform)
            {
                _locomotion.Torch = null;
                _locomotion.TorchLit = false;
            }
            _locomotion = null;
        }

        /// <summary>The arm told whether to hold it up and where to point it.</summary>
        private void Point()
        {
            if (_locomotion != null)
            {
                _locomotion.TorchLit = IsLit;
                _locomotion.TorchAimPoint = AimPoint;
            }
        }

        private void Switch(bool on, bool click)
        {
            IsLit = on;
            if (_light != null)
            {
                _light.enabled = on;
                ShowGlow();
                _shaftRenderer.enabled = on;
                _glareRenderer.enabled = on;
            }
            if (click && _click != null && Application.isPlaying)
            {
                _click.pitch = on ? 1.0f : SwitchClick.OffPitch;
                _click.PlayOneShot(SwitchClick.Clip, 0.7f);
            }
        }

        /// <summary>The light round a lit torch, as strong as the place it is in is out of the sky's light: none on the street.</summary>
        private void ShowGlow()
        {
            _glow.intensity = FlashlightRules.GlowIntensity(CurrentSewers.DarknessAt(_beam.position));
            _glow.enabled = IsLit && _glow.intensity > 0.0f;
        }

        /// <summary>Its size: a giant's torch reaches as far as the giant is big.</summary>
        private float Scale => Mathf.Max(0.01f, transform.lossyScale.y);

        /// <summary>The beam, the light round it, the shaft and the lens's glow, at the lens, along the torch.</summary>
        private void Build()
        {
            Transform lens = Lens != null ? Lens : transform;
            _beam = new GameObject("Beam").transform;
            _beam.SetParent(lens, false);
            _beam.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.back);   // its +Z along the torch
            _light = _beam.gameObject.AddComponent<Light>();
            _light.type = LightType.Spot;
            _light.color = FlashlightRules.Colour;
            _light.intensity = FlashlightRules.Intensity;
            _light.range = FlashlightRules.Range;
            _light.spotAngle = FlashlightRules.SpillDegrees;
            _light.innerSpotAngle = FlashlightRules.SpillDegrees * 0.9f;   // the cookie shapes the beam, not the cone's own fade
            _light.cookie = FlashlightLook.Cookie;
            _light.shadows = LightShadows.Soft;
            _light.shadowNearPlane = FlashlightRules.ShadowNearPlane;
            CityLightLayers.MarkBeam(_light);

            var glow = new GameObject("Glow");
            glow.transform.SetParent(_beam, false);
            _glow = glow.AddComponent<Light>();
            _glow.type = LightType.Point;
            _glow.color = FlashlightRules.Colour;
            _glow.range = FlashlightRules.GlowReach;
            _glow.shadows = LightShadows.None;
            CityLightLayers.MarkBeam(_glow);

            _shaft = new GameObject("Shaft").transform;
            _shaft.SetParent(_beam, false);
            float length = FlashlightRules.Range * FlashlightRules.ShaftShare;
            float width = 2.0f * length * Mathf.Tan(FlashlightRules.ShaftDegrees * 0.5f * Mathf.Deg2Rad);
            _shaft.localScale = new Vector3(width, width, length);
            _shaft.gameObject.AddComponent<MeshFilter>().sharedMesh = FlashlightLook.Shaft;
            _shaftRenderer = Unshaded(_shaft.gameObject.AddComponent<MeshRenderer>(), FlashlightLook.ShaftMaterial);

            _glare = new GameObject("Glare").transform;
            _glare.SetParent(_beam, false);
            _glare.gameObject.AddComponent<MeshFilter>().sharedMesh = GlowShapes.FlatQuad;
            _glareRenderer = Unshaded(_glare.gameObject.AddComponent<MeshRenderer>(), FlashlightLook.GlareMaterial);

            _click = gameObject.AddComponent<AudioSource>();
            _click.playOnAwake = false;
            _click.spatialBlend = 1.0f;
            _click.minDistance = 1.0f;
            _click.maxDistance = 12.0f;
        }

        private static MeshRenderer Unshaded(MeshRenderer renderer, Material material)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return renderer;
        }

        private void LateUpdate()
        {
            if (_light != null)
            {
                _light.range = FlashlightRules.Range * Scale;
                _glow.range = FlashlightRules.GlowReach * Scale;
                if (IsLit)
                {
                    ShowGlow();
                }
            }
        }

        /// <summary>
        /// Before each camera draws: the lens's glow facing it, bigger and brighter the more the beam points at it; and the shaft fading out
        /// as the camera comes to look along the beam (<see cref="FlashlightRules.ShaftShown"/>).
        /// </summary>
        private void FaceCamera(ScriptableRenderContext context, Camera camera)
        {
            if (!IsLit || _beam == null || camera == null)
            {
                return;
            }
            Vector3 beam = _beam.forward;
            Vector3 toCamera = camera.transform.position - _beam.position;
            Vector3 toEye = toCamera.sqrMagnitude > 1e-6f ? toCamera.normalized : -beam;
            float towardEye = Vector3.Dot(beam, toEye);
            float into = Mathf.Pow(Mathf.Max(0.0f, towardEye), 8.0f);
            _glare.rotation = Quaternion.FromToRotation(Vector3.up, toEye);
            _glare.localScale = Vector3.one * FlashlightRules.GlareSize * (1.0f + into * FlashlightRules.GlareFlare);
            _glareRenderer.GetPropertyBlock(GlareTint);
            GlareTint.SetColor("_BaseColor", FlashlightRules.Colour * (0.4f + 0.6f * into));
            _glareRenderer.SetPropertyBlock(GlareTint);
            _shaftRenderer.GetPropertyBlock(ShaftTint);
            Color colour = FlashlightRules.Colour;
            float shaft = FlashlightRules.ShaftStrength * FlashlightRules.ShaftShown(towardEye);
            ShaftTint.SetColor("_BaseColor", new Color(colour.r, colour.g, colour.b, shaft));
            _shaftRenderer.SetPropertyBlock(ShaftTint);
        }
    }
}
