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
        public float chargeThresholdTime = 0.15f;

        protected float currentChargeTime = 0f;
        protected bool isCharging = false;
        protected bool isPressing = false;
        protected float pressTime = 0f;
        
        private GameObject activeChargeEffectInstance = null;
        private GameObject sparkChargeObject = null;
        private Vector3 originalSparkChargeScale = Vector3.one;

        public bool IsCharging => isCharging;
        public float ChargePercent => GetAdjustedMaxChargeTime() > 0f ? (currentChargeTime / GetAdjustedMaxChargeTime()) : 0f;

        public override void Initialize(Nytherion.Data.ScriptableObjects.Weapons.WeaponData data)
        {
            base.Initialize(data);
            if (data != null)
            {
                maxChargeTime = data.maxChargeTime;
                chargeThresholdTime = data.chargeThresholdTime;
            }
            FindSparkChargeObject();
        }

        private void FindSparkChargeObject()
        {
            if (firePoint != null)
            {
                Transform sparkChargeTr = firePoint.Find("SparkCharge");
                if (sparkChargeTr != null)
                {
                    sparkChargeObject = sparkChargeTr.gameObject;
                    originalSparkChargeScale = sparkChargeTr.localScale;
                    sparkChargeObject.SetActive(false);
                }
            }
        }

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
            if (isPressing)
            {
                pressTime += Time.deltaTime;

                if (!isCharging)
                {
                    if (pressTime >= chargeThresholdTime)
                    {
                        isCharging = true;
                        currentChargeTime = 0f;
                        SpawnChargeEffect();
                        if (sparkChargeObject != null)
                        {
                            sparkChargeObject.SetActive(true);
                            Animator anim = sparkChargeObject.GetComponent<Animator>();
                            if (anim != null)
                            {
                                anim.enabled = true;
                                anim.Rebind();
                                anim.Play("Idle", -1, 0f);
                                anim.Update(0f);
                            }
                        }
                    }
                }
                else
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

                    if (sparkChargeObject != null)
                    {
                        float scaleMultiplier = Mathf.Lerp(0.2f, 1.2f, chargePercent);
                        sparkChargeObject.transform.localScale = originalSparkChargeScale * scaleMultiplier;
                    }

                    OnCharging(chargePercent);
                }
            }
        }

        protected bool IsChargingEnabled()
        {
            if (weaponData == null) return true;

            if (!string.IsNullOrEmpty(weaponData.requiredRelicId))
            {
                if (playerManager != null && playerManager.playerRelicManager != null)
                {
                    return playerManager.playerRelicManager.IsRelicActive(weaponData.requiredRelicId);
                }
                return false;
            }

            return true;
        }

        public override void Attack(Vector2 direction, Vector3 targetPosition = default)
        {
            if (!CanAttack()) return;

            if (!IsChargingEnabled())
            {
                FireImmediateNormal();
                return;
            }

            float adjustedMaxChargeTime = GetAdjustedMaxChargeTime();
            if (adjustedMaxChargeTime <= 0f)
            {
                FireImmediate();
                return;
            }

            isPressing = true;
            pressTime = 0f;
            isCharging = false;
            currentChargeTime = 0f;
        }

        private void FireImmediateNormal()
        {
            isPressing = false;
            isCharging = false;
            currentChargeTime = 0f;
            ClearChargeEffect();
            if (sparkChargeObject != null)
            {
                sparkChargeObject.transform.localScale = originalSparkChargeScale;
                sparkChargeObject.SetActive(false);
            }

            float rotationOffset = weaponData != null ? weaponData.spriteRotationOffset : 0f;
            float scaleSign = Mathf.Sign(transform.lossyScale.y);
            Vector2 releaseDirection = Quaternion.Euler(0f, 0f, -rotationOffset * scaleSign) * transform.right;
            FireChargedAttack(releaseDirection, 0f);

            lastAttackTime = Time.time;
            PlayFireAnimation();
        }

        private void FireImmediate()
        {
            isPressing = false;
            isCharging = false;
            currentChargeTime = 0f;
            ClearChargeEffect();
            if (sparkChargeObject != null)
            {
                sparkChargeObject.transform.localScale = originalSparkChargeScale;
                sparkChargeObject.SetActive(false);
            }

            float rotationOffset = weaponData != null ? weaponData.spriteRotationOffset : 0f;
            float scaleSign = Mathf.Sign(transform.lossyScale.y);
            Vector2 releaseDirection = Quaternion.Euler(0f, 0f, -rotationOffset * scaleSign) * transform.right;
            FireChargedAttack(releaseDirection, 1.0f); // 1.0 = 풀차징

            lastAttackTime = Time.time;
            PlayFireAnimation();
        }

        public override void AttackEnd()
        {
            if (!isPressing) return;

            float rotationOffset = weaponData != null ? weaponData.spriteRotationOffset : 0f;
            float scaleSign = Mathf.Sign(transform.lossyScale.y);
            Vector2 releaseDirection = Quaternion.Euler(0f, 0f, -rotationOffset * scaleSign) * transform.right;

            if (!isCharging)
            {
                isPressing = false;
                FireChargedAttack(releaseDirection, 0f);
                lastAttackTime = Time.time;
                PlayFireAnimation();
            }
            else
            {
                isPressing = false;
                isCharging = false;
                ClearChargeEffect();
                if (sparkChargeObject != null)
                {
                    sparkChargeObject.transform.localScale = originalSparkChargeScale;
                    sparkChargeObject.SetActive(false);
                }

                float adjustedMaxChargeTime = GetAdjustedMaxChargeTime();
                float finalChargePercent = adjustedMaxChargeTime > 0f ? (currentChargeTime / adjustedMaxChargeTime) : 1.0f;

                FireChargedAttack(releaseDirection, finalChargePercent);

                lastAttackTime = Time.time;
                currentChargeTime = 0f;
                PlayFireAnimation();
            }
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
            isPressing = false;
            isCharging = false;
            currentChargeTime = 0f;
            ClearChargeEffect();
            if (sparkChargeObject != null)
            {
                sparkChargeObject.transform.localScale = originalSparkChargeScale;
                sparkChargeObject.SetActive(false);
            }
        }
    }
}
