using System.Collections;
using UnityEngine;
using Nytherion.Core.Managers;
using Nytherion.GamePlay.Characters.Player;

namespace Nytherion.GamePlay.Combat.Weapons
{
    public class Spear : MeleeWeapon, IChargeableWeapon
    {
        [Header("Spear Aim Settings")]
        [Tooltip("플레이어 기준 위아래 최대 조준 각도 (부채꼴의 절반 크기)")]
        [SerializeField] private float maxAimAngle = 45f;

        [Tooltip("대기 상태에서 플레이어 중심 대비 무기의 오프셋")]
        [SerializeField] private Vector3 idleOffset = new Vector3(0.3f, -0.1f, 0f);

        [Header("Spear Charging Settings")]
        [Tooltip("차징이 가능한 무기인지 여부")]
        [SerializeField] private bool isChargeable = false;

        [Tooltip("최대 차징에 걸리는 시간(초)")]
        [SerializeField] private float maxChargeTime = 1.0f;

        [Tooltip("차징 시 활성화할 자식 이펙트 오브젝트 (미지정 시 이름에 'Charge'가 포함된 자식을 자동 탐색)")]
        [SerializeField] private GameObject chargingEffectObject;

        [Tooltip("차징 시 활성화할 자식 파티클 시스템 (미지정 시 chargingEffectObject에서 자동 획득)")]
        [SerializeField] private ParticleSystem chargingParticle;

        [Header("Spear Visual Charge Settings")]
        [Tooltip("차징 시 적용할 머티리얼 (스프라이트 빛남/흰색 플래시용)")]
        [SerializeField] private Material chargingMaterial;

        [Tooltip("차징 진행도에 따라 제어할 머티리얼 프로퍼티 이름 (예: _FlashAmount)")]
        [SerializeField] private string chargePropertyName = "_FlashAmount";

        [Header("Spear Attack Settings")]
        [Tooltip("찌르기 동작 시 앞으로 뻗어나갈 최대 거리")]
        [SerializeField] private float thrustDistance = 1.5f;

        [Tooltip("찌르기 애니메이션 전체 진행 시간(초)")]
        [SerializeField] private float attackDuration = 0.2f;

        [Tooltip("최대 거리에 도달하는 타이밍 비율 (0.0 ~ 1.0)")]
        [SerializeField] private float peakTimePercent = 0.25f;

        [Header("Spear Effect Settings")]
        [Tooltip("공격 시 활성화할 자식 이펙트 오브젝트 (미지정 시 Animator가 있는 자식을 자동 탐색)")]
        [SerializeField] private GameObject slashEffectObject;

        [Tooltip("이펙트 오브젝트의 Animator (미지정 시 slashEffectObject에서 자동 획득)")]
        [SerializeField] private Animator slashEffectAnimator;

        [Tooltip("실행할 이펙트 애니메이션의 State 이름")]
        [SerializeField] private string effectStateName = "AttackEffect";

        // 무기 방향 설정을 강제하기 위해 true로 재정의
        public override bool OverrideRotation => true;

        [Header("Spear Threshold Settings")]
        [SerializeField] private float chargeThresholdTime = 0.15f;

        private PlayerController playerController;
        private SpriteRenderer spriteRenderer;
        private Coroutine attackCoroutine;
        private Coroutine effectCoroutine;
        private bool isAttacking = false;

        // 차징 진행 상태 및 머티리얼 캐싱
        private bool isCharging = false;
        private bool isPressing = false;
        private float pressTime = 0f;
        private float currentChargeTime = 0f;
        private Material originalMaterial;
        private bool isMaterialSwapped = false;

        // IChargeableWeapon 인터페이스 구현
        public bool IsCharging => isCharging;
        public float ChargePercent => GetAdjustedMaxChargeTime() > 0f ? Mathf.Clamp01(currentChargeTime / GetAdjustedMaxChargeTime()) : 0f;

        // 물리 프레임 업데이트 주기 문제로 인한 트리거 누락을 방지하기 위한 수동 충돌 타겟 관리
        private System.Collections.Generic.HashSet<Nytherion.Core.Interfaces.IDamageable> spearHitTargets = new();

