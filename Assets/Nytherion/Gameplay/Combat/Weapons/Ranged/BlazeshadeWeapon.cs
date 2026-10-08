using System;
using System.Collections;
using System.Collections.Generic;
using Nytherion.Core.Interfaces;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Combat.Weapons;
using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    /// <summary>
    /// 공격 입력 없이 장착 중인 플레이어 주위에 불꽃 영역을 유지하고,
    /// 범위 안의 적에게 WeaponData.cooldown 주기로 피해를 줍니다.
    /// </summary>
    public sealed class BlazeshadeWeapon : WeaponBase, IChargeableWeapon
    {
        [Header("Blazeshade Aura")]
        [SerializeField] private GameObject auraVisualPrefab;
        [SerializeField] private LayerMask enemyLayers;
        [SerializeField, Min(1)] private int overlapBufferSize = 64;
        [SerializeField] private int auraSortingOrderOffset = -2;
        [Header("TargetMaker")]
        [SerializeField] private GameObject summonVisualPrefab;
        [SerializeField] private GameObject targetAttackVisualPrefab;
        [SerializeField] private AnimationClip summonClip;
        [SerializeField] private AnimationClip targetAttackClip;

        private readonly HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();

        private Collider2D[] overlapBuffer;
        private GameObject auraVisualInstance;
        private SpriteRenderer auraRenderer;
        private PlayerController playerController;
        private Vector3 rightFacingLocalPosition;
        private Vector3 rightFacingLocalScale;
        private float rightFacingRotation;
        private float nextDamageTime;
        private bool initialized;
        private bool facingPoseCached;
        private Coroutine targetAttack;
        private GameObject targetVisual;
        private bool targetMode;

        public override bool OverrideRotation => true;
        public override bool AllowAutoFire => HasTargetMaker;
        public override bool AllowHeldAttackRetry => HasTargetMaker;

        // 수동 공격과 범용 차징을 모두 차단하기 위한 비활성 IChargeableWeapon 구현입니다.
        public bool IsCharging => false;
        public float ChargePercent => 0f;

        protected override void Awake()
        {
            base.Awake();
            playerController = GetComponentInParent<PlayerController>();
            EnsureOverlapBuffer();
        }

        private void Start()
        {
            if (!initialized)
            {
                ConfigureAura(weaponData);
            }

            CacheRightFacingPose(weaponData);
            ApplyFacingPose();
        }

        public override void Initialize(WeaponData data)
        {
            base.Initialize(data);
            initialized = true;
            CacheRightFacingPose(data);
            ConfigureAura(data);
        }

        private void Update()
        {
            if (weaponData == null)
            {
                return;
            }

            bool targeting = HasTargetMaker;
            if (targeting != targetMode)
            {
                targetMode = targeting;
                if (!targeting) CancelTargetAttack();
                nextDamageTime = Time.time + Mathf.Max(0.05f, weaponData.cooldown);
            }
            if (auraVisualInstance != null) auraVisualInstance.SetActive(!targeting);
            if (targeting) return;

            SyncAuraTransform();
            ResizeAuraVisual(weaponData.range * EffectSizeMultiplier);

            if (Time.time < nextDamageTime)
            {
                return;
            }

            DealAuraDamage();
            nextDamageTime = Time.time + Mathf.Max(0.05f, weaponData.cooldown);
        }

        private void LateUpdate()
        {
            ApplyFacingPose();
        }

        public override bool CanAttack()
        {
            // 지속 오라의 피해 주기는 공격속도가 아닙니다. 지정 공격은 연출이 끝나면 다시 사용할 수 있습니다.
            return HasTargetMaker && targetAttack == null && weaponData != null &&
                summonVisualPrefab != null && targetAttackVisualPrefab != null &&
                summonClip != null && targetAttackClip != null;
        }

        public override void Attack(Vector2 direction, Vector3 targetPosition = default)
        {
            if (!CanAttack()) return;
            targetPosition.z = 0f;
            if (auraVisualInstance != null) auraVisualInstance.SetActive(false);
            WeaponVFXHelper.PlayFireEffect(weaponData.fireEffectPrefab, transform.position, transform.rotation, transform);
            targetAttack = StartCoroutine(AttackAtTarget(targetPosition));
        }

        private IEnumerator AttackAtTarget(Vector3 center)
        {
            float radius = Mathf.Max(0.1f, weaponData.range) * EffectSizeMultiplier;
            float damage = Mathf.Max(0f, weaponData.damage * EffectiveDamageMultiplier);
            targetVisual = CreateTargetVisual(summonVisualPrefab, center, radius * 0.7f);
            yield return new WaitForSeconds(summonClip.length);
            Destroy(targetVisual);
            targetVisual = CreateTargetVisual(targetAttackVisualPrefab, center, radius);
            damagedTargets.Clear();
            float endTime = Time.time + targetAttackClip.length;
            while (Time.time < endTime)
            {
                // 공격 애니메이션 도중 진입한 적도 맞지만 같은 적의 여러 콜라이더와 재진입은 중복 타격하지 않습니다.
                DealDamage(center, radius, damage);
                yield return null;
            }
            Destroy(targetVisual);
            targetVisual = null;
            targetAttack = null;
        }

        private GameObject CreateTargetVisual(GameObject prefab, Vector3 center, float radius)
        {
            GameObject visual = Instantiate(prefab, center, Quaternion.identity);
            SpriteRenderer renderer = visual.GetComponentInChildren<SpriteRenderer>();
            if (renderer != null && renderer.sprite != null)
            {
                float diameter = Mathf.Max(renderer.sprite.bounds.size.x, renderer.sprite.bounds.size.y);
                visual.transform.localScale = Vector3.one * (radius * 2f / Mathf.Max(0.01f, diameter));
                if (playerManager != null)
                {
                    SpriteRenderer source = playerManager.GetComponentInChildren<SpriteRenderer>();
                    if (source != null)
                    {
                        renderer.sortingLayerID = source.sortingLayerID;
                        renderer.sortingOrder = source.sortingOrder + 2;
                    }
                }
            }
            return visual;
        }

        private void CancelTargetAttack()
        {
            if (targetAttack != null) StopCoroutine(targetAttack);
            targetAttack = null;
            if (targetVisual != null) Destroy(targetVisual);
            targetVisual = null;
        }

        private void OnDisable()
        {
            CancelTargetAttack();
            if (auraVisualInstance != null) auraVisualInstance.SetActive(false);
        }

        public override void AttackEnd()
        {
        }

        private void CacheRightFacingPose(WeaponData data)
        {
            rightFacingLocalPosition = transform.localPosition;
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

            bool isFacingRight = playerController == null || playerController.IsFacingRight;

            Vector3 localPosition = rightFacingLocalPosition;
            localPosition.x = isFacingRight ? rightFacingLocalPosition.x : -rightFacingLocalPosition.x;
            transform.localPosition = localPosition;

            Vector3 localScale = rightFacingLocalScale;
            localScale.x = Mathf.Abs(rightFacingLocalScale.x) * (isFacingRight ? 1f : -1f);
            transform.localScale = localScale;

            float rotation = isFacingRight ? rightFacingRotation : -rightFacingRotation;
            transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
        }

        private void ConfigureAura(WeaponData data)
        {
            if (data == null)
            {
                return;
            }

            EnsureOverlapBuffer();
            EnsureAuraVisual();
            ResizeAuraVisual(data.range * EffectSizeMultiplier);
            SyncAuraTransform();
            nextDamageTime = Time.time + Mathf.Max(0.05f, data.cooldown);
        }

        private void EnsureOverlapBuffer()
        {
            int size = Mathf.Max(1, overlapBufferSize);
            if (overlapBuffer == null || overlapBuffer.Length != size)
            {
                overlapBuffer = new Collider2D[size];
            }
        }

        private void EnsureAuraVisual()
        {
            if (auraVisualInstance != null || auraVisualPrefab == null)
            {
                return;
            }

            Transform owner = playerManager != null ? playerManager.transform : transform.parent;
            auraVisualInstance = Instantiate(auraVisualPrefab, owner);
            auraVisualInstance.name = auraVisualPrefab.name;
            auraVisualInstance.transform.localPosition = Vector3.zero;
            auraVisualInstance.transform.localRotation = Quaternion.identity;
            auraRenderer = auraVisualInstance.GetComponentInChildren<SpriteRenderer>();
            SyncAuraSorting();
        }

        private void ResizeAuraVisual(float radius)
        {
            if (auraVisualInstance == null || auraRenderer == null || auraRenderer.sprite == null)
            {
                return;
            }

            Vector2 spriteSize = auraRenderer.sprite.bounds.size;
            float nativeDiameter = Mathf.Max(spriteSize.x, spriteSize.y);
            float targetDiameter = Mathf.Max(0.1f, radius) * 2f;
            float scale = nativeDiameter > Mathf.Epsilon ? targetDiameter / nativeDiameter : 1f;
            auraVisualInstance.transform.localScale = Vector3.one * scale;
        }

        private void SyncAuraTransform()
        {
            if (auraVisualInstance == null)
            {
                return;
            }

            Transform owner = playerManager != null ? playerManager.transform : transform.parent;
            if (owner != null && auraVisualInstance.transform.parent != owner)
            {
                auraVisualInstance.transform.SetParent(owner, false);
            }

            auraVisualInstance.transform.localPosition = Vector3.zero;
            auraVisualInstance.transform.localRotation = Quaternion.identity;
            SyncAuraSorting();
        }

        private void SyncAuraSorting()
        {
            if (auraRenderer == null || playerManager == null)
            {
                return;
            }

            SpriteRenderer playerRenderer = playerManager.GetComponentInChildren<SpriteRenderer>();
            if (playerRenderer == null)
            {
                return;
            }

            auraRenderer.sortingLayerID = playerRenderer.sortingLayerID;
            auraRenderer.sortingOrder = playerRenderer.sortingOrder + auraSortingOrderOffset;
        }

        private void DealAuraDamage()
        {
            EnsureOverlapBuffer();
            damagedTargets.Clear();

            Vector2 center = playerManager != null ? playerManager.transform.position : transform.position;
            float radius = Mathf.Max(0.1f, weaponData.range) * EffectSizeMultiplier;
            float damage = Mathf.Max(0f, weaponData.damage * EffectiveDamageMultiplier);
            DealDamage(center, radius, damage);
        }

        private void DealDamage(Vector2 center, float radius, float damage)
        {
            int hitCount;
            do
            {
                hitCount = Physics2D.OverlapCircleNonAlloc(center, radius, overlapBuffer, enemyLayers);
                if (hitCount < overlapBuffer.Length) break;
                Array.Resize(ref overlapBuffer, overlapBuffer.Length * 2);
            } while (true);

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = overlapBuffer[i];
                if (hit == null)
                {
                    continue;
                }

                IDamageable target = hit.GetComponentInParent<IDamageable>();
                if (target == null || !damagedTargets.Add(target))
                {
                    continue;
                }

                if (target is MonoBehaviour targetBehaviour && !targetBehaviour.gameObject.activeInHierarchy)
                {
                    continue;
                }

                target.TakeDamage(damage);
            }
            Array.Clear(overlapBuffer, 0, hitCount);
        }

        private void OnDestroy()
        {
            CancelTargetAttack();
            if (auraVisualInstance != null)
            {
                Destroy(auraVisualInstance);
            }
        }

        private void OnDrawGizmosSelected()
        {
            float radius = (weaponData != null ? Mathf.Max(0.1f, weaponData.range) : 2.75f) * EffectSizeMultiplier;
            Vector3 center = playerManager != null ? playerManager.transform.position : transform.position;
            Gizmos.color = new Color(1f, 0.25f, 0.05f, 0.45f);
            Gizmos.DrawWireSphere(center, radius);
        }
    }
}
