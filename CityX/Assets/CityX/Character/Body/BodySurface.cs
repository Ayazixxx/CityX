using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The body's own surface: the skinned body (<see cref="SkinName"/>) and the skull hung off the head bone (<see cref="HeadName"/>, see
    /// HeadMesh). Skin colour and paint go on these and nothing else: painting every renderer under a body also painted what it wore and
    /// held, so a coat turned skin-tan after a redraw and, in multiplayer, the shirt wore the face paint (owner, 2026-10-22).
    /// </summary>
    public static class BodySurface
    {
        public const string SkinName = "Skin";

        /// <summary>The skull's object (the head BONE is "head", lower case).</summary>
        public const string HeadName = "Head";

        /// <summary>The body's own renderers under <paramref name="root"/>: never a garment, hair, a hat or a weapon.</summary>
        public static List<Renderer> Renderers(Transform root)
        {
            var found = new List<Renderer>();
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                string name = renderer.gameObject.name;
                if (name == SkinName || name == HeadName)
                {
                    found.Add(renderer);
                }
            }
            return found;
        }
    }
}
