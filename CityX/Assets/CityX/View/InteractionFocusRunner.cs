using UnityEngine;

namespace CityX.View
{
    /// <summary>Resolves InteractionFocus's offers after every Update has had its say. Made by InteractionFocus itself, hidden.</summary>
    public sealed class InteractionFocusRunner : MonoBehaviour
    {
        private void LateUpdate()
        {
            InteractionFocus.Resolve();
        }
    }
}
