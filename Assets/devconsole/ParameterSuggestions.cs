using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace ivy.devconsole
{
    [AddComponentMenu("")]
    public class ParameterSuggestions : MonoBehaviour
    {
        public TextMeshProUGUI argumentName;
        public GameObject parameterSuggestion;
        public RectTransform selectionBackground;
        public Vector2 customPosition;
        readonly int limit = 5;
        int _selected;
        string[] choices;
        public int Selected
        {
            get { return _selected; }
            set
            {
                if (choices == null || choices.Length == 0) return;
                var lim = Mathf.Min(choices.Length, limit);
                _selected = value;
                if (_selected >= lim) _selected %= lim;
                selectionBackground.anchoredPosition = new Vector2(0, _selected < 0 ? 100000 : -38 + -_selected * 30);
            }
        }

        public bool HasChoices => choices != null && choices.Length > 0;

        public string SelectedValue => Selected >= 0 ? choices[Selected] : null;

        public void Awake() => SetChoices(null, "Command");

        public void SetChoices(string[] choices, string header)
        {
            if (choices != null && this.choices != null && this.choices.Length > 0 && this.choices.Length == choices.Length && choices[0] == this.choices[0])
            {
                return;
            }

            Selected = -1;
            this.choices = choices;
            foreach (Transform c in transform.GetChild(1)) Destroy(c.gameObject);
            argumentName.text = header;
            int index = 0;
            float targetWidth = Mathf.Max(argumentName.GetPreferredValues(header).x + 20, 140);
            float targetHeight = 32;
            if (choices != null)
            {
                foreach (var choice in choices)
                {
                    if (index >= limit) continue;
                    var choiceInstance = Instantiate(parameterSuggestion, transform.GetChild(1));
                    var choiceRect = choiceInstance.GetComponent<RectTransform>();
                    var choiceText = choiceInstance.GetComponentInChildren<TextMeshProUGUI>();

                    choiceRect.anchoredPosition = new Vector2(choiceRect.anchoredPosition.x, -15.3f - (index * choiceRect.sizeDelta.y));
                    choiceText.text = choice;
                    var ww = choiceText.GetPreferredValues(choice);
                    targetWidth = Mathf.Max(targetWidth, ww.x + 20);
                    targetHeight = choiceRect.sizeDelta.y;
                    index++;
                }
            }
            var containerRect = GetComponent<RectTransform>();
            containerRect.sizeDelta = new Vector2(targetWidth, index * targetHeight + 24);
            if (customPosition != Vector2.zero) containerRect.anchoredPosition = customPosition;
        }
    }
}