        public override bool CanAttack()
        {
            // 차징 중이거나 공격 애니메이션이 진행 중일 때는 추가 공격을 방지하기 위해 CanAttack을 false로 제한
            if (isCharging || isAttacking)
            {
                return false;
            }

            if (weaponData == null)
            {
                return true; // weaponData가 없는 예외 환경에서도 에러 없이 작동 보장
            }

            return base.CanAttack();
        }

        public override void Start()
        {
            // base.Start() 호출 시 부모에 WeaponAniRelay가 존재하지 않으면 NullReferenceException이 발생하여 
            // 스크립트 실행이 중단되는 현상을 방지하고자 base.Start()를 우회하고 안전 처리를 직접 수행합니다.
            DisableHitbox();
            
            WeaponAniRelay weaponAniRelay = GetComponentInParent<WeaponAniRelay>();
            if (weaponAniRelay != null)
            {
                weaponAniRelay.currentWeapon = this;
            }

            // 부모 또는 부모의 부모에서 PlayerController와 SpriteRenderer 캐싱
            playerController = GetComponentInParent<PlayerController>();
            
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            // 콜라이더 자가 캐싱 및 초기 비활성화 안전 처리
            if (col == null)
            {
                col = GetComponent<Collider2D>();
                if (col == null)
                {
                    col = GetComponentInChildren<Collider2D>();
                }
            }

            if (col != null)
            {
                // 물리 반발 없이 겹쳐지며 충돌을 정확히 감지하도록 트리거 속성 보장
                col.isTrigger = true;
            }

            DisableHitbox();

            // 자식 이펙트 및 차징 오브젝트 자동 캐싱
            InitializeEffectObjects();

            // 초기 위치 및 회전 설정
            UpdateWeaponAim();
        }

        private void InitializeEffectObjects()
        {
            try
            {
                // 1. 공격 슬래시 이펙트 캐싱
                if (slashEffectObject == null)
                {
                    foreach (Transform child in transform)
                    {
                        Animator childAnimator = child.GetComponent<Animator>();
                        if (childAnimator != null && !child.name.Contains("Charge") && !child.name.Contains("charging"))
                        {
                            slashEffectObject = child.gameObject;
                            slashEffectAnimator = childAnimator;
                            break;
                        }
                    }
                }
                else if (slashEffectAnimator == null)
                {
                    slashEffectAnimator = slashEffectObject.GetComponent<Animator>();
                }

                if (slashEffectObject != null)
                {
                    slashEffectObject.SetActive(false);
                }

                // 2. 차징 이펙트 및 파티클 캐싱
                if (chargingEffectObject == null)
                {
                    foreach (Transform child in transform)
                    {
                        if (child.name.Contains("Charge") || child.name.Contains("charging") || child.name.Contains("Particle"))
                        {
                            chargingEffectObject = child.gameObject;
                            chargingParticle = child.GetComponent<ParticleSystem>();
                            if (chargingParticle == null)
                            {
                                chargingParticle = child.GetComponentInChildren<ParticleSystem>();
                            }
                            break;
                        }
                    }
                }
                else if (chargingParticle == null)
                {
                    chargingParticle = chargingEffectObject.GetComponent<ParticleSystem>();
                    if (chargingParticle == null)
                    {
                        chargingParticle = chargingEffectObject.GetComponentInChildren<ParticleSystem>();
                    }
                }

                // 시작 시 차징 이펙트 및 파티클 확실히 꺼두기
                SetChargingEffect(false);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Spear] InitializeEffectObjects Exception: " + e.Message);
            }
        }

