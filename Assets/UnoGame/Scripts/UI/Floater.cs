using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnoGame.UI
{
    /// <summary>Gentle idle bobbing.</summary>
    public sealed class Floater : MonoBehaviour
    {
        public float amplitude = 12f;
        public float speed = 1.4f;
        public float rotate = 0f;
        public float phase;
        Vector2 basePos;
        RectTransform rt;

        void OnEnable()
        {
            rt = (RectTransform)transform;
            basePos = rt.anchoredPosition;
        }

        void Update()
        {
            float t = Time.unscaledTime * speed + phase;
            rt.anchoredPosition = basePos + new Vector2(0, Mathf.Sin(t) * amplitude);
            if (rotate != 0f) rt.localEulerAngles = new Vector3(0, 0, Mathf.Sin(t * 0.8f) * rotate);
        }

        public void Rebase() => basePos = ((RectTransform)transform).anchoredPosition;
    }
}
