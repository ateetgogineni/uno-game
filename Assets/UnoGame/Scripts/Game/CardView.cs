using System;
using UnoGame.Core;
using UnoGame.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnoGame.Game
{
    /// <summary>
    /// One card on screen. The root carries position/rotation/size scale; the Visual child is flipped,
    /// so a flip never fights with a move.
    /// </summary>
    public sealed class CardView : MonoBehaviour, IPointerClickHandler
    {
        public const float Width = 200f, Height = 280f;

        public RectTransform visual;
        public Image shadow;
        public Image glow;
        public Image face;
        public Image dim;

        public int Id { get; private set; } = -1;
        public bool FaceUp { get; private set; }
        public bool Lifted;
        public event Action<CardView> Clicked;

        public RectTransform Rect => (RectTransform)transform;
        public int SortKey => Id >= 0 ? Cards.SortKey(Table.FaceOf(Id), Id) : 0;

        public void Set(int id, bool faceUp)
        {
            Id = id;
            FaceUp = faceUp;
            face.sprite = faceUp && id >= 0 ? ArtLibrary.I.Face(id) : ArtLibrary.I.cardBack;
        }

        /// <summary>Re-draw for the current Flip side.</summary>
        public void Refresh() => Set(Id, FaceUp);

        /// <summary>Show a specific face (UNO Flip: the other side of an opponent's card).</summary>
        public void ShowFace(Face f, int side) => face.sprite = ArtLibrary.I.FaceSprite(f, side);

        public Tween Flip(bool toFaceUp, int id, float duration = 0.28f, float delay = 0f)
        {
            bool swapped = false;
            return Tw.Value(duration, k =>
            {
                float s;
                if (k < 0.5f) s = 1f - k * 2f;
                else
                {
                    if (!swapped) { swapped = true; Set(id, toFaceUp); }
                    s = k * 2f - 1f;
                }
                visual.localScale = new Vector3(Mathf.Max(0.02f, s), 1f, 1f);
            }, Ease.Linear, delay, visual, "flip").OnComplete(() =>
            {
                if (!swapped) Set(id, toFaceUp);
                visual.localScale = Vector3.one;
            });
        }

        public void SetDim(bool on, bool instant = false)
        {
            float a = on ? 0.45f : 0f;
            if (instant) { Tw.Kill(dim, "alpha"); var c = dim.color; c.a = a; dim.color = c; }
            else if (!Mathf.Approximately(dim.color.a, a)) Tw.Alpha(dim, a, 0.15f);
        }

        public void SetGlow(Color color, bool on)
        {
            if (on)
            {
                color.a = glow.color.a;
                glow.color = color;
                Tw.Alpha(glow, 0.9f, 0.2f);
            }
            else if (glow.color.a > 0f) Tw.Alpha(glow, 0f, 0.2f);
        }

        public void SetInteractive(bool on) => face.raycastTarget = on;

        public void OnPointerClick(PointerEventData e) => Clicked?.Invoke(this);

        /// <summary>Builds the card hierarchy (saved as Prefabs/Card.prefab by the editor builder).</summary>
        public static CardView Build()
        {
            var root = new GameObject("Card", typeof(RectTransform));
            root.layer = 5;
            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(Width, Height);
            var cv = root.AddComponent<CardView>();
            cv.visual = UIKit.Node("Visual", rt).Fill();
            cv.shadow = UIKit.Img(cv.visual, "Shadow", UIKit.S("card_shadow"), new Color(1, 1, 1, 0.85f));
            cv.shadow.type = Image.Type.Simple;
            cv.shadow.rectTransform.Fill(-23, -31, -23, -15);
            cv.glow = UIKit.Img(cv.visual, "Glow", UIKit.S("card_glow"), new Color(1, 1, 1, 0f));
            cv.glow.type = Image.Type.Simple;
            cv.glow.rectTransform.Fill(-26, -26, -26, -26);
            cv.face = UIKit.Img(cv.visual, "Face", ArtLibrary.I.cardBack, Color.white, true);
            cv.face.preserveAspect = false;
            cv.face.type = Image.Type.Simple;
            cv.face.rectTransform.Fill();
            cv.dim = UIKit.Img(cv.visual, "Dim", ArtLibrary.I.cardMask, new Color(0.02f, 0.03f, 0.05f, 0f));
            cv.dim.type = Image.Type.Simple;
            cv.dim.rectTransform.Fill();
            return cv;
        }
    }
}
