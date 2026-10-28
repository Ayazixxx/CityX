using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>Turns a glow's quad to face the camera every frame (the main one, or the first there is: a capture's own).</summary>
    public sealed class FacesCamera : MonoBehaviour
    {
        private static Camera[] _cameras = new Camera[4];

        private void LateUpdate()
        {
            Camera view = Camera.main;
            if (view == null && Camera.allCamerasCount > 0)
            {
                if (_cameras.Length < Camera.allCamerasCount)
                {
                    _cameras = new Camera[Camera.allCamerasCount];
                }
                Camera.GetAllCameras(_cameras);
                view = _cameras[0];
            }
            if (view == null)
            {
                return;
            }
            Vector3 away = transform.position - view.transform.position;
            if (away.sqrMagnitude > 1e-6f)
            {
                transform.rotation = Quaternion.LookRotation(away, view.transform.up);
            }
        }
    }
}
