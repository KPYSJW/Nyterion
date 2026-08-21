using System;
using System.Collections;
using System.Collections.Generic;
using Nytherion.Core.Enums;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Combat;
using Nytherion.GamePlay.Skills;
using UnityEngine;

namespace Nytherion.Gameplay.Relics.Modules
{
    /// <summary>
    /// 플레이어 주변에서 부유하며 공격에 맞춰 투사체를 발사하는 동반 유물 효과의 공통 기반 클래스.
    /// </summary>
    [Serializable]
    public abstract class FollowerAttackRelicEffectBase : RelicEffectBase
    {
        [Header("동반체")]
        [Tooltip("플레이어에게 고정되어 따라다닐 동반체 스프라이트")]
        public Sprite companionSprite;
        [Tooltip("플레이어 기준 동반체의 고정 위치")]
        public Vector3 localOffset = new Vector3(0.35f, 0.3f, 0f);
        [Tooltip("동반체의 크기")]
        public Vector3 companionScale = new Vector3(0.3f, 0.3f, 0.3f);
        [Tooltip("상하 부유 높이")]
        public float floatingHeight = 0.08f;
        [Tooltip("상하 부유 속도")]
        public float floatingSpeed = 2f;
        [Tooltip("동반체가 플레이어 주위를 공전하는 기본 반경")]
        public float orbitRadius = 0.45f;
        [Tooltip("동반체가 플레이어 주위를 공전하는 속도")]
        public float orbitSpeed = 0.45f;
        [Tooltip("각 동반체별로 시작되는 위치와 반경의 변화 범위")]
        public float orbitRadiusVariance = 0.12f;
        [Range(0.1f, 1f)]
        [Tooltip("공전 궤도의 가로 반경에 대한 세로 반경의 비율")]
        public float orbitVerticalRatio = 0.6f;
        [Tooltip("플레이어 스프라이트보다 앞에 표시할 정렬 순서 오프셋")]
        public int sortingOrderOffset = 2;

        [Header("투사체")]
        [Tooltip("동반체가 발사할 전용 투사체 프리팹")]
        public GameObject projectilePrefab;
        [Tooltip("플레이어 무기 투사체와 별도로 적용할 동반체 투사체 크기 배율")]
        public Vector3 projectileScale = new Vector3(0.3f, 0.3f, 0.3f);
        [Tooltip("동반체 중심에서 발사 방향으로 떨어진 투사체 생성 위치")]
        public float projectileForwardOffset = 0.35f;
        [Tooltip("투사체 이동 속도")]
        public float projectileSpeed = 8f;
        [Tooltip("투사체가 풀로 돌아가기까지의 시간")]
        public float projectileLifetime = 1.5f;
        [Tooltip("현재 장착 무기 공격력에 적용할 피해 비율")]
        public float damageRatio = 0.5f;
        [Tooltip("유물 레벨당 추가 피해 비율")]
        public float damageRatioPerLevel = 0.1f;

        private PlayerManager cachedPlayerManager;
        private PlayerCombat cachedPlayerCombat;
        private GameObject companionObject;
        private int currentLevel;
        private bool hasLoggedMissingProjectile;

        protected virtual string CompanionObjectName => "Follower Attack Companion";

        protected virtual void AddProjectileTraits(List<EquipmentTrait> traits)
        {
        }

        public override void ApplyEffect(PlayerManager playerManager, int level)
        {
            Detach();

            if (playerManager == null)
            {
                return;
            }

            cachedPlayerManager = playerManager;
            cachedPlayerCombat = playerManager.PlayerCombat != null
                ? playerManager.PlayerCombat
                : playerManager.GetComponent<PlayerCombat>();
            currentLevel = Mathf.Max(1, level);

            CreateCompanion();

            if (cachedPlayerCombat != null)
            {
                cachedPlayerCombat.OnPlayerAttack += HandlePlayerAttack;
            }
            else
            {
                Debug.LogWarning("[FollowerAttackRelicEffectBase] PlayerCombat을 찾을 수 없어 동반 공격을 연결하지 못했습니다.");
            }
        }

