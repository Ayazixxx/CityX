using UnityEngine;

namespace CityX.UI
{
    /// <summary>
    /// Switches the creator's 3D half on and off with its UI half.
    ///
    /// They cannot be the same object: a body cannot live under a Canvas, and
    /// the panel has to be under one. Something has to keep the two in step,
    /// and a component that does it on enable and disable cannot forget the
    /// way a caller can.
    /// </summary>
    public sealed class CreatorStage : MonoBehaviour
    {
        public GameObject Stage;

        private void OnEnable()
        {
            if (Stage != null)
            {
                Stage.SetActive(true);
            }
        }

        private void OnDisable()
        {
            if (Stage != null)
            {
                Stage.SetActive(false);
            }
        }
    }
}
