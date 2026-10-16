using UnityEngine;
using UnityEngine.UI;

namespace UnoGame.UI
{
    /// <summary>Loading-screen card that keeps flipping between its back and random faces.</summary>
    public sealed class CardFlipper : MonoBehaviour
    {
        public Image face;
        public float period = 1.1f;
        int lastHalf = -1;

        void Update()
        {
            float t = Time.unscaledTime / period;
            float s = Mathf.Cos(t * Mathf.PI);
            transform.localScale = new Vector3(Mathf.Max(0.02f, Mathf.Abs(s)), 1f, 1f);
            transform.localEulerAngles = new Vector3(0, 0, Mathf.Sin(t * 0.7f) * 6f);
            int half = Mathf.FloorToInt(t + 0.5f);
            if (half == lastHalf || face == null || ArtLibrary.I == null) return;
            lastHalf = half;
            var cards = ArtLibrary.I.cards;
            face.sprite = half % 2 == 0 || cards.Count == 0 ? ArtLibrary.I.cardBack : cards[Random.Range(0, cards.Count)];
        }
    }
}
