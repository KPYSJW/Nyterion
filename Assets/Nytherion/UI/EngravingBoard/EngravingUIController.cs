using UnityEngine;
using Nytherion.Core;

namespace Nytherion.UI.EngravingBoard
{
    public class EngravingUIController : UIPanelBase
    {
        public static EngravingUIController Instance { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void ToggleUI()
        {
            Toggle();
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
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
}