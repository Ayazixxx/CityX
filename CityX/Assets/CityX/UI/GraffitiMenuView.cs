using CityX.Character;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// THE GRAFFITI CANVAS (owner, 2026-10-15: "go up to walls and right click with it equip in hand and it opens some free draw menu that just
    /// lets you adjust color and brush size and lets you [draw] on whatever section of wall you are right in front of"). Right-click a wall with
    /// a spray can in hand (PlayerInputReader.SprayRequested) and this opens over the game: the wall in front of you, photographed square on,
    /// 2.6 m across (any graffiti already there included), with your paint on top; the character creator's colour square and hue strip; the
    /// brush size; Done, Clear and Cancel. Drag to spray -- the can hisses. Done puts the piece up on the wall for everyone
    /// (GraffitiNetwork.PutForEveryone), as a lit panel on the wall (GraffitiPanel). Spraying near a piece already up carries on that piece.
    ///
    /// Built in code when the HUD wakes (PlayerHud adds it), as the skill menu is: no prefab, no images. Its own canvas over everything, and
    /// the player's controls and camera rest while it is open.
    /// </summary>
    public sealed class GraffitiMenuView : MonoBehaviour
    {
        /// <summary>How far from the player's body a wall can be sprayed, metres.</summary>
        public const float ReachMetres = 3.2f;

        /// <summary>The canvas on screen, design pixels on the 1152 x 648 HUD.</summary>
        public const float CanvasSize = 500.0f;

        /// <summary>The brush, in pixels of radius on the 256-wide piece (1 is a fine line, 24 a fat fill).</summary>
        public const int MinBrush = 1;

        public const int MaxBrush = 24;

        public const int SortingOrder = 58;

        public static readonly Color DefaultColour = new Color(0.95f, 0.15f, 0.2f);

        private static readonly Color Backdrop = new Color(0.02f, 0.02f, 0.03f, 0.72f);
        private static readonly Color PanelColour = new Color(0.09f, 0.09f, 0.11f, 0.95f);
        private static readonly Color ButtonColour = new Color(0.16f, 0.16f, 0.19f, 1.0f);
        private static readonly Color DoneColour = new Color(0.20f, 0.42f, 0.22f, 1.0f);
        private static readonly Color Ink = new Color(0.92f, 0.9f, 0.85f, 1.0f);

        /// <summary>The menu open now (null when none is).</summary>
        public static GraffitiMenuView Current { get; private set; }

        public static bool IsAnyOpen => Current != null && Current.IsOpen;

        /// <summary>The frame it last closed on, so the Escape or Enter that closed it is not also read by another menu.</summary>
        public static int ClosedOnFrame { get; private set; } = -1;

        public bool IsOpen => _root != null && _root.activeSelf;

        /// <summary>The paint being drawn (for tests).</summary>
        public GraffitiCanvas Canvas => _canvas;

        public Color Colour { get; private set; } = DefaultColour;

        public int Brush { get; private set; } = 6;

        /// <summary>Where the piece being painted goes: its centre on the wall and the wall's outward normal.</summary>
        public Vector3 Centre => _centre;

        public Vector3 Normal => _normal;

        /// <summary>True while carrying on a piece that was already up.</summary>
        public bool CarryingOn => _editing != null;

        /// <summary>The menu's own canvas (null until first opened): a capture draws it with its camera.</summary>
        public UnityEngine.Canvas Overlay => _root != null ? _root.GetComponent<UnityEngine.Canvas>() : null;

        private readonly GraffitiCanvas _canvas = new GraffitiCanvas();
        private GameObject _root;
        private RawImage _wall;
        private RawImage _paint;
        private RawImage _square;
        private RawImage _hueStrip;
        private Image _swatch;
        private Text _brushLabel;
        private Texture2D _paintTexture;
        private Texture2D _maskTexture;
        private RenderTexture _snapshot;
        private int _shownVersion = -1;
        private float _hue;
        private float _saturation = 0.85f;
        private float _value = 0.95f;
        private Vector2? _last;
        private AudioSource _hiss;
        private bool _wasCursorCaptured;

        private PlayerInputReader _reader;
        private Behaviour _cameraDriver;
        private ISprayCan _can;
        private Vector3 _centre;
        private Vector3 _normal;
        private int _id;
        private GraffitiPanel _editing;

        private void OnEnable()
        {
            PlayerInputReader.SprayRequested += OnSprayRequested;
        }

        private void OnDisable()
        {
            PlayerInputReader.SprayRequested -= OnSprayRequested;
            if (IsOpen)
            {
                Close(apply: false);
            }
        }

        private void OnDestroy()
        {
            if (_paintTexture != null)
            {
                Destroy(_paintTexture);
            }
            if (_maskTexture != null)
            {
                Destroy(_maskTexture);
            }
            if (_root != null)
            {
                Destroy(_root);   // its own root object, not under the HUD (see Build)
            }
            if (_snapshot != null)
            {
                _snapshot.Release();
                Destroy(_snapshot);
            }
            if (Current == this)
            {
                Current = null;
            }
        }

        private void OnSprayRequested(PlayerInputReader reader)
        {
            if (IsOpen || reader == null || IsAnyOpen)
            {
                return;
            }
            Camera view = Camera.main;
            if (view == null || !TryFindWall(reader.transform, view, out Vector3 point, out Vector3 normal))
            {
                return;
            }
            OpenAt(reader, point, normal);
        }

        /// <summary>
        /// The wall the player is facing: down the middle of the screen, the first solid thing that is not a body, within reach of the body,
        /// and upright (a wall, not a floor or a roof). Public for tests.
        /// </summary>
        public static bool TryFindWall(Transform body, Camera view, out Vector3 point, out Vector3 normal)
        {
            point = default;
            normal = default;
            Vector3 chest = body.position + Vector3.up * 1.3f;
            Ray ray = view.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0.0f));
            float range = Vector3.Distance(view.transform.position, chest) + ReachMetres + 1.0f;
            RaycastHit[] hits = Physics.RaycastAll(ray, range, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                Collider c = hit.collider;
                if (c == null || c.transform.IsChildOf(body) || c.GetComponentInParent<IDamageable>() != null
                    || (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) || c.GetComponentInParent<GraffitiPanel>() != null)
                {
                    continue;
                }
                if (Mathf.Abs(hit.normal.y) > 0.35f || Vector3.Distance(hit.point, chest) > ReachMetres + 1.3f)
                {
                    return false;
                }
                point = hit.point;
                normal = new Vector3(hit.normal.x, 0.0f, hit.normal.z).normalized;
                return true;
            }
            return false;
        }

        /// <summary>Opens the canvas on a wall spot (public for tests): carries on the piece already there, if any.</summary>
        public void OpenAt(PlayerInputReader reader, Vector3 point, Vector3 normal)
        {
            Build();
            _reader = reader;
            _can = reader != null ? reader.EquippedSprayCan : null;
            if (_can != null)
            {
                Colour = new Color(_can.Colour.r, _can.Colour.g, _can.Colour.b, 1.0f);
                Brush = Mathf.Clamp(_can.Brush, MinBrush, MaxBrush);
            }
            Color.RGBToHSV(Colour, out _hue, out _saturation, out _value);
            ShowSquare();
            _normal = new Vector3(normal.x, 0.0f, normal.z).normalized;
            if (GraffitiWall.TryFindNear(point, _normal, out GraffitiPanel panel))
            {
                _editing = panel;
                _centre = panel.Piece.Centre;
                _normal = panel.Piece.Normal.normalized;
                _id = panel.Piece.Id;
                if (!_canvas.Decode(panel.Piece.Paint))
                {
                    _canvas.Clear();
                }
            }
            else
            {
                _editing = null;
                _centre = point;
                _id = GraffitiWall.IdFor(point, _normal);
                _canvas.Clear();
            }
            // Paint sticks only to the wall's own face: not past its corner, on a pillar, or in a window's hole (shown hatched).
            _canvas.Mask = GraffitiWall.WallMask(_centre, _normal, GraffitiWall.PieceSize, _reader != null ? _reader.transform : null);
            ShowMask();
            TakeSnapshot();
            _shownVersion = -1;
            _last = null;
            EnsureEventSystem();
            _root.SetActive(true);
            Current = this;
            Refresh();
            if (_reader != null)
            {
                _reader.enabled = false;   // the player stands still while painting, as with the other menus
            }
            Camera main = Camera.main;
            _cameraDriver = main != null ? main.GetComponent<ThirdPersonCamera>() : null;
            if (_cameraDriver != null)
            {
                _cameraDriver.enabled = false;
            }
            _wasCursorCaptured = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>Closes it: with <paramref name="apply"/> the piece goes up for everyone; without, the wall is left as it was.</summary>
        public void Close(bool apply)
        {
            if (!IsOpen)
            {
                return;
            }
            if (apply && (!_canvas.IsEmpty || _editing != null))
            {
                var piece = new GraffitiPiece
                {
                    Id = _id, Centre = _centre, Normal = _normal, Size = GraffitiWall.PieceSize, Paint = _canvas.Encode(),
                };
                GraffitiNetwork.PutForEveryone(piece);
            }
            if (_can != null)
            {
                _can.Colour = Colour;
                _can.Brush = Brush;
            }
            if (_hiss != null)
            {
                _hiss.Stop();
            }
            _root.SetActive(false);
            Current = null;
            ClosedOnFrame = Time.frameCount;
            if (_reader != null)
            {
                _reader.enabled = true;
            }
            if (_cameraDriver != null)
            {
                _cameraDriver.enabled = true;
            }
            if (_wasCursorCaptured)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        /// <summary>Sprays at a point of the canvas, 0..1 across and up (the mouse goes through this; so do tests).</summary>
        public void SprayAt(Vector2 uv)
        {
            Vector2 pixel = uv * GraffitiCanvas.Size;
            if (_last.HasValue)
            {
                _canvas.Stroke(_last.Value, pixel, Brush, Colour);
            }
            else
            {
                _canvas.Spray(pixel.x, pixel.y, Brush, Colour, 0.7f);
            }
            _last = pixel;
        }

        /// <summary>The can taken off the wall: the next spray starts a new line.</summary>
        public void LiftBrush() => _last = null;

        public void SetColour(Color colour)
        {
            Colour = new Color(colour.r, colour.g, colour.b, 1.0f);
            Color.RGBToHSV(Colour, out _hue, out _saturation, out _value);
            if (_square != null)
            {
                ShowSquare();
            }
            Refresh();
        }

        public void SetBrush(int size)
        {
            Brush = Mathf.Clamp(size, MinBrush, MaxBrush);
            Refresh();
        }

        public void ClearPaint()
        {
            _canvas.Clear();
            LiftBrush();
        }

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    Close(apply: false);
                    return;
                }
                if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                {
                    Close(apply: true);
                    return;
                }
                if (keyboard.leftBracketKey.wasPressedThisFrame)
                {
                    SetBrush(Brush - 1);
                }
                if (keyboard.rightBracketKey.wasPressedThisFrame)
                {
                    SetBrush(Brush + 1);
                }
            }
            bool spraying = false;
            if (mouse != null && mouse.leftButton.isPressed)
            {
                Vector2 screen = mouse.position.ReadValue();
                if (TryPointIn(_paint.rectTransform, screen, out Vector2 uv))
                {
                    SprayAt(uv);
                    spraying = true;
                }
                else
                {
                    LiftBrush();
                    if (TryPointIn(_square.rectTransform, screen, out Vector2 sv))
                    {
                        _saturation = sv.x;
                        _value = sv.y;
                        Colour = Color.HSVToRGB(_hue, _saturation, _value);
                        Refresh();
                    }
                    else if (TryPointIn(_hueStrip.rectTransform, screen, out Vector2 h))
                    {
                        _hue = Mathf.Clamp01(h.x);
                        Colour = Color.HSVToRGB(_hue, _saturation, _value);
                        ShowSquare();
                        Refresh();
                    }
                }
            }
            else
            {
                LiftBrush();
            }
            if (_hiss != null)
            {
                if (spraying && !_hiss.isPlaying)
                {
                    _hiss.Play();
                }
                else if (!spraying && _hiss.isPlaying)
                {
                    _hiss.Stop();
                }
            }
            ShowPaint();
        }

        /// <summary>Uploads the paint to the screen when it has changed.</summary>
        public void ShowPaint()
        {
            if (_paintTexture == null || _canvas.Version == _shownVersion)
            {
                return;
            }
            _shownVersion = _canvas.Version;
            _paintTexture.SetPixels32(_canvas.Pixels);
            _paintTexture.Apply(false, false);
        }

        private static bool TryPointIn(RectTransform rect, Vector2 screen, out Vector2 uv)
        {
            uv = default;
            if (rect == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, null, out Vector2 local))
            {
                return false;
            }
            Rect r = rect.rect;
            uv = new Vector2((local.x - r.xMin) / r.width, (local.y - r.yMin) / r.height);
            return uv.x >= 0.0f && uv.x <= 1.0f && uv.y >= 0.0f && uv.y <= 1.0f;
        }

        /// <summary>Hatches the part of the canvas that is not the wall, where paint will not stick (GraffitiCanvas.Mask).</summary>
        private void ShowMask()
        {
            byte[] mask = _canvas.Mask;
            const int size = GraffitiCanvas.Size;
            var pixels = new Color32[size * size];
            if (mask != null)
            {
                var stripe = new Color32(0, 0, 0, 170);
                var gap = new Color32(30, 8, 8, 110);
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        int i = y * size + x;
                        if (mask[i] == 0)
                        {
                            pixels[i] = ((x + y) / 6) % 2 == 0 ? stripe : gap;
                        }
                    }
                }
            }
            _maskTexture.SetPixels32(pixels);
            _maskTexture.Apply(false, false);
        }

        /// <summary>The colour square for the hue now: a new texture each time (the old one let go of, so dragging the hue leaks nothing).</summary>
        private void ShowSquare()
        {
            Texture old = _square.texture;
            _square.texture = CreatorControls.BuildSquare(_hue);
            if (old != null)
            {
                Destroy(old);
            }
        }

        private void Refresh()
        {
            if (_swatch != null)
            {
                _swatch.color = Colour;
            }
            if (_brushLabel != null)
            {
                _brushLabel.text = "BRUSH " + Brush;
            }
        }

        /// <summary>The wall section, photographed square on from just in front of it; the piece being carried on is hidden (it is the paint layer).</summary>
        private void TakeSnapshot()
        {
            if (_snapshot == null)
            {
                _snapshot = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32) { name = "GraffitiWall" };
            }
            var cameraObject = new GameObject("GraffitiSnapshot");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = GraffitiWall.PieceSize * 0.5f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 3.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.2f, 0.2f, 0.22f);
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, _normal).normalized;
            cameraObject.transform.SetPositionAndRotation(_centre + _normal * 0.6f, Quaternion.LookRotation(-_normal, up));
            camera.targetTexture = _snapshot;
            Renderer hidden = _editing != null ? _editing.GetComponent<Renderer>() : null;
            bool hid = hidden != null && hidden.enabled;
            if (hid)
            {
                hidden.enabled = false;
            }
            try
            {
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    camera.Render();
                }
            }
            finally
            {
                if (hid)
                {
                    hidden.enabled = true;
                }
                camera.targetTexture = null;
                Destroy(cameraObject);
            }
            _wall.texture = _snapshot;
        }

        // --- building it ---

        private void Build()
        {
            if (_root != null)
            {
                return;
            }
            // A ROOT canvas of its own, not under the HUD's: a canvas inside another takes the outer one's render mode and sorting and fades
            // with it, so the HUD fading out for a menu would fade this too (the skill menu stands alone the same way). Kept in the HUD's scene.
            _root = new GameObject("GraffitiCanvas", typeof(RectTransform));
            if (gameObject.scene.IsValid())
            {
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_root, gameObject.scene);
            }
            var canvas = _root.AddComponent<UnityEngine.Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = SkillMenuView.DesignResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            _root.AddComponent<GraphicRaycaster>();
            var root = (RectTransform)_root.transform;

            Image backdrop = MakeImage("Backdrop", root, Backdrop);
            Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true;   // nothing behind the menu takes a click

            Text title = MakeText("Title", root, "GRAFFITI", 24, TextAnchor.MiddleCenter);
            Place(title.rectTransform, new Vector2(0.0f, 296.0f), new Vector2(400.0f, 34.0f));

            // The wall, and the paint over it.
            Image frame = MakeImage("Frame", root, PanelColour);
            Place(frame.rectTransform, new Vector2(-160.0f, -6.0f), new Vector2(CanvasSize + 12.0f, CanvasSize + 12.0f));
            _wall = MakeRaw("Wall", root, null, Color.white);
            Place(_wall.rectTransform, new Vector2(-160.0f, -6.0f), new Vector2(CanvasSize, CanvasSize));
            _maskTexture = new Texture2D(GraffitiCanvas.Size, GraffitiCanvas.Size, TextureFormat.RGBA32, false)
            {
                name = "GraffitiMask", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point,
            };
            RawImage masked = MakeRaw("NotTheWall", root, _maskTexture, Color.white);
            Place(masked.rectTransform, new Vector2(-160.0f, -6.0f), new Vector2(CanvasSize, CanvasSize));
            _paintTexture = new Texture2D(GraffitiCanvas.Size, GraffitiCanvas.Size, TextureFormat.RGBA32, false)
            {
                name = "GraffitiPaint", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
            };
            _paint = MakeRaw("Paint", root, _paintTexture, Color.white);
            Place(_paint.rectTransform, new Vector2(-160.0f, -6.0f), new Vector2(CanvasSize, CanvasSize));

            // The colour, the brush, the buttons.
            Image side = MakeImage("Side", root, PanelColour);
            Place(side.rectTransform, new Vector2(300.0f, -6.0f), new Vector2(250.0f, CanvasSize + 12.0f));
            Text colourLabel = MakeText("ColourLabel", root, "COLOUR", 14, TextAnchor.MiddleLeft);
            Place(colourLabel.rectTransform, new Vector2(300.0f, 222.0f), new Vector2(210.0f, 20.0f));
            _square = MakeRaw("SaturationValue", root, CreatorControls.BuildSquare(_hue), Color.white);
            Place(_square.rectTransform, new Vector2(300.0f, 112.0f), new Vector2(210.0f, 190.0f));
            _hueStrip = MakeRaw("Hue", root, CreatorControls.BuildHueStrip(), Color.white);
            Place(_hueStrip.rectTransform, new Vector2(300.0f, 2.0f), new Vector2(210.0f, 20.0f));
            _swatch = MakeImage("Swatch", root, Colour);
            Place(_swatch.rectTransform, new Vector2(240.0f, -46.0f), new Vector2(90.0f, 40.0f));

            _brushLabel = MakeText("BrushLabel", root, "BRUSH", 14, TextAnchor.MiddleLeft);
            Place(_brushLabel.rectTransform, new Vector2(320.0f, -100.0f), new Vector2(170.0f, 22.0f));
            MakeButton(root, "-", new Vector2(212.0f, -100.0f), new Vector2(36.0f, 30.0f), ButtonColour, () => SetBrush(Brush - 1));
            MakeButton(root, "+", new Vector2(388.0f, -100.0f), new Vector2(36.0f, 30.0f), ButtonColour, () => SetBrush(Brush + 1));

            MakeButton(root, "DONE", new Vector2(300.0f, -160.0f), new Vector2(210.0f, 38.0f), DoneColour, () => Close(apply: true));
            MakeButton(root, "CLEAR", new Vector2(246.0f, -206.0f), new Vector2(102.0f, 34.0f), ButtonColour, ClearPaint);
            MakeButton(root, "CANCEL", new Vector2(354.0f, -206.0f), new Vector2(102.0f, 34.0f), ButtonColour, () => Close(apply: false));

            Text hint = MakeText("Hint", root,
                "Drag on the wall to spray (hatched: not the wall, paint will not stick)   [ ] brush size   Enter done   Esc cancel", 13,
                TextAnchor.MiddleCenter);
            hint.color = new Color(Ink.r, Ink.g, Ink.b, 0.7f);
            Place(hint.rectTransform, new Vector2(0.0f, -290.0f), new Vector2(900.0f, 22.0f));

            _hiss = gameObject.AddComponent<AudioSource>();
            _hiss.clip = HissClip();
            _hiss.loop = true;
            _hiss.playOnAwake = false;
            _hiss.spatialBlend = 0.0f;
            _hiss.volume = 0.35f;

            _root.SetActive(false);
        }

        private static AudioClip _hissClip;

        /// <summary>The can's hiss: a second of soft noise, made once (no sound file).</summary>
        private static AudioClip HissClip()
        {
            if (_hissClip != null)
            {
                return _hissClip;
            }
            const int rate = 22050;
            var samples = new float[rate];
            uint state = 2463534242u;
            float smooth = 0.0f;
            for (int i = 0; i < samples.Length; i++)
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                float white = (state >> 8) / 8388608.0f - 1.0f;
                smooth = Mathf.Lerp(smooth, white, 0.55f);   // take the harshest top off
                samples[i] = (white - smooth * 0.6f) * 0.5f;
            }
            _hissClip = AudioClip.Create("SprayHiss", samples.Length, 1, rate, false);
            _hissClip.SetData(samples, 0);
            return _hissClip;
        }

        private static Font Font => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        private static RectTransform NewChild(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Centred on the screen's middle, at <paramref name="at"/> design pixels, <paramref name="size"/> big.</summary>
        private static void Place(RectTransform rect, Vector2 at, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = at;
            rect.sizeDelta = size;
        }

        private static Image MakeImage(string name, RectTransform parent, Color colour)
        {
            Image image = NewChild(name, parent).gameObject.AddComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        private static RawImage MakeRaw(string name, RectTransform parent, Texture texture, Color colour)
        {
            RawImage image = NewChild(name, parent).gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        private static Text MakeText(string name, RectTransform parent, string content, int size, TextAnchor alignment)
        {
            Text text = NewChild(name, parent).gameObject.AddComponent<Text>();
            text.font = Font;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.color = Ink;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.text = content;
            return text;
        }

        private static void MakeButton(RectTransform parent, string label, Vector2 at, Vector2 size, Color colour, UnityEngine.Events.UnityAction onClick)
        {
            Image face = MakeImage(label + "Button", parent, colour);
            face.raycastTarget = true;
            Place(face.rectTransform, at, size);
            var button = face.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.onClick.AddListener(onClick);
            Text text = MakeText("Label", face.rectTransform, label, 15, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null || Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }
            var events = new GameObject("EventSystem");
            events.AddComponent<EventSystem>();
            events.AddComponent<InputSystemUIInputModule>();
        }
    }
}
