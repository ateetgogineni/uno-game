using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnoGame.UI
{
    /// <summary>Press feedback for buttons: squash on press, springy release, click sound.</summary>
    public sealed class ButtonFx : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        public bool sound = true;
        Vector3 rest = Vector3.one;
        bool down;
        Selectable sel;

        void Awake()
        {
            rest = transform.localScale;
            sel = GetComponent<Selectable>();
        }

        bool Usable => sel == null || sel.IsInteractable();

        public void OnPointerDown(PointerEventData e)
        {
            if (!Usable) return;
            down = true;
            Tw.Scale(transform, rest * 0.92f, 0.08f, Ease.OutQuad);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!down) return;
            down = false;
            Tw.Scale(transform, rest, 0.32f, Ease.OutBackStrong);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (sound && Usable) App.Play(Sound.Click);
        }

        void OnDisable()
        {
            down = false;
            Tw.Kill(transform, "scale");
            transform.localScale = rest;
        }
    }
}
