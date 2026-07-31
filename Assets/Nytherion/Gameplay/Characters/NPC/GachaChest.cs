using UnityEngine;
using Nytherion.Core.Enums;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Relics;
using VContainer;
using VContainer.Unity;
using System.Collections;
using System.Collections.Generic;

namespace Nytherion.GamePlay.Characters.NPC
{
    public class GachaChest : MonoBehaviour
    {
        [Header("Gacha Chest Settings")]
        [SerializeField] private float interactionDistance = 2.0f;
        [SerializeField] private GameObject worldPromptUI; // "1회: X / 10회: C" 월드 UI
        [SerializeField] private GameObject droppedRelicPrefab; // DroppedRelic 프리팹
        [SerializeField, Min(10), Tooltip("첫 가챠 때 미리 생성할 드롭 유물 수입니다.")]
        private int droppedRelicPoolSize = 30;
        [SerializeField] private Animator animator;
        [SerializeField] private float closeDelay = 1.0f; // 아이템 생성 후 상자가 닫히기까지 대기 시간

        [Header("드롭 유물 착지 간격")]
        [SerializeField, Min(0.1f), Tooltip("드롭 유물끼리 확보할 최소 중심 간격입니다.")]
        private float relicLandingClearance = 0.65f;
        [SerializeField, Min(0.1f), Tooltip("첫 번째 착지 고리의 반지름입니다.")]
        private float relicFirstLandingRadius = 0.85f;
        [SerializeField, Min(0.1f), Tooltip("유물이 날아갈 수 있는 최대 착지 반경입니다.")]
        private float relicMaxLandingRadius = 1.45f;
        [SerializeField, Min(1), Tooltip("비어 있는 위치를 찾기 위해 검사할 최대 착지 고리 수입니다.")]
        private int relicLandingSearchRingCount = 8;

        private GachaManager gachaManager;
        private RelicManager relicManager;
        private CurrencyDataManager currencyDataManager;
        private ObjectPoolManager objectPoolManager;
        private Transform playerTransform;

        private bool isPlayerInRange = false;
        private bool isAnimating = false; // 애니메이션 진행 중 중복 입력 방지

        private List<ScriptableObject> pendingDrawnItems = new List<ScriptableObject>();

        [Inject]
        public void Construct(
            GachaManager gachaManager,
            RelicManager relicManager,
            CurrencyDataManager currencyDataManager,
            ObjectPoolManager objectPoolManager,
            PlayerController playerController)
        {
            this.gachaManager = gachaManager;
            this.relicManager = relicManager;
            this.currencyDataManager = currencyDataManager;
            this.objectPoolManager = objectPoolManager;
            if (playerController != null)
            {
                this.playerTransform = playerController.transform;
            }
        }

        private void Start()
        {
            // VContainer 의존성 해결 보완 (Inject 누락 대비)
            if (gachaManager == null || relicManager == null || currencyDataManager == null || objectPoolManager == null || playerTransform == null)
            {
                LifetimeScope lifetimeScope = LifetimeScope.Find<GameSceneLifetimeScope>();
                if (lifetimeScope != null)
                {
                    if (gachaManager == null) lifetimeScope.Container.TryResolve<GachaManager>(out gachaManager);
                    if (relicManager == null) lifetimeScope.Container.TryResolve<RelicManager>(out relicManager);
                    if (currencyDataManager == null) lifetimeScope.Container.TryResolve<CurrencyDataManager>(out currencyDataManager);
                    if (objectPoolManager == null) lifetimeScope.Container.TryResolve<ObjectPoolManager>(out objectPoolManager);
                    if (playerTransform == null)
                    {
                        PlayerController playerController;
                        if (lifetimeScope.Container.TryResolve<PlayerController>(out playerController))
                        {
                            playerTransform = playerController.transform;
                        }
                    }
                }
            }

            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            RefreshWorldPromptUI();
        }

        private void Update()
        {
            if (playerTransform == null) return;

            // 플레이어와의 거리를 체크하여 범위 내에 있는지 판단
            float distance = Vector2.Distance(transform.position, playerTransform.position);
            bool inRange = distance <= interactionDistance;

            if (inRange != isPlayerInRange)
            {
                isPlayerInRange = inRange;
                RefreshWorldPromptUI();
            }

            // 범위 내에 있고 애니메이션 진행 중이 아닐 때 키보드 입력 감지
            if (isPlayerInRange && !isAnimating)
            {
                if (Input.GetKeyDown(KeyCode.X))
                {
                    TryPerformGacha(1);
                }
                else if (Input.GetKeyDown(KeyCode.C))
                {
                    TryPerformGacha(10);
                }
            }
        }

