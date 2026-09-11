using System.Collections.Generic;
using UnityEngine;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.Core.Managers;
using Nytherion.Core.Enums;

namespace Nytherion.GamePlay.Relics
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(RelicPickupDetector))]
    public class DroppedRelic : MonoBehaviour
    {
        private static readonly HashSet<DroppedRelic> activeDroppedRelics = new HashSet<DroppedRelic>();

        [Header("상자 드롭 궤적")]
        [SerializeField, Min(0.05f), Tooltip("상자에서 최종 위치까지 날아가는 시간입니다.")]
        private float launchDuration = 0.42f;
        [SerializeField, Min(0.1f), Tooltip("상자에서 떨어지는 최소 거리입니다.")]
        private float launchMinDistance = 0.9f;
        [SerializeField, Min(0.1f), Tooltip("상자에서 떨어지는 최대 거리입니다.")]
        private float launchMaxDistance = 1.45f;
        [SerializeField, Min(0f), Tooltip("포물선의 최고 높이입니다.")]
        private float launchArcHeight = 0.75f;

        [Header("유성 비행 이펙트")]
        [SerializeField, Tooltip("유성 중심에 표시할 스프라이트입니다.")]
        private Sprite meteorHeadSprite;
        [SerializeField, Min(0.1f), Tooltip("유성 헤드의 크기 배수입니다.")]
        private float meteorHeadScale = 1.4f;
        [SerializeField, Min(0.05f), Tooltip("유성 꼬리의 길이 배수입니다.")]
        private float meteorTailLength = 1.05f;

        [Header("유물 착지 이펙트")]
        [SerializeField, Tooltip("유물이 착지하는 순간 한 번 재생할 이펙트 프리팹입니다.")]
        private GameObject dropRelicEffectPrefab;

        [Header("드롭 유물 밀어내기")]
        [SerializeField] private bool enableRelicSeparation = true;
        [SerializeField, Min(0.01f), Tooltip("두 유물 중심이 이 거리보다 가까우면 서로 밀어냅니다.")]
        private float relicSeparationDistance = 0.6f;
        [SerializeField, Min(0.01f), Tooltip("겹친 유물을 밀어내는 힘입니다.")]
        private float relicSeparationForce = 5f;

        [Header("재뽑기 자동 획득")]
        [SerializeField, Min(0.1f), Tooltip("다음 뽑기 전에 남은 유물이 플레이어에게 이동하는 속도입니다.")]
        private float autoCollectMoveSpeed = 8f;
        [SerializeField, Min(0.01f), Tooltip("플레이어 충돌이 감지되지 않는 경우에도 자동 획득할 최소 거리입니다.")]
        private float autoCollectArrivalDistance = 0.08f;

        private const float SettleVelocityThreshold = 0.08f;
        private const float RequiredSettleDuration = 0.12f;
        private const float MaxConcealDuration = 0.5f;
        private const float RevealDuration = 0.28f;

        private RelicData relicData;
        private Transform playerTransform;
        private RelicManager relicManager;
        private ObjectPoolManager objectPoolManager;
        private string poolTag;

        private Rigidbody2D rb;
        private SpriteRenderer spriteRenderer;
        private Collider2D col;
        private RelicPickupDetector relicPickupDetector;
        private RarityWorldLightVFX rarityLightEffect;
        private GachaMeteorVFX meteorEffect;
        private bool isCollected;
        private bool isConcealed;
        private bool isRevealing;
        private bool isLaunching;
        private float settledTime;
        private float concealElapsedTime;
        private float revealElapsedTime;
        private float launchElapsedTime;
        private Color originalSpriteColor;
        private Vector3 launchStartPosition;
        private Vector3 launchEndPosition;
        private bool hasLaunchTargetOverride;
        private Vector3 launchTargetOverride;
        private bool isAutoCollecting;
        private Transform autoCollectTarget;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            col = GetComponent<Collider2D>();
            relicPickupDetector = GetComponent<RelicPickupDetector>();
            rarityLightEffect = GetComponent<RarityWorldLightVFX>();
            meteorEffect = GetComponent<GachaMeteorVFX>();

            if (relicPickupDetector == null)
            {
                relicPickupDetector = gameObject.AddComponent<RelicPickupDetector>();
            }

            // 충돌은 감지만 하되 뚫고 지나가도록 설정할 수도 있으나,
            // 흩뿌려질 때는 벽에 부딪힐 수 있도록 Trigger를 끄거나 켜둘 수 있습니다.
            // 여기서는 충돌을 감지하기만 하는 트리거로 설계합니다.
            col.isTrigger = true;
        }

        private void OnEnable()
        {
            activeDroppedRelics.Add(this);
        }

        private void OnDisable()
        {
            activeDroppedRelics.Remove(this);
        }

        private void OnValidate()
        {
            launchDuration = Mathf.Max(0.05f, launchDuration);
            launchMinDistance = Mathf.Max(0.1f, launchMinDistance);
            launchMaxDistance = Mathf.Max(launchMinDistance, launchMaxDistance);
            launchArcHeight = Mathf.Max(0f, launchArcHeight);
            meteorHeadScale = Mathf.Max(0.1f, meteorHeadScale);
            meteorTailLength = Mathf.Max(0.05f, meteorTailLength);
            relicSeparationDistance = Mathf.Max(0.01f, relicSeparationDistance);
            relicSeparationForce = Mathf.Max(0.01f, relicSeparationForce);
            autoCollectMoveSpeed = Mathf.Max(0.1f, autoCollectMoveSpeed);
            autoCollectArrivalDistance = Mathf.Max(0.01f, autoCollectArrivalDistance);
        }

        public static bool HasUncollectedRelics()
        {
            foreach (DroppedRelic droppedRelic in activeDroppedRelics)
            {
                if (droppedRelic != null && droppedRelic.IsAwaitingCollection())
                {
                    return true;
                }
            }

            return false;
        }

        public static void BeginAutoCollectAll(Transform player)
        {
            if (player == null)
            {
                return;
            }

            List<DroppedRelic> relicSnapshot = new List<DroppedRelic>(activeDroppedRelics);
            for (int i = 0; i < relicSnapshot.Count; i++)
            {
                relicSnapshot[i]?.BeginAutoCollect(player);
            }
        }

        public void Init(RelicData data, Transform player, RelicManager manager)
        {
            relicData = data;
            playerTransform = player;
            relicManager = manager;
            isCollected = false;
            isConcealed = false;
            isRevealing = false;
            isLaunching = false;
            isAutoCollecting = false;
            autoCollectTarget = null;
            settledTime = 0f;
            concealElapsedTime = 0f;
            revealElapsedTime = 0f;
            launchElapsedTime = 0f;

            rb.simulated = true;
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.WakeUp();

            if (relicData != null && relicData.Image != null)
            {
                spriteRenderer.sprite = relicData.Image;
            }

            originalSpriteColor = spriteRenderer.color;
            col.enabled = false;
            relicPickupDetector.enabled = false;
            bool shouldConcealRelic = relicData != null;

            if (shouldConcealRelic)
            {
                if (rarityLightEffect == null)
                {
                    rarityLightEffect = gameObject.AddComponent<RarityWorldLightVFX>();
                }

                spriteRenderer.enabled = false;
                col.enabled = false;
                relicPickupDetector.enabled = false;
                rarityLightEffect.BeginConceal(
                    relicData.rarity,
                    spriteRenderer,
                    meteorHeadSprite,
                    null);
                isConcealed = true;
            }
            else
            {
                spriteRenderer.enabled = true;
                spriteRenderer.color = originalSpriteColor;
                rarityLightEffect?.Clear();
            }

            StartLaunch();
        }

        public void SetPool(ObjectPoolManager manager, string tag)
        {
            objectPoolManager = manager;
            poolTag = tag;
        }

        public void SetLaunchTarget(Vector3 targetPosition)
        {
            hasLaunchTargetOverride = true;
            launchTargetOverride = targetPosition;
        }

        private void StartLaunch()
        {
            launchStartPosition = transform.position;

            if (hasLaunchTargetOverride)
            {
                launchEndPosition = launchTargetOverride;
                launchEndPosition.z = launchStartPosition.z;
                hasLaunchTargetOverride = false;
            }
            else
            {
                float horizontalDirection = Random.Range(-1f, 1f);
                if (Mathf.Abs(horizontalDirection) < 0.2f)
                {
                    horizontalDirection = horizontalDirection < 0f ? -0.2f : 0.2f;
                }

                Vector2 launchDirection = new Vector2(horizontalDirection, Random.Range(-0.15f, 0.15f)).normalized;
                float launchDistance = Random.Range(launchMinDistance, launchMaxDistance);
                launchEndPosition = launchStartPosition + (Vector3)(launchDirection * launchDistance);
            }

            launchElapsedTime = 0f;
            isLaunching = true;

            rb.simulated = false;

            if (meteorEffect == null)
            {
                meteorEffect = gameObject.AddComponent<GachaMeteorVFX>();
            }

            meteorEffect.Configure(meteorHeadScale, meteorTailLength);
            meteorEffect.Begin(spriteRenderer, GetRarityEffectColor(relicData != null ? relicData.rarity : Rarity.Common), meteorHeadSprite);
        }

        private void UpdateLaunch()
        {
            launchElapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(launchElapsedTime / launchDuration);
            Vector3 position = Vector3.Lerp(launchStartPosition, launchEndPosition, progress);
            position.y += 4f * launchArcHeight * progress * (1f - progress);
            transform.position = position;

            if (progress < 1f)
            {
                return;
            }

            isLaunching = false;
            transform.position = launchEndPosition;
            rb.position = launchEndPosition;
            rb.simulated = true;
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.Sleep();
            meteorEffect?.End();
            PlayDropRelicEffect();
            rarityLightEffect?.BeginLandingBurst();

            if (!isConcealed)
            {
                col.enabled = true;
                ActivatePickupDetector();
            }
        }

        private void PlayDropRelicEffect()
        {
            if (dropRelicEffectPrefab == null)
            {
                return;
            }

            GameObject effectInstance = Instantiate(dropRelicEffectPrefab, transform.position, Quaternion.identity);
            Animator[] animators = effectInstance.GetComponentsInChildren<Animator>(true);

            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator == null)
                {
                    continue;
                }

                animator.Rebind();
                animator.Update(0f);
            }

            Color effectColor = GetRarityEffectColor(relicData != null ? relicData.rarity : Rarity.Common);
            SpriteRenderer[] effectRenderers = effectInstance.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < effectRenderers.Length; i++)
            {
                effectRenderers[i].color = effectColor;
            }
        }

        private static Color GetRarityEffectColor(Rarity rarity)
        {
            return rarity switch
            {
                Rarity.Legendary => new Color(1f, 0.34f, 0.03f),
                Rarity.Epic => new Color(0.65f, 0.12f, 1f),
                Rarity.Rare => new Color(0.03f, 0.52f, 1f),
                Rarity.Uncommon => new Color(0.2f, 0.9f, 0.72f),
                _ => new Color(0.86f, 0.86f, 0.86f)
            };
        }

        private void FixedUpdate()
        {
            if (isAutoCollecting)
            {
                UpdateAutoCollection();
                return;
            }

            if (!isLaunching && !isCollected)
            {
                ResolveOverlappingRelics();
            }

            if (isLaunching || !isConcealed || isRevealing) return;

            concealElapsedTime += Time.fixedDeltaTime;
            if (concealElapsedTime >= MaxConcealDuration)
            {
                BeginReveal();
                return;
            }

            if (rb.velocity.sqrMagnitude <= SettleVelocityThreshold * SettleVelocityThreshold)
            {
                settledTime += Time.fixedDeltaTime;
                if (settledTime >= RequiredSettleDuration)
                {
                    BeginReveal();
                }
            }
            else
            {
                settledTime = 0f;
            }
        }

        private bool IsAwaitingCollection()
        {
            return isActiveAndEnabled && relicData != null && !isCollected;
        }

        private void BeginAutoCollect(Transform target)
        {
            if (!IsAwaitingCollection() || target == null)
            {
                return;
            }

            isAutoCollecting = true;
            autoCollectTarget = target;
            relicPickupDetector.enabled = false;
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        private void UpdateAutoCollection()
        {
            if (autoCollectTarget == null)
            {
                isAutoCollecting = false;
                return;
            }

            if (isLaunching || isConcealed || isRevealing)
            {
                return;
            }

            Vector2 targetPosition = autoCollectTarget.position;
            Vector2 nextPosition = Vector2.MoveTowards(
                rb.position,
                targetPosition,
                autoCollectMoveSpeed * Time.fixedDeltaTime);
            rb.MovePosition(nextPosition);

            if ((targetPosition - nextPosition).sqrMagnitude <= autoCollectArrivalDistance * autoCollectArrivalDistance)
            {
                CollectRelic();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!isAutoCollecting || autoCollectTarget == null || other == null)
            {
                return;
            }

            Transform otherTransform = other.transform;
            if (otherTransform == autoCollectTarget ||
                otherTransform.IsChildOf(autoCollectTarget) ||
                autoCollectTarget.IsChildOf(otherTransform))
            {
                CollectRelic();
            }
        }

        private void ResolveOverlappingRelics()
        {
            if (!enableRelicSeparation || rb == null || relicData == null)
            {
                return;
            }

            foreach (DroppedRelic other in activeDroppedRelics)
            {
                if (other == null || other == this || other.isLaunching || other.isCollected || other.relicData == null || other.rb == null)
                {
                    continue;
                }

                Vector2 offset = rb.position - other.rb.position;
                float distance = offset.magnitude;
                if (distance >= relicSeparationDistance)
                {
                    continue;
                }

                Vector2 direction = distance > Mathf.Epsilon
                    ? offset / distance
                    : GetPairSeparationDirection(other);
                float overlapRatio = 1f - distance / relicSeparationDistance;
                rb.AddForce(direction * (relicSeparationForce * overlapRatio), ForceMode2D.Force);
            }
        }

        private Vector2 GetPairSeparationDirection(DroppedRelic other)
        {
            int pairId = Mathf.Abs(GetInstanceID() + other.GetInstanceID());
            float angle = pairId % 360;
            Vector2 pairDirection = new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad));
            return GetInstanceID() < other.GetInstanceID() ? pairDirection : -pairDirection;
        }

        private void Update()
        {
            if (isLaunching)
            {
                UpdateLaunch();
            }

            if (!isRevealing) return;

            revealElapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(revealElapsedTime / RevealDuration);
            Color revealedColor = originalSpriteColor;
            revealedColor.a *= progress;
            spriteRenderer.color = revealedColor;
            rarityLightEffect?.SetRevealProgress(progress);

            if (progress >= 1f)
            {
                isRevealing = false;
                rarityLightEffect?.Clear();
                spriteRenderer.color = originalSpriteColor;
                col.enabled = true;
                ActivatePickupDetector();
            }
        }

        private void BeginReveal()
        {
            isConcealed = false;
            isRevealing = true;
            revealElapsedTime = 0f;

            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.Sleep();

            spriteRenderer.enabled = true;
            Color hiddenColor = originalSpriteColor;
            hiddenColor.a = 0f;
            spriteRenderer.color = hiddenColor;
        }

        private void ActivatePickupDetector()
        {
            if (relicPickupDetector == null) return;

            relicPickupDetector.enabled = true;
            relicPickupDetector.Initialize(playerTransform, CollectRelic);
        }

        private void CollectRelic()
        {
            if (isCollected || isLaunching || isConcealed || isRevealing)
            {
                return;
            }

            isCollected = true;
            if (relicManager != null && relicData != null)
            {
                relicManager.AddNewRelicToStorage(relicData);
            }

            ReturnToPool();
        }

        private void ReturnToPool()
        {
            if (objectPoolManager == null || string.IsNullOrEmpty(poolTag))
            {
                Destroy(gameObject);
                return;
            }

            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = true;
            rb.Sleep();
            isLaunching = false;
            isAutoCollecting = false;
            autoCollectTarget = null;
            col.enabled = false;
            relicPickupDetector.enabled = false;
            rarityLightEffect?.Clear();
            meteorEffect?.End();
            spriteRenderer.sprite = null;
            hasLaunchTargetOverride = false;
            relicData = null;
            playerTransform = null;
            relicManager = null;

            objectPoolManager.ReturnToPool(poolTag, gameObject);
        }
    }

}
