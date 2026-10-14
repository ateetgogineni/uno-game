using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnoGame.UI
{
    /// <summary>Breathing scale, used to draw attention (Play button, UNO button).</summary>
    public sealed class Pulse : MonoBehaviour
    {
        public float amount = 0.06f;
        public float speed = 3f;
        Vector3 rest;

        void OnEnable() => rest = transform.localScale == Vector3.zero ? Vector3.one : transform.localScale;

        void Update()
        {
            float s = 1f + Mathf.Sin(Time.unscaledTime * speed) * amount;
            transform.localScale = rest * s;
        }

        void OnDisable() => transform.localScale = rest;
    }
}
