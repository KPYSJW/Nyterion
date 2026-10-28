using UnityEngine;
using Nytherion.UI.Controllers;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.Core.Data;
using VContainer;
using VContainer.Unity;
using System;
using Nytherion.Core.Enums;
using Nytherion.GamePlay.Characters.NPC;
using Nytherion.GamePlay.Relics;
using Nytherion.GamePlay.Skills;

namespace Nytherion.Core.Managers
{
    public class InteractionManager : BaseManager
    {

        [Header("Interaction Settings")]
        [SerializeField] private float interactionDistance = 1.5f;
        [SerializeField] private LayerMask interactableLayer;

        private Transform playerTransform;
        private InputManager inputManager;
        private EventManager eventManager;
        private ShopUI shopUI;
        private RelicUIController relicUIController;

        private static readonly Collider2D[] interactionBuffer = new Collider2D[10];

        [Inject]
        public void Construct(
            InputManager inputManager,
            EventManager eventManager,
            ShopUI shopUI,
            RelicUIController relicUIController,
            PlayerController playerController)
        {
            this.inputManager = inputManager;
            this.eventManager = eventManager;
            this.shopUI = shopUI;
            this.relicUIController = relicUIController;
            playerTransform = playerController.transform;

        }

        protected override void OnInitializeInternal()
        {
            if (inputManager != null)
            {
                inputManager.onInteract += HandleInteraction;
            }
            base.OnInitializeInternal();
        }

        protected override void OnDestroy()
        {
            if (inputManager != null) inputManager.onInteract -= HandleInteraction;
            base.OnDestroy();
        }

        private void HandleInteraction()
        {
            if (shopUI != null && shopUI.IsOpen)
            {
                shopUI.Close();
                return;
            }
            if (relicUIController != null && relicUIController.IsOpen)
            {
                relicUIController.Close();
                return;
            }
            if (Time.timeScale <= 0f || UIPanelBase.IsAnyPanelOpen ||
                RelicPickupDetector.HasSelectedRelic(playerTransform)) return;

            GetCurrentTarget()?.Interact();
        }

        public string GetInteractionPrompt()
        {
            IInteractable target = GetCurrentTarget();
            if (target == null) return null;
            if (target is PortalGateEndpoint || target is VillagePortal) return null;
            switch (target.Type)
            {
                case InteractableType.ShopDealer: return "상점 열기";
                case InteractableType.RelicAltar: return "유물 관리";
                default: return "상호작용";
            }
        }

        public Transform GetPortalTarget()
        {
            IInteractable target = GetCurrentTarget();
            return target is PortalGateEndpoint || target is VillagePortal
                ? ((Component)target).transform : null;
        }

        private IInteractable GetCurrentTarget()
        {
            if (playerTransform == null) return null;

            int hitCount = Physics2D.OverlapCircleNonAlloc(playerTransform.position, interactionDistance, interactionBuffer, interactableLayer);
            if (hitCount == 0) return null;

            IInteractable closestInteractable = null;
            float closestDistanceSqr = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D col = interactionBuffer[i];
                float distanceSqr = (col.transform.position - playerTransform.position).sqrMagnitude;
                if (distanceSqr < closestDistanceSqr)
                {
                    if (col.TryGetComponent(out IInteractable interactableObject))
                    {
                        if (interactableObject is ShopDealer dealer && !dealer.IsInteractable) continue;
                        if (interactableObject is RelicAltar altar && !altar.IsInteractable) continue;
                        if (interactableObject is IConditionalInteractable conditional &&
                            !conditional.CanInteract(playerTransform)) continue;
                        closestDistanceSqr = distanceSqr;
                        closestInteractable = interactableObject;
                    }
                }
            }

            return closestInteractable;
        }

        public override void PopulateSaveData(SaveData saveData)
        {

        }
        
        public override void LoadFromSaveData(SaveData saveData)
        {

        }
    }
}
