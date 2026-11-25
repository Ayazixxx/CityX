using CityX.Wearables;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CityX.UI
{
    /// <summary>The heat press screen's hands: drawing, the colour, the brush, a picture laid on, and the keys (docs/HEAT_PRESS.md).</summary>
    public sealed partial class HeatPressView
    {
        /// <summary>How much a picture grows or shrinks for one notch of the wheel.</summary>
        public const float PictureScaleStep = 1.08f;

        /// <summary>Degrees a picture turns for a pixel of right-drag.</summary>
        public const float TurnDegreesPerPixel = 0.4f;

        private bool _stroking;
        private Vector2 _lastPixel;
        private int _shownVersion = -1;
        private ShirtDesignCanvas.Side _shownSide;
        private Color32[] _shownPixels;
        private float _hue;
        private float _saturation = 0.85f;
        private float _value = 0.95f;

        // The picture being laid on: its pixels, where its middle is (design pixels), how wide, how turned.
        private bool _placing;
        private Texture2D _picture;
        private Vector2 _pictureCentre;
        private float _pictureWidth;
        private float _pictureDegrees;
        private Vector2 _dragFrom;
        private bool _dragging;
        private bool _turning;

        /// <summary>Whether a picture is being laid on now.</summary>
        public bool PlacingPicture => _placing;

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }
            ReceivePicture();
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard != null && Keys(keyboard))
            {
                return;   // closed
            }
            if (mouse != null)
            {
                Pointer(mouse, keyboard);
            }
            ShowDesign();
        }

        /// <summary>The keys; true when one closed the screen.</summary>
        private bool Keys(Keyboard keyboard)
        {
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (_placing) CancelPicture();
                else Close();
                return !_placing && !IsOpen;
            }
            bool control = keyboard.ctrlKey.isPressed;
            if (control && keyboard.zKey.wasPressedThisFrame)
            {
                if (keyboard.shiftKey.isPressed) Canvas.Redo();
                else Canvas.Undo();
            }
            if (control && keyboard.yKey.wasPressedThisFrame)
            {
                Canvas.Redo();
            }
            if (keyboard.tabKey.wasPressedThisFrame)
            {
                TurnShirt();
            }
            if (keyboard.leftBracketKey.wasPressedThisFrame) SetBrush(Brush - 2);
            if (keyboard.rightBracketKey.wasPressedThisFrame) SetBrush(Brush + 2);
            if (!control && keyboard.bKey.wasPressedThisFrame) SetTool(Tool.Brush);
            if (!control && keyboard.xKey.wasPressedThisFrame) SetTool(Tool.Eraser);
            if (_placing && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)) PlacePicture();
            return false;
        }

        private void Pointer(Mouse mouse, Keyboard keyboard)
        {
            Vector2 screen = mouse.position.ReadValue();
            bool overShirt = PixelAt(screen, out Vector2 pixel, out bool inside);
            float wheel = mouse.scroll.ReadValue().y;
            if (_placing)
            {
                DragPicture(mouse, pixel, overShirt && inside, wheel);
                return;
            }
            if (Mathf.Abs(wheel) > 0.01f && inside)
            {
                SetBrush(Brush + (wheel > 0.0f ? 1 : -1));
            }
            bool alt = keyboard != null && keyboard.altKey.isPressed;
            if (mouse.leftButton.wasPressedThisFrame && inside)
            {
                if (alt)
                {
                    Color32 picked = Canvas.ColourAt(Side, Mathf.FloorToInt(pixel.x), Mathf.FloorToInt(pixel.y));   // the eyedropper
                    if (picked.a > 0) SetColour(picked);
                    return;
                }
                Canvas.BeginChange(Side);
                _stroking = true;
                _lastPixel = pixel;
                Canvas.Stroke(Side, pixel, pixel, Brush, Colour, CurrentTool == Tool.Eraser);
                return;
            }
            if (_stroking && mouse.leftButton.isPressed && overShirt)
            {
                Canvas.Stroke(Side, _lastPixel, pixel, Brush, Colour, CurrentTool == Tool.Eraser);
                _lastPixel = pixel;
            }
            if (!mouse.leftButton.isPressed)
            {
                _stroking = false;
            }
            if (mouse.leftButton.isPressed && !_stroking)
            {
                PickColour(screen);
            }
        }

        /// <summary>The design pixel under the pointer (rows up), and whether it is inside the print area.</summary>
        private bool PixelAt(Vector2 screen, out Vector2 pixel, out bool inside)
        {
            pixel = default;
            inside = false;
            RectTransform area = _design != null ? _design.rectTransform : null;
            if (area == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, null, out Vector2 local))
            {
                return false;
            }
            Rect r = area.rect;
            pixel = new Vector2((local.x - r.xMin) / r.width * ShirtDesignArea.Width, (local.y - r.yMin) / r.height * ShirtDesignArea.Height);
            inside = pixel.x >= 0.0f && pixel.y >= 0.0f && pixel.x <= ShirtDesignArea.Width && pixel.y <= ShirtDesignArea.Height;
            return true;
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

        /// <summary>The colour square and the hue strip, dragged on.</summary>
        private void PickColour(Vector2 screen)
        {
            if (TryPointIn(_square.rectTransform, screen, out Vector2 sv))
            {
                _saturation = sv.x;
                _value = sv.y;
                Colour = Color.HSVToRGB(_hue, _saturation, _value);
                ShowTools();
            }
            else if (TryPointIn(_hueStrip.rectTransform, screen, out Vector2 h))
            {
                _hue = Mathf.Clamp01(h.x);
                Colour = Color.HSVToRGB(_hue, _saturation, _value);
                ShowSquare();
                ShowTools();
            }
        }

        public void SetColour(Color colour)
        {
            Colour = new Color(colour.r, colour.g, colour.b, 1.0f);
            Color.RGBToHSV(Colour, out _hue, out _saturation, out _value);
            ShowSquare();
            ShowTools();
        }

        public void SetBrush(int size)
        {
            Brush = Mathf.Clamp(size, MinBrush, MaxBrush);
            ShowTools();
        }

        public void SetTool(Tool tool)
        {
            if (tool == Tool.Picture)
            {
                AskForPicture();
                return;
            }
            CancelPicture();
            CurrentTool = tool;
            ShowTools();
        }

        /// <summary>Front to back and back again.</summary>
        public void TurnShirt()
        {
            Side = Side == ShirtDesignCanvas.Side.Front ? ShirtDesignCanvas.Side.Back : ShirtDesignCanvas.Side.Front;
            _stroking = false;
            ShowLook(Chosen >= 0 ? LookOf(_shirts[Chosen].ItemId) : null);
            ShowTools();
        }

        /// <summary>The design as it is now on the shirt shown, uploaded when it has changed.</summary>
        private void ShowDesign()
        {
            if (_designTexture == null || (Canvas.Version == _shownVersion && Side == _shownSide))
            {
                return;
            }
            _shownVersion = Canvas.Version;
            _shownSide = Side;
            _shownPixels = Canvas.Composite(Side, _shownPixels ?? new Color32[ShirtDesignArea.Width * ShirtDesignArea.Height]);
            _designTexture.SetPixels32(_shownPixels);
            _designTexture.Apply(false, false);
            ShowTools();
        }

        // ---- a picture off this computer ----

        private void AskForPicture()
        {
            if (Chosen < 0)
            {
                return;
            }
            if (!PictureFile.Supported)
            {
                Say("Pictures from your computer need Windows.");
                return;
            }
            if (PictureFile.Ask("Pick a picture for your shirt"))
            {
                Say("Pick a picture in the window that opened (PNG or JPG).");
            }
        }

        /// <summary>A picture picked in the Open window: laid over the shirt to be moved, sized and turned.</summary>
        private void ReceivePicture()
        {
            string path = PictureFile.Poll();
            if (path == null)
            {
                return;
            }
            if (path.Length == 0)
            {
                Say(Hint);
                return;
            }
            Texture2D picture = PictureFile.Read(path, out string problem);
            if (picture == null)
            {
                Say(problem);
                return;
            }
            BeginPicture(picture);
        }

        /// <summary>Starts laying <paramref name="picture"/> over the shirt (public so a test hands one in without the Open window).</summary>
        public void BeginPicture(Texture2D picture)
        {
            CancelPicture();
            if (picture == null || Chosen < 0)
            {
                return;
            }
            _picture = picture;
            _placing = true;
            _pictureCentre = new Vector2(ShirtDesignArea.Width, ShirtDesignArea.Height) * 0.5f;
            float aspect = picture.height / (float)picture.width;
            _pictureWidth = 0.8f * Mathf.Min(ShirtDesignArea.Width, ShirtDesignArea.Height / aspect);   // in the area, a little in from its edges
            _pictureDegrees = 0.0f;
            _pictureImage.texture = picture;
            _pictureImage.gameObject.SetActive(true);
            CurrentTool = Tool.Picture;
            ShowPicture();
            ShowTools();
            Say("Drag to move it, scroll to size it, right-drag to turn it. PLACE lays it on the shirt.");
        }

        private void DragPicture(Mouse mouse, Vector2 pixel, bool inside, float wheel)
        {
            if (Mathf.Abs(wheel) > 0.01f)
            {
                _pictureWidth = Mathf.Clamp(_pictureWidth * (wheel > 0.0f ? PictureScaleStep : 1.0f / PictureScaleStep), 8.0f, ShirtDesignArea.Width * 3.0f);
            }
            if (mouse.leftButton.wasPressedThisFrame && inside)
            {
                _dragging = true;
                _dragFrom = pixel;
            }
            if (mouse.rightButton.wasPressedThisFrame && inside)
            {
                _turning = true;
                _dragFrom = pixel;
            }
            if (_dragging && mouse.leftButton.isPressed)
            {
                _pictureCentre += pixel - _dragFrom;
                _dragFrom = pixel;
            }
            if (_turning && mouse.rightButton.isPressed)
            {
                _pictureDegrees -= (pixel.x - _dragFrom.x) * TurnDegreesPerPixel;
                _dragFrom = pixel;
            }
            if (!mouse.leftButton.isPressed) _dragging = false;
            if (!mouse.rightButton.isPressed) _turning = false;
            ShowPicture();
        }

        /// <summary>Turns the picture being laid on by <paramref name="degrees"/> (the turn buttons).</summary>
        public void TurnPicture(float degrees)
        {
            _pictureDegrees += degrees;
            ShowPicture();
        }

        /// <summary>The picture over the whole print area, as big as fits.</summary>
        public void FitPicture()
        {
            if (!_placing) return;
            float aspect = _picture.height / (float)_picture.width;
            _pictureCentre = new Vector2(ShirtDesignArea.Width, ShirtDesignArea.Height) * 0.5f;
            _pictureWidth = Mathf.Min(ShirtDesignArea.Width, ShirtDesignArea.Height / aspect);
            _pictureDegrees = 0.0f;
            ShowPicture();
        }

        /// <summary>Lays the picture on the shirt where it is (undoable).</summary>
        public void PlacePicture()
        {
            if (!_placing)
            {
                return;
            }
            Canvas.Place(Side, _picture.GetPixels32(), _picture.width, _picture.height, _pictureCentre, _pictureWidth, _pictureDegrees);
            CancelPicture();
            Say(Hint);
        }

        public void CancelPicture()
        {
            if (_picture != null)
            {
                Destroy(_picture);
                _picture = null;
            }
            _placing = _dragging = _turning = false;
            if (_pictureImage != null)
            {
                _pictureImage.gameObject.SetActive(false);
            }
            if (CurrentTool == Tool.Picture)
            {
                CurrentTool = Tool.Brush;
            }
            ShowTools();
        }

        /// <summary>The picture where it is, over the print area: design pixels to the screen's.</summary>
        private void ShowPicture()
        {
            if (!_placing || _pictureImage == null)
            {
                return;
            }
            float scale = _design.rectTransform.rect.width / ShirtDesignArea.Width;
            var rect = _pictureImage.rectTransform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = _pictureCentre * scale;
            rect.sizeDelta = new Vector2(_pictureWidth, _pictureWidth * _picture.height / _picture.width) * scale;
            rect.localRotation = Quaternion.Euler(0.0f, 0.0f, _pictureDegrees);
        }
    }
}
