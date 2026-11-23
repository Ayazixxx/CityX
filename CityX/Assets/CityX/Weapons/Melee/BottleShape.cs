using CityX.Items;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// ONE KIND OF GLASS BOTTLE'S SHAPE AND GLASS (docs/GLASS_BOTTLE.md; the kinds are Items/GlassBottleKinds): its outline as a potter's
    /// profile (how wide it is at each height, standing on its base, in a real bottle's metres), turned into its model by BottleBuilder, or the
    /// owner's own model for the first bottle; its glass, its paper label and its cap; and where its body, shoulder and neck are, which is
    /// where it is held, where it breaks and how it lies in the hand. Every bottle is drawn <see cref="LifeScale"/> times life (the owner's size for
    /// the first one: "2x", then "1.2x bigger"), as the hands that hold it are.
    /// </summary>
    public sealed class BottleShape
    {
        /// <summary>A bottle in the game against a real one.</summary>
        public const float LifeScale = 2.4f;

        /// <summary>This bottle against the others, on top of <see cref="LifeScale"/> (owner, 2026-11-23, of the soda bottle: "scale the whole soda bottle up 1.1x").</summary>
        public float Size = 1.0f;

        /// <summary>This bottle in the game against a real one.</summary>
        public float Scale => LifeScale * Size;

        public string ItemId;

        /// <summary>(radius, height above the base), bottom to top, metres of a real bottle; null for a bottle with a model of its own.</summary>
        public Vector2[] Profile;

        /// <summary>The bottle's own model, for one that has no profile (in the project; its base at the origin, +Y up, a real bottle's size).</summary>
        public string ModelPath;

        public Color Glass;

        /// <summary>A paper label round the body between these heights, or none.</summary>
        public bool HasLabel;
        public float LabelFrom, LabelTo;
        public Color Label;

        /// <summary>
        /// A printed picture on the glass (its path in the project, transparent where nothing is printed), or none: wrapped round the body between
        /// <see cref="DecalFrom"/> and <see cref="DecalTo"/> heights, once on each side, each <see cref="DecalArcDegrees"/> round the bottle.
        /// </summary>
        public string DecalPath;
        public float DecalFrom, DecalTo, DecalArcDegrees;

        /// <summary>A crown cap or a foil over the mouth, as its own profile, or none.</summary>
        public Vector2[] Cap;
        public Color CapColour;

        /// <summary>Where the straight body ends and the shoulder begins, where the neck begins, and the top of the mouth: heights above the base.</summary>
        public float BodyTop, NeckBase, Height;

        public float BodyRadius, NeckRadius;

        /// <summary>
        /// The middle of the fist on the neck, above the base (a real bottle's metres): its index finger's side just above where the neck begins, so the
        /// fist is all on the neck and none of it on the shoulder, the little finger's side up over the mouth (owner, 2026-11-23: "the thumb is
        /// clipping into the soda bottle instead of gripping around it"; the fist is wider than any bottle's neck is long).
        /// </summary>
        public float GripHeight => Mathf.Min(NeckBase + FistIndexSide / Size, Height);

        /// <summary>From the fist's middle to the outside of its index finger, along a held bottle, a real bottle's metres (the hand's 11 cm in the game).</summary>
        public const float FistIndexSide = 0.048f;

        /// <summary>From the fist to the base, in the game's metres: how far the bottle reaches as a club.</summary>
        public float Reach => GripHeight * Scale;

        /// <summary>From the fist to the furthest point of the broken half, in the game's metres.</summary>
        public float BrokenReach => (GripHeight - (BrokenAt - LongestPoint)) * Scale;

        /// <summary>Where a bottle broken on a blow ends, above the base (its points reach lower): most of the way down the body.</summary>
        public float BrokenAt => BodyTop * 0.52f;

        /// <summary>Where one broken down to nothing ends: the shoulder, just under the neck.</summary>
        public float ShatteredAt => Mathf.Lerp(BodyTop, NeckBase, 0.55f);

        /// <summary>The longest of the broken edge's points, a real bottle's metres (BottleMesh.Fracture).</summary>
        public const float LongestPoint = 0.034f;

        private static readonly BottleShape[] Shapes =
        {
            // The owner's model (Meshy's, from the Godot game): a broad-shouldered spirit bottle, in the amber it always was.
            new BottleShape
            {
                ItemId = GlassBottleKinds.Classic, ModelPath = "Assets/CityX/Weapons/Bottles/ClassicBottle.glb",
                Glass = new Color(0.36f, 0.20f, 0.07f),
                BodyTop = 0.105f, NeckBase = 0.150f, Height = 0.230f, BodyRadius = 0.046f, NeckRadius = 0.0135f,
            },
            // A longneck: straight sides, a short sloped shoulder, a long neck, a crown cap. Brown glass, a cream label.
            new BottleShape
            {
                ItemId = GlassBottleKinds.Beer,
                Profile = new[]
                {
                    new Vector2(0.000f, 0.000f), new Vector2(0.026f, 0.000f), new Vector2(0.0305f, 0.004f), new Vector2(0.0305f, 0.060f),
                    new Vector2(0.0305f, 0.112f), new Vector2(0.0285f, 0.128f), new Vector2(0.0205f, 0.150f), new Vector2(0.0150f, 0.168f),
                    new Vector2(0.0132f, 0.190f), new Vector2(0.0130f, 0.214f), new Vector2(0.0150f, 0.218f), new Vector2(0.0150f, 0.226f),
                    new Vector2(0.0128f, 0.230f), new Vector2(0.0105f, 0.230f),
                },
                Glass = new Color(0.33f, 0.16f, 0.05f),
                HasLabel = true, LabelFrom = 0.030f, LabelTo = 0.092f, Label = new Color(0.90f, 0.85f, 0.70f),
                Cap = new[]
                {
                    new Vector2(0.0165f, 0.219f), new Vector2(0.0172f, 0.223f), new Vector2(0.0165f, 0.232f), new Vector2(0.0120f, 0.2345f),
                    new Vector2(0.000f, 0.2345f),
                },
                CapColour = new Color(0.72f, 0.60f, 0.22f),
                BodyTop = 0.112f, NeckBase = 0.168f, Height = 0.230f, BodyRadius = 0.0305f, NeckRadius = 0.0132f,
            },
            // A claret bottle: tall straight sides, high round shoulders, a short straight neck with a ring under the lip. Dark green, a pale
            // label, a foil over the cork.
            new BottleShape
            {
                ItemId = GlassBottleKinds.Wine,
                Profile = new[]
                {
                    new Vector2(0.000f, 0.008f), new Vector2(0.022f, 0.000f), new Vector2(0.034f, 0.000f), new Vector2(0.0372f, 0.005f),
                    new Vector2(0.0372f, 0.090f), new Vector2(0.0372f, 0.178f), new Vector2(0.0350f, 0.198f), new Vector2(0.0262f, 0.214f),
                    new Vector2(0.0168f, 0.226f), new Vector2(0.0146f, 0.244f), new Vector2(0.0143f, 0.282f), new Vector2(0.0165f, 0.285f),
                    new Vector2(0.0165f, 0.291f), new Vector2(0.0146f, 0.294f), new Vector2(0.0146f, 0.300f), new Vector2(0.0118f, 0.300f),
                },
                Glass = new Color(0.07f, 0.20f, 0.10f),
                HasLabel = true, LabelFrom = 0.050f, LabelTo = 0.132f, Label = new Color(0.92f, 0.90f, 0.82f),
                Cap = new[]
                {
                    new Vector2(0.0156f, 0.252f), new Vector2(0.0153f, 0.281f), new Vector2(0.0176f, 0.285f), new Vector2(0.0176f, 0.292f),
                    new Vector2(0.0157f, 0.2955f), new Vector2(0.0157f, 0.3025f), new Vector2(0.000f, 0.3025f),
                },
                CapColour = new Color(0.45f, 0.07f, 0.09f),
                BodyTop = 0.178f, NeckBase = 0.226f, Height = 0.300f, BodyRadius = 0.0372f, NeckRadius = 0.0145f,
            },
            // A returnable soda bottle: a thick foot, a slight waist, a long shoulder running straight up into the neck. Pale sea-green glass,
            // a gold crown cap, and the owner's PHIZZ BANANA SODA printed on the glass, front and back (owner, 2026-11-23: "put this logo on the
            // soda bottle but make it a golden cap instead of red", "the phizz logo up top and the banana soda handrawn wording below it").
            new BottleShape
            {
                ItemId = GlassBottleKinds.Soda,
                Profile = new[]
                {
                    new Vector2(0.000f, 0.000f), new Vector2(0.025f, 0.000f), new Vector2(0.0300f, 0.005f), new Vector2(0.0300f, 0.024f),
                    new Vector2(0.0272f, 0.052f), new Vector2(0.0290f, 0.086f), new Vector2(0.0296f, 0.106f), new Vector2(0.0262f, 0.130f),
                    new Vector2(0.0196f, 0.160f), new Vector2(0.0146f, 0.190f), new Vector2(0.0130f, 0.222f), new Vector2(0.0150f, 0.226f),
                    new Vector2(0.0150f, 0.234f), new Vector2(0.0128f, 0.238f), new Vector2(0.0105f, 0.238f),
                },
                Glass = new Color(0.50f, 0.74f, 0.66f),
                DecalPath = "Assets/CityX/Weapons/Bottles/PhizzLabel.png", DecalFrom = 0.026f, DecalTo = 0.116f, DecalArcDegrees = 178.0f,
                Cap = new[]
                {
                    new Vector2(0.0165f, 0.227f), new Vector2(0.0172f, 0.231f), new Vector2(0.0165f, 0.240f), new Vector2(0.0120f, 0.2425f),
                    new Vector2(0.000f, 0.2425f),
                },
                CapColour = new Color(0.86f, 0.66f, 0.16f),
                Size = 1.1f,
                BodyTop = 0.106f, NeckBase = 0.176f, Height = 0.238f, BodyRadius = 0.0300f, NeckRadius = 0.0138f,
            },
        };

        /// <summary>The shape of bottle <paramref name="itemId"/>, or null if it is no bottle.</summary>
        public static BottleShape For(string itemId)
        {
            foreach (BottleShape shape in Shapes)
            {
                if (shape.ItemId == itemId)
                {
                    return shape;
                }
            }
            return null;
        }
    }
}
