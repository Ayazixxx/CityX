using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// A SHIRT THROUGH THE PRESS (docs/HEAT_PRESS.md). The player's machine takes the shirt out of their inventory (worn or carried), lays it
    /// folded on the drawer, runs the pressing, and at the steam the shirt carries its new design; when the drawer has brought it out it goes
    /// back where it came from (worn again, or into the bag; with no room it is left lying on the press for anyone, a dropped item). Other
    /// machines are told (<see cref="HeatPressNetwork"/>) and show the same: the shirt on the drawer, the clamp, the steam, the print.
    /// </summary>
    public sealed partial class HeatPress
    {
        /// <summary>Every press in the world on this machine, by id (the same on every machine: <see cref="IdFor"/>).</summary>
        public static readonly Dictionary<int, HeatPress> All = new Dictionary<int, HeatPress>();

        /// <summary>A player asked to use a press (E on it, HeatPressUse): the press screen opens.</summary>
        public static event System.Action<HeatPress, GameObject> UseRequested;

        public int Id { get; private set; }

        /// <summary>The folded shirt lying on the drawer now (null when none is).</summary>
        public IPrintable ShirtShown => _shirt != null ? _shirt.GetComponent<IPrintable>() : null;

        /// <summary>A pressing of this machine's own player under way: what goes back to them, and where.</summary>
        private sealed class Job
        {
            public Inventory Inventory;
            public string ItemId;
            public bool WasWorn;
            public ItemInstanceData Data;
        }

        private Job _job;
        private GameObject _shirt;
        private string _newDesign = string.Empty;
        private bool _printed;
        private bool _givenBack = true;

        /// <summary>Where on the drawer the folded shirt lies: the pad's middle, on its top.</summary>
        private static readonly Vector3 OnThePad = new Vector3(0.0f, HeatPressShape.PadTop + 0.005f, 0.0f);

        /// <summary>A press's id from where it stands (to the centimetre), so every machine that has it calls it the same.</summary>
        public static int IdFor(Vector3 position)
        {
            unchecked
            {
                int x = Mathf.RoundToInt(position.x * 100.0f), y = Mathf.RoundToInt(position.y * 100.0f), z = Mathf.RoundToInt(position.z * 100.0f);
                int id = ((x * 73856093) ^ (y * 19349663) ^ (z * 83492791)) & 0x7fffffff;
                return id == 0 ? 1 : id;
            }
        }

        private void OnEnable()
        {
            Id = IdFor(transform.position);
            if (!All.TryGetValue(Id, out HeatPress standing) || standing == null)
            {
                All[Id] = this;   // the first to stand here is the press (a second made there is let go: HeatPressNetwork.Place)
            }
        }

        private void OnDisable()
        {
            if (All.TryGetValue(Id, out HeatPress press) && press == this)
            {
                All.Remove(Id);
            }
            GiveBack();   // a press taken away mid-pressing still hands the shirt back
        }

        /// <summary>The press's prompt was used (HeatPressUse).</summary>
        public void RequestUse(GameObject player)
        {
            if (!Pressing)
            {
                UseRequested?.Invoke(this, player);
            }
        }

        /// <summary>
        /// Presses this machine's own player's shirt: the one worn in <paramref name="wornSlot"/>, or the one in storage slot
        /// <paramref name="storageIndex"/>, comes out of their inventory, and back with <paramref name="designId"/> printed on it. False, and
        /// nothing changed, when the press is busy or the shirt cannot be taken (gone, or worn with no room in the bag to take it off).
        /// </summary>
        public bool PressShirt(Inventory inventory, string itemId, string wornSlot, int storageIndex, string designId)
        {
            if (Pressing || inventory == null || string.IsNullOrEmpty(designId) || !_givenBack)
            {
                return false;
            }
            if (!inventory.TakeOut(itemId, wornSlot, storageIndex, inventory.gameObject, out ItemInstanceData data))
            {
                return false;
            }
            _job = new Job { Inventory = inventory, ItemId = itemId, WasWorn = !string.IsNullOrEmpty(wornSlot), Data = data };
            _givenBack = false;
            inventory.Saving += GiveBack;   // saved mid-pressing (quit to the title, the game closed): the shirt goes back in first, printed
            ShowPressing(itemId, data.DesignId, designId, inventory.PrefabForItem(itemId));
            HeatPressNetwork.TellPressing(this, itemId, data.DesignId, designId);
            return true;
        }

        /// <summary>Shows a pressing (this machine's player's, or one another machine told of): the shirt on the drawer, then the cycle.</summary>
        public void ShowPressing(string itemId, string oldDesign, string newDesign, GameObject shirtPrefab)
        {
            RemoveShirt();
            if (shirtPrefab != null && Drawer != null)
            {
                _shirt = Instantiate(shirtPrefab, Drawer, false);
                _shirt.name = shirtPrefab.name + " (in the press)";
                InertModel.Strip(_shirt);
                _shirt.transform.localPosition = OnThePad;
                _shirt.transform.localRotation = Quaternion.Euler(0.0f, 180.0f, 0.0f);   // collar to the hinge: the print reads upright from the front
                FitOnThePad(_shirt.transform);
                ShirtShown?.ShowDesign(oldDesign);
            }
            _newDesign = newDesign ?? string.Empty;
            _printed = false;
            Begin();
        }

        private void TickShirt(float seconds)
        {
            if (!_printed && seconds >= HeatPressCycle.DoneAt && seconds < HeatPressCycle.TotalSeconds + 1.0f)
            {
                _printed = true;
                ShirtShown?.ShowDesign(_newDesign);   // the steam: the shirt is done
            }
            if (_shirt != null && seconds >= HeatPressCycle.TotalSeconds - HeatPressCycle.SlideSeconds)
            {
                GiveBack();
                RemoveShirt();   // taken off the drawer as it slides home
            }
        }

        /// <summary>This machine's player's shirt back to them, printed; with no room for it, left lying on the press for anyone.</summary>
        private void GiveBack()
        {
            if (_givenBack || _job == null)
            {
                _givenBack = true;
                return;
            }
            _givenBack = true;
            Job job = _job;
            _job = null;
            if (job.Inventory != null)
            {
                job.Inventory.Saving -= GiveBack;
            }
            ItemInstanceData data = job.Data;
            data.DesignId = _newDesign;
            bool back = job.Inventory != null
                        && ((job.WasWorn && job.Inventory.EquipItemId(job.ItemId, job.Inventory.gameObject, data)) || job.Inventory.AddItem(job.ItemId, 1, data));
            if (back)
            {
                return;
            }
            GameObject prefab = job.Inventory != null ? job.Inventory.PrefabForItem(job.ItemId) : null;
            Vector3 front = transform.TransformPoint(new Vector3(0.0f, OnThePad.y, HeatPressShape.PadHalf.y + HeatPressShape.DrawerTravel - 0.2f));
            DroppedItems.Shared.Drop(new ItemDrop
            {
                ItemId = job.ItemId, Data = data, Position = front, Rotation = transform.rotation, Placed = true,
            }, shared: true, null, prefab);
        }

        /// <summary>The folded shirt brought down to lie within the pad (folded for the ground it is bigger), its middle on the pad's.</summary>
        private void FitOnThePad(Transform shirt)
        {
            var bounds = new Bounds();
            bool any = false;
            foreach (MeshFilter filter in shirt.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                Bounds own = filter.sharedMesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    var local = new Vector3(corner % 2 == 0 ? own.min.x : own.max.x, (corner / 2) % 2 == 0 ? own.min.y : own.max.y, corner < 4 ? own.min.z : own.max.z);
                    Vector3 onDrawer = Drawer.InverseTransformPoint(filter.transform.TransformPoint(local));
                    if (any) bounds.Encapsulate(onDrawer);
                    else bounds = new Bounds(onDrawer, Vector3.zero);
                    any = true;
                }
            }
            if (!any || bounds.size.x <= 0.0f || bounds.size.z <= 0.0f)
            {
                return;
            }
            Vector2 room = HeatPressShape.PadHalf * 2.0f * ShirtOnThePadShare;
            float scale = Mathf.Min(1.0f, Mathf.Min(room.x / bounds.size.x, room.y / bounds.size.z));
            shirt.localScale *= scale;
            Vector3 middle = OnThePad + (bounds.center - OnThePad) * scale;
            shirt.localPosition += new Vector3(-middle.x, OnThePad.y - (OnThePad.y + (bounds.min.y - OnThePad.y) * scale), -middle.z);
        }

        /// <summary>How much of the pad a folded shirt may cover.</summary>
        private const float ShirtOnThePadShare = 0.9f;

        private void RemoveShirt()
        {
            if (_shirt != null)
            {
                Destroy(_shirt);
                _shirt = null;
            }
        }
    }
}
