using UnityEngine;
using Nytherion.Core.Managers;
using Nytherion.Core.Enums;
using Zenject;

namespace Nytherion.UI.Controllers
{
    public class EngravingUIController : UIPanelBase
    {
        private EventManager eventManager;
        private InputManager inputManager;

        [Inject]
        public void Construct(
            [Inject(Id = "EngravingCanvasGroup")] CanvasGroup controlledCanvasGroup,
            EventManager eventManager, 
            InputManager inputManager)
        {
            this.controlledCanvasGroup = controlledCanvasGroup;
            this.eventManager = eventManager;
            this.inputManager = inputManager;
        }

        private void OnEnable()
        {
            if (eventManager != null)
            {
                eventManager.OnInteraction += HandleInteraction;
            }
        }
        private void OnDisable()
        {
            if (eventManager != null)
            {
                eventManager.OnInteraction -= HandleInteraction;
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
        }
        protected override void OnPanelStateChanged(bool isOpen)
        {
            if (inputManager == null) return;

            if (isOpen)
            {
                inputManager.DisableMovement();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                inputManager.EnableMovement();
            }
        }
    }
}