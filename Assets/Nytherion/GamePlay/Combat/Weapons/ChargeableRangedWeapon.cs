using UnityEngine;
using Nytherion.Core.Managers;

namespace Nytherion.GamePlay.Combat.Weapons
{
    public interface IChargeableWeapon
    {
        bool IsCharging { get; }
        float ChargePercent { get; }
    }

    public abstract class ChargeableRangedWeapon : RangedWeapon, IChargeableWeapon
    {
        [Header("Charge Settings")]
        public float maxChargeTime = 1.5f;

        protected float currentChargeTime = 0f;
        protected bool isCharging = false;
        private GameObject activeChargeEffectInstance = null;

        public bool IsCharging => isCharging;
        public float ChargePercent => GetAdjustedMaxChargeTime() > 0f ? (currentChargeTime / GetAdjustedMaxChargeTime()) : 0f;

        protected float GetAdjustedMaxChargeTime()
        {
            if (playerManager == null || playerManager.currentPlayerData == null)
            {
                return maxChargeTime;
            }

            // 차징 시간 감소율 적용 (1.0 = 100% 감소 = 0초)
            float reduction = playerManager.currentPlayerData.chargeTimeReduction;
            return Mathf.Max(0f, maxChargeTime * (1f - reduction));
        }

        private void Update()
        {
            if (isCharging)
            {
                float adjustedMaxChargeTime = GetAdjustedMaxChargeTime();

                if (adjustedMaxChargeTime <= 0f)
                {
                    // 차징 시간이 0이면 즉시 최대 차징으로 발사
                    FireImmediate();
                    return;
                }

                currentChargeTime += Time.deltaTime;
                currentChargeTime = Mathf.Clamp(currentChargeTime, 0f, adjustedMaxChargeTime);

                float chargePercent = currentChargeTime / adjustedMaxChargeTime;

                OnCharging(chargePercent);
            }
        }

        public override void Attack(Vector2 direction, Vector3 targetPosition = default)
        {
            if (!CanAttack()) return;

            float adjustedMaxChargeTime = GetAdjustedMaxChargeTime();
            if (adjustedMaxChargeTime <= 0f)
            {
                FireImmediate();
                return;
            }

            isCharging = true;
            currentChargeTime = 0f;
            SpawnChargeEffect();
        }

        private void FireImmediate()
        {
            isCharging = false;
            currentChargeTime = 0f;
            ClearChargeEffect();

            float rotationOffset = weaponData != null ? weaponData.spriteRotationOffset : 0f;
            float scaleSign = Mathf.Sign(transform.lossyScale.y);
            Vector2 releaseDirection = Quaternion.Euler(0f, 0f, -rotationOffset * scaleSign) * transform.right;
            FireChargedAttack(releaseDirection, 1.0f); // 1.0 = 풀차징

            lastAttackTime = Time.time;
            PlayFireAnimation();
        }

        public override void AttackEnd()
        {
            if (!isCharging) return;

            isCharging = false;
            ClearChargeEffect();

            float adjustedMaxChargeTime = GetAdjustedMaxChargeTime();
            float finalChargePercent = adjustedMaxChargeTime > 0f ? (currentChargeTime / adjustedMaxChargeTime) : 1.0f;

            float rotationOffset = weaponData != null ? weaponData.spriteRotationOffset : 0f;
            float scaleSign = Mathf.Sign(transform.lossyScale.y);
            Vector2 releaseDirection = Quaternion.Euler(0f, 0f, -rotationOffset * scaleSign) * transform.right;

            FireChargedAttack(releaseDirection, finalChargePercent);

            lastAttackTime = Time.time;
            currentChargeTime = 0f;
            PlayFireAnimation();
        }

        protected virtual void OnCharging(float chargePercent) { }

        protected abstract void FireChargedAttack(Vector2 direction, float chargePercent);

        private void SpawnChargeEffect()
        {
            if (firePoint != null && weaponData != null && weaponData.chargeEffectPrefab != null && activeChargeEffectInstance == null)
            {
                if (ObjectPoolManager.Instance != null)
                {
                    activeChargeEffectInstance = ObjectPoolManager.Instance.SpawnFromPool(weaponData.chargeEffectPrefab, firePoint.position, firePoint.rotation);
                }
                else
                {
                    activeChargeEffectInstance = Instantiate(weaponData.chargeEffectPrefab, firePoint.position, firePoint.rotation);
                }

                if (activeChargeEffectInstance != null)
                {
                    activeChargeEffectInstance.transform.SetParent(firePoint);

                    AutoReturnToPool autoReturn;
                    if (activeChargeEffectInstance.TryGetComponent<AutoReturnToPool>(out autoReturn))
                    {
                        autoReturn.enabled = false;
                    }

                    // 파티클 시스템들의 Simulation Space를 강제로 Local로 설정하여 잔상/흘러내림 제거
                    ParticleSystem[] particleSystems = activeChargeEffectInstance.GetComponentsInChildren<ParticleSystem>();
                    for (int i = 0; i < particleSystems.Length; i++)
                    {
                        ParticleSystem ps = particleSystems[i];
                        ParticleSystem.MainModule mainModule = ps.main;
                        mainModule.simulationSpace = ParticleSystemSimulationSpace.Local;
                    }
                }
            }
        }

        private void ClearChargeEffect()
        {
            if (activeChargeEffectInstance != null)
            {
                AutoReturnToPool autoReturn;
                if (activeChargeEffectInstance.TryGetComponent<AutoReturnToPool>(out autoReturn))
                {
                    autoReturn.enabled = true;
                }

                if (ObjectPoolManager.Instance != null && weaponData != null && weaponData.chargeEffectPrefab != null)
                {
                    ObjectPoolManager.Instance.ReturnToPool(weaponData.chargeEffectPrefab.name, activeChargeEffectInstance);
                }
                else
                {
                    Destroy(activeChargeEffectInstance);
                }
                activeChargeEffectInstance = null;
            }
        }

        private void OnDisable()
        {
            isCharging = false;
            currentChargeTime = 0f;
            ClearChargeEffect();
        }
    }
}