using CityX.Character;
using CityX.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CityX.UIEditor
{
    /// <summary>
    /// Builds chat's input panel and the claim banner into the open scene.
    /// Ported from ui/chat_box.gd and ui/claim_banner.gd.
    ///
    /// ONE CANVAS FOR BOTH, because they are the same on-screen presence at
    /// different moments -- the banner announces a claim's outcome, chat is
    /// how /claim gets typed in the first place. Splitting them into two
    /// builders would mean two sortingOrder decisions to keep in sync for
    /// no real benefit.
    ///
    /// STRAIGHT INTO THE SCENE, not a prefab -- same reasoning as
    /// DevConsoleBuilder and PauseMenuBuilder: this is wired to the claim
    /// system, the speech bubble, the input reader and the camera, none of
    /// which exist outside a scene.
    ///
    /// EVERY LISTENER PlayerChat OWNS IS WIRED IN ITS OWN Awake, not here --
    /// see PauseMenuView's doc comment for why an editor-time
    /// AddListener never survives the scene being saved.
    /// </summary>
    public static class PlayerChatBuilder
    {
        private static readonly Vector2 DesignResolution = new Vector2(1152.0f, 648.0f);
        private static readonly Color FieldInk = new Color(0.10f, 0.12f, 0.16f, 0.95f);
        private static readonly Color Ink = new Color(0.92f, 0.94f, 0.98f, 1.0f);
        private static readonly Color BannerInk = new Color(0.95f, 0.90f, 0.55f, 1.0f);

        public static PlayerChat AddToScene(ClaimSystem claim, SpeechBubble bubble,
            PlayerInputReader inputReader, ThirdPersonCamera orbitCamera,
            DevConsoleView console, PauseMenuView pause, MapPanelView map,
            InventoryPanelView inventory, Ragdoll ragdoll)
        {
            var root = new GameObject("PlayerChat", typeof(RectTransform));
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above the HUD (40), below the map (50) -- chat and the map
            // never show at once, guarded on both sides, so their relative
            // order does not matter beyond both sitting over the HUD.
            canvas.sortingOrder = 45;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = DesignResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            PlayerChat view = root.AddComponent<PlayerChat>();

            var banner = new GameObject("Banner", typeof(RectTransform));
            var bannerRect = (RectTransform)banner.transform;
            bannerRect.SetParent(root.transform, false);
            bannerRect.anchorMin = new Vector2(0.5f, 0.72f);
            bannerRect.anchorMax = new Vector2(0.5f, 0.72f);
            bannerRect.pivot = new Vector2(0.5f, 0.5f);
            bannerRect.sizeDelta = new Vector2(700.0f, 60.0f);
            Text bannerText = banner.AddComponent<Text>();
            bannerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            bannerText.fontSize = 30;
            bannerText.fontStyle = FontStyle.Bold;
            bannerText.color = BannerInk;
            bannerText.alignment = TextAnchor.MiddleCenter;
            bannerText.raycastTarget = false;
            var bannerComponent = banner.AddComponent<ClaimBanner>();
            bannerComponent.Label = bannerText;

            var panel = new GameObject("Panel", typeof(RectTransform));
            var panelRect = (RectTransform)panel.transform;
            panelRect.SetParent(root.transform, false);
            panelRect.anchorMin = new Vector2(0.0f, 0.0f);
            panelRect.anchorMax = new Vector2(0.5f, 0.0f);
            panelRect.pivot = new Vector2(0.0f, 0.0f);
            panelRect.offsetMin = new Vector2(12.0f, 12.0f);
            panelRect.offsetMax = new Vector2(0.0f, 46.0f);
            panel.AddComponent<Image>().color = FieldInk;

            InputField input = panel.AddComponent<InputField>();
            Text typed = new GameObject("Text", typeof(RectTransform)).AddComponent<Text>();
            var typedRect = (RectTransform)typed.transform;
            typedRect.SetParent(panel.transform, false);
            typedRect.anchorMin = Vector2.zero;
            typedRect.anchorMax = Vector2.one;
            typedRect.offsetMin = new Vector2(8.0f, 2.0f);
            typedRect.offsetMax = new Vector2(-8.0f, -2.0f);
            typed.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            typed.fontSize = 16;
            typed.color = Ink;
            typed.alignment = TextAnchor.MiddleLeft;
            typed.supportRichText = false;
            typed.raycastTarget = false;
            input.textComponent = typed;

            panel.SetActive(false);

            view.Panel = panel;
            view.Input = input;
            view.Banner = bannerComponent;
            view.Bubble = bubble;
            view.Claim = claim;
            view.InputReader = inputReader;
            view.OrbitCamera = orbitCamera;
            view.Console = console;
            view.Pause = pause;
            view.Map = map;
            view.Inventory = inventory;
            view.Ragdoll = ragdoll;

            UiKit.EnsureEventSystem();
            return view;
        }
    }
}
