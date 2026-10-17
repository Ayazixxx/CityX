using UnityEngine;

namespace CityX.Core
{
    /// <summary>Finding a part of a model by name, wherever it sits in the hierarchy.</summary>
    public static class TransformSearch
    {
        /// <summary>
        /// The first transform called <paramref name="name"/> in <paramref name="root"/>'s hierarchy, the root itself included:
        /// depth first, children in order, inactive ones too. Null when there is none.
        /// </summary>
        public static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }
    }
}
