using System.Collections.Generic;
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
    ///
    /// Two panels share the creator's stage (the character list stands its characters on the creator's belt): it is up while either is,
    /// and stays up across the change from one to the other (TitleScreen.Show puts the new panel up before it takes the old one down).
    /// </summary>
    public sealed class CreatorStage : MonoBehaviour
    {
        public GameObject Stage;

        private static readonly Dictionary<GameObject, int> Users = new Dictionary<GameObject, int>();

        private void OnEnable()
        {
            if (Stage == null)
            {
                return;
            }
            Users.TryGetValue(Stage, out int users);
            Users[Stage] = users + 1;
            Stage.SetActive(true);
        }

        private void OnDisable()
        {
            if (Stage == null)
            {
                return;
            }
            Users.TryGetValue(Stage, out int users);
            if (users <= 1)
            {
                Users.Remove(Stage);
                Stage.SetActive(false);
            }
            else
            {
                Users[Stage] = users - 1;
            }
        }
    }
}
