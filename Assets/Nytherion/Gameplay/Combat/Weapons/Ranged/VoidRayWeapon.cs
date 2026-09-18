using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Weapons;
using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    /// <summary>공격 입력을 누르는 동안 하나의 지속 광선을 유지합니다.</summary>
    public class VoidRayWeapon : RangedWeapon
    {
        private VoidRayWeaponData data;
        private VoidRayBeam activeBeam;
        private double nextDamageTime;
        private bool damageScheduleInitialized;
        private bool missingReferenceLogged;

        public bool IsFiring => activeBeam != null && activeBeam.IsFiring;
        public override bool AllowAutoFire => false;
        internal Transform CasterTransform => playerManager != null ? playerManager.transform : transform;

        internal Vector2 CurrentFireDirection
        {
            get
            {
                float scaleSign = transform.lossyScale.y < 0f ? -1f : 1f;
                float rotationOffset = data != null ? data.spriteRotationOffset : 0f;
                Vector2 direction = Quaternion.Euler(0f, 0f, -rotationOffset * scaleSign) * transform.right;
                return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            }
        }

        public override void Initialize(WeaponData weaponData)
        {
            StopActiveBeam(true);
            base.Initialize(weaponData);
            data = weaponData as VoidRayWeaponData;
            damageScheduleInitialized = false;
            missingReferenceLogged = false;
        }

        public override bool CanAttack()
        {
            return isActiveAndEnabled && activeBeam == null && ValidateReferences();
        }

        public override void Attack(Vector2 direction, Vector3 targetPosition = default)
        {
            if (!CanAttack()) return;

            ObjectPoolManager pool = ObjectPoolManager.Instance;
            GameObject instance = pool != null
                ? pool.SpawnFromPool(data.projectilePrefab, firePoint.position, Quaternion.identity, 2)
                : Instantiate(data.projectilePrefab, firePoint.position, Quaternion.identity);
            if (instance == null)
            {
                LogMissingReferenceOnce("광선 프리팹을 생성하지 못했습니다.");
                return;
            }

            if (!instance.TryGetComponent(out activeBeam))
            {
                LogMissingReferenceOnce("광선 프리팹에 VoidRayBeam 컴포넌트가 없습니다.");
                if (pool != null) pool.ReturnToPool(data.projectilePrefab.name, instance);
                else Destroy(instance);
                return;
            }

            PrepareDamageSchedule(Time.timeAsDouble);
            activeBeam.Initialize(this, firePoint, data, pool);
            lastAttackTime = Time.time;
        }

        public override void AttackEnd()
        {
            StopActiveBeam(false);
        }

        private void PrepareDamageSchedule(double now)
        {
            if (!damageScheduleInitialized || nextDamageTime < now)
                nextDamageTime = now;
            damageScheduleInitialized = true;
        }

        internal bool TryConsumeDamageTick(double now)
        {
            if (data == null || !damageScheduleInitialized || now + 0.000001d < nextDamageTime)
                return false;

            nextDamageTime += data.DamageInterval;
            return true;
        }

        internal float GetDamagePerTick()
        {
            return data != null ? data.DamagePerTick * EffectiveDamageMultiplier : 0f;
        }

        internal void OnBeamReleased(VoidRayBeam beam)
        {
            if (activeBeam != beam) return;
            activeBeam = null;
            lastAttackTime = Time.time;
        }

        private void StopActiveBeam(bool immediate)
        {
            if (activeBeam == null) return;
            if (immediate) activeBeam.StopImmediately();
            else activeBeam.StopFiring();
            activeBeam = null;
            lastAttackTime = Time.time;
        }

        private bool ValidateReferences()
        {
            if (data != null && firePoint != null && data.projectilePrefab != null &&
                data.projectilePrefab.TryGetComponent<VoidRayBeam>(out _))
                return true;

            LogMissingReferenceOnce("VoidRayWeaponData, FirePoint 또는 VoidRayBeam 프리팹 참조를 확인해 주세요.");
            return false;
        }

        private void LogMissingReferenceOnce(string message)
        {
            if (missingReferenceLogged) return;
            missingReferenceLogged = true;
            Debug.LogError("[VoidRayWeapon] " + message, this);
        }

        private void OnDisable()
        {
            StopActiveBeam(true);
        }
    }
}

