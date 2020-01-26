using System;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using System.Linq;

namespace Ivy.Devconsole
{
    [AddComponentMenu("")]
    public class DevConsoleInput : MonoBehaviour, IDeselectHandler
    {
        public TMP_InputField field;
        public Func<string, string[]> completionProvider;
        public Action<string> submitHandler;
        public Action<string> suggestionSelectionHandler;
        public ParameterSuggestions suggestions;
        bool shouldReselect;
        bool tabbing;

        string currentParamaterText;

        private void Update()
        {
            if (shouldReselect) field.Select();

            if (Input.GetKeyDown(KeyCode.F1))
            {
                suggestions.gameObject.SetActive(!suggestions.gameObject.activeSelf);
                if (suggestions.gameObject.activeSelf) OnValueChange();
            }
            if (Input.GetKeyDown(KeyCode.Tab) && suggestions.HasChoices)
            {
                tabbing = true;
                suggestions.Selected++;
                suggestionSelectionHandler?.Invoke(suggestions.SelectedValue);
                field.MoveToEndOfLine(false, false);
            }
            else
            {
                tabbing = false;
            }

            if (Input.GetKeyDown(KeyCode.Return))
            {
                if (field.text != "") submitHandler?.Invoke(field.text);
                field.text = "";
                EventSystem.current.SetSelectedGameObject(null);
                field.Select();
            }
        }

        public void OnValueChange()
        {
            var text = field.text;
            if (!tabbing)
            {
                currentParamaterText = Devconsole.GetArgumentValue(text);
            }
            if (!suggestions.gameObject.activeSelf) return;

            suggestions.customPosition = new Vector2(text == "" ? 0 : field.preferredWidth, 30);
            var choices = completionProvider?.Invoke(field.text);
            suggestions.SetChoices(choices?.Where(v => currentParamaterText == "" || v.ToLower().Contains(currentParamaterText)).ToArray(), Devconsole.GetArgumentName(text));
        }

        public void OnDeselect(BaseEventData eventData) => shouldReselect = true;
    }
}

