using UnityEngine;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// What the creator's picture pages (HAIR, FACE) are built from, so the two lay out and scroll alike: a piece hung from the top of the
    /// page, the scrolling window of tiles four to a row, a tile's place in it and its picture, and the hint under a page where a drag on
    /// the body turns it. Canvas units, from the top of the creator's panel, centred in it.
    /// </summary>
    public static class CreatorPageKit
    {
        /// <summary>A tile's size and the distance from one to the next, four to a row, four rows in view (more scroll).</summary>
        public const float TileSize = 50.0f, TilePitch = 56.0f;

        public const int PerRow = 4, RowsInView = 4;

        /// <summary>The window the tiles scroll in: four across, four rows down, without the gap after the last row.</summary>
        public const float WindowWidth = PerRow * TilePitch;

        public const float WindowHeight = RowsInView * TilePitch - (TilePitch - TileSize);

        /// <summary>A tile's frame, plain and lit (the hairstyle the head wears, the part the face wears).</summary>
        public static readonly Color TileIdle = new Color(0.17f, 0.18f, 0.22f, 1.0f);

        public static readonly Color TileLit = CreatorKit.Gold;

        public const string TurnHint = "Drag the body to turn it.\nScroll over it to zoom.";

        private const float TurnHintY = -526.0f;

        /// <summary>A <typeparamref name="T"/> on a new object hung from the top middle of <paramref name="parent"/>, centred on <paramref name="position"/>.</summary>
        public static T MakeRect<T>(Transform parent, string name, Vector2 position, Vector2 size) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1.0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return go.AddComponent<T>();
        }

        /// <summary>
        /// The scrolling window for <paramref name="count"/> tiles, its top edge at <paramref name="topY"/>: four rows in view, the rest
        /// reached with the wheel. Returns what the tiles go in (<see cref="MakeTile"/>); the window itself is its parent.
        /// </summary>
        public static Transform MakeTileWindow(Transform parent, string name, float topY, int count)
        {
            var view = new GameObject(name, typeof(RectTransform));
            var viewRect = (RectTransform)view.transform;
            viewRect.SetParent(parent, false);
            viewRect.anchorMin = viewRect.anchorMax = new Vector2(0.5f, 1.0f);
            viewRect.pivot = new Vector2(0.5f, 1.0f);
            viewRect.anchoredPosition = new Vector2(0.0f, topY);
            viewRect.sizeDelta = new Vector2(WindowWidth, WindowHeight);
            Image catcher = view.AddComponent<Image>();
            catcher.color = new Color(0.0f, 0.0f, 0.0f, 0.0f);   // clear, but takes the wheel for the scrolling
            view.AddComponent<RectMask2D>();

            var tiles = new GameObject("Tiles", typeof(RectTransform));
            var tilesRect = (RectTransform)tiles.transform;
            tilesRect.SetParent(view.transform, false);
            tilesRect.anchorMin = new Vector2(0.0f, 1.0f);
            tilesRect.anchorMax = new Vector2(1.0f, 1.0f);
            tilesRect.pivot = new Vector2(0.5f, 1.0f);
            tilesRect.anchoredPosition = Vector2.zero;
            int rows = (count + PerRow - 1) / PerRow;
            tilesRect.sizeDelta = new Vector2(0.0f, Mathf.Max(WindowHeight, rows * TilePitch - (TilePitch - TileSize)));

            ScrollRect scroll = view.AddComponent<ScrollRect>();
            scroll.content = tilesRect;
            scroll.viewport = viewRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24.0f;
            return tiles.transform;
        }

        /// <summary>Tile number <paramref name="index"/> in a window's <paramref name="tiles"/>, four to a row: its frame, plain.</summary>
        public static Image MakeTile(Transform tiles, string name, int index)
        {
            var position = new Vector2(((index % PerRow) - (PerRow - 1) * 0.5f) * TilePitch, -TileSize * 0.5f - (index / PerRow) * TilePitch);
            Image frame = MakeRect<Image>(tiles, name, position, new Vector2(TileSize, TileSize));
            frame.color = TileIdle;
            return frame;
        }

        /// <summary>A tile's picture inside its frame, or, where there is none yet, <paramref name="label"/> so it can still be told apart.</summary>
        public static void ShowPicture(Transform tile, Texture icon, string label)
        {
            if (icon == null)
            {
                CreatorKit.Caption(tile, label, 11, CreatorKit.Paper);
                return;
            }
            var go = new GameObject("Icon", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(tile, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(TileSize - 6.0f, TileSize - 6.0f);
            RawImage picture = go.AddComponent<RawImage>();
            picture.texture = icon;
            picture.raycastTarget = false;   // a press belongs to the tile under it
        }

        /// <summary>The hint at the foot of a page where a drag on the body turns it rather than paints it.</summary>
        public static Text MakeTurnHint(Transform page, string name)
        {
            return CreatorKit.MakeLabel(page, name, TurnHint, 10, CreatorKit.Hint, new Vector2(0.0f, TurnHintY), 30.0f);
        }
    }
}
