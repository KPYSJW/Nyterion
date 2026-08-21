using System.Collections.Generic;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Combat;
using Nytherion.GamePlay.Skills;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Companions
{
    /// <summary>
    /// 유물 장착 중에만 유지되는 지상형 전투 소환수입니다.
    /// 프리팹별 이동, 공격, 애니메이션 설정은 Inspector에서 조정합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class SummonedCompanion : MonoBehaviour
    {
        [Header("표시")]
        [SerializeField] private GameObject visual;
        [SerializeField] private int sortingOrderOffset = 1;
        [SerializeField] private bool invertFacing;

        [Header("추적")]
        [SerializeField] private float followOffsetX = 1.2f;
        [SerializeField] private float followOffsetY = 0.6f;
        [SerializeField] private float wakeupDistance = 1f;
        [SerializeField] private float stopRadius = 0.5f;
        [SerializeField] private float leashRange = 36f;
        [SerializeField] private float minMoveSpeed = 4f;
        [SerializeField] private float maxMoveSpeed = 5f;
        [SerializeField] private float acceleration = 12f;

        [Header("공격")]
        [SerializeField] private float attackRange = 5f;
        [SerializeField] protected float attackInterval = 1f;
        [SerializeField] protected float attackFreezeDuration = 0.5f;
        [SerializeField] private string projectilePoolTag = "Player_Arrow";
        [SerializeField] private float projectileSpeed = 10f;
        [SerializeField] private float baseDamage = 5f;
        [SerializeField] private float weaponDamageRatio = 0.5f;
        [SerializeField] private float damageRatioPerLevel = 0.1f;

        [Header("애니메이션")]
        [SerializeField] private string walkingBoolParam = "IsWalking";
        [SerializeField] private string jumpTriggerParam;
        [SerializeField] private float jumpInterval = 0.3f;
        [SerializeField] private bool moveOnlyDuringJumpAnimation;
        [SerializeField] private float jumpMovementDistance = 1f;
        [SerializeField] private float jumpMovementDuration = 0.06666667f;
        [SerializeField] private string attackTriggerParam = "Attack1";
        [SerializeField] private string alternateAttackTriggerParam = "Attack2";

        protected static readonly Collider2D[] EnemyBuffer = new Collider2D[20];

        protected Transform owner;
        protected PlayerManager ownerManager;
        protected PlayerCombat ownerCombat;
        private PlayerHealth ownerHealth;
        private Rigidbody2D companionRigidbody;
        private Animator animator;
        protected SpriteRenderer spriteRenderer;
        protected int level;
        private bool isInitialized;
        private bool isMoving;
        private bool isJumpMovementActive;
        private bool isJumpAnimationActive;
        private Vector2 jumpMovementStartPosition;
        private Vector2 jumpMovementDestination;
        private float jumpMovementStartTime;
        private bool isAlternateAttackNext;
        private bool hasLoggedMissingPool;
        private float currentSpeed;
        protected float attackFreezeTimer;
        protected float nextAttackTime;
        private float nextJumpTime;
        private Vector3 targetPosition;
        protected Vector3 lastPlayerAttackPosition;
        protected float lastPlayerAttackTime = float.NegativeInfinity;
        protected Transform defensiveTarget;
        protected float lastPlayerHurtTime = float.NegativeInfinity;
        private float lastRecordedPlayerHealth = -1f;

        protected virtual bool ShouldPursueCombatTarget => false;
        protected virtual float CombatApproachDistance => 0f;
        protected virtual float CombatStopDistance => 0f;

        public void Initialize(PlayerManager playerManager, int companionLevel)
        {
            UnsubscribeOwnerEvents();

            ownerManager = playerManager;
            owner = playerManager != null ? playerManager.transform : null;
            if (owner == null)
            {
                Destroy(gameObject);
                return;
            }

            level = Mathf.Max(1, companionLevel);
            ownerCombat = ownerManager.PlayerCombat != null
                ? ownerManager.PlayerCombat
                : ownerManager.GetComponent<PlayerCombat>();
            ownerHealth = ownerManager.playerHealth != null
                ? ownerManager.playerHealth
                : ownerManager.GetComponent<PlayerHealth>();

            CacheComponents();
            EnsureRigidbody();
            transform.SetParent(null, true);
            transform.position = owner.position + GetFollowOffset();
            companionRigidbody.position = transform.position;
            companionRigidbody.velocity = Vector2.zero;
            SynchronizeSortingOrder();

            if (ownerCombat != null)
            {
                ownerCombat.OnPlayerAttack += HandlePlayerAttack;
            }

            if (ownerHealth != null)
            {
                lastRecordedPlayerHealth = ownerHealth.CurrentHealth;
                PlayerHealth.OnHealthChanged += HandleHealthChanged;
            }

            isInitialized = true;
            isMoving = false;
            currentSpeed = 0f;
            attackFreezeTimer = 0f;
            nextAttackTime = Time.time;
            nextJumpTime = Time.time;
        }

        private void Awake()
        {
            CacheComponents();
            EnsureRigidbody();
        }

        private void Update()
        {
            if (!isInitialized)
            {
                return;
            }

            if (owner == null)
            {
                Destroy(gameObject);
                return;
            }

            if (attackFreezeTimer > 0f)
            {
                attackFreezeTimer -= Time.deltaTime;
            }

            float distanceToOwner = Vector2.Distance(transform.position, owner.position);
            if (distanceToOwner > leashRange)
            {
                TeleportToOwner();
            }

            Transform combatTarget = FindPriorityTarget();
            UpdateMovementTarget(combatTarget);
            UpdateFlip(combatTarget);
            AutoAttack(combatTarget);
        }

        private void FixedUpdate()
        {
            if (!isInitialized || owner == null || companionRigidbody == null)
            {
                return;
            }

            if (!isMoving || (moveOnlyDuringJumpAnimation && !isJumpMovementActive))
            {
                companionRigidbody.velocity = Vector2.zero;
                currentSpeed = 0f;
                return;
            }

            if (moveOnlyDuringJumpAnimation)
            {
                float elapsed = Mathf.Max(0f, Time.time - jumpMovementStartTime);
                float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, jumpMovementDuration));
                companionRigidbody.MovePosition(Vector2.Lerp(
                    jumpMovementStartPosition,
                    jumpMovementDestination,
                    progress));
                return;
            }

            Vector2 direction = targetPosition - transform.position;
            float distance = direction.magnitude;
            if (distance <= stopRadius)
            {
                companionRigidbody.velocity = Vector2.zero;
                currentSpeed = 0f;
                return;
            }

            float desiredSpeed = Mathf.Clamp(
                distance * maxMoveSpeed / Mathf.Max(0.01f, wakeupDistance),
                minMoveSpeed,
                maxMoveSpeed);
            currentSpeed = Mathf.MoveTowards(currentSpeed, desiredSpeed, acceleration * Time.fixedDeltaTime);
            companionRigidbody.velocity = direction.normalized * currentSpeed;
        }

        private void CacheComponents()
        {
            GameObject visualObject = visual != null ? visual : gameObject;
            animator = visualObject.GetComponent<Animator>();
            if (animator == null)
            {
                animator = visualObject.GetComponentInChildren<Animator>();
            }

            spriteRenderer = visualObject.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = visualObject.GetComponentInChildren<SpriteRenderer>();
            }
        }

        private void EnsureRigidbody()
        {
            if (companionRigidbody == null)
            {
                companionRigidbody = GetComponent<Rigidbody2D>();
                if (companionRigidbody == null)
                {
                    companionRigidbody = gameObject.AddComponent<Rigidbody2D>();
                }
            }

            companionRigidbody.gravityScale = 0f;
            companionRigidbody.drag = 3.5f;
            companionRigidbody.constraints = RigidbodyConstraints2D.FreezeRotation;
            companionRigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        private Vector3 GetFollowOffset()
        {
            PlayerController playerController = owner != null ? owner.GetComponent<PlayerController>() : null;
            bool isFacingRight = playerController == null || playerController.IsFacingRight;
            float side = isFacingRight ? -1f : 1f;

            if (playerController != null && Mathf.Abs(playerController.MoveInput.x) > 0.1f)
            {
                side = playerController.MoveInput.x > 0f ? -1f : 1f;
            }

            return new Vector3(side * followOffsetX, followOffsetY, 0f);
        }

        private void TeleportToOwner()
        {
            transform.position = owner.position + GetFollowOffset();
            companionRigidbody.position = transform.position;
            companionRigidbody.velocity = Vector2.zero;
            currentSpeed = 0f;
            SetMoving(false);
        }

        private void UpdateMovementTarget(Transform combatTarget)
        {
            float distanceToTarget;
            bool shouldMove;

            if (ShouldPursueCombatTarget && combatTarget != null)
            {
                targetPosition = combatTarget.position;
                distanceToTarget = Vector2.Distance(transform.position, targetPosition);
                shouldMove = distanceToTarget > CombatApproachDistance;
                if (isMoving && distanceToTarget <= CombatStopDistance)
                {
                    shouldMove = false;
                }
            }
            else
            {
                targetPosition = owner.position + GetFollowOffset();
                distanceToTarget = Vector2.Distance(transform.position, targetPosition);
                shouldMove = distanceToTarget > wakeupDistance;
                if (isMoving && distanceToTarget <= stopRadius)
                {
                    shouldMove = false;
                }
            }

            SetMoving(shouldMove && attackFreezeTimer <= 0f);
        }

        /// <summary>
        /// Jump 애니메이션의 이동 시작 프레임 Animation Event에서 호출합니다.
        /// </summary>
        public void BeginJumpMovement()
        {
            if (moveOnlyDuringJumpAnimation && isMoving)
            {
                isJumpMovementActive = true;
                jumpMovementStartTime = Time.time;
                jumpMovementStartPosition = companionRigidbody.position;

                Vector2 direction = (Vector2)targetPosition - jumpMovementStartPosition;
                float moveDistance = Mathf.Min(direction.magnitude, Mathf.Max(0f, jumpMovementDistance));
                jumpMovementDestination = moveDistance > 0.001f
                    ? jumpMovementStartPosition + direction.normalized * moveDistance
                    : jumpMovementStartPosition;
            }
        }

        /// <summary>
        /// Jump 애니메이션의 착지 프레임 Animation Event에서 호출합니다.
        /// </summary>
        public void EndJumpMovement()
        {
            if (moveOnlyDuringJumpAnimation && isJumpMovementActive && companionRigidbody != null)
            {
                companionRigidbody.position = jumpMovementDestination;
            }

            isJumpMovementActive = false;
            isJumpAnimationActive = false;
            if (companionRigidbody != null)
            {
                companionRigidbody.velocity = Vector2.zero;
            }
        }

        protected void SetMoving(bool shouldMove)
        {
            if (isMoving == shouldMove)
            {
                if (shouldMove && Time.time >= nextJumpTime && !isJumpAnimationActive)
                {
                    TriggerJumpAnimation();
                    nextJumpTime = Time.time + Mathf.Max(0.01f, jumpInterval);
                }
                return;
            }

            isMoving = shouldMove;
            SetAnimationBool(walkingBoolParam, isMoving);
            if (!isMoving)
            {
                isJumpMovementActive = false;
            }
            if (isMoving)
            {
                TriggerJumpAnimation();
                nextJumpTime = Time.time + Mathf.Max(0.01f, jumpInterval);
            }
        }

        private void TriggerJumpAnimation()
        {
            if (string.IsNullOrEmpty(jumpTriggerParam) || isJumpAnimationActive)
            {
                return;
            }

            isJumpMovementActive = false;
            isJumpAnimationActive = moveOnlyDuringJumpAnimation;
            TriggerAnimation(jumpTriggerParam);
        }

        private void UpdateFlip(Transform combatTarget)
        {
            if (owner == null)
            {
                return;
            }

            float horizontalDifference;
            if (combatTarget != null)
            {
                horizontalDifference = combatTarget.position.x - transform.position.x;
            }
            else
            {
                horizontalDifference = isMoving && companionRigidbody != null &&
                                     Mathf.Abs(companionRigidbody.velocity.x) > 0.05f
                    ? companionRigidbody.velocity.x
                    : owner.position.x - transform.position.x;
            }
            if (Mathf.Abs(horizontalDifference) <= 0.05f)
            {
                return;
            }

            Vector3 scale = transform.localScale;
            float facingDirection = horizontalDifference > 0f ? 1f : -1f;
            scale.x = Mathf.Abs(scale.x) * facingDirection * (invertFacing ? -1f : 1f);
            transform.localScale = scale;
        }

        private void AutoAttack(Transform target)
        {
            if (Time.time < nextAttackTime)
            {
                return;
            }

            if (target == null)
            {
                nextAttackTime = Time.time + 0.1f;
                return;
            }

            if (TryAttack(target))
            {
                nextAttackTime = Time.time + Mathf.Max(0.01f, attackInterval);
            }
        }

        protected virtual bool TryAttack(Transform target)
        {
            FireAtTarget(target);
            return true;
        }

        protected virtual Transform FindPriorityTarget()
        {
            return FindClosestEnemy(transform.position, attackRange);
        }

        protected Transform FindClosestEnemy(Vector2 searchCenter, float searchRange)
        {
            int hitCount = Physics2D.OverlapCircleNonAlloc(searchCenter, searchRange, EnemyBuffer);
            Transform closestTarget = null;
            float closestDistanceSqr = Mathf.Infinity;

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = EnemyBuffer[i];
                if (hit == null || !hit.CompareTag("Enemy") || hit.GetComponent<IDamageable>() == null)
                {
                    continue;
                }

                float distanceSqr = (hit.transform.position - transform.position).sqrMagnitude;
                if (distanceSqr < closestDistanceSqr)
                {
                    closestDistanceSqr = distanceSqr;
                    closestTarget = hit.transform;
                }
            }

            return closestTarget;
        }

        /// <summary>
        /// Croak 애니메이션의 공격 프레임 Animation Event에서 호출합니다.
        /// </summary>
        protected void FireAtTarget(Transform target)
        {
            attackFreezeTimer = attackFreezeDuration;
            SetMoving(false);

            if (ObjectPoolManager.Instance == null || string.IsNullOrEmpty(projectilePoolTag))
            {
                LogMissingProjectilePool();
                TriggerAttackAnimation();
                return;
            }

            Vector2 direction = (target.position - transform.position).normalized;
            GameObject projectile = ObjectPoolManager.Instance.SpawnFromPool(
                projectilePoolTag,
                transform.position,
                Quaternion.identity);
            if (projectile == null)
            {
                TriggerAttackAnimation();
                return;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            projectile.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

            if (projectile.TryGetComponent(out Rigidbody2D projectileRigidbody))
            {
                projectileRigidbody.velocity = direction * projectileSpeed;
            }

            if (projectile.TryGetComponent(out IProj projectileController))
            {
                projectileController.SetSpeed(projectileSpeed);
            }

            WeaponBase currentWeapon = ownerCombat != null ? ownerCombat.currentWeapon : null;
            WeaponData weaponData = currentWeapon != null ? currentWeapon.weaponData : null;
            CombatModifierSnapshot modifiers = ownerManager != null && ownerManager.playerRelicManager != null
                ? ownerManager.playerRelicManager.CombatModifiers
                : CombatModifierSnapshot.Empty;

            if (projectile.TryGetComponent(out CollisionObject collisionObject))
            {
                List<Nytherion.Core.Enums.EquipmentTrait> traits = currentWeapon != null
                    ? currentWeapon.GetTraits()
                    : new List<Nytherion.Core.Enums.EquipmentTrait>();
                collisionObject.Configure(
                    GetProjectileDamage(currentWeapon),
                    traits,
                    0f,
                    weaponData != null ? weaponData.hitEffectPrefab : null,
                    modifiers);
            }

            bool shouldUseHoming = weaponData != null && weaponData.hasHomingProjectiles;
            shouldUseHoming |= modifiers.HasProjectileHoming;
            if (!projectile.TryGetComponent(out HomingProj homingProjectile) && shouldUseHoming)
            {
                homingProjectile = projectile.AddComponent<HomingProj>();
            }
            if (homingProjectile != null)
            {
                homingProjectile.SetHomingEnabled(shouldUseHoming, projectileSpeed);
            }

            if (spriteRenderer != null && projectile.TryGetComponent(out SpriteRenderer projectileRenderer))
            {
                projectileRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
                projectileRenderer.sortingOrder = spriteRenderer.sortingOrder + 1;
            }

            TriggerAttackAnimation();
        }

        protected float GetProjectileDamage(WeaponBase currentWeapon)
        {
            float levelRatio = weaponDamageRatio +
                               damageRatioPerLevel * Mathf.Max(0, level - 1);
            if (currentWeapon != null && currentWeapon.weaponData != null)
            {
                return currentWeapon.weaponData.damage * currentWeapon.CurrentDamageMultiplier * Mathf.Max(0f, levelRatio);
            }

            return baseDamage + damageRatioPerLevel * Mathf.Max(0, level - 1);
        }

        protected void TriggerAttackAnimation()
        {
            string animationParam = isAlternateAttackNext && !string.IsNullOrEmpty(alternateAttackTriggerParam)
                ? alternateAttackTriggerParam
                : attackTriggerParam;
            TriggerAnimation(animationParam);
            isAlternateAttackNext = !isAlternateAttackNext;
        }

        private void TriggerAnimation(string parameterName)
        {
            if (animator == null || string.IsNullOrEmpty(parameterName))
            {
                return;
            }

            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.name != parameterName)
                {
                    continue;
                }

                if (parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    animator.SetTrigger(parameterName);
                }
                return;
            }
        }

        private void SetAnimationBool(string parameterName, bool value)
        {
            if (animator == null || string.IsNullOrEmpty(parameterName))
            {
                return;
            }

            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.name == parameterName && parameter.type == AnimatorControllerParameterType.Bool)
                {
                    animator.SetBool(parameterName, value);
                    return;
                }
            }
        }

        private void HandlePlayerAttack(Vector2 direction, Vector3 targetPosition)
        {
            lastPlayerAttackPosition = targetPosition;
            lastPlayerAttackTime = Time.time;
        }

        private void HandleHealthChanged(float currentHealth, float maxHealth)
        {
            if (owner == null)
            {
                return;
            }

            if (lastRecordedPlayerHealth > 0f && currentHealth < lastRecordedPlayerHealth)
            {
                FindPlayerAttacker();
            }
            lastRecordedPlayerHealth = currentHealth;
        }

        protected virtual void FindPlayerAttacker()
        {
            int hitCount = Physics2D.OverlapCircleNonAlloc(owner.position, attackRange, EnemyBuffer);
            float closestDistanceSqr = Mathf.Infinity;
            defensiveTarget = null;

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = EnemyBuffer[i];
                if (hit == null || !hit.CompareTag("Enemy") || hit.GetComponent<IDamageable>() == null)
                {
                    continue;
                }

                float distanceSqr = (owner.position - hit.transform.position).sqrMagnitude;
                if (distanceSqr < closestDistanceSqr)
                {
                    closestDistanceSqr = distanceSqr;
                    defensiveTarget = hit.transform;
                }
            }

            if (defensiveTarget != null)
            {
                lastPlayerHurtTime = Time.time;
            }
        }

        private void SynchronizeSortingOrder()
        {
            if (spriteRenderer == null || owner == null)
            {
                return;
            }

            SpriteRenderer ownerRenderer = owner.GetComponent<SpriteRenderer>();
            if (ownerRenderer == null)
            {
                ownerRenderer = owner.GetComponentInChildren<SpriteRenderer>();
            }
            if (ownerRenderer == null)
            {
                return;
            }

            spriteRenderer.sortingLayerID = ownerRenderer.sortingLayerID;
            spriteRenderer.sortingOrder = ownerRenderer.sortingOrder + sortingOrderOffset;
        }

        private void LogMissingProjectilePool()
        {
            if (hasLoggedMissingPool)
            {
                return;
            }

            hasLoggedMissingPool = true;
            Debug.LogWarning("[SummonedCompanion] 소환수 투사체 풀을 찾을 수 없습니다.");
        }

        private void OnDestroy()
        {
            UnsubscribeOwnerEvents();
        }

        private void UnsubscribeOwnerEvents()
        {
            if (ownerCombat != null)
            {
                ownerCombat.OnPlayerAttack -= HandlePlayerAttack;
            }
            if (ownerHealth != null)
            {
                PlayerHealth.OnHealthChanged -= HandleHealthChanged;
            }

            ownerCombat = null;
            ownerHealth = null;
        }
    }
}
