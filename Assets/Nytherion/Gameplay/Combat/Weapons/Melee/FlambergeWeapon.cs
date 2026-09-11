using UnityEngine;
using System.Collections;
using Nytherion.Core.Managers;
using Nytherion.GamePlay.Combat.Weapons;

namespace Nytherion.GamePlay.Combat.Weapon
{
    public class FlambergeWeapon : MeleeWeapon, IChargeableWeapon
    {
        public override bool OverrideRotation => true;

        [Header("Flamberge Target Settings")]
        [Tooltip("플레이어 위치 기준 적을 탐색할 수 있는 최대 범위 (중거리 범위)")]
        [SerializeField] private float searchRadius = 5.0f;

        [Header("Slash Effect Settings")]
        [Tooltip("적 위치에 생성할 검기 이펙트 프리팹")]
        [SerializeField] private GameObject slashEffectPrefab;

        [Header("Animator Settings")]
        [SerializeField] private string attackTriggerName = "Attack";

        [Header("Local Child Effect Settings")]
        [Tooltip("무기 공격 시 활성화할 자식 이펙트 오브젝트")]
        [SerializeField] private GameObject localEffectObject;
        
        [Tooltip("자식 이펙트 오브젝트의 Animator")]
        [SerializeField] private Animator localEffectAnimator;
        
        [Tooltip("재생할 자식 이펙트 애니메이션 이름")]
        [SerializeField] private string localEffectClipName = "Sword_Effect";
        
        [Tooltip("자식 이펙트가 켜져 있을 지속 시간")]
        [SerializeField] private float localEffectDuration = 0.1f;

        [Header("Flamberge Charge Settings")]
        [Tooltip("최대 차징 공격 횟수")]
        [SerializeField] private int maxChargeAttackCount = 4;
        
        [Tooltip("차징 이펙트 생성 기준 위치 (없으면 transform을 기본으로 사용)")]
        [SerializeField] private Transform firePoint;

        [Header("Flamberge Charge Attack Visuals")]
        [Tooltip("다중 타격 검기들의 최소 각도 편차")]
        [SerializeField] private float minChargeAttackAngleOffset = -35f;
        
        [Tooltip("다중 타격 검기들의 최대 각도 편차")]
        [SerializeField] private float maxChargeAttackAngleOffset = 35f;

        [Tooltip("다중 타격 검기들의 최소 크기 배율")]
        [SerializeField] private float minChargeAttackScaleMultiplier = 0.7f;

        [Tooltip("다중 타격 검기들의 최대 크기 배율")]
        [SerializeField] private float maxChargeAttackScaleMultiplier = 1.3f;

        private Coroutine localEffectRoutine;
        private WaitForSeconds localEffectWait;
        private bool isFacingRight = true;

        [Header("Visual Settings")]
        [Tooltip("무기 이미지의 실제 비주얼을 담당하는 Transform")]
        [SerializeField] private Transform visualTransform;

        [Header("Visual Calibration Offsets")]
        [Tooltip("우측 조준 상태일 때의 미세 위치 오프셋")]
        [SerializeField] private Vector3 rightOffset = Vector3.zero;
        
        [Tooltip("좌측 조준 상태일 때의 미세 위치 오프셋")]
        [SerializeField] private Vector3 leftOffset = Vector3.zero;

        // 차징 내부 변수
        private float maxChargeTime = 1.5f;
        private float chargeThresholdTime = 0.15f;
        private float currentChargeTime = 0f;
        private bool isCharging = false;
        private bool isPressing = false;
        private float pressTime = 0f;
        private GameObject activeChargeEffectInstance = null;
        private GameObject sparkChargeObject = null;
        private Vector3 originalSparkChargeScale = Vector3.one;

        // IChargeableWeapon 인터페이스 구현
        public bool IsCharging => isCharging;
        public float ChargePercent => GetAdjustedMaxChargeTime() > 0f ? Mathf.Clamp01(currentChargeTime / GetAdjustedMaxChargeTime()) : 0f;

        public override bool CanAttack()
        {
            if (isPressing || isCharging)
            {
                return false;
            }

            return base.CanAttack();
        }

        public override void Start()
        {
            base.Start();
            
            localEffectWait = new WaitForSeconds(localEffectDuration);

            if (animator == null)
            {
                animator = GetComponent<Animator>();
                if (animator == null)
                {
                    animator = GetComponentInChildren<Animator>();
                }
            }

            if (firePoint == null)
            {
                firePoint = this.transform;
            }

            if (weaponData != null)
            {
                maxChargeTime = weaponData.maxChargeTime;
                chargeThresholdTime = weaponData.chargeThresholdTime;
            }

            FindSparkChargeObject();
        }