        public override void RemoveEffect(PlayerManager playerManager, int level)
        {
            Detach();
        }

        private void CreateCompanion()
        {
            companionObject = new GameObject(CompanionObjectName);
            companionObject.transform.SetParent(cachedPlayerManager.transform, false);

            PlayerController playerController = cachedPlayerManager.GetComponent<PlayerController>();
            SpriteRenderer playerRenderer = cachedPlayerManager.GetComponent<SpriteRenderer>();
            if (playerRenderer == null)
            {
                playerRenderer = cachedPlayerManager.GetComponentInChildren<SpriteRenderer>();
            }

            SpriteRenderer companionRenderer = companionObject.AddComponent<SpriteRenderer>();
            companionRenderer.sprite = companionSprite;

            FloatingFollowerVisual companion = companionObject.AddComponent<FloatingFollowerVisual>();
            companion.Initialize(
                playerController,
                playerRenderer,
                localOffset,
                companionScale,
                floatingHeight,
                floatingSpeed,
                orbitRadius,
                orbitSpeed,
                orbitRadiusVariance,
                orbitVerticalRatio);

            if (playerRenderer != null)
            {
                companionRenderer.sortingLayerID = playerRenderer.sortingLayerID;
                companionRenderer.sortingOrder = playerRenderer.sortingOrder + sortingOrderOffset;
            }
            else
            {
                companionRenderer.sortingOrder = sortingOrderOffset;
            }
        }

        private void HandlePlayerAttack(Vector2 direction, Vector3 targetPosition)
        {
            if (projectilePrefab == null || ObjectPoolManager.Instance == null || direction.sqrMagnitude <= 0.0001f)
            {
                LogMissingProjectileConfiguration();
                return;
            }

            Vector2 normalizedDirection = direction.normalized;
            FollowerAttackSetBonusRuntime setBonus = cachedPlayerManager != null
                ? cachedPlayerManager.GetComponent<FollowerAttackSetBonusRuntime>()
                : null;
            int projectileCount = 1 + (setBonus != null ? setBonus.AdditionalProjectileCount : 0);
            float spreadAngle = setBonus != null ? setBonus.AdditionalProjectileSpreadAngle : 0f;

            for (int projectileIndex = 0; projectileIndex < projectileCount; projectileIndex++)
            {
                float spreadOffset = projectileCount > 1
                    ? (projectileIndex - ((projectileCount - 1) * 0.5f)) * spreadAngle
                    : 0f;
                Vector2 projectileDirection =
                    (Quaternion.AngleAxis(spreadOffset, Vector3.forward) * (Vector3)normalizedDirection).normalized;
                FireProjectile(
                    projectileDirection,
                    setBonus != null ? setBonus.DamageMultiplier : 1f);
            }
        }

        private void FireProjectile(Vector2 direction, float setDamageMultiplier)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Vector3 spawnPosition = companionObject != null
                ? companionObject.transform.position
                : cachedPlayerManager.transform.TransformPoint(localOffset);
            spawnPosition += (Vector3)(direction * projectileForwardOffset);

            GameObject projectile = ObjectPoolManager.Instance.SpawnFromPool(
                projectilePrefab,
                spawnPosition,
                Quaternion.AngleAxis(angle, Vector3.forward));

            if (projectile == null)
            {
                return;
            }

            projectile.transform.localScale = Vector3.Scale(projectile.transform.localScale, projectileScale);

            if (projectile.TryGetComponent(out Rigidbody2D rigidbody))
            {
                rigidbody.velocity = direction * projectileSpeed;
            }

            if (projectile.TryGetComponent(out IProj projectileController))
            {
                projectileController.SetSpeed(projectileSpeed);
            }