        private void OnMouseDown()
        {
            if (isAnimating) return;

            // 마우스 클릭 시 플레이어가 범위 내에 있다면 가이드 UI 활성화
            if (playerTransform != null)
            {
                float distance = Vector2.Distance(transform.position, playerTransform.position);
                if (distance <= interactionDistance)
                {
                    RefreshWorldPromptUI();
                }
            }
        }

        private void TryPerformGacha(int count)
        {
            if (isAnimating) return;

            if (gachaManager == null || currencyDataManager == null || relicManager == null || playerTransform == null)
            {
                Debug.LogError("[GachaChest] 필수 컴포넌트가 누락되어 가챠를 실행할 수 없습니다.");
                RefreshWorldPromptUI();
                return;
            }

            if (DroppedRelic.HasUncollectedRelics())
            {
                StartCoroutine(CollectRemainingRelicsThenPerformGacha(count));
                return;
            }

            if (!PerformGacha(count))
            {
                RefreshWorldPromptUI();
            }
        }

        private IEnumerator CollectRemainingRelicsThenPerformGacha(int count)
        {
            isAnimating = true;
            RefreshWorldPromptUI();

            DroppedRelic.BeginAutoCollectAll(playerTransform);
            while (DroppedRelic.HasUncollectedRelics())
            {
                yield return null;
            }

            isAnimating = false;
            TryPerformGacha(count);
        }

        private bool PerformGacha(int count)
        {

            // 1. 토큰 보유량 검사 (10회 시 10개 검사 필수)
            int currentToken = currencyDataManager.GetCurrency(CurrencyType.Token);
            if (currentToken < count)
            {
                Debug.LogWarning($"[GachaChest] 토큰 부족! 필요 토큰: {count}, 보유 토큰: {currentToken}");
                return false;
            }

            // 2. 가챠 생성 (TryDrawItemsOnly는 실제 인벤토리에 넣지 않고 목록만 뽑아 토큰을 뺌)
            pendingDrawnItems = gachaManager.TryDrawItemsOnly(GachaType.Relic, count);
            if (pendingDrawnItems == null || pendingDrawnItems.Count == 0)
            {
                Debug.LogWarning("[GachaChest] 가챠 풀 에러 또는 생성된 아이템 없음.");
                return false;
            }

            isAnimating = true;
            RefreshWorldPromptUI();

            // 3. 상자 열림 애니메이션 재생
            if (animator != null)
            {
                animator.SetTrigger("Open");
            }
            else
            {
                // Animator가 없는 경우 백업 실행
                OnOpenAnimationEnd();
            }

            return true;
        }

        /// <summary>
        /// Open 애니메이션 클립의 마지막 프레임 Animation Event에서 호출할 메서드
        /// </summary>
        public void OnOpenAnimationEnd()
        {
            // 4. 물리 드롭 유물 스폰
            if (pendingDrawnItems != null && pendingDrawnItems.Count > 0)
            {
                List<Vector2> reservedLandingPositions = new List<Vector2>();
                float landingAngleOffset = Random.Range(0f, 360f);

                foreach (ScriptableObject item in pendingDrawnItems)
                {
                    if (item is RelicData relicData)
                    {
                        SpawnDroppedRelic(relicData, reservedLandingPositions, landingAngleOffset);
                    }
                }
                pendingDrawnItems.Clear();
            }

            // 대기 후 Close 애니메이션 재생 및 Idle 복귀
            StartCoroutine(CloseSequenceRoutine());
        }

        private IEnumerator CloseSequenceRoutine()
        {
            if (closeDelay > 0f)
            {
                yield return new WaitForSeconds(closeDelay);
            }

            if (animator != null)
            {
                animator.SetTrigger("Close");
            }

            // Close 애니메이션 동작 후 입력 상태 해제
            yield return new WaitForSeconds(0.5f);
            isAnimating = false;
            RefreshWorldPromptUI();
        }

        private void RefreshWorldPromptUI()
        {
            if (worldPromptUI == null)
            {
                return;
            }

            bool shouldShow = isPlayerInRange && !isAnimating;
            if (worldPromptUI.activeSelf != shouldShow)
            {
                worldPromptUI.SetActive(shouldShow);
            }
        }

