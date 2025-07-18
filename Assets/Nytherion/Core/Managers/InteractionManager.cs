using UnityEngine;
using Nytherion.UI.Controllers;
using Nytherion.Core.Systems;
using Nytherion.GamePlay.Characters.Player;
using Zenject;

namespace Nytherion.Core.Managers
{
    public class InteractionManager : MonoBehaviour
    {
        public static InteractionManager Instance { get; private set; }

        [Header("Interaction Settings")]
        [SerializeField] private float interactionDistance = 1.5f;
        [SerializeField] private LayerMask interactableLayer;
        
        private Transform playerTransform;
        private InputManager _inputManager;

        [Inject]
        public void Construct(InputManager inputManager, PlayerController playerController)
        {
            _inputManager = inputManager;
            playerTransform = playerController.transform;
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            if (_inputManager != null)
            {
                _inputManager.onInteract += HandleInteraction;
            }
        }

        private void OnDestroy()
        {
            if (_inputManager != null) _inputManager.onInteract -= HandleInteraction;
        }

        private void HandleInteraction()
        {
            if (ShopUI.Instance != null && ShopUI.Instance.IsOpen)
            {
                ShopUI.Instance.Close();
                return;
            }
            if (GachaUIController.Instance != null && GachaUIController.Instance.IsOpen)
            {
                GachaUIController.Instance.Close();
                return;
            }
            if (EngravingUIController.Instance != null && EngravingUIController.Instance.IsOpen)
            {
                EngravingUIController.Instance.Close();
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
                EventManager.Instance.TriggerInteractionEvent(closestInteractable.Type);

                closestInteractable.Interact();
            }
        }
    }
}