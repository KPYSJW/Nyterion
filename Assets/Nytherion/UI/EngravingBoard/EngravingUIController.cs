using UnityEngine;
using Nytherion.Core;
using Nytherion.Core.Enums;

namespace Nytherion.UI.EngravingBoard
{
    public class EngravingUIController : UIPanelBase
    {
        public static EngravingUIController Instance { get; private set; }
        private void OnEnable()
        {
            if (EventManager.Instance != null)
            {
                EventManager.Instance.OnInteraction += HandleInteraction;
            }
        }
        private void OnDisable()
        {
            if (EventManager.Instance != null)
            {
                EventManager.Instance.OnInteraction -= HandleInteraction;
            }
        }
        private void HandleInteraction(InteractableType type)
        {
            if (type == InteractableType.EngravingAltar)
            {
                Toggle();
            }
        }
        protected override void Awake()
        {
            base.Awake();
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }
        protected override void OnPanelStateChanged(bool isOpen)
        {
            if (InputManager.Instance == null) return;

            if (isOpen)
            {
                InputManager.Instance.DisableMovement();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                InputManager.Instance.EnableMovement();
            }
        }
    }
}