using UnityEngine;
using Nytherion.Core.Managers;
using Nytherion.Core.Enums;
using Zenject;

namespace Nytherion.UI.Controllers
{
    public class EngravingUIController : UIPanelBase
    {
        public static EngravingUIController Instance { get; private set; }
        private EventManager _eventManager;
        private InputManager _inputManager;

        [Inject]
        public void Construct(
            [Inject(Id = "EngravingCanvasGroup")] CanvasGroup controlledCanvasGroup,
            EventManager eventManager, 
            InputManager inputManager)
        {
            this.controlledCanvasGroup = controlledCanvasGroup;
            _eventManager = eventManager;
            _inputManager = inputManager;
        }

        private void OnEnable()
        {
            if (_eventManager != null)
            {
                _eventManager.OnInteraction += HandleInteraction;
            }
        }
        private void OnDisable()
        {
            if (_eventManager != null)
            {
                _eventManager.OnInteraction -= HandleInteraction;
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
            if (_inputManager == null) return;

            if (isOpen)
            {
                _inputManager.DisableMovement();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                _inputManager.EnableMovement();
            }
        }
    }
}