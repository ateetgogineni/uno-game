using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UnoGame.UI
{
    /// <summary>Reports press and release (the Dare "hold steady" challenge).</summary>
    public sealed class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public event Action Down;
        public event Action Up;
        public bool IsHeld { get; private set; }

        public void OnPointerDown(PointerEventData e)
        {
            IsHeld = true;
            Down?.Invoke();
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!IsHeld) return;
            IsHeld = false;
            Up?.Invoke();
        }

        void OnDisable() => IsHeld = false;
    }
}
