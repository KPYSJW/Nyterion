using System;
using System.Collections.Generic;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Characters.Player;
using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    /// <summary>플레이어 중심의 원형 공격을 펼치고 등록된 이펙트를 순서대로 사용합니다.</summary>
    public sealed class GuardianStaffWeapon : WeaponBase
    {
        [Header("발사 연출")]
        public Transform firePoint;

        [SerializeField] private GuardianStaffAttackEffect[] attackEffects;
        [SerializeField] private LayerMask enemyLayers;

        private readonly HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();
        private Collider2D[] overlapBuffer = new Collider2D[64];
        private PlayerController playerController;
        private Vector3 rightFacingLocalPosition;
        private Vector3 rightFacingLocalScale;
        private float rightFacingRotation;
        private bool facingPoseCached;
        private int nextEffectIndex;

        public override bool OverrideRotation => true;

        protected override void Awake()
        {
            base.Awake();
            playerController = GetComponentInParent<PlayerController>();
        }

        private void Start()
        {
            if (!facingPoseCached) CacheRightFacingPose(weaponData);
            ApplyFacingPose();
        }

        public override void Initialize(WeaponData data)
        {
            base.Initialize(data);
            nextEffectIndex = 0;
            CacheRightFacingPose(data);
        }

        private void LateUpdate() => ApplyFacingPose();

        public override bool CanAttack()
        {
            return weaponData != null && attackEffects != null && attackEffects.Length > 0 &&
                attackEffects[nextEffectIndex] != null && base.CanAttack();
        }

        public override void Attack(Vector2 direction, Vector3 targetPosition = default)
        {
            if (!CanAttack()) return;
            Transform owner = playerManager != null ? playerManager.transform :
                playerController != null ? playerController.transform : transform.parent;
            Vector3 center = HasTargetMaker ? targetPosition : owner != null ? owner.position : transform.position;
            center.z = 0f;
            GuardianStaffAttackEffect prefab = attackEffects[nextEffectIndex];
            ObjectPoolManager pool = ObjectPoolManager.Instance;
            GameObject visual = pool != null
                ? pool.SpawnFromPool(prefab.gameObject, center, Quaternion.identity)
                : Instantiate(prefab.gameObject, center, Quaternion.identity);
            if (visual == null) return;

            lastAttackTime = Time.time;
            if (firePoint != null && weaponData.fireEffectPrefab != null)
                WeaponVFXHelper.PlayFireEffect(weaponData.fireEffectPrefab, firePoint.position, firePoint.rotation, firePoint);
            nextEffectIndex = (nextEffectIndex + 1) % attackEffects.Length;
            float radius = Mathf.Max(0.01f, weaponData.range) * EffectSizeMultiplier * prefab.RadiusMultiplier;
            SpriteRenderer ownerRenderer = owner != null ? owner.GetComponentInChildren<SpriteRenderer>() : null;
            visual.GetComponent<GuardianStaffAttackEffect>().Play(HasTargetMaker ? null : owner, radius, pool, ownerRenderer, GetComponent<SpriteRenderer>());
            DealDamage(center, radius);
        }

        private void DealDamage(Vector2 center, float radius)
        {
            // 버퍼가 가득 차면 확장해 다시 조회하므로 밀집한 적도 빠짐없이 타격합니다.
            int hitCount;
            do
            {
                hitCount = Physics2D.OverlapCircleNonAlloc(center, radius, overlapBuffer, enemyLayers);
                if (hitCount < overlapBuffer.Length) break;
                Array.Resize(ref overlapBuffer, overlapBuffer.Length * 2);
            } while (true);

            damagedTargets.Clear();
            float damage = Mathf.Max(0f, weaponData.damage * EffectiveDamageMultiplier);
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = overlapBuffer[i];
                if (hit == null) continue;
                IDamageable target = hit.GetComponentInParent<IDamageable>();
                if (target == null || !damagedTargets.Add(target)) continue;
                if (target is MonoBehaviour behaviour && !behaviour.gameObject.activeInHierarchy) continue;
                Vector3 hitPosition = hit.bounds.center;
                target.TakeDamage(damage);
                ApplyStatusEffects(target);
                WeaponVFXHelper.PlayHitEffect(weaponData.hitEffectPrefab, hitPosition);
            }
            Array.Clear(overlapBuffer, 0, hitCount);
        }

        public override void AttackEnd() { }

        private void CacheRightFacingPose(WeaponData data)
        {
            rightFacingLocalPosition = transform.localPosition;
            rightFacingLocalScale = transform.localScale;
            rightFacingLocalScale.x = Mathf.Abs(rightFacingLocalScale.x);
            rightFacingRotation = data != null ? data.spriteRotationOffset : transform.localEulerAngles.z;
            facingPoseCached = true;
        }

        private void ApplyFacingPose()
        {
            if (!facingPoseCached) CacheRightFacingPose(weaponData);
            bool right = playerController == null || playerController.IsFacingRight;
            Vector3 position = rightFacingLocalPosition;
            position.x *= right ? 1f : -1f;
            transform.localPosition = position;
            Vector3 scale = rightFacingLocalScale;
            scale.x *= right ? 1f : -1f;
            transform.localScale = scale;
            transform.localRotation = Quaternion.Euler(0f, 0f, right ? rightFacingRotation : -rightFacingRotation);
        }
    }
}
