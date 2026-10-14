using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UnoGame.UI
{
    /// <summary>
    /// Small helpers for building uGUI hierarchies from code. The editor scene builder uses them to lay
    /// out the scenes (the result is saved in the .unity files and can be tweaked in the editor);
    /// a few runtime effects use them too.
    /// </summary>
    public static class UIKit
    {
        static TMP_FontAsset font;

        public static TMP_FontAsset Font
        {
            get
            {
                if (font == null) font = Resources.Load<TMP_FontAsset>("Fonts/Inter SDF");
                if (font == null) font = TMP_Settings.defaultFontAsset;
                return font;
            }
        }

        public static Sprite S(string name) => ArtLibrary.I.Get(name);

        public static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Anchor at a point (0..1), place at pos with size; pivot defaults to the centre.</summary>
        public static RectTransform At(this RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Stretch to the parent with insets (left, bottom, right, top).</summary>
        public static RectTransform Fill(this RectTransform rt, float l = 0, float b = 0, float r = 0, float t = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        /// <summary>Stretch horizontally, fixed height, anchored to a vertical position (0 bottom .. 1 top).</summary>
        public static RectTransform HStrip(this RectTransform rt, float anchorY, float y, float height, float inset = 0)
        {
            rt.anchorMin = new Vector2(0, anchorY);
            rt.anchorMax = new Vector2(1, anchorY);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0, y);
            rt.sizeDelta = new Vector2(-inset * 2, height);
            return rt;
        }

        public static Image Img(Transform parent, string name, Sprite sprite, Color? color = null, bool raycast = false)
        {
            var rt = Node(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color ?? Color.white;
            img.raycastTarget = raycast;
            if (sprite != null && sprite.border != Vector4.zero) img.type = Image.Type.Sliced;
            return img;
        }

        public static TextMeshProUGUI Txt(Transform parent, string name, string text, float size, Color? color = null,
            TextAlignmentOptions align = TextAlignmentOptions.Center, FontStyles style = FontStyles.Normal)
        {
            var rt = Node(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color ?? Color.white;
            t.alignment = align;
            t.fontStyle = style;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.richText = true;
            return t;
        }

        static Material outlineMat;

        /// <summary>Dark outline + drop shadow material (Resources/Fonts/Inter SDF Outline, made by the editor setup).</summary>
        public static Material OutlineMat
        {
            get
            {
                if (outlineMat == null) outlineMat = Resources.Load<Material>("Fonts/Inter SDF Outline");
                return outlineMat;
            }
        }

        /// <summary>Gives the text a dark outline and drop shadow so it reads on any background.</summary>
        public static TextMeshProUGUI Shadowed(this TextMeshProUGUI t)
        {
            if (OutlineMat != null) t.fontSharedMaterial = OutlineMat;
            return t;
        }

        public static Button Btn(Transform parent, string name, Sprite bg, Vector2 size, string label = null, float fontSize = 54, Color? labelColor = null)
        {
            var img = Img(parent, name, bg, Color.white, true);
            img.rectTransform.sizeDelta = size;
            var btn = img.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = img;
            img.gameObject.AddComponent<ButtonFx>();
            if (label != null)
            {
                var t = Txt(img.transform, "Label", label, fontSize, labelColor ?? Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                t.rectTransform.Fill(10, 14, 10, 4);
            }
            return btn;
        }

        public static Button IconBtn(Transform parent, string name, string iconSprite, float size, Color? bg = null, float iconScale = 0.55f)
        {
            var img = Img(parent, name, S("circle"), bg ?? new Color(0, 0, 0, 0.35f), true);
            img.rectTransform.sizeDelta = new Vector2(size, size);
            var btn = img.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = img;
            img.gameObject.AddComponent<ButtonFx>();
            var icon = Img(img.transform, "Icon", S(iconSprite));
            icon.rectTransform.At(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * iconScale, size * iconScale));
            return btn;
        }

        public static CanvasGroup Group(Component c, float alpha = 1f, bool interactive = true)
        {
            var g = c.GetComponent<CanvasGroup>();
            if (g == null) g = c.gameObject.AddComponent<CanvasGroup>();
            g.alpha = alpha;
            g.interactable = interactive;
            g.blocksRaycasts = interactive;
            return g;
        }

        /// <summary>A full-screen dark backdrop that blocks clicks.</summary>
        public static Image Dim(Transform parent, float alpha = 0.6f)
        {
            var img = Img(parent, "Dim", S("rounded_small"), new Color(0.03f, 0.04f, 0.06f, alpha), true);
            img.type = Image.Type.Simple;
            img.rectTransform.Fill(-400, -400, -400, -400);
            return img;
        }
    }
}