        private void SpawnDroppedRelic(
            RelicData relicData,
            List<Vector2> reservedLandingPositions,
            float landingAngleOffset)
        {
            if (droppedRelicPrefab == null)
            {
                Debug.LogError("[GachaChest] droppedRelicPrefab이 할당되지 않았습니다. 백업으로 즉시 보관함에 추가합니다.");
                if (relicManager != null)
                {
                    relicManager.AddNewRelicToStorage(relicData);
                }
                return;
            }

            // 상자 위치 근처에서 스폰
            Vector3 spawnPos = transform.position + new Vector3(0, 0.2f, 0);
            Vector2 landingPosition = FindAvailableLandingPosition(reservedLandingPositions, landingAngleOffset);
            GameObject obj = objectPoolManager != null
                ? objectPoolManager.SpawnFromPool(droppedRelicPrefab, spawnPos, Quaternion.identity, droppedRelicPoolSize)
                : Instantiate(droppedRelicPrefab, spawnPos, Quaternion.identity);

            if (obj == null)
            {
                Debug.LogError("[GachaChest] 드롭 유물 풀에서 오브젝트를 가져오지 못했습니다. 유물을 보관함에 추가합니다.");
                relicManager?.AddNewRelicToStorage(relicData);
                return;
            }

            DroppedRelic dropped = obj.GetComponent<DroppedRelic>();
            if (dropped != null)
            {
                dropped.SetPool(objectPoolManager, objectPoolManager != null ? droppedRelicPrefab.name : null);
                dropped.SetLaunchTarget(landingPosition);
                dropped.Init(relicData, playerTransform, relicManager);
                reservedLandingPositions.Add(landingPosition);
            }
            else
            {
                Debug.LogError("[GachaChest] 생성된 프리팹에 DroppedRelic 컴포넌트가 없습니다!");
                if (objectPoolManager != null)
                {
                    objectPoolManager.ReturnToPool(droppedRelicPrefab.name, obj);
                }
                else
                {
                    Destroy(obj);
                }
                if (relicManager != null)
                {
                    relicManager.AddNewRelicToStorage(relicData);
                }
            }
        }

        private Vector2 FindAvailableLandingPosition(List<Vector2> reservedLandingPositions, float angleOffset)
        {
            Vector2 chestPosition = transform.position;

            for (int ring = 1; ring <= relicLandingSearchRingCount; ring++)
            {
                float radius = Mathf.Min(
                    relicMaxLandingRadius,
                    relicFirstLandingRadius + relicLandingClearance * (ring - 1));
                int slotCount = Mathf.Max(6, Mathf.FloorToInt(2f * Mathf.PI * radius / relicLandingClearance));

                for (int slot = 0; slot < slotCount; slot++)
                {
                    float angle = angleOffset + 360f * slot / slotCount;
                    Vector2 direction = new Vector2(
                        Mathf.Cos(angle * Mathf.Deg2Rad),
                        Mathf.Sin(angle * Mathf.Deg2Rad));
                    Vector2 candidate = chestPosition + direction * radius;

                    if (IsLandingPositionAvailable(candidate, reservedLandingPositions))
                    {
                        return candidate;
                    }
                }
            }

            // 공간이 모두 찬 경우에도 최대 반경은 넘기지 않는다.
            // 이 경우에는 드롭 유물의 밀어내기가 최종 간격을 정리한다.
            float fallbackRadius = relicMaxLandingRadius;
            Vector2 fallbackDirection = new Vector2(
                Mathf.Cos(angleOffset * Mathf.Deg2Rad),
                Mathf.Sin(angleOffset * Mathf.Deg2Rad));
            return chestPosition + fallbackDirection * fallbackRadius;
        }

        private bool IsLandingPositionAvailable(Vector2 candidate, List<Vector2> reservedLandingPositions)
        {
            float clearanceSqr = relicLandingClearance * relicLandingClearance;
            for (int i = 0; i < reservedLandingPositions.Count; i++)
            {
                if ((reservedLandingPositions[i] - candidate).sqrMagnitude < clearanceSqr)
                {
                    return false;
                }
            }

            Collider2D[] overlaps = Physics2D.OverlapCircleAll(candidate, relicLandingClearance * 0.5f);
            for (int i = 0; i < overlaps.Length; i++)
            {
                if (overlaps[i] != null && overlaps[i].GetComponent<DroppedRelic>() != null)
                {
                    return false;
                }
            }

            return true;
        }

        private void OnValidate()
        {
            relicLandingClearance = Mathf.Max(0.1f, relicLandingClearance);
            relicFirstLandingRadius = Mathf.Max(0.1f, relicFirstLandingRadius);
            relicMaxLandingRadius = Mathf.Max(relicFirstLandingRadius, relicMaxLandingRadius);
            relicLandingSearchRingCount = Mathf.Max(1, relicLandingSearchRingCount);
        }

        private void OnDrawGizmosSelected()
        {
            // 에디터 상에서 상호작용 반경 가이드 라인 그리기
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactionDistance);
        }
    }
}
