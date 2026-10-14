using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnoGame.UI
{
    /// <summary>
    /// Portrait layout scaling: tall phones scale by width (extra height goes to the table), short/wide
    /// screens (tablets) scale by height so nothing gets cut off at the top or bottom.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class AdaptiveScaler : MonoBehaviour
    {
        CanvasScaler scaler;

        void OnEnable()
        {
            scaler = GetComponent<CanvasScaler>();
            Apply();
        }

        void Update() => Apply();

        void Apply()
        {
            if (scaler == null || Screen.width <= 0) return;
            float aspect = Screen.height / (float)Screen.width;
            float reference = scaler.referenceResolution.y / scaler.referenceResolution.x;
            float match = aspect >= reference ? 0f : 1f;
            if (!Mathf.Approximately(scaler.matchWidthOrHeight, match)) scaler.matchWidthOrHeight = match;
        }
    }
}
