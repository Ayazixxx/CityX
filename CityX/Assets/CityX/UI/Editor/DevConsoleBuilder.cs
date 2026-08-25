using CityX.Character;
using CityX.UI;
using CityX.View;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CityX.UIEditor
{
    /// <summary>
    /// Builds the dev console into the open scene.
    ///
    /// STRAIGHT INTO THE SCENE RATHER THAN A PREFAB, unlike the HUD. The
    /// console is wired to half a dozen things that only exist in a scene --
    /// the rain, the clock, the free camera, the body -- so a prefab of it
    /// would be a prefab of empty slots that somebody has to fill in by hand
    /// every time, which is exactly the class of mistake the capture rig has
    /// already been caught by twice.
    /// </summary>
    public static class DevConsoleBuilder
    {
        private static readonly Vector2 DesignResolution = new Vector2(1152.0f, 648.0f);
        private static readonly Color PanelInk = new Color(0.04f, 0.05f, 0.07f, 0.88f);
        private static readonly Color FieldInk = new Color(0.10f, 0.12f, 0.16f, 0.95f);
        private static readonly Color Ink = new Color(0.82f, 0.86f, 0.92f, 1.0f);

        /// <summary>
        /// <paramref name="radioPrefab"/> is handed in rather than built here,
        /// because building it needs the View editor assembly and the UI
        /// editor has no business referencing that just to fill one slot.
        /// </summary>
        public static DevConsoleView AddToScene(PlayerVitals vitals, RainEffect rain,
            DayNightCycle cycle, FlyCamera fly, GameObject radioPrefab,
            GameObject bottlePrefab = null, GameObject skelliJeansPrefab = null, GameObject hoodedCloakPrefab = null,
            PlayerInputReader inputReader = null, ThirdPersonCamera orbitCamera = null,
            GameObject grapplingHookPrefab = null, GameObject pistolPrefab = null,
            GameObject midnightPatchworkCoatPrefab = null)
        {
            var root = new GameObject("DevConsole", typeof(RectTransform));
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above the HUD, which sorts at 40. A console you cannot read
            // because the health bar is drawn over it is not a console.
            canvas.sortingOrder = 60;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = DesignResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            DevConsoleView view = root.AddComponent<DevConsoleView>();

            var panel = new GameObject("Panel", typeof(RectTransform));
            var panelRect = (RectTransform)panel.transform;
            panelRect.SetParent(root.transform, false);
            panelRect.anchorMin = new Vector2(0.0f, 1.0f);
            panelRect.anchorMax = new Vector2(1.0f, 1.0f);
            panelRect.pivot = new Vector2(0.5f, 1.0f);
            panelRect.offsetMin = new Vector2(12.0f, -260.0f);
            panelRect.offsetMax = new Vector2(-12.0f, -12.0f);
            panel.AddComponent<Image>().color = PanelInk;

            Text output = MakeText(panel.transform, "Output", 14);
            var outputRect = (RectTransform)output.transform;
            outputRect.anchorMin = Vector2.zero;
            outputRect.anchorMax = Vector2.one;
            outputRect.offsetMin = new Vector2(10.0f, 42.0f);
            outputRect.offsetMax = new Vector2(-10.0f, -8.0f);
            output.alignment = TextAnchor.LowerLeft;
            output.text = "`  to close.   /help  for the list.";

            var field = new GameObject("Input", typeof(RectTransform));
            var fieldRect = (RectTransform)field.transform;
            fieldRect.SetParent(panel.transform, false);
            fieldRect.anchorMin = new Vector2(0.0f, 0.0f);
            fieldRect.anchorMax = new Vector2(1.0f, 0.0f);
            fieldRect.pivot = new Vector2(0.5f, 0.0f);
            fieldRect.offsetMin = new Vector2(10.0f, 8.0f);
            fieldRect.offsetMax = new Vector2(-10.0f, 34.0f);
            field.AddComponent<Image>().color = FieldInk;

            InputField input = field.AddComponent<InputField>();
            Text typed = MakeText(field.transform, "Text", 15);
            var typedRect = (RectTransform)typed.transform;
            typedRect.anchorMin = Vector2.zero;
            typedRect.anchorMax = Vector2.one;
            typedRect.offsetMin = new Vector2(8.0f, 2.0f);
            typedRect.offsetMax = new Vector2(-8.0f, -2.0f);
            typed.alignment = TextAnchor.MiddleLeft;
            typed.supportRichText = false;
            input.textComponent = typed;

            view.Panel = panel;
            view.Input = input;
            view.Output = output;
            view.Vitals = vitals;
            view.Rain = rain;
            view.Cycle = cycle;
            view.Fly = fly;
            view.CivilianPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/Civilian.prefab");
            view.RadioPrefab = radioPrefab;
            view.BottlePrefab = bottlePrefab;
            view.SkelliJeansPrefab = skelliJeansPrefab;
            view.HoodedCloakPrefab = hoodedCloakPrefab;
            view.GrapplingHookPrefab = grapplingHookPrefab;
            view.PistolPrefab = pistolPrefab;
            view.MidnightPatchworkCoatPrefab = midnightPatchworkCoatPrefab;
            view.InputReader = inputReader;
            view.OrbitCamera = orbitCamera;

            EnsureEventSystem();
            return view;
        }

        private static Text MakeText(Transform parent, string name, int size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Text text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = Ink;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>
        /// Without one of these an InputField never receives a keystroke -- no
        /// error, no warning, it simply stays empty. And it has to be the Input
        /// System module: this project runs the new system only, so Unity's
        /// default StandaloneInputModule would compile, sit there, and deliver
        /// nothing.
        /// </summary>
        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }
            var events = new GameObject("EventSystem");
            events.AddComponent<EventSystem>();
            events.AddComponent<InputSystemUIInputModule>();
        }
    }
}
