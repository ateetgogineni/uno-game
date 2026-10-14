using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnoGame.Core;
using UnoGame.UI;
using UnityEngine;
using UnityEngine.UI;

namespace UnoGame.Game
{
    /// <summary>Two-button confirmation dialog.</summary>
    public sealed class ConfirmDialog : MonoBehaviour
    {
        public CanvasGroup group;
        public RectTransform panel;
        public TextMeshProUGUI title;
        public TextMeshProUGUI body;
        public Button ok;
        public TextMeshProUGUI okLabel;
        public Button cancel;
        public TextMeshProUGUI cancelLabel;
        Action onOk, onCancel;

        public bool IsOpen => gameObject.activeSelf;

        void Awake()
        {
            ok.onClick.AddListener(() => Close(true));
            cancel.onClick.AddListener(() => Close(false));
        }

        public void Show(string t, string b, string okText, string cancelText, Action okAction, Action cancelAction = null)
        {
            title.text = t;
            body.text = b;
            okLabel.text = okText;
            cancel.gameObject.SetActive(!string.IsNullOrEmpty(cancelText));
            if (cancelLabel != null) cancelLabel.text = cancelText ?? "";
            var okRt = (RectTransform)ok.transform;
            okRt.anchoredPosition = new Vector2(cancel.gameObject.activeSelf ? 170f : 0f, okRt.anchoredPosition.y);
            onOk = okAction;
            onCancel = cancelAction;
            gameObject.SetActive(true);
            group.alpha = 0f;
            group.blocksRaycasts = true;
            Tw.Alpha(group, 1f, 0.18f);
            panel.localScale = Vector3.one * 0.8f;
            Tw.Scale(panel, 1f, 0.3f, Ease.OutBack);
        }

        public void Close(bool accepted)
        {
            if (!IsOpen) return;
            group.blocksRaycasts = false;
            Tw.Alpha(group, 0f, 0.15f).OnComplete(() => gameObject.SetActive(false));
            var a = accepted ? onOk : onCancel;
            onOk = onCancel = null;
            a?.Invoke();
        }
    }
}
