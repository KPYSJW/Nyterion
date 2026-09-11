using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Weapons;
using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    /// <summary>발사 지점에서 일정 시간 유지되는 관통 레이저 무기입니다.</summary>
    public class LaserWeapon : RangedWeapon
    {
        [Header("레이저 틱 반동")]
        [SerializeField, Min(0f)] private float tickRecoilDistance = 0.035f;
        [SerializeField, Min(0.01f)] private float recoilReturnDuration = 0.18f;

        private WeaponLaserBeam activeBeam;
        private Vector3 recoilRestLocalPosition;
        private Vector3 recoilLocalDirection = Vector3.left;
        private Vector3 lastAppliedRecoilLocalPosition;
        private float currentRecoilDistance;
        private float recoilReturnStartDistance;
        private float recoilReturnElapsed;
        private bool hasRecoilRestPose;
        private bool isReturningFromRecoil;

        public override bool AllowAutoFire => false;
        public bool IsFiring => activeBeam != null && activeBeam.IsFiring;
        internal Transform CasterTransform => playerManager != null ? playerManager.transform : transform;

        public override void Initialize(WeaponData data)
        {
            StopBeam();
            RestoreRecoilImmediately();
            base.Initialize(data);
        }

        public override bool CanAttack()
        {
            return isActiveAndEnabled && weaponData is LaserWeaponData &&
                   firePoint != null && !IsFiring && base.CanAttack();
        }

        public override void Attack(Vector2 direction, Vector3 targetPosition = default)
        {
            if (!CanAttack()) return;

            LaserWeaponData data = (LaserWeaponData)weaponData;
            if (data.projectilePrefab == null ||
                !data.projectilePrefab.TryGetComponent<WeaponLaserBeam>(out _))
            {
                Debug.LogError("[LaserWeapon] WeaponLaserBeam이 있는 레이저 프리팹을 연결하세요.", this);
                return;
            }

            // 기존 원거리 무기와 같은 프리팹 기반 풀을 사용합니다. 풀이 없는 수동 씬도 지원합니다.
            StopBeam();
            ObjectPoolManager pool = ObjectPoolManager.Instance;
            GameObject instance = pool != null
                ? pool.SpawnFromPool(data.projectilePrefab, firePoint.position, Quaternion.identity, 2)
                : Instantiate(data.projectilePrefab, firePoint.position, Quaternion.identity);
            if (instance == null) return;

            // PlayerCombat이 플레이어 중심을 기준으로 계산한 방향을 그대로 사용합니다.
            // 레이저의 시작 위치만 firePoint로 두어, 마우스가 플레이어와 총구 사이에 있어도
            // 총구에서 플레이어 쪽으로 역발사되지 않게 합니다.
            Vector2 aimDirection = direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : (Vector2)firePoint.right;

            activeBeam = instance.GetComponent<WeaponLaserBeam>();
            BeginTickRecoil();
            activeBeam.Initialize(this, firePoint, aimDirection, data,
                data.damage * EffectiveDamageMultiplier, pool);
            lastAttackTime = Time.time;
            PlayFireAnimation();

            if (data.fireEffectPrefab != null)
            {
                float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
                WeaponVFXHelper.PlayFireEffect(data.fireEffectPrefab, firePoint.position,
                    Quaternion.Euler(0f, 0f, angle), firePoint);
            }
        }

        public override void AttackEnd()
        {
            // 버튼을 놓아도 이미 시작된 발사는 설정된 시간까지 유지합니다.
        }

        internal void OnBeamDamageTick(WeaponLaserBeam beam, Vector2 fireDirection)
        {
            if (activeBeam != beam || !hasRecoilRestPose) return;

            PreserveExternalPositionChange();
            Vector3 worldBackDirection = fireDirection.sqrMagnitude > 0.0001f
                ? -(Vector3)fireDirection.normalized
                : -transform.right;
            recoilLocalDirection = transform.parent != null
                ? transform.parent.InverseTransformVector(worldBackDirection).normalized
                : worldBackDirection.normalized;
            currentRecoilDistance += Mathf.Max(0f, tickRecoilDistance);
            ApplyRecoilPosition();
        }

        internal void OnBeamFiringEnded(WeaponLaserBeam beam)
        {
            if (activeBeam == beam)
            {
                lastAttackTime = Time.time;
                BeginRecoilReturn();
            }
        }

        internal void OnBeamReleased(WeaponLaserBeam beam)
        {
            if (activeBeam == beam)
            {
                activeBeam = null;
            }
        }

        private void BeginTickRecoil()
        {
            // 이전 공격의 복귀가 끝나기 전에 재사용되더라도 기준 위치가 누적되어 밀리지 않게 합니다.
            RestoreRecoilImmediately();
            recoilRestLocalPosition = transform.localPosition;
            recoilLocalDirection = Vector3.left;
            lastAppliedRecoilLocalPosition = transform.localPosition;
            currentRecoilDistance = 0f;
            recoilReturnStartDistance = 0f;
            recoilReturnElapsed = 0f;
            hasRecoilRestPose = true;
            isReturningFromRecoil = false;
        }

        private void BeginRecoilReturn()
        {
            if (!hasRecoilRestPose) return;

            if (currentRecoilDistance <= Mathf.Epsilon)
            {
                RestoreRecoilImmediately();
                return;
            }

            recoilReturnStartDistance = currentRecoilDistance;
            recoilReturnElapsed = 0f;
            isReturningFromRecoil = true;
        }

        private void LateUpdate()
        {
            AdvanceRecoil(Time.deltaTime);
        }

        private void AdvanceRecoil(float deltaTime)
        {
            if (!hasRecoilRestPose || !isReturningFromRecoil || deltaTime <= 0f) return;

            PreserveExternalPositionChange();
            recoilReturnElapsed += deltaTime;
            float progress = recoilReturnDuration > 0f
                ? Mathf.Clamp01(recoilReturnElapsed / recoilReturnDuration)
                : 1f;
            float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
            currentRecoilDistance = Mathf.Lerp(recoilReturnStartDistance, 0f, easedProgress);
            ApplyRecoilPosition();

            if (progress >= 1f) RestoreRecoilImmediately();
        }

        private void ApplyRecoilPosition()
        {
            if (hasRecoilRestPose)
            {
                transform.localPosition = recoilRestLocalPosition +
                    recoilLocalDirection * currentRecoilDistance;
                lastAppliedRecoilLocalPosition = transform.localPosition;
            }
        }

        private void PreserveExternalPositionChange()
        {
            if (!hasRecoilRestPose) return;

            // 플레이어/장착 시스템이 공격 중 무기 위치를 옮긴 경우에도 새 위치를 기준으로 복귀합니다.
            recoilRestLocalPosition += transform.localPosition - lastAppliedRecoilLocalPosition;
        }

        private void RestoreRecoilImmediately()
        {
            if (hasRecoilRestPose)
            {
                PreserveExternalPositionChange();
                transform.localPosition = recoilRestLocalPosition;
                lastAppliedRecoilLocalPosition = recoilRestLocalPosition;
            }

            currentRecoilDistance = 0f;
            recoilReturnStartDistance = 0f;
            recoilReturnElapsed = 0f;
            hasRecoilRestPose = false;
            isReturningFromRecoil = false;
        }

        private void StopBeam()
        {
            if (activeBeam != null)
            {
                activeBeam.StopImmediately();
                activeBeam = null;
            }
        }

        private void OnDisable()
        {
            StopBeam();
            RestoreRecoilImmediately();
        }
    }
}