        private void Update()
        {
            // 공격 중이 아닐 때만 마우스 조준 방향으로 회전 및 위치 실시간 갱신
            if (!isAttacking)
            {
                UpdateWeaponAim();
            }

            // 마우스 버튼이 유지되고 있을 때 누적 시간 및 차징 판단 업데이트
            if (isChargeable && isPressing && !isAttacking)
            {
                pressTime += Time.deltaTime;

                if (!isCharging)
                {
                    if (pressTime >= chargeThresholdTime)
                    {
                        isCharging = true;
                        currentChargeTime = 0f;
                        SetChargingEffect(true); // 차징 파티클 활성화

                        if (chargingMaterial != null && spriteRenderer != null && !isMaterialSwapped)
                        {
                            originalMaterial = spriteRenderer.sharedMaterial;
                            spriteRenderer.material = Instantiate(chargingMaterial);
                            isMaterialSwapped = true;
                        }
                    }
                }
                else
                {
                    currentChargeTime += Time.deltaTime;
                    float adjustedMaxCharge = GetAdjustedMaxChargeTime();
                    
                    float chargePercent = adjustedMaxCharge > 0f ? Mathf.Clamp01(currentChargeTime / adjustedMaxCharge) : 1f;
                    UpdateChargingVisual(chargePercent);

                    // 차징 시간이 한계를 넘고도 릴리즈가 안 될 경우 (인풋 상태 꼬임 방지용 자동 릴리즈)
                    if (currentChargeTime >= (adjustedMaxCharge * 3.0f))
                    {
                        AttackEnd(); // 강제 릴리즈 발사
                        return;
                    }
                }
            }
        }

        private float GetAdjustedMaxChargeTime()
        {
            if (playerController != null && playerController.PlayerData != null)
            {
                // 플레이어의 차징 시간 감소율 적용
                float reduction = playerController.PlayerData.chargeTimeReduction;
                return Mathf.Max(0f, maxChargeTime * (1f - reduction));
            }
            return maxChargeTime;
        }