        public override void Initialize(Nytherion.Data.ScriptableObjects.Weapons.WeaponData data)
        {
            base.Initialize(data);
            if (data != null)
            {
                maxChargeTime = data.maxChargeTime;
                chargeThresholdTime = data.chargeThresholdTime;
            }
            
            if (firePoint == null)
            {
                firePoint = this.transform;
            }
            
            FindSparkChargeObject();
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
                }
            }
        }

        private void LateUpdate()
        {
            RotateToMouse();
        }

        private void RotateToMouse()
        {
            if (Camera.main == null) return;

            // 마우스의 월드 좌표 구하기
            Vector2 mouseScreenPos = Input.mousePosition;
            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, 0f));
            mouseWorldPos.z = 0f;

            // 조준 중심점 (플레이어 몸체 중심) 구하기
            Vector3 centerPos = transform.position;
            PlayerManager player = GetComponentInParent<PlayerManager>();
            if (player != null)
            {
                centerPos = player.transform.position + new Vector3(0f, 0f, 0f);
            }

            // 플레이어 중심에서 마우스 방향으로 향하는 순수 조준각 계산
            Vector2 dir = ((Vector2)mouseWorldPos - (Vector2)centerPos).normalized;
            float aimAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            // 1. 마우스가 플레이어 기준 우측/좌측 반원 중 어디에 있는지 판정
            float deltaFromRight = Mathf.DeltaAngle(0f, aimAngle);
            if (Mathf.Abs(deltaFromRight) <= 90f)
            {
                isFacingRight = true;
            }
            else
            {
                isFacingRight = false;
            }

            // 2. 좌우 고정 로컬 각도 및 스케일 적용 (스케일 반전 대신 Y축 180도 회전 적용 및 마우스 각도 추적)
            if (isFacingRight)
            {
                // 우측 조준 상태: 각도를 우측 부채꼴 범위(-45도 ~ +45도)로 제한
                float clampedAngle = Mathf.Clamp(aimAngle, -45f, 45f);
                
                // 부모의 회전 왜곡을 차단하기 위해 월드 회전 적용
                transform.rotation = Quaternion.Euler(0f, 0f, clampedAngle);
                transform.localScale = new Vector3(1f, 1f, 1f);
                transform.localPosition = rightOffset;

                if (visualTransform != null)
                {
                    visualTransform.localScale = new Vector3(1f, 1f, 1f);
                }
            }
            else
            {
                // 좌측 조준 상태: 180도(좌측 수평) 기준 각도 차이를 구하고 좌측 부채꼴 범위(-45도 ~ +45도)로 제한
                float deltaAngleFromLeft = Mathf.DeltaAngle(180f, aimAngle);
                float clampedDelta = Mathf.Clamp(deltaAngleFromLeft, -45f, 45f);

                // Y축 180도 회전 및 월드 회전 적용
                transform.rotation = Quaternion.Euler(0f, 180f, -clampedDelta);
                transform.localScale = new Vector3(1f, 1f, 1f);
                transform.localPosition = leftOffset;

                if (visualTransform != null)
                {
                    visualTransform.localScale = new Vector3(1f, 1f, 1f);
                }
            }
        }

        private void PlayLocalEffect()
        {
            if (localEffectObject == null || localEffectAnimator == null)
            {
                return;
            }

            if (localEffectRoutine != null)
            {
                StopCoroutine(localEffectRoutine);
            }

            localEffectRoutine = StartCoroutine(PlayLocalEffectRoutine());
        }

        private IEnumerator PlayLocalEffectRoutine()
        {
            localEffectObject.SetActive(true);

            localEffectAnimator.Play(localEffectClipName, 0, 0f);
            localEffectAnimator.Update(0f);

            yield return localEffectWait;

            localEffectObject.SetActive(false);
            localEffectRoutine = null;
        }

        public override void Attack(Vector2 direction, Vector3 targetPosition = default)
        {
            if (!CanAttack())
            {
                return;
            }

            if (!IsChargingEnabled())
            {
                ExecuteAttack(0f);
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

        public override void AttackEnd()
        {
            if (!isPressing) return;

            if (!isCharging)
            {
                isPressing = false;
                ExecuteAttack(0f);
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

                ExecuteAttack(finalChargePercent);
                currentChargeTime = 0f;
            }
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

            ExecuteAttack(1.0f);
        }

        private void ExecuteAttack(float chargePercent)
        {
            // 1. 플레이어 자체 휘두르기 애니메이션 재생
            if (animator != null)
            {
                animator.SetTrigger(attackTriggerName);
            }

            // 2. 자식 이펙트 활성화 및 재생
            PlayLocalEffect();

            // 3. 마우스의 월드 좌표 구하기
            Vector3 mouseWorldPos = Vector3.zero;
            if (Camera.main != null)
            {
                Vector2 mouseScreenPos = Input.mousePosition;
                mouseWorldPos = Camera.main.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, 0f));
                mouseWorldPos.z = 0f;
            }

            // 4. 플레이어 중심 사거리(searchRadius) 이내로 마우스 위치 보정
            Vector3 spawnPos = mouseWorldPos;
            float distFromPlayer = Vector2.Distance(transform.position, mouseWorldPos);
            if (distFromPlayer > searchRadius)
            {
                Vector2 dirToMouse = ((Vector2)mouseWorldPos - (Vector2)transform.position).normalized;
                spawnPos = transform.position + (Vector3)(dirToMouse * searchRadius);
            }

            // 5. 차징 시간에 비례해서 공격 횟수 증가 (최대 maxChargeAttackCount회)
            int attackCount = 1;
            if (IsChargingEnabled() && chargePercent > 0f)
            {
                attackCount = Mathf.RoundToInt(Mathf.Lerp(1f, (float)maxChargeAttackCount, chargePercent));
            }

            // 6. 검기 이펙트 생성
            if (slashEffectPrefab != null)
            {
                for (int i = 0; i < attackCount; i++)
                {
                    GameObject effect = null;

                    // ObjectPoolManager를 통해 검기 이펙트 생성 시도
                    if (ObjectPoolManager.Instance != null)
                    {
                        effect = ObjectPoolManager.Instance.SpawnFromPool(slashEffectPrefab, spawnPos, Quaternion.identity);
                    }
                    else
                    {
                        effect = Instantiate(slashEffectPrefab, spawnPos, Quaternion.identity);
                    }

                    // 생성된 이펙트에 데미지 및 부가 세팅 설정
                    if (effect != null)
                    {
                        // 플레이어와 생성 위치 간의 방향 계산
                        Vector2 attackDir = ((Vector2)spawnPos - (Vector2)transform.position).normalized;
                        float baseAngle = Mathf.Atan2(attackDir.y, attackDir.x) * Mathf.Rad2Deg;

                        // [각도 설정] 마우스 방향 정렬 + 미세한 랜덤 각도 추가 (차징 다중 타격 시 각도 편차 무작위화)
                        float randomRotation = (attackCount > 1) 
                            ? Random.Range(minChargeAttackAngleOffset, maxChargeAttackAngleOffset) 
                            : Random.Range(-15f, 15f);
                        effect.transform.rotation = Quaternion.Euler(0f, 0f, baseAngle + randomRotation);

                        // [스케일 설정] 플레이어가 보는 좌우 방향 및 랜덤 Y 반전 + 차징 다중 타격 시 크기 무작위화
                        float flipX = (attackDir.x >= 0) ? 1.0f : -1.0f;
                        float randomFlipY = (Random.value > 0.5f) ? 1.0f : -1.0f;
                        float scaleMultiplier = (attackCount > 1) 
                            ? Random.Range(minChargeAttackScaleMultiplier, maxChargeAttackScaleMultiplier) 
                            : 1.0f;

                        Vector3 originalScale = slashEffectPrefab.transform.localScale;
                        effect.transform.localScale = new Vector3(
                            originalScale.x * flipX * scaleMultiplier, 
                            originalScale.y * randomFlipY * scaleMultiplier, 
                            originalScale.z
                        );

                        if (effect.TryGetComponent<CollisionObject>(out CollisionObject collisionObj))
                        {
                            collisionObj.damage = weaponData.damage * EffectiveDamageMultiplier;
                            collisionObj.traits = GetTraits();
                            
                            if (collisionObj.hitEffectPrefab == null)
                            {
                                collisionObj.hitEffectPrefab = weaponData.hitEffectPrefab;
                            }
                        }
                        else if (effect.TryGetComponent<FlambergeCollision>(out FlambergeCollision flambergeCol))
                        {
                            flambergeCol.damage = weaponData.damage * EffectiveDamageMultiplier;
                            
                            if (flambergeCol.hitEffectPrefab == null)
                            {
                                flambergeCol.hitEffectPrefab = weaponData.hitEffectPrefab;
                            }
                        }
                    }
                }
            }

            lastAttackTime = Time.time;
        }

        private bool IsChargingEnabled()
        {
            if (weaponData == null) return false;

            if (!string.IsNullOrEmpty(weaponData.requiredRelicId))
            {
                if (playerManager != null && playerManager.playerRelicManager != null)
                {
                    return playerManager.playerRelicManager.IsRelicActive(weaponData.requiredRelicId);
                }
            }
            return false;
        }

        private float GetAdjustedMaxChargeTime()
        {
            if (playerManager == null || playerManager.currentPlayerData == null)
            {
                return maxChargeTime;
            }

            float reduction = playerManager.currentPlayerData.chargeTimeReduction;
            return Mathf.Max(0f, maxChargeTime * (1f - reduction));
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

        #if UNITY_EDITOR
        // 에디터 상에서 적 탐색 범위를 시각적으로 확인하기 위한 기즈모
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, searchRadius);
        }
        #endif
    }
}
