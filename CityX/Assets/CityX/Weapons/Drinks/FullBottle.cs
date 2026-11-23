using CityX.Items;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A FULL PHIZZ BOTTLE (docs/PHIZZ_CAN.md; owner, 2026-11-23: "make the glass bottle full of piss yellow liquid and when you drink its, it visually lowers
    /// until empty", "100 health over 10 seconds", drunk empty it "stays as a weapon"): the soda bottle standing in the fist round its body; drunk, the left
    /// hand pulls the gold cap off, the drink in it goes down as it is drunk, it heals, and what is left is the soda bottle, a weapon to swing and break.
    /// The drink is the glass coloured below a level (CityXLiquid.hlsl): the glass is opaque, so the game's black line goes round it.
    /// </summary>
    public sealed class FullBottle : HeldDrink
    {
        public const string ItemId = ItemRegistry.PhizzBottle;

        /// <summary>What is left of it: the soda bottle, the weapon.</summary>
        public const string EmptyItemId = GlassBottleKinds.Soda;

        public const string DrinkClip = "bottle_drink";

        /// <summary>What a full bottle heals, and over how long (the owner's).</summary>
        public const float Heal = 100.0f;
        public const float Seconds = 10.0f;

        /// <summary>
        /// The bottle in the right hand bone's frame, standing as the can does: the fist round the straight of its body, below the shoulder (higher, the thumb went over the shoulder into the glass: CanGripPlayTests). Keep in step with
        /// CityX/SourceArt/Drink/can_drink_build.py's BOTTLE_HOLD.
        /// </summary>
        public static readonly Vector3 Hold = new Vector3(0.10f, -0.120f, -0.105f);

        /// <summary>The drink's colour (the owner's "piss yellow").</summary>
        public static readonly Color LiquidColour = new Color(0.93f, 0.82f, 0.18f);

        /// <summary>How high it is filled, a real bottle's metres above its base: to the foot of the neck, as a bottle from the shop is.</summary>
        public const float FilledTo = 0.19f;

        /// <summary>The drink's level is found from points round the inside of the bottle, this many round, at its foot and at the fill line.</summary>
        private const int LevelPoints = 8;

        public const float BottleMass = 0.6f;

        [Tooltip("The glass: coloured below the drink's level (its material has _CITYX_LIQUID on).")]
        public Renderer[] Glass = new Renderer[0];

        [Tooltip("The gold cap on the bottle, hidden once pulled off.")]
        public GameObject Cap;

        [Tooltip("The cap pulled off: it falls, lies about and goes (DroppedCan).")]
        public GameObject CapPrefab;

        [Tooltip("Played as the cap comes off.")]
        public AudioClip OpenSound;

        private static readonly int LevelId = Shader.PropertyToID("_CityXLiquidLevel");
        private static readonly int ColourId = Shader.PropertyToID("_CityXLiquidColour");
        private MaterialPropertyBlock _block;
        private float _drunk;

        public override string DrinkItemId => ItemId;
        public override string Clip => DrinkClip;
        public override float HealAmount => Heal;
        public override float HealSeconds => Seconds;
        protected override Vector3 HoldPosition => Hold;

        /// <summary>The thumb turned round the bottle's side (measured on the skin: CanGripPlayTests).</summary>
        protected override float ThumbTurn => -90.0f;

        protected override float ThumbRootFold => 18.0f;

        protected override float ThumbTipFold => 25.0f;

        /// <summary>How much is left, 0 empty to 1 full.</summary>
        public float Left => 1.0f - _drunk;

        /// <summary>Its shape: the soda bottle's, at the soda bottle's size.</summary>
        private static BottleShape Shape => BottleShape.For(GlassBottleKinds.Soda);

        /// <summary>The cap comes off once the left hand has pulled it far enough; it falls away.</summary>
        protected override void ShowOpened(float was, float now)
        {
            bool off = now > 0.5f;
            if (Cap == null || Cap.activeSelf != off)
            {
                return;
            }
            Cap.SetActive(!off);
            if (!off)
            {
                return;
            }
            if (OpenSound != null)
            {
                AudioSource.PlayClipAtPoint(OpenSound, Cap.transform.position);
            }
            if (CapPrefab != null)
            {
                GameObject cap = Instantiate(CapPrefab, Cap.transform.position, Cap.transform.rotation);
                var body = cap.GetComponent<Rigidbody>();
                if (body != null)
                {
                    body.linearVelocity = transform.up * 1.2f - transform.right * 0.6f;
                }
            }
        }

        public override void SetDrunk(float drunk)
        {
            _drunk = Mathf.Clamp01(drunk);
        }

        private void LateUpdate()
        {
            ShowLevel();
        }

        /// <summary>The drink's level, a height in the world: up from the lowest point inside the bottle toward its highest, as much as is left.</summary>
        public float Level
        {
            get
            {
                BottleShape shape = Shape;
                float scale = shape != null ? shape.Scale : 1.0f;
                float radius = shape != null ? shape.BodyRadius * 0.9f : 0.03f;
                float lowest = float.MaxValue, highest = float.MinValue;
                for (int i = 0; i < LevelPoints; i++)
                {
                    float angle = i * Mathf.PI * 2.0f / LevelPoints;
                    var round = new Vector3(Mathf.Cos(angle) * radius, 0.0f, Mathf.Sin(angle) * radius);
                    foreach (float height in new[] { 0.004f, FilledTo })
                    {
                        float y = transform.TransformPoint((round + Vector3.up * height) * scale).y;
                        lowest = Mathf.Min(lowest, y);
                        highest = Mathf.Max(highest, y);
                    }
                }
                return Left <= 0.0f ? float.NegativeInfinity : Mathf.Lerp(lowest, highest, Left);
            }
        }

        private void ShowLevel()
        {
            if (Glass == null || Glass.Length == 0)
            {
                return;
            }
            _block ??= new MaterialPropertyBlock();
            float level = Level;
            foreach (Renderer glass in Glass)
            {
                if (glass == null)
                {
                    continue;
                }
                glass.GetPropertyBlock(_block);
                _block.SetFloat(LevelId, float.IsNegativeInfinity(level) ? -1.0e6f : level);
                _block.SetColor(ColourId, LiquidColour);
                glass.SetPropertyBlock(_block);
            }
        }

        public override void Finished()
        {
            DrinkStack.Drunk(this, Holder, EmptyItemId);
        }
    }
}
