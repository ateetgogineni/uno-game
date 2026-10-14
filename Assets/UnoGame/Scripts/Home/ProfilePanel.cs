using System;
using TMPro;
using UnoGame.Net;
using UnoGame.UI;
using UnityEngine;
using UnityEngine.UI;

namespace UnoGame.Home
{
    /// <summary>Edit name and avatar.</summary>
    public sealed class ProfilePanel : MonoBehaviour
    {
        public CanvasGroup group;
        public RectTransform panel;
        public Image preview;
        public TMP_InputField nameInput;
        public Button[] avatarButtons = new Button[Avatars.Count];
        public Image[] avatarRings = new Image[Avatars.Count];
        public Button save;
        public Button close;
        public TextMeshProUGUI error;

        int avatar;
        Action onSaved;

        public bool IsOpen => gameObject.activeSelf;

        void Awake()
        {
            for (int i = 0; i < avatarButtons.Length; i++)
            {
                int a = i;
                avatarButtons[i].onClick.AddListener(() => Select(a));
            }
            save.onClick.AddListener(Save);
            close.onClick.AddListener(Hide);
        }

        public void Show(Action saved)
        {
            onSaved = saved;
            var p = App.I.Profile;
            gameObject.SetActive(true);
            nameInput.text = p.name;
            error.text = "";
            Select(p.avatar);
            group.alpha = 0f;
            Tw.Alpha(group, 1f, 0.2f);
            panel.localScale = Vector3.one * 0.85f;
            Tw.Scale(panel, 1f, 0.35f, Ease.OutBack);
        }

        void Select(int a)
        {
            avatar = a;
            preview.sprite = ArtLibrary.I.Avatar(a);
            Tw.Punch(preview.transform, 0.12f, 0.3f);
            for (int i = 0; i < avatarRings.Length; i++) avatarRings[i].enabled = i == a;
        }

        void Save()
        {
            string clean = Profile.CleanName(nameInput.text);
            if (clean == null)
            {
                error.text = "Use 2-14 letters or numbers";
                App.Play(Sound.Error);
                Tw.Shake(nameInput.GetComponent<RectTransform>());
                return;
            }
            var p = App.I.Profile;
            p.name = clean;
            p.avatar = avatar;
            p.Save();
            App.I.Match.SendHello();
            onSaved?.Invoke();
            Hide();
        }

        public void Hide()
        {
            if (!IsOpen) return;
            Tw.Alpha(group, 0f, 0.15f).OnComplete(() => gameObject.SetActive(false));
        }

        public void Build()
        {
            var root = (RectTransform)transform;
            root.Fill();
            group = UIKit.Group(root);
            UIKit.Dim(root, 0.7f);
            panel = UIKit.Img(root, "Panel", UIKit.S("panel"), Color.white, true).rectTransform;
            panel.At(new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(940, 1260));
            var title = UIKit.Txt(panel, "Title", "Your Profile", 58, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            title.rectTransform.At(new Vector2(0.5f, 1), new Vector2(0, -80), new Vector2(700, 80));
            close = UIKit.IconBtn(panel, "Close", "icon_close", 88, new Color(1, 1, 1, 0.12f));
            ((RectTransform)close.transform).At(new Vector2(1, 1), new Vector2(-70, -70), new Vector2(88, 88));

            var ring = UIKit.Img(panel, "PreviewRing", UIKit.S("circle"), Color.white);
            ring.rectTransform.At(new Vector2(0.5f, 1), new Vector2(0, -275), new Vector2(232, 232));
            preview = UIKit.Img(panel, "Preview", ArtLibrary.I.avatars[0], Color.white);
            preview.rectTransform.At(new Vector2(0.5f, 1), new Vector2(0, -275), new Vector2(220, 220));

            // name field
            var field = UIKit.Img(panel, "NameField", UIKit.S("rounded"), new Color(0, 0, 0, 0.35f), true);
            field.rectTransform.At(new Vector2(0.5f, 1), new Vector2(0, -470), new Vector2(720, 110));
            field.pixelsPerUnitMultiplier = 1.3f;
            var area = UIKit.Node("Text Area", field.rectTransform).Fill(36, 6, 36, 6);
            area.gameObject.AddComponent<RectMask2D>();
            var placeholder = UIKit.Txt(area, "Placeholder", "Your name", 46, new Color(1, 1, 1, 0.35f), TextAlignmentOptions.Center);
            placeholder.rectTransform.Fill();
            var text = UIKit.Txt(area, "Text", "", 46, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            text.rectTransform.Fill();
            nameInput = field.gameObject.AddComponent<TMP_InputField>();
            nameInput.textViewport = area;
            nameInput.textComponent = text;
            nameInput.placeholder = placeholder;
            nameInput.characterLimit = 14;
            nameInput.lineType = TMP_InputField.LineType.SingleLine;
            nameInput.contentType = TMP_InputField.ContentType.Standard;
            nameInput.fontAsset = UIKit.Font;
            nameInput.pointSize = 46;
            var edit = UIKit.Img(field.rectTransform, "EditIcon", UIKit.S("icon_edit"), new Color(1, 1, 1, 0.5f));
            edit.rectTransform.At(new Vector2(1, 0.5f), new Vector2(-50, 0), new Vector2(44, 44));
            error = UIKit.Txt(panel, "Error", "", 32, new Color(1f, 0.45f, 0.45f));
            error.rectTransform.At(new Vector2(0.5f, 1), new Vector2(0, -552), new Vector2(720, 44));

            var label = UIKit.Txt(panel, "AvatarLabel", "Choose your avatar", 38, new Color(1, 1, 1, 0.75f));
            label.rectTransform.At(new Vector2(0.5f, 1), new Vector2(0, -606), new Vector2(720, 50));
            for (int i = 0; i < Avatars.Count; i++)
            {
                int col = i % 4, row = i / 4;
                var pos = new Vector2(-255 + col * 170, -715 - row * 162);
                var sel = UIKit.Img(panel, "Selected" + i, UIKit.S("ring_thick"), Palette.Gold);
                sel.rectTransform.At(new Vector2(0.5f, 1), pos, new Vector2(158, 158));
                avatarRings[i] = sel;
                var b = UIKit.Btn(panel, "Avatar" + i, ArtLibrary.I.avatars[i], new Vector2(136, 136));
                b.GetComponent<Image>().type = Image.Type.Simple;
                ((RectTransform)b.transform).At(new Vector2(0.5f, 1), pos, new Vector2(136, 136));
                avatarButtons[i] = b;
            }
            save = UIKit.Btn(panel, "Save", UIKit.S("btn_green"), new Vector2(460, 136), "SAVE", 54);
            ((RectTransform)save.transform).At(new Vector2(0.5f, 0), new Vector2(0, 100), new Vector2(460, 136));
        }
    }
}
