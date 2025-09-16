using UnityEngine;
using Nytherion.Core.Managers;
using Nytherion.Core.Enums;
using Nytherion.UI.EngravingBoard;
using VContainer;
using VContainer.Unity;

namespace Nytherion.UI.Controllers
{
    public class EngravingUIController : UIPanelBase, IInitializable
    {
        private EventManager eventManager;
        private InputManager inputManager;
        private GameSceneUIRefs gameSceneUIRefs;
        private EngravingGridUI engravingGridUI;

        [Inject]
        public void Construct(
            GameSceneUIRefs gameSceneUIRefs,
            EventManager eventManager,
            InputManager inputManager,
            EngravingGridUI engravingGridUI
            )
        {
            Debug.Log("[EngravingUIController] Construct 호출됨");
            this.gameSceneUIRefs = gameSceneUIRefs;
            this.eventManager = eventManager;
            this.inputManager = inputManager;
            this.engravingGridUI = engravingGridUI;
            this.controlledCanvasGroup = gameSceneUIRefs.EngravingCanvasGroup;

            if (engravingGridUI != null && controlledCanvasGroup != null)
            {
                bool isChildOfCanvasGroup = engravingGridUI.transform.IsChildOf(controlledCanvasGroup.transform);
            }
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
        private void Start()
        {
            base.Awake();
        }

        private string GetTransformPath(Transform transform)
        {
            if (transform == null) return "null";

            string path = transform.name;
            Transform parent = transform.parent;

            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }

        public void Initialize()
        {
            if (gameSceneUIRefs != null && controlledCanvasGroup == null)
            {
                controlledCanvasGroup = gameSceneUIRefs.EngravingCanvasGroup;

                if (controlledCanvasGroup != null)
                {
                    controlledCanvasGroup.alpha = 0f;
                    controlledCanvasGroup.interactable = false;
                    controlledCanvasGroup.blocksRaycasts = false;
                }
            }
        }

        protected override void OnPanelStateChanged(bool isOpen)
        {
            if (inputManager == null) return;

            if (isOpen)
            {
                inputManager.DisableMovement();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                if (engravingGridUI != null)
                {
                    StartCoroutine(engravingGridUI.Initialize());
                }
                else
                {
                    Debug.LogWarning("[EngravingUIController] EngravingGridUI가 null이어서 새로고침할 수 없습니다");
                }
            }
            else
            {
                inputManager.EnableMovement();
            }
        }
    }
}