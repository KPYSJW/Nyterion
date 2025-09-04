using UnityEngine;
using Nytherion.UI.Controllers;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.Core.Data;
using Zenject;

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
        private GachaUIController gachaUIController;
        private EngravingUIController engravingUIController;

        [Inject]
        public void Construct(
            InputManager inputManager,
            EventManager eventManager,
            ShopUI shopUI,
            GachaUIController gachaUIController,
            EngravingUIController engravingUIController,
            PlayerController playerController)
        {
            this.inputManager = inputManager;
            this.eventManager = eventManager;
            this.shopUI = shopUI;
            this.gachaUIController = gachaUIController;
            this.engravingUIController = engravingUIController;
            playerTransform = playerController.transform;
        }

        protected override void OnInitializeInternal()
        {
            if (inputManager != null)
            {
                inputManager.onInteract += HandleInteraction;
            }
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
            if (gachaUIController != null && gachaUIController.IsOpen)
            {
                gachaUIController.Close();
                return;
            }
            if (engravingUIController != null && engravingUIController.IsOpen)
            {
                engravingUIController.Close();
                return;
            }
            if (playerTransform == null) return;

            Collider2D[] colliders = Physics2D.OverlapCircleAll(playerTransform.position, interactionDistance, interactableLayer);
            if (colliders.Length == 0) return;

            IInteractable closestInteractable = null;
            float closestDistanceSqr = float.MaxValue;
            foreach (var collider in colliders)
            {
                float distanceSqr = (collider.transform.position - playerTransform.position).sqrMagnitude;
                if (distanceSqr < closestDistanceSqr)
                {
                    if (collider.TryGetComponent(out IInteractable interactableObject))
                    {
                        closestDistanceSqr = distanceSqr;
                        closestInteractable = interactableObject;
                    }
                }
            }

            if (closestInteractable != null)
            {
                eventManager.TriggerInteractionEvent(closestInteractable.Type);

                closestInteractable.Interact();
            }
        }

        public override void PopulateSaveData(SaveData saveData)
        {

        }
        
        public override void LoadFromSaveData(SaveData saveData)
        {

        }
    }
}