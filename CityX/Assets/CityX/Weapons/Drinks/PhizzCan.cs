using CityX.Items;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A CAN OF PHIZZ (docs/PHIZZ_CAN.md; owner, 2026-11-23: "he holds it in his right hand ... thumb wrapped around for grip", "make it heal how we described
    /// 80 hp over 10 seconds"): drunk, the left hand lifts the tab, the hole shows black, it heals, and the hand lets the empty can fall.
    /// </summary>
    public sealed class PhizzCan : HeldDrink
    {
        public const string ItemId = ItemRegistry.PhizzCan;

        public const string DrinkClip = "can_drink";

        /// <summary>The can in the air, thrown (PhizzCanBuilder).</summary>
        public const string ThrownPrefabPath = "Assets/CityX/Weapons/Cans/phizz_can_thrown.prefab";

        /// <summary>What a can heals, and over how long (the owner's).</summary>
        public const float Heal = 80.0f;
        public const float Seconds = 10.0f;

        /// <summary>
        /// The can in the right hand bone's frame: the fist round its middle, its side on the palm, its hole toward the wrist (the body as it is carried).
        /// Keep in step with CityX/SourceArt/Drink/can_drink_build.py's CAN_HOLD.
        /// </summary>
        public static readonly Vector3 Hold = new Vector3(0.10f, -0.121f, -0.148f);

        /// <summary>How far the tab lifts on its rivet, degrees.</summary>
        public const float TabOpenDegrees = 75.0f;

        public const float CanMass = 0.4f;

        [Tooltip("The tab's hinge at the rivet: turned about its Z to lift the tab (PhizzCanBuilder).")]
        public Transform TabHinge;

        [Tooltip("The black of the opened hole, hidden until the tab is lifted.")]
        public GameObject Hole;

        [Tooltip("Played as the tab cracks the lid (the owner's sound).")]
        public AudioClip OpenSound;

        [Tooltip("The can emptied, as it is dropped: falls, lies about and goes (DroppedCan).")]
        public GameObject EmptyPrefab;

        public override string DrinkItemId => ItemId;
        public override string Clip => DrinkClip;
        public override float HealAmount => Heal;
        public override float HealSeconds => Seconds;
        protected override Vector3 HoldPosition => Hold;

        protected override float MiddleHeight => CanMesh.Height * BottleShape.LifeScale * 0.5f;

        /// <summary>The tab lifted, and the hole shown (with the owner's sound) once it has cracked the lid.</summary>
        protected override void ShowOpened(float was, float now)
        {
            if (TabHinge != null)
            {
                TabHinge.localRotation = Quaternion.AngleAxis(TabOpenDegrees * now, Vector3.forward);
            }
            bool cracked = now > 0.5f;
            if (Hole != null)
            {
                if (cracked && !Hole.activeSelf && OpenSound != null)
                {
                    AudioSource.PlayClipAtPoint(OpenSound, transform.position);
                }
                Hole.SetActive(cracked);
            }
        }

        /// <summary>The empty can let fall from where the hand held it; this one is hidden until the next can of a stack is taken out.</summary>
        public override void LetGo(Vector3 velocity)
        {
            if (EmptyPrefab != null)
            {
                GameObject empty = Instantiate(EmptyPrefab, transform.position, transform.rotation);
                var body = empty.GetComponent<Rigidbody>();
                if (body != null)
                {
                    body.linearVelocity = velocity;
                }
            }
            gameObject.SetActive(false);
        }

        public override void Finished()
        {
            DrinkStack.Drunk(this, Holder, null);
        }

        /// <summary>Broken where it lies: it is left there open and empty.</summary>
        protected override void Burst(Vector3 point, Vector3 direction)
        {
            if (EmptyPrefab != null)
            {
                Instantiate(EmptyPrefab, transform.position, transform.rotation);
            }
        }

        /// <summary>The next can of a stack in the hand: shut, shown again.</summary>
        public void Refill()
        {
            gameObject.SetActive(true);
            SetOpened(0.0f);
        }
    }
}