            WeaponBase currentWeapon = cachedPlayerCombat != null ? cachedPlayerCombat.currentWeapon : null;
            WeaponData weaponData = currentWeapon != null ? currentWeapon.weaponData : null;
            CombatModifierSnapshot modifiers = cachedPlayerManager.playerRelicManager != null
                ? cachedPlayerManager.playerRelicManager.CombatModifiers
                : CombatModifierSnapshot.Empty;

            if (projectile.TryGetComponent(out CollisionObject collisionObject))
            {
                List<EquipmentTrait> traits = currentWeapon != null
                    ? new List<EquipmentTrait>(currentWeapon.GetTraits())
                    : new List<EquipmentTrait>();
                AddProjectileTraits(traits);

                collisionObject.poolTag = projectilePrefab.name;
                collisionObject.hitEffectPrefab = weaponData != null ? weaponData.hitEffectPrefab : null;
                collisionObject.Configure(
                    GetProjectileDamage(currentWeapon) * setDamageMultiplier,
                    traits,
                    0f,
                    weaponData != null ? weaponData.hitEffectPrefab : null,
                    modifiers);
            }

            if (projectile.TryGetComponent(out SpriteRenderer projectileRenderer))
            {
                SpriteRenderer companionRenderer = companionObject != null
                    ? companionObject.GetComponent<SpriteRenderer>()
                    : null;
                if (companionRenderer != null)
                {
                    projectileRenderer.sortingLayerID = companionRenderer.sortingLayerID;
                    projectileRenderer.sortingOrder = companionRenderer.sortingOrder + 1;
                }
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

            if (projectile.TryGetComponent(out AutoReturnToPool autoReturnToPool))
            {
                autoReturnToPool.InitializeDelay(projectileLifetime);
            }
            else
            {
                if (!projectile.TryGetComponent(out TimedCompanionProjectileReturn timedReturn))
                {
                    timedReturn = projectile.AddComponent<TimedCompanionProjectileReturn>();
                }

                timedReturn.Begin(projectilePrefab.name, projectileLifetime);
            }
        }

        private float GetProjectileDamage(WeaponBase currentWeapon)
        {
            if (currentWeapon == null || currentWeapon.weaponData == null)
            {
                return 0f;
            }

            float levelRatio = damageRatio + (damageRatioPerLevel * Mathf.Max(0, currentLevel - 1));
            return currentWeapon.weaponData.damage * currentWeapon.CurrentDamageMultiplier * Mathf.Max(0f, levelRatio);
        }

        private void LogMissingProjectileConfiguration()
        {
            if (hasLoggedMissingProjectile)
            {
                return;
            }

            hasLoggedMissingProjectile = true;
            Debug.LogWarning("[FollowerAttackRelicEffectBase] 동반체 투사체 프리팹 또는 ObjectPoolManager가 없어 발사하지 못했습니다.");
        }

        private void Detach()
        {
            if (cachedPlayerCombat != null)
            {
                cachedPlayerCombat.OnPlayerAttack -= HandlePlayerAttack;
            }

            if (companionObject != null)
            {
                UnityEngine.Object.Destroy(companionObject);
            }

            cachedPlayerManager = null;
            cachedPlayerCombat = null;
            companionObject = null;
            currentLevel = 0;
            hasLoggedMissingProjectile = false;
        }
    }

    /// <summary>
    /// 플레이어 중심을 기준으로 바라보는 방향의 반대편 위에서 부유하는 동반체 시각 오브젝트.
    /// </summary>
    public class FloatingFollowerVisual : MonoBehaviour
    {
        private PlayerController playerController;
        private SpriteRenderer playerSpriteRenderer;
        private Vector3 baseLocalOffset;
        private float floatingHeight;
        private float floatingSpeed;
        private float floatingPhase;
        private float orbitCenterY;
        private float orbitCenterZ;
        private float orbitRadius;
        private float orbitSpeed;
        private float orbitRadiusVariance;
        private float orbitVerticalRatio;
        private float orbitPhase;
        private float orbitDirection;
        private float orbitRadiusPhase;
        private SpriteRenderer spriteRenderer;

