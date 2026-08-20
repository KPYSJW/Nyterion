using UnityEngine;
using Nytherion.Core.Managers;
using Nytherion.Core.Enums;
using Nytherion.UI.RelicBoard;
using VContainer;
using VContainer.Unity;

namespace Nytherion.UI.Controllers
{
    public class RelicUIController : UIPanelBase, IInitializable
    {
        private EventManager eventManager;
        private InputManager inputManager;
        private GameSceneUIRefs gameSceneUIRefs;
        private RelicGridUI relicGridUI;
        private RelicManager relicManager;
        private RelicEffectStatusUI effectStatusUI;

        [Inject]
        public void Construct(
            GameSceneUIRefs gameSceneUIRefs,
            EventManager eventManager,
            InputManager inputManager,
            RelicGridUI relicGridUI,
            RelicManager relicManager
            )
        {
            this.gameSceneUIRefs = gameSceneUIRefs;
            this.eventManager = eventManager;
            this.inputManager = inputManager;
            this.relicGridUI = relicGridUI;
            this.relicManager = relicManager;
            this.controlledCanvasGroup = gameSceneUIRefs.RelicCanvasGroup;

            EnsureEffectStatusUI();

            if (relicGridUI != null && controlledCanvasGroup != null)
            {
                bool isChildOfCanvasGroup = relicGridUI.transform.IsChildOf(controlledCanvasGroup.transform);
            }
        }

        private void OnEnable()
        {
            if (eventManager != null)
            {
                eventManager.OnInteraction += HandleInteraction;
            }
            if (inputManager != null)
            {
                inputManager.onToggleRelicUI += Toggle;
            }
        }
        private void OnDisable()
        {
            if (eventManager != null)
            {
                eventManager.OnInteraction -= HandleInteraction;
            }
            if (inputManager != null)
            {
                inputManager.onToggleRelicUI -= Toggle;
            }
        }
        private void HandleInteraction(InteractableType type)
        {
            if (type == InteractableType.RelicAltar)
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
                controlledCanvasGroup = gameSceneUIRefs.RelicCanvasGroup;

                if (controlledCanvasGroup != null)
                {
                    controlledCanvasGroup.alpha = 0f;
                    controlledCanvasGroup.interactable = false;
                    controlledCanvasGroup.blocksRaycasts = false;
                }
            }

            EnsureEffectStatusUI();
        }

        protected override void OnPanelStateChanged(bool isOpen)
        {
            if (inputManager == null) return;

            if (isOpen)
            {
                inputManager.DisableMovement();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                if (relicGridUI != null)
                {
                    StartCoroutine(relicGridUI.Initialize());
                }
                else
                {
                    Debug.LogWarning("[RelicUIController] RelicGridUI가 null이어서 새로고침할 수 없습니다");
                }

                effectStatusUI?.Refresh();
            }
            else
            {
                inputManager.EnableMovement();
            }
        }

        private void EnsureEffectStatusUI()
        {
            if (effectStatusUI == null)
            {
                effectStatusUI = GetComponent<RelicEffectStatusUI>();
                if (effectStatusUI == null)
                {
                    effectStatusUI = gameObject.AddComponent<RelicEffectStatusUI>();
                }
            }

            if (gameSceneUIRefs != null && relicManager != null)
            {
                effectStatusUI.Initialize(
                    relicManager,
                    gameSceneUIRefs.RelicSetEffectStatusPanel,
                    gameSceneUIRefs.RelicTranscendenceEffectStatusPanel,
                    gameSceneUIRefs.RelicSetEffectStatusEntryPrefab,
                    gameSceneUIRefs.RelicTranscendenceEffectStatusEntryPrefab,
                    gameSceneUIRefs.RelicTooltip);
            }
        }
    }
}
