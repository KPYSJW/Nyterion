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
        private Vector2 aimDirection = Vector2.right;
        private Vector3 baseLocalScale = Vector3.one;
        private bool facingRight = true;

        public bool IsFiring => activeBeam != null && activeBeam.IsFiring;
        public override bool OverrideRotation => true;
        public override bool AllowAutoFire => false;
        internal Transform CasterTransform => playerManager != null ? playerManager.transform : transform;
        internal Vector2 CurrentMuzzleDirection => transform.right;

        internal Vector2 CurrentFireDirection => aimDirection;

        public override void Initialize(WeaponData weaponData)
        {
            StopActiveBeam(true);
            base.Initialize(weaponData);
            data = weaponData as VoidRayWeaponData;
            baseLocalScale = new Vector3(Mathf.Abs(transform.localScale.x),
                Mathf.Abs(transform.localScale.y), Mathf.Abs(transform.localScale.z));
            aimDirection = Vector2.right;
            facingRight = true;
            damageScheduleInitialized = false;
            missingReferenceLogged = false;
        }

        internal void UpdateAimAndPose(Vector3 mouseWorldPosition, Vector3 playerCenter)
        {
            Vector2 centerDirection = mouseWorldPosition - playerCenter;
            if (centerDirection.sqrMagnitude >= 0.0064f)
                centerDirection.Normalize();
            else
                centerDirection = aimDirection;
            if (Mathf.Abs(centerDirection.x) > 0.01f)
                facingRight = centerDirection.x > 0f;

            VoidRayWeaponData poseData = data != null ? data : weaponData as VoidRayWeaponData;
            Vector3 baseOffset = poseData != null
                ? poseData.visualPositionOffset
                : transform.localPosition;
            float orbitAngle = Mathf.Atan2(centerDirection.y, centerDirection.x) * Mathf.Rad2Deg;
            Vector2 rotatedOffset = Quaternion.Euler(0f, 0f, orbitAngle) *
                new Vector2(baseOffset.x, baseOffset.y);
            transform.localPosition = new Vector3(rotatedOffset.x, rotatedOffset.y, baseOffset.z);

            Vector2 weaponDirection = mouseWorldPosition - transform.position;
            if (weaponDirection.sqrMagnitude >= 0.0064f)
                weaponDirection.Normalize();
            else
                weaponDirection = centerDirection;
            float weaponAngle = Mathf.Atan2(weaponDirection.y, weaponDirection.x) * Mathf.Rad2Deg;
            transform.localRotation = Quaternion.Euler(0f, 0f, weaponAngle);
            transform.localScale = new Vector3(
                baseLocalScale.x,
                facingRight ? baseLocalScale.y : -baseLocalScale.y,
                baseLocalScale.z);

            if (firePoint == null)
            {
                LogMissingReferenceOnce("마우스 조준 자세를 계산할 FirePoint 참조가 없습니다.");
                return;
            }

            Vector2 fromMuzzle = mouseWorldPosition - firePoint.position;
            if (fromMuzzle.sqrMagnitude >= 0.0064f)
                aimDirection = fromMuzzle.normalized;
        }

        public override bool CanAttack()
        {
            return isActiveAndEnabled && activeBeam == null && ValidateReferences();
        }

        public override void Attack(Vector2 direction, Vector3 targetPosition = default)
        {
            if (!CanAttack()) return;

            Vector2 targetDirection = targetPosition - firePoint.position;
            if (targetDirection.sqrMagnitude >= 0.0064f)
                aimDirection = targetDirection.normalized;

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

