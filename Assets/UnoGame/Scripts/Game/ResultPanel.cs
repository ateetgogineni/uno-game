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
    /// <summary>End-of-game panel.</summary>
    public sealed class ResultPanel : MonoBehaviour
    {
        public CanvasGroup group;
        public RectTransform content;
        public Image rays;
        public Image ribbon;
        public TextMeshProUGUI title;
        public Image myAvatar, oppAvatar;
        public Image myCrown, oppCrown;
        public TextMeshProUGUI myName, oppName;
        public TextMeshProUGUI pointsText;
        public TextMeshProUGUI statsText;
        public Button playAgain;
        public Button home;
        public RectTransform confettiLayer;

        public bool IsOpen => gameObject.activeSelf;

        public void Show(bool won, PlayerInfo me, PlayerInfo opp, int points, string reason, Profile profile)
        {
            gameObject.SetActive(true);
            group.alpha = 0f;
            Tw.Alpha(group, 1f, 0.3f);
            ribbon.sprite = UIKit.S(won ? "ribbon_gold" : "ribbon_gray");
            title.text = won ? "YOU WON!" : "YOU LOST";
            rays.gameObject.SetActive(won);
            myAvatar.sprite = ArtLibrary.I.Avatar(me.avatar);
            oppAvatar.sprite = ArtLibrary.I.Avatar(opp.avatar);
            myName.text = me.name;
            oppName.text = opp.name;
            myCrown.gameObject.SetActive(won);
            oppCrown.gameObject.SetActive(!won);
            string why = reason == "left" ? (won ? "Opponent left the game" : "You left the game")
                : reason == "idle" ? (won ? "Opponent was inactive" : "You were inactive") : null;
            pointsText.text = won ? $"+{points} points" : $"{opp.name} scored {points}";
            if (why != null) pointsText.text = why + "\n<size=80%>" + pointsText.text + "</size>";
            statsText.text = $"Wins  <color=#FFC21A>{profile.wins}</color>     Games  <color=#FFC21A>{profile.games}</color>     Streak  <color=#FFC21A>{profile.streak}</color>";

            content.localScale = Vector3.one * 0.6f;
            Tw.Scale(content, 1f, 0.5f, Ease.OutBack);
            ribbon.rectTransform.localScale = Vector3.one * 1.6f;
            Tw.Scale(ribbon.rectTransform, 1f, 0.45f, Ease.OutBackStrong, 0.15f);
            playAgain.transform.localScale = Vector3.zero;
            home.transform.localScale = Vector3.zero;
            Tw.Scale(playAgain.transform, 1f, 0.4f, Ease.OutBack, 0.45f);
            Tw.Scale(home.transform, 1f, 0.4f, Ease.OutBack, 0.55f);
            if (won) Confetti.Burst(confettiLayer, 110, 5f);
        }

        void Update()
        {
            if (rays.gameObject.activeSelf) rays.transform.Rotate(0, 0, -18f * Time.unscaledDeltaTime);
        }
    }
}