        public void Initialize(
            PlayerController owner,
            SpriteRenderer ownerSpriteRenderer,
            Vector3 localOffset,
            Vector3 localScale,
            float height,
            float speed,
            float targetOrbitRadius,
            float targetOrbitSpeed,
            float targetOrbitRadiusVariance,
            float targetOrbitVerticalRatio)
        {
            playerController = owner;
            playerSpriteRenderer = ownerSpriteRenderer;
            baseLocalOffset = localOffset;
            floatingHeight = Mathf.Max(0f, height);
            floatingSpeed = Mathf.Max(0f, speed);
            floatingPhase = UnityEngine.Random.value * Mathf.PI * 2f;
            orbitCenterY = localOffset.y;
            orbitCenterZ = localOffset.z;
            orbitRadius = Mathf.Max(
                0.1f,
                targetOrbitRadius + UnityEngine.Random.Range(-targetOrbitRadiusVariance, targetOrbitRadiusVariance));
            orbitSpeed = Mathf.Max(0.05f, targetOrbitSpeed * UnityEngine.Random.Range(0.8f, 1.2f));
            orbitRadiusVariance = Mathf.Max(0f, targetOrbitRadiusVariance * 0.5f);
            orbitVerticalRatio = Mathf.Clamp(targetOrbitVerticalRatio, 0.1f, 1f);
            orbitPhase = UnityEngine.Random.value * Mathf.PI * 2f;
            orbitDirection = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            orbitRadiusPhase = UnityEngine.Random.value * Mathf.PI * 2f;
            spriteRenderer = GetComponent<SpriteRenderer>();
            transform.localScale = localScale;
            UpdatePosition();
        }

        private void LateUpdate()
        {
            UpdatePosition();
        }

        private void UpdatePosition()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            float orbitTime = Time.time * orbitSpeed * orbitDirection;
            float orbitAngle = orbitPhase + orbitTime;
            float currentOrbitRadius = Mathf.Max(
                0.1f,
                orbitRadius + Mathf.Sin((orbitTime * 0.37f) + orbitRadiusPhase) * orbitRadiusVariance);
            float floatingOffset = Mathf.Sin((Time.time * floatingSpeed) + floatingPhase) * floatingHeight;

            transform.localPosition = new Vector3(
                Mathf.Cos(orbitAngle) * currentOrbitRadius,
                orbitCenterY + (Mathf.Sin(orbitAngle) * currentOrbitRadius * orbitVerticalRatio) + floatingOffset,
                orbitCenterZ);

            bool isFacingRight = playerSpriteRenderer != null
                ? !playerSpriteRenderer.flipX
                : playerController == null || playerController.IsFacingRight;

            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = isFacingRight;
            }
        }
    }

    /// <summary>
    /// 애니메이션 길이와 무관하게 동반 유물 투사체를 지정 시간 뒤 풀로 반환합니다.
    /// </summary>
    public sealed class TimedCompanionProjectileReturn : MonoBehaviour
    {
        private Coroutine returnCoroutine;
        private string poolTag;

        public void Begin(string targetPoolTag, float delay)
        {
            poolTag = targetPoolTag;

            if (returnCoroutine != null)
            {
                StopCoroutine(returnCoroutine);
            }

            returnCoroutine = StartCoroutine(ReturnAfterDelay(Mathf.Max(0.01f, delay)));
        }

        private void OnDisable()
        {
            if (returnCoroutine != null)
            {
                StopCoroutine(returnCoroutine);
                returnCoroutine = null;
            }
        }

        private IEnumerator ReturnAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            returnCoroutine = null;

            if (ObjectPoolManager.Instance != null && !string.IsNullOrEmpty(poolTag))
            {
                ObjectPoolManager.Instance.ReturnToPool(poolTag, gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
