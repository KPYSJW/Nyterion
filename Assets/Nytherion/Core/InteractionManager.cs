// 경로: Nytherion/Core/InteractionManager.cs
using UnityEngine;
using Nytherion.Core;
using Nytherion.GamePlay.Characters.NPC;
using Nytherion.UI.EngravingBoard;
using Nytherion.UI.Gacha;
using Nytherion.UI.Shop;
using Nytherion.UI.Inventory;

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
            GameObject player = GameObject.FindGameObjectWithTag("Player");
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
        Debug.Log("F 키 입력 감지됨. 상호작용 시작.");

        if (EngravingUIController.Instance != null && EngravingUIController.Instance.IsOpen)
        {
            Debug.Log("열려있는 각인 UI를 닫습니다.");
            EngravingUIController.Instance.Close();
            return;
        }
        if (GachaUIController.Instance != null && GachaUIController.Instance.IsOpen)
        {
            Debug.Log("열려있는 가챠 UI를 닫습니다.");
            GachaUIController.Instance.Close();
            return;
        }
        if (ShopUI.Instance != null && ShopUI.Instance.IsOpen)
        {
            ShopUI.Instance.Close();
            return;
        }
        Debug.Log("닫을 UI가 없음. 주변 오브젝트 검색 시작.");

        if (playerTransform == null) return;

        Collider2D[] colliders = Physics2D.OverlapCircleAll(playerTransform.position, interactionDistance, interactableLayer);
        if (colliders.Length == 0)
        {
            Debug.Log("주변에 상호작용 가능한 오브젝트 없음.");
            return;
        }

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
            Debug.Log("가장 가까운 오브젝트와 상호작용: " + ((MonoBehaviour)closestInteractable).name);
            closestInteractable.Interact();
        }
    }
}