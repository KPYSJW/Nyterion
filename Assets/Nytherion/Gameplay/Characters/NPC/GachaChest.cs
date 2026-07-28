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
        [SerializeField] private Animator animator;
        [SerializeField] private float closeDelay = 1.0f; // 아이템 생성 후 상자가 닫히기까지 대기 시간

        private GachaManager gachaManager;
        private RelicManager relicManager;
        private CurrencyDataManager currencyDataManager;
        private Transform playerTransform;

        private bool isPlayerInRange = false;
        private bool isAnimating = false; // 애니메이션 진행 중 중복 입력 방지

        private List<ScriptableObject> pendingDrawnItems = new List<ScriptableObject>();

        [Inject]
        public void Construct(
            GachaManager gachaManager,
            RelicManager relicManager,
            CurrencyDataManager currencyDataManager,
            PlayerController playerController)
        {
            this.gachaManager = gachaManager;
            this.relicManager = relicManager;
            this.currencyDataManager = currencyDataManager;
            if (playerController != null)
            {
                this.playerTransform = playerController.transform;
            }
        }

        private void Start()
        {
            // VContainer 의존성 해결 보완 (Inject 누락 대비)
            if (gachaManager == null || relicManager == null || currencyDataManager == null || playerTransform == null)
            {
                LifetimeScope lifetimeScope = LifetimeScope.Find<GameSceneLifetimeScope>();
                if (lifetimeScope != null)
                {
                    if (gachaManager == null) lifetimeScope.Container.TryResolve<GachaManager>(out gachaManager);
                    if (relicManager == null) lifetimeScope.Container.TryResolve<RelicManager>(out relicManager);
                    if (currencyDataManager == null) lifetimeScope.Container.TryResolve<CurrencyDataManager>(out currencyDataManager);
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

            if (worldPromptUI != null)
            {
                worldPromptUI.SetActive(false);
            }
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
                if (worldPromptUI != null)
                {
                    worldPromptUI.SetActive(isPlayerInRange && !isAnimating);
                }
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
                    if (worldPromptUI != null)
                    {
                        worldPromptUI.SetActive(true);
                    }
                }
            }
        }

        private void TryPerformGacha(int count)
        {
            if (isAnimating) return;

            if (gachaManager == null || currencyDataManager == null || relicManager == null || playerTransform == null)
            {
                Debug.LogError("[GachaChest] 필수 컴포넌트가 누락되어 가챠를 실행할 수 없습니다.");
                return;
            }

            // 1. 토큰 보유량 검사 (10회 시 10개 검사 필수)
            int currentToken = currencyDataManager.GetCurrency(CurrencyType.Token);
            if (currentToken < count)
            {
                Debug.LogWarning($"[GachaChest] 토큰 부족! 필요 토큰: {count}, 보유 토큰: {currentToken}");
                return;
            }

            // 2. 가챠 생성 (TryDrawItemsOnly는 실제 인벤토리에 넣지 않고 목록만 뽑아 토큰을 뺌)
            pendingDrawnItems = gachaManager.TryDrawItemsOnly(GachaType.Relic, count);
            if (pendingDrawnItems == null || pendingDrawnItems.Count == 0)
            {
                Debug.LogWarning("[GachaChest] 가챠 풀 에러 또는 생성된 아이템 없음.");
                return;
            }

            isAnimating = true;
            if (worldPromptUI != null)
            {
                worldPromptUI.SetActive(false);
            }

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
        }

        /// <summary>
        /// Open 애니메이션 클립의 마지막 프레임 Animation Event에서 호출할 메서드
        /// </summary>
        public void OnOpenAnimationEnd()
        {
            // 4. 물리 드롭 유물 스폰
            if (pendingDrawnItems != null && pendingDrawnItems.Count > 0)
            {
                foreach (ScriptableObject item in pendingDrawnItems)
                {
                    if (item is RelicData relicData)
                    {
                        SpawnDroppedRelic(relicData);
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

            if (isPlayerInRange && worldPromptUI != null)
            {
                worldPromptUI.SetActive(true);
            }
        }

        private void SpawnDroppedRelic(RelicData relicData)
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
            GameObject obj = Instantiate(droppedRelicPrefab, spawnPos, Quaternion.identity);
            DroppedRelic dropped = obj.GetComponent<DroppedRelic>();
            if (dropped != null)
            {
                dropped.Init(relicData, playerTransform, relicManager);
            }
            else
            {
                Debug.LogError("[GachaChest] 생성된 프리팹에 DroppedRelic 컴포넌트가 없습니다!");
                Destroy(obj);
                if (relicManager != null)
                {
                    relicManager.AddNewRelicToStorage(relicData);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            // 에디터 상에서 상호작용 반경 가이드 라인 그리기
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactionDistance);
        }
    }
}
