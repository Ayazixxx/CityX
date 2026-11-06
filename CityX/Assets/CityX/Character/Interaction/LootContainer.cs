using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A DRAWER, A CUPBOARD DOOR, A LID: a moving part of a piece of furniture with things inside (docs/LOOTING.md; owner, 2026-10-30: "i want the
    /// looting to be detailed like skyrim or project zomboid, you have look it cabinets and drawers and stuff"). E opens it (LootSearcher) and it
    /// stays open once searched, as a ransacked room does. Sits on the part's hinge; its closed pose is wherever it was put before it woke.
    /// </summary>
    public sealed class LootContainer : MonoBehaviour
    {
        /// <summary>Every container awake in the world, for the searcher to look through.</summary>
        public static readonly HashSet<LootContainer> All = new HashSet<LootContainer>();

        public ContainerKind Kind;

        /// <summary>How far a drawer comes out, metres.</summary>
        public float Slide = 0.4f;

        /// <summary>What the loot window calls it ("Drawer", "Wardrobe").</summary>
        public string Label = "Drawer";

        /// <summary>The middle of its front, in its own frame: where a player looks to open it.</summary>
        public Vector3 LookPoint;

        /// <summary>The item ids inside (ItemRegistry's).</summary>
        public List<string> Items = new List<string>();

        public bool IsOpen { get; private set; }

        /// <summary>How far open it is now, 0 to 1.</summary>
        public float Open01 { get; private set; }

        public Vector3 Centre => transform.TransformPoint(LookPoint);

        private Vector3 _closedPosition;
        private Quaternion _closedRotation;

        private void Awake()
        {
            _closedPosition = transform.localPosition;
            _closedRotation = transform.localRotation;
        }

        private void OnEnable()
        {
            All.Add(this);
        }

        private void OnDisable()
        {
            All.Remove(this);
        }

        public void Open()
        {
            IsOpen = true;
        }

        public void Close()
        {
            IsOpen = false;
        }

        /// <summary>Raised with each item a player takes out of it (a body's gear then comes off the body, CorpseLoot).</summary>
        public event System.Action<string> Taken;

        public void NotifyTaken(string itemId)
        {
            Taken?.Invoke(itemId);
        }

        private void Update()
        {
            if (Kind == ContainerKind.Body)
            {
                return;   // it sits on a ragdoll's bone, which is the physics' to move
            }
            float target = IsOpen ? 1.0f : 0.0f;
            if (Mathf.Approximately(Open01, target))
            {
                return;
            }
            Open01 = Mathf.MoveTowards(Open01, target, Time.deltaTime / ContainerMotion.Seconds);
            ContainerMotion.PoseAt(Kind, Open01, Slide, out Vector3 offset, out Quaternion turn);
            transform.localPosition = _closedPosition + _closedRotation * offset;
            transform.localRotation = _closedRotation * turn;
        }
    }
}
