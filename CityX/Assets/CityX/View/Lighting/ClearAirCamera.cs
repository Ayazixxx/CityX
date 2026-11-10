using UnityEngine;
using UnityEngine.Rendering;

namespace CityX.View
{
    /// <summary>
    /// A camera that draws without the city's haze (CityHaze), whatever the scene it is in has set: the creator's two cameras stand in the
    /// title scene, whose night air is laid over every camera's picture, and it turned their daylight sky to black. The haze is switched
    /// off as this camera starts to draw and put back when it has drawn.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class ClearAirCamera : MonoBehaviour
    {
        private Camera _camera;
        private Vector4 _was;
        private bool _cleared;

        private void OnEnable()
        {
            _camera = GetComponent<Camera>();
            RenderPipelineManager.beginCameraRendering += Before;
            RenderPipelineManager.endCameraRendering += After;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= Before;
            RenderPipelineManager.endCameraRendering -= After;
            PutBack();
        }

        private void Before(ScriptableRenderContext context, Camera drawing)
        {
            if (drawing != _camera || _cleared)
            {
                return;
            }
            _was = CityHaze.Drawn;
            CityHaze.Drawn = Vector4.zero;
            _cleared = true;
        }

        private void After(ScriptableRenderContext context, Camera drawing)
        {
            if (drawing == _camera)
            {
                PutBack();
            }
        }

        private void PutBack()
        {
            if (_cleared)
            {
                CityHaze.Drawn = _was;
                _cleared = false;
            }
        }
    }
}
