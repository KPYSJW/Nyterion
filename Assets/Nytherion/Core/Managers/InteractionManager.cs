using UnityEngine;
using Nytherion.UI.Controllers;
using Nytherion.Core.Systems;

namespace Nytherion.Core.Managers
{
    public class InteractionManager : MonoBehaviour
    {
        public static InteractionManager Instance { get; private set; }

        [Header("Interaction Settings")]
        [SerializeField] private float interactionDistance = 1.5f;
        [SerializeField] private LayerMask interactableLayer;
        [SerializeField] private Transform playerTransform;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            if (InputManager.Instance != null)
            {
                InputManager.Instance.onInteract += HandleInteraction;
            }

            if (playerTransform == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag(Tags.Player);
                if (player != null) playerTransform = player.transform;
                else Debug.LogError("InteractionManager: 'Player' 태그를 가진 오브젝트를 찾을 수 없습니다!");
            }
        }

        private void OnDestroy()
        {
            if (InputManager.Instance != null) InputManager.Instance.onInteract -= HandleInteraction;
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