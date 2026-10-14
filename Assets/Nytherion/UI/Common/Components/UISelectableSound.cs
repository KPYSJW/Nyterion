using Nytherion.Core.Enums;
using Nytherion.Core.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Nytherion.UI.Components
{
    /// <summary>포인터와 키보드 조작만 소리로 알리고 초기값 설정/화면 갱신은 무음으로 처리합니다.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class UISelectableSound : MonoBehaviour, IPointerEnterHandler, ISelectHandler,
        IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, IMoveHandler, ISubmitHandler
    {
        private AudioManager audioManager;
        private Selectable control;
        private Button button;
        private bool pointerHeld;
        private int inputFrame = -1;
        private float previousValue;

        public void Bind(AudioManager audio, Selectable selectable)
        {
            audioManager = audio;
            control = selectable;
            button = control as Button;
            if (button != null)
            {
                button.onClick.RemoveListener(PlayClick);
                button.onClick.AddListener(PlayClick);
            }
            previousValue = GetValue();
        }

        private bool CanInteract => control != null && control.IsActive() && control.IsInteractable();
        private bool CanPlayHover => CanInteract && GetComponentInParent<MenuHoverSoundScope>() != null;

        private float GetValue()
        {
            if (control is Slider slider) return slider.value;
            if (control is Toggle toggle) return toggle.isOn ? 1f : 0f;
            return 0f;
        }

        private void PlayClick() => audioManager?.RequestUISound(UISoundType.Click);

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (CanPlayHover && !eventData.dragging) audioManager?.RequestUISound(UISoundType.Hover);
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (CanPlayHover && (eventData is AxisEventData || eventData is PointerEventData))
                audioManager?.RequestUISound(UISoundType.Hover);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !CanInteract) return;
            pointerHeld = true;
            inputFrame = Time.frameCount;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            pointerHeld = false;
            inputFrame = Time.frameCount;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            inputFrame = Time.frameCount;
            if (CanInteract && (control is TMP_Dropdown || control is Dropdown)) PlayClick();
            PlayToggleChange();
        }

        public void OnMove(AxisEventData eventData) => inputFrame = Time.frameCount;

        public void OnSubmit(BaseEventData eventData)
        {
            inputFrame = Time.frameCount;
            if (CanInteract && (control is TMP_Dropdown || control is Dropdown || control is TMP_InputField))
                PlayClick();
            PlayToggleChange();
        }

        private void PlayToggleChange()
        {
            if (!(control is Toggle) || !CanInteract) return;
            float value = GetValue();
            if (!Mathf.Approximately(value, previousValue))
                audioManager?.RequestUISound(UISoundType.Adjust);
            previousValue = value;
        }

        private void LateUpdate()
        {
            float value = GetValue();
            if (!Mathf.Approximately(value, previousValue) && CanInteract &&
                (pointerHeld || inputFrame == Time.frameCount))
                audioManager?.RequestUISound(UISoundType.Adjust);
            previousValue = value;
        }

        private void OnEnable()
        {
            pointerHeld = false;
            inputFrame = -1;
            previousValue = GetValue();
        }

        private void OnDisable()
        {
            pointerHeld = false;
            inputFrame = -1;
        }

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(PlayClick);
        }
    }
}