        private void UpdateWeaponAim()
        {
            if (playerController == null || InputManager.Instance == null)
            {
                return;
            }

            // 1. 마우스 월드 좌표 계산
            Vector2 mouseScreenPos = InputManager.Instance.MousePosition;
            if (Camera.main == null)
            {
                return;
            }

            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, 0f));
            mouseWorldPos.z = 0f;

            // 2. 플레이어 중심(또는 부모 트랜스폼 기준)에서의 방향 벡터 구하기
            Vector3 basePosition = transform.parent != null ? transform.parent.position : transform.position;
            Vector3 targetDir = mouseWorldPos - basePosition;

            // 3. 조준 각도 및 부채꼴 제한 계산
            float targetAngle = Mathf.Atan2(targetDir.y, targetDir.x) * Mathf.Rad2Deg;
            
            // 플레이어가 바라보는 중심 각도 (우측: 0도, 좌측: 180도)
            float centerAngle = playerController.IsFacingRight ? 0f : 180f;

            // 마우스 각도와 플레이어 바라보는 방향 각도의 차이 연산 후 Clamp
            float angleDiff = Mathf.DeltaAngle(centerAngle, targetAngle);
            angleDiff = Mathf.Clamp(angleDiff, -maxAimAngle, maxAimAngle);
            
            // 제한 범위가 적용된 최종 조준 각도
            float finalAimAngle = centerAngle + angleDiff;

            // 4. 좌우 바라보는 상태에 따른 스케일, 오프셋, 그리고 최종 회전 설정
            bool facingRight = playerController.IsFacingRight;
            Vector3 targetLocalPos = idleOffset;

            if (!facingRight)
            {
                // 좌측을 향할 때는 X축 오프셋을 대칭으로 반전
                targetLocalPos.x = -idleOffset.x;
                // 좌측을 향할 때는 스프라이트의 상하 반전(Y축 반전)을 통해 윗면이 늘 위를 향하도록 보정
                transform.localScale = new Vector3(1f, -1f, 1f);
            }
            else
            {
                transform.localScale = new Vector3(1f, 1f, 1f);
            }

            transform.localPosition = targetLocalPos;

            // 기본 이미지 방향이 우측 대각선(45도)을 가리키고 있으므로 
            // 우측(0도)일 땐 -45도 회전, 좌측(180도)일 땐 +45도 보정을 적용해 수평을 맞춥니다.
            float finalRotationZ = finalAimAngle + (facingRight ? -45f : 45f);
            transform.localRotation = Quaternion.Euler(0f, 0f, finalRotationZ);
        }

        public override void Attack(Vector2 direction, Vector3 targetPosition = default)
        {
            try
            {
                if (!CanAttack())
                {
                    return;
                }

                if (isChargeable)
                {
                    // 이미 차징 중이거나 공격 중인 경우 무시
                    if (isCharging || isAttacking)
                    {
                        return;
                    }

                    isPressing = true;
                    pressTime = 0f;
                    isCharging = false;
                    currentChargeTime = 0f;
                }
                else
                {
                    // 차징 미적용 무기면 즉시 일반 공격 실행
                    ExecuteSpearAttack(1f, thrustDistance);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Spear] Attack Exception: " + e.Message + "\n" + e.StackTrace);
            }
        }

        private void SetChargingEffect(bool active)
        {
            try
            {
                if (chargingEffectObject != null)
                {
                    chargingEffectObject.SetActive(active);
                }

                if (chargingParticle != null)
                {
                    if (active)
                    {
                        if (!chargingParticle.isPlaying)
                        {
                            chargingParticle.Play(true);
                        }
                    }
                    else
                    {
                        chargingParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Spear] SetChargingEffect Exception: " + e.Message + "\n" + e.StackTrace);
            }
        }

        private void ExecuteSpearAttack(float finalDamageMultiplier, float finalThrustDistance)
        {
            try
            {
                lastAttackTime = Time.time;

                if (attackCoroutine != null)
                {
                    StopCoroutine(attackCoroutine);
                }

                if (playerController != null && InputManager.Instance != null && Camera.main != null)
                {
                    Vector2 mouseScreenPos = InputManager.Instance.MousePosition;
                    Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, 0f));
                    mouseWorldPos.z = 0f;

                    Vector3 basePosition = transform.parent != null ? transform.parent.position : transform.position;
                    Vector3 targetDir = mouseWorldPos - basePosition;

                    float targetAngle = Mathf.Atan2(targetDir.y, targetDir.x) * Mathf.Rad2Deg;
                    float centerAngle = playerController.IsFacingRight ? 0f : 180f;
                    float angleDiff = Mathf.DeltaAngle(centerAngle, targetAngle);
                    angleDiff = Mathf.Clamp(angleDiff, -maxAimAngle, maxAimAngle);
                    float finalAimAngle = centerAngle + angleDiff;

                    // 근접 공격 속도 배율
                    float speedMultiplier = 1f;
                    if (playerController != null && playerController.PlayerData != null)
                    {
                        speedMultiplier = Mathf.Max(0.1f, playerController.PlayerData.meleeSpeed);
                    }

                    float currentAttackDuration = attackDuration / speedMultiplier;

                    attackCoroutine = StartCoroutine(ThrustRoutine(finalAimAngle, currentAttackDuration, finalThrustDistance, speedMultiplier, finalDamageMultiplier));
                    PlaySlashEffect(currentAttackDuration, speedMultiplier);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Spear] ExecuteSpearAttack Exception: " + e.Message + "\n" + e.StackTrace);
            }
        }

        private void PlaySlashEffect(float currentAttackDuration, float speedMultiplier)
        {
            try
            {
                if (slashEffectObject == null || slashEffectAnimator == null)
                {
                    return;
                }

                if (effectCoroutine != null)
                {
                    StopCoroutine(effectCoroutine);
                }

                effectCoroutine = StartCoroutine(PlaySlashEffectRoutine(currentAttackDuration, speedMultiplier));
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Spear] PlaySlashEffect Exception: " + e.Message + "\n" + e.StackTrace);
            }
        }

        private IEnumerator PlaySlashEffectRoutine(float currentAttackDuration, float speedMultiplier)
        {
            slashEffectObject.SetActive(true);
            
            // Animator 재생 속도를 공격 속도 배율에 맞게 조정
            slashEffectAnimator.speed = speedMultiplier;
            
            // 애니메이션을 강제로 처음부터 재생하고 프레임 즉시 업데이트
            slashEffectAnimator.Play(effectStateName, 0, 0f);
            slashEffectAnimator.Update(0f);

            // 찌를 때만 이펙트가 노출되도록 전체 공격(찌르기+복귀) 지속 시간의 절반(0.5f) 동안만 대기
            float currentEffectDuration = currentAttackDuration * 0.5f;
            yield return new WaitForSeconds(currentEffectDuration);

            // 완료 후 Animator 재생 속도 원복 및 비활성화
            slashEffectAnimator.speed = 1f;
            slashEffectObject.SetActive(false);
            effectCoroutine = null;
        }

        private IEnumerator ThrustRoutine(float aimAngle, float currentAttackDuration, float activeThrustDistance, float speedMultiplier, float finalDamageMultiplier)
        {
            isAttacking = true;
            spearHitPoolClear(); // 타격했던 대상 캐시 초기화

            // 1. 공격 시작 각도 및 스케일 고정
            bool facingRight = playerController.IsFacingRight;
            transform.localScale = new Vector3(1f, facingRight ? 1f : -1f, 1f);
            float finalRotationZ = aimAngle + (facingRight ? -45f : 45f);
            transform.localRotation = Quaternion.Euler(0f, 0f, finalRotationZ);

            // 2. 조준된 최종 각도 방향 벡터
            Vector3 thrustDir = new Vector3(Mathf.Cos(aimAngle * Mathf.Deg2Rad), Mathf.Sin(aimAngle * Mathf.Deg2Rad), 0f);

            // 3. 대기 상태 오프셋 위치
            Vector3 targetIdlePos = idleOffset;
            if (!facingRight)
            {
                targetIdlePos.x = -idleOffset.x;
            }

            float elapsed = 0f;
            EnableHitbox(); // 공격 활성화 시점 콜라이더 활성화

            // 4. 프레임별 찌르기 애니메이션 보간
            while (elapsed < currentAttackDuration)
            {
                elapsed += Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / currentAttackDuration);
                float currentThrustDistance = 0f;

                if (normalizedTime < peakTimePercent)
                {
                    // 전진 구간: 0 -> activeThrustDistance (선형 보간)
                    float t = normalizedTime / peakTimePercent;
                    currentThrustDistance = Mathf.Lerp(0f, activeThrustDistance, t);

                    // 전진하는 매 프레임 수동으로 즉각적인 Overlap 검사를 실행하여 타격 신뢰성을 보장
                    CheckManualCollision(finalDamageMultiplier);
                }
                else
                {
                    // 복귀 구간 진입 시점(최대 도달 직후) 즉시 콜라이더 비활성화
                    DisableHitbox();

                    // 복귀 구간: activeThrustDistance -> 0 (선형 보간)
                    float t = (normalizedTime - peakTimePercent) / (1f - peakTimePercent);
                    currentThrustDistance = Mathf.Lerp(activeThrustDistance, 0f, t);
                }

                transform.localPosition = targetIdlePos + thrustDir * currentThrustDistance;
                yield return null;
            }

            // 5. 원래 위치로 복귀 및 정리
            transform.localPosition = targetIdlePos;
            DisableHitbox();

            isAttacking = false;
            attackCoroutine = null;
        }

        private void spearHitPoolClear()
        {
            spearHitTargets.Clear();
        }

        private void CheckManualCollision(float finalDamageMultiplier)
        {
            ContactFilter2D filter = new ContactFilter2D();
            filter.useTriggers = true;

            Collider2D[] results = new Collider2D[15];
            int count = col.OverlapCollider(filter, results);

            for (int i = 0; i < count; i++)
            {
                Collider2D other = results[i];
                if (other == null)
                {
                    continue;
                }

                if (other.CompareTag("Enemy"))
                {
                    Nytherion.Core.Interfaces.IDamageable target = other.GetComponent<Nytherion.Core.Interfaces.IDamageable>();
                    if (target != null && !spearHitTargets.Contains(target))
                    {
                        spearHitTargets.Add(target);
                        
                        // 차징 배율이 곱해진 대미지 전달
                        float baseDmg = weaponData != null ? weaponData.damage : 10f;
                        target.TakeDamage(baseDmg * finalDamageMultiplier);
                        ApplyStatusEffects(target);

                        if (weaponData != null)
                        {
                            WeaponEffectHelper.PlayHitEffect(weaponData.hitEffectPrefab, other.transform.position);
                        }
                    }
                }
            }
        }

        public override void AttackEnd()
        {
            try
            {
                if (isChargeable && isPressing)
                {
                    isPressing = false;

                    if (!isCharging)
                    {
                        // 임계값 미만으로 누르고 뗀 경우 -> 일반 찌르기 공격 (100% 대미지, 기본 거리)
                        ExecuteSpearAttack(1f, thrustDistance);
                    }
                    else
                    {
                        // 임계값 이상 누르고 뗀 경우 -> 차징 찌르기 공격 (차징량 비례)
                        isCharging = false;
                        SetChargingEffect(false); // 차징 파티클 비활성화
                        ResetChargingMaterial(); // 머티리얼 복구

                        float adjustedMaxCharge = GetAdjustedMaxChargeTime();
                        float chargePercent = adjustedMaxCharge > 0f ? Mathf.Clamp01(currentChargeTime / adjustedMaxCharge) : 1f;

                        float finalDamageMultiplier = Mathf.Lerp(0.5f, 1.5f, chargePercent);
                        float finalThrustDistance = Mathf.Lerp(thrustDistance * 0.5f, thrustDistance, chargePercent);

                        ExecuteSpearAttack(finalDamageMultiplier, finalThrustDistance);
                        currentChargeTime = 0f;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Spear] AttackEnd Exception: " + e.Message + "\n" + e.StackTrace);
            }
        }

        private void UpdateChargingVisual(float chargePercent)
        {
            if (spriteRenderer == null)
            {
                return;
            }

            if (isMaterialSwapped && spriteRenderer.material != null)
            {
                if (spriteRenderer.material.HasProperty(chargePropertyName))
                {
                    if (chargePropertyName.Contains("Color") || chargePropertyName.Contains("color"))
                    {
                        Color targetColor = Color.Lerp(Color.white, new Color(2f, 2f, 2f, 1f), chargePercent);
                        spriteRenderer.material.SetColor(chargePropertyName, targetColor);
                    }
                    else
                    {
                        spriteRenderer.material.SetFloat(chargePropertyName, chargePercent);
                    }
                }
            }
            else
            {
                // Fallback: 지정된 머티리얼이 없는 경우 스프라이트 틴트 색상을 서서히 노란색/밝은 톤으로 보간하여 피드백 제공
                spriteRenderer.color = Color.Lerp(Color.white, new Color(1f, 1f, 0.4f, 1f), chargePercent);
            }
        }

        private void ResetChargingMaterial()
        {
            try
            {
                if (isMaterialSwapped && spriteRenderer != null)
                {
                    Material tempMat = spriteRenderer.material;
                    spriteRenderer.sharedMaterial = originalMaterial;
                    if (tempMat != null)
                    {
                        Destroy(tempMat);
                    }
                    isMaterialSwapped = false;
                }

                if (spriteRenderer != null)
                {
                    spriteRenderer.color = Color.white;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Spear] ResetChargingMaterial Exception: " + e.Message + "\n" + e.StackTrace);
            }
        }

        private void OnDisable()
        {
            // 무기가 장착 해제되거나 비활성화될 때 코루틴 및 상태 초기화 안전 처리
            if (attackCoroutine != null)
            {
                StopCoroutine(attackCoroutine);
                attackCoroutine = null;
            }

            if (effectCoroutine != null)
            {
                StopCoroutine(effectCoroutine);
                effectCoroutine = null;
            }

            isAttacking = false;
            isPressing = false;
            isCharging = false;
            currentChargeTime = 0f;
            SetChargingEffect(false);
            ResetChargingMaterial();

            if (slashEffectObject != null)
            {
                slashEffectObject.SetActive(false);
            }

            DisableHitbox();
        }
    }
}
