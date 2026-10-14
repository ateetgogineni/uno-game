using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnoGame.UI
{
    /// <summary>Keeps this RectTransform inside the device safe area (notches, rounded corners).</summary>
    [ExecuteAlways]
    public sealed class SafeArea : MonoBehaviour
    {
        Rect last;
        Vector2Int lastSize;

        void OnEnable() => Apply();

        void Update()
        {
            if (Screen.safeArea != last || lastSize.x != Screen.width || lastSize.y != Screen.height) Apply();
        }

        void Apply()
        {
            var rt = (RectTransform)transform;
            last = Screen.safeArea;
            lastSize = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;
            Vector2 min = last.position, max = last.position + last.size;
            min.x /= Screen.width; min.y /= Screen.height;
            max.x /= Screen.width; max.y /= Screen.height;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
