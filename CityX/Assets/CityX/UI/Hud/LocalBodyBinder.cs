using CityX.Character;
using UnityEngine;

namespace CityX.UI
{
    /// <summary>Points the scene's HUD and menus at the body this machine plays, when that is a clone spawned for a joining player (<see cref="LocalPlayerBody"/>).</summary>
    public static class LocalBodyBinder
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Listen()
        {
            LocalPlayerBody.Changed -= Bind;
            LocalPlayerBody.Changed += Bind;
        }

        /// <summary>Every screen that reads the player, the closed ones too. All of them read these fields as they need them, bar the HUD and chat, which listen and so rebind.</summary>
        public static void Bind(GameObject body)
        {
            if (body == null)
            {
                return;
            }
            var vitals = body.GetComponent<PlayerVitals>();
            var inventory = body.GetComponent<Inventory>();
            var reader = body.GetComponent<PlayerInputReader>();
            Transform place = body.transform;

            foreach (PlayerHud hud in All<PlayerHud>())
            {
                hud.Bind(vitals);
            }
            foreach (DevConsoleView console in All<DevConsoleView>())
            {
                console.Vitals = vitals;
                console.InputReader = reader;
            }
            foreach (InventoryPanelView panel in All<InventoryPanelView>())
            {
                panel.Inventory = inventory;
                panel.Player = body;
                panel.InputReader = reader;
            }
            foreach (LoadoutPanelView panel in All<LoadoutPanelView>())
            {
                panel.Inventory = inventory;
                panel.Player = body;
                panel.InputReader = reader;
            }
            foreach (StatsPanelView panel in All<StatsPanelView>())
            {
                panel.Vitals = vitals;
                panel.InputReader = reader;
            }
            foreach (PlayerMenuView menu in All<PlayerMenuView>())
            {
                menu.Player = place;
                menu.InputReader = reader;
            }
            foreach (MapPanelView map in All<MapPanelView>())
            {
                map.InputReader = reader;
            }
            foreach (MapCanvas map in All<MapCanvas>())
            {
                map.Player = place;
            }
            foreach (MapWaypoint waypoint in All<MapWaypoint>())
            {
                waypoint.Player = place;
            }
            foreach (MiniMapView map in All<MiniMapView>())
            {
                map.Player = place;
            }
            foreach (DamageNumbers numbers in All<DamageNumbers>())
            {
                numbers.Player = place;
            }
            foreach (PlaceholderPanelView panel in All<PlaceholderPanelView>())
            {
                panel.InputReader = reader;
            }
            foreach (BookPanelView panel in All<BookPanelView>())
            {
                panel.InputReader = reader;
            }
            foreach (PlayerRedrawStarter starter in All<PlayerRedrawStarter>())
            {
                starter.InputReader = reader;
            }
            foreach (PlayerChat chat in All<PlayerChat>())
            {
                chat.Bubble = body.GetComponentInChildren<SpeechBubble>(true);
                chat.InputReader = reader;
                chat.Ragdoll = body.GetComponent<Ragdoll>();
            }
        }

        private static T[] All<T>() where T : Object => Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }
}
