using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CrystalMagic.UI
{
    public static class UIBindingExtensions
    {
        public static IDisposable BindText(this UIBindingScope scope, UIProperty<string> property, TMP_Text text, bool invokeImmediately = true)
        {
            if (scope == null || property == null || text == null)
                return null;

            return scope.Bind(property, value => text.text = value ?? string.Empty, invokeImmediately);
        }

        public static IDisposable BindActive(this UIBindingScope scope, UIProperty<bool> property, GameObject gameObject, bool invokeImmediately = true)
        {
            if (scope == null || property == null || gameObject == null)
                return null;

            return scope.Bind(property, value => gameObject.SetActive(value), invokeImmediately);
        }

        public static IDisposable BindInteractable(this UIBindingScope scope, UIProperty<bool> property, Selectable selectable, bool invokeImmediately = true)
        {
            if (scope == null || property == null || selectable == null)
                return null;

            return scope.Bind(property, value => selectable.interactable = value, invokeImmediately);
        }

        public static IDisposable BindSprite(this UIBindingScope scope, UIProperty<Sprite> property, Image image, bool invokeImmediately = true)
        {
            if (scope == null || property == null || image == null)
                return null;

            return scope.Bind(property, value => image.sprite = value, invokeImmediately);
        }

        public static IDisposable BindToggle(this UIBindingScope scope, UIProperty<bool> property, Toggle toggle, bool twoWay = false, bool invokeImmediately = true)
        {
            if (scope == null || property == null || toggle == null)
                return null;

            IDisposable binding = scope.Bind(property, value => toggle.SetIsOnWithoutNotify(value), invokeImmediately);
            if (!twoWay)
                return binding;

            scope.Bind(() => toggle.onValueChanged.AddListener(OnValueChanged), () => toggle.onValueChanged.RemoveListener(OnValueChanged));
            return binding;

            void OnValueChanged(bool value)
            {
                property.Value = value;
            }
        }

        public static IDisposable BindSlider(this UIBindingScope scope, UIProperty<float> property, Slider slider, bool twoWay = false, bool invokeImmediately = true)
        {
            if (scope == null || property == null || slider == null)
                return null;

            IDisposable binding = scope.Bind(property, value => slider.SetValueWithoutNotify(value), invokeImmediately);
            if (!twoWay)
                return binding;

            scope.Bind(() => slider.onValueChanged.AddListener(OnValueChanged), () => slider.onValueChanged.RemoveListener(OnValueChanged));
            return binding;

            void OnValueChanged(float value)
            {
                property.Value = value;
            }
        }

        public static IDisposable BindInputField(this UIBindingScope scope, UIProperty<string> property, InputField inputField, bool twoWay = false, bool invokeImmediately = true)
        {
            if (scope == null || property == null || inputField == null)
                return null;

            IDisposable binding = scope.Bind(property, value => inputField.SetTextWithoutNotify(value ?? string.Empty), invokeImmediately);
            if (!twoWay)
                return binding;

            scope.Bind(() => inputField.onValueChanged.AddListener(OnValueChanged), () => inputField.onValueChanged.RemoveListener(OnValueChanged));
            return binding;

            void OnValueChanged(string value)
            {
                property.Value = value;
            }
        }
    }
}
