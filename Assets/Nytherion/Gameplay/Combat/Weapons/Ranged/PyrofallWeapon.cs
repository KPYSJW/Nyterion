using System.Collections.Generic;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Characters.Player;
using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    /// <summary>
    /// 지정한 지점의 카메라 상단에서 화염구를 떨어뜨리는 무기입니다.
    /// </summary>
    public sealed class PyrofallWeapon : WeaponBase
    {
        private const string ZenithRelicId = "Zenith";

        [Header("Pyrofall Settings")]
        [SerializeField, Min(0f)] private float cameraTopPadding;
        [SerializeField, Min(0f)] private float minimumDropHeight = 1f;

        [Header("Zenith Auto Targeting")]
        [SerializeField] private LayerMask autoTargetEnemyLayers;
        [SerializeField, Min(1)] private int autoTargetBufferSize = 64;

        [Header("Cast Flash")]
        [SerializeField] private SpriteRenderer castFlashRenderer;
        [SerializeField] private Sprite[] castFlashFrames;
        [SerializeField, Min(1f)] private float castFlashAnimationFps = 14f;

        private Camera gameplayCamera;
        private PlayerController playerController;
        private readonly HashSet<IDamageable> autoTargetEnemies = new HashSet<IDamageable>();
        private readonly List<Vector3> autoTargetPositions = new List<Vector3>();
        private Collider2D[] autoTargetBuffer;
        private Vector3 rightFacingLocalPosition;
        private Vector3 rightFacingLocalScale;
        private float rightFacingRotation;
        private bool facingPoseCached;
        private float castFlashAnimationTime;
        private bool isCastFlashPlaying;

        public override bool OverrideRotation => true;

        protected override void Awake()
        {
            base.Awake();
            gameplayCamera = Camera.main;
            playerController = GetComponentInParent<PlayerController>();
            if (autoTargetEnemyLayers.value == 0)
            {
                autoTargetEnemyLayers = LayerMask.GetMask("Enemy");
            }
            EnsureAutoTargetBuffer();
            HideCastFlash();
        }

        private void Start()
        {
            if (!facingPoseCached)
            {
                CacheRightFacingPose(weaponData);
            }

            ApplyFacingPose();
        }

        public override void Initialize(WeaponData data)
        {
            base.Initialize(data);
            CacheRightFacingPose(data);
        }

        public override void Attack(Vector2 direction, Vector3 targetPosition = default)
        {
            if (!CanAttack() || weaponData == null || weaponData.projectilePrefab == null)
            {
                return;
            }

            lastAttackTime = Time.time;
            PlayFireAnimation();
            PlayCastFlash();

            SpawnFireball(targetPosition);

            int extraProjectileCount = GetExtraProjectileCount();
            bool useZenithTargeting = extraProjectileCount > 0 && IsZenithActive();
            if (useZenithTargeting)
            {
                CollectVisibleEnemyTargets(targetPosition.z);
            }

            for (int i = 0; i < extraProjectileCount; i++)
            {
                Vector3 destination = useZenithTargeting && autoTargetPositions.Count > 0
                    ? autoTargetPositions[i % autoTargetPositions.Count]
                    : GetRandomCameraTarget(targetPosition);
                SpawnFireball(destination);
            }
        }

        private void SpawnFireball(Vector3 destination)
        {
            Vector3 spawnPosition = destination;
            spawnPosition.y = GetCameraTopY(destination);

            GameObject projectile = ObjectPoolManager.Instance != null
                ? ObjectPoolManager.Instance.SpawnFromPool(
                    weaponData.projectilePrefab,
                    spawnPosition,
                    Quaternion.identity)
                : Instantiate(weaponData.projectilePrefab, spawnPosition, Quaternion.identity);

            if (projectile != null && projectile.TryGetComponent(out PyrofallProjectile fireball))
            {
                fireball.Initialize(
                    destination,
                    weaponData.damage * EffectiveDamageMultiplier,
                    Mathf.Max(0.1f, weaponData.range),
                    Mathf.Max(0.1f, weaponData.projectileSpeed),
                    weaponData.projectilePrefab.name,
                    weaponData.hitEffectPrefab,
                    this);
            }
        }

        private int GetExtraProjectileCount()
        {
            if (playerManager == null || playerManager.currentPlayerData == null)
            {
                return 0;
            }

            return Mathf.Max(0, Mathf.FloorToInt(playerManager.currentPlayerData.extraProjectiles));
        }

        private bool IsZenithActive()
        {
            return playerManager != null &&
                   playerManager.playerRelicManager != null &&
                   playerManager.playerRelicManager.IsRelicActive(ZenithRelicId);
        }

        private void CollectVisibleEnemyTargets(float targetZ)
        {
            autoTargetEnemies.Clear();
            autoTargetPositions.Clear();

            if (gameplayCamera == null || !gameplayCamera.isActiveAndEnabled)
            {
                gameplayCamera = Camera.main;
            }
            if (gameplayCamera == null)
            {
                return;
            }

            EnsureAutoTargetBuffer();
            float depth = Mathf.Abs(targetZ - gameplayCamera.transform.position.z);
            Vector3 viewportCornerA = gameplayCamera.ViewportToWorldPoint(new Vector3(0f, 0f, depth));
            Vector3 viewportCornerB = gameplayCamera.ViewportToWorldPoint(new Vector3(1f, 1f, depth));
            Vector2 minimum = Vector2.Min(viewportCornerA, viewportCornerB);
            Vector2 maximum = Vector2.Max(viewportCornerA, viewportCornerB);
            int hitCount = Physics2D.OverlapAreaNonAlloc(
                minimum,
                maximum,
                autoTargetBuffer,
                autoTargetEnemyLayers);

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = autoTargetBuffer[i];
                if (hit == null)
                {
                    continue;
                }

                IDamageable target = hit.GetComponentInParent<IDamageable>();
                MonoBehaviour targetBehaviour = target as MonoBehaviour;
                if (targetBehaviour == null || !targetBehaviour.gameObject.activeInHierarchy ||
                    !autoTargetEnemies.Add(target))
                {
                    continue;
                }

                Vector3 targetPosition = hit.bounds.center;
                Vector3 viewportPosition = gameplayCamera.WorldToViewportPoint(targetPosition);
                if (viewportPosition.z < 0f || viewportPosition.x < 0f || viewportPosition.x > 1f ||
                    viewportPosition.y < 0f || viewportPosition.y > 1f)
                {
                    autoTargetEnemies.Remove(target);
                    continue;
                }

                targetPosition.z = targetZ;
                autoTargetPositions.Add(targetPosition);
            }

            for (int i = autoTargetPositions.Count - 1; i > 0; i--)
            {
                int swapIndex = Random.Range(0, i + 1);
                Vector3 temporary = autoTargetPositions[i];
                autoTargetPositions[i] = autoTargetPositions[swapIndex];
                autoTargetPositions[swapIndex] = temporary;
            }
        }

        private void EnsureAutoTargetBuffer()
        {
            int size = Mathf.Max(1, autoTargetBufferSize);
            if (autoTargetBuffer == null || autoTargetBuffer.Length != size)
            {
                autoTargetBuffer = new Collider2D[size];
            }
        }

        private Vector3 GetRandomCameraTarget(Vector3 fallbackTarget)
        {
            if (gameplayCamera == null || !gameplayCamera.isActiveAndEnabled)
            {
                gameplayCamera = Camera.main;
            }

            if (gameplayCamera == null)
            {
                return fallbackTarget;
            }

            float depth = Mathf.Abs(fallbackTarget.z - gameplayCamera.transform.position.z);
            Vector3 randomTarget = gameplayCamera.ViewportToWorldPoint(
                new Vector3(Random.value, Random.value, depth));
            randomTarget.z = fallbackTarget.z;
            return randomTarget;
        }

        private void Update()
        {
            if (!isCastFlashPlaying)
            {
                return;
            }

            castFlashAnimationTime += Time.deltaTime;
            int frameIndex = Mathf.FloorToInt(castFlashAnimationTime * castFlashAnimationFps);
            if (castFlashFrames == null || frameIndex >= castFlashFrames.Length)
            {
                HideCastFlash();
                return;
            }

            if (castFlashRenderer != null)
            {
                castFlashRenderer.sprite = castFlashFrames[frameIndex];
            }
        }

        private void LateUpdate()
        {
            ApplyFacingPose();
        }

        public override void AttackEnd()
        {
        }

        private void PlayCastFlash()
        {
            if (castFlashRenderer == null || castFlashFrames == null || castFlashFrames.Length == 0)
            {
                return;
            }

            castFlashAnimationTime = 0f;
            isCastFlashPlaying = true;
            castFlashRenderer.sprite = castFlashFrames[0];
            castFlashRenderer.enabled = true;
        }

        private void HideCastFlash()
        {
            castFlashAnimationTime = 0f;
            isCastFlashPlaying = false;
            if (castFlashRenderer != null)
            {
                castFlashRenderer.enabled = false;
            }
        }

        private void CacheRightFacingPose(WeaponData data)
        {
            rightFacingLocalPosition = data != null
                ? data.visualPositionOffset
                : transform.localPosition;
            rightFacingLocalScale = transform.localScale;
            rightFacingLocalScale.x = Mathf.Abs(rightFacingLocalScale.x);
            rightFacingRotation = data != null
                ? data.spriteRotationOffset
                : Mathf.DeltaAngle(0f, transform.localEulerAngles.z);
            facingPoseCached = true;
        }

        private void ApplyFacingPose()
        {
            if (!facingPoseCached)
            {
                CacheRightFacingPose(weaponData);
            }

            if (playerController == null)
            {
                playerController = GetComponentInParent<PlayerController>();
            }

            bool isFacingRight = playerController == null || playerController.IsFacingRight;

            Vector3 localPosition = rightFacingLocalPosition;
            localPosition.x = isFacingRight
                ? rightFacingLocalPosition.x
                : -rightFacingLocalPosition.x;
            transform.localPosition = localPosition;

            Vector3 localScale = rightFacingLocalScale;
            localScale.x = Mathf.Abs(rightFacingLocalScale.x) * (isFacingRight ? 1f : -1f);
            transform.localScale = localScale;

            float rotation = isFacingRight ? rightFacingRotation : -rightFacingRotation;
            transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
        }

        private float GetCameraTopY(Vector3 targetPosition)
        {
            if (gameplayCamera == null || !gameplayCamera.isActiveAndEnabled)
            {
                gameplayCamera = Camera.main;
            }

            float minimumY = targetPosition.y + minimumDropHeight;
            if (gameplayCamera == null)
            {
                return minimumY;
            }

            float depth = Mathf.Abs(targetPosition.z - gameplayCamera.transform.position.z);
            float cameraTopY = gameplayCamera.ViewportToWorldPoint(new Vector3(0.5f, 1f, depth)).y;
            return Mathf.Max(minimumY, cameraTopY + cameraTopPadding);
        }

        private void OnDisable()
        {
            HideCastFlash();
        }

    }
}
