using UnityEngine;
using Nytherion.UI.Controllers;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.Core.Data;
using VContainer;
using VContainer.Unity;
using System;

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
                // InteractionManager는 이벤트를 직접 발생시키지 않고,
                // 각 Interactable 객체가 자신의 Interact() 메서드에서 책임을 지도록 위임합니다.
                Debug.Log($"[InteractionManager] {closestInteractable.GetType().Name}.Interact() 호출");
                closestInteractable.Interact();
            }
            else
            {
                Debug.Log("[InteractionManager] 상호작용 가능한 객체를 찾지 못했습니다");
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