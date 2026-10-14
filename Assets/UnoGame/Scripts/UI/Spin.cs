using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnoGame.UI
{
    /// <summary>Continuous rotation.</summary>
    public sealed class Spin : MonoBehaviour
    {
        public float degreesPerSecond = -90f;
        void Update() => transform.Rotate(0, 0, degreesPerSecond * Time.unscaledDeltaTime);
    }
}
