using System.Collections;
using UnityEngine;
using Nytherion.Core.Managers;
using Nytherion.GamePlay.Characters.Player;

namespace Nytherion.GamePlay.Combat.Weapons
{
    public class Spear : MeleeWeapon, IChargeableWeapon
    {
        private const string RedSpearPennonRelicId = "RedSpearPennon";
        private const string ThrustingManualRelicId = "Thrusting Manual";

        [Header("Spear Aim Settings")]
        [Tooltip("플레이어 기준 위아래 최대 조준 각도 (부채꼴의 절반 크기)")]
        [SerializeField] private float maxAimAngle = 45f;

        [Tooltip("원본 스프라이트에서 창끝이 향하는 로컬 각도 (+X: 0도, +Y: 90도)")]
        [SerializeField] private float spriteForwardAngle = 45f;

        [Tooltip("대기 상태에서 플레이어 중심 대비 무기의 오프셋")]
        [SerializeField] private Vector3 idleOffset = new Vector3(0.3f, -0.1f, 0f);

        [Tooltip("공격 방향으로 창끝을 돌리는 준비 시간(초). 근접 공격 속도의 영향을 받습니다.")]
        [SerializeField, Min(0f)] private float aimTransitionDuration = 0.12f;

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

        [Header("Spear Swing Combo Settings")]
        [Tooltip("기본/앞 타격 종료 자세에서 다음 스윙 준비 자세까지 부드럽게 이동하는 시간(초). 공격 속도와 무관합니다.")]
        [SerializeField, Min(0.01f)] private float swingPreparationDuration = 0.18f;

        [Tooltip("오른쪽을 바라볼 때의 1타 스윙 준비 위치")]
        [SerializeField] private Vector3 firstSwingPreparationPosition = new Vector3(-0.8f, 0.05f, 0f);

        [Tooltip("오른쪽을 바라볼 때의 1타 스윙 준비 회전각")]
        [SerializeField] private float firstSwingPreparationRotation = -275f;

        [Tooltip("오른쪽을 바라볼 때의 1타 스윙 종료 위치")]
        [SerializeField] private Vector3 firstSwingEndPosition = new Vector3(0.5f, -0.5f, 0f);

        [Tooltip("오른쪽을 바라볼 때의 1타 스윙 종료 회전각")]
        [SerializeField] private float firstSwingEndRotation = -135f;

        [Tooltip("스윙 시작에서 화면에 보여 줄 궤적 비율")]
        [SerializeField, Range(0.05f, 0.4f)] private float swingVisibleStartRatio = 0.18f;

        [Tooltip("스윙 시작 가시 구간에 사용할 시간(초)")]
        [SerializeField, Min(0.01f)] private float swingVisibleStartDuration = 0.04f;

        [Tooltip("스윙 종료 직전에 화면에 보여 줄 궤적 비율")]
        [SerializeField, Range(0.05f, 0.4f)] private float swingVisibleEndRatio = 0.22f;

        [Tooltip("스윙 종료 직전 가시 구간에 사용할 시간(초)")]
        [SerializeField, Min(0.01f)] private float swingVisibleEndDuration = 0.06f;

        [Tooltip("스윙의 중앙 궤적을 빠르게 통과하는 시간(초)")]
        [SerializeField, Min(0.01f)] private float swingFastMiddleDuration = 0.02f;

        [Tooltip("스윙 원운동의 중심. (0, 0)은 플레이어의 근접 무기 장착점입니다")]
        [SerializeField] private Vector3 swingOrbitCenterOffset = Vector3.zero;

        [Tooltip("오른쪽을 바라볼 때의 2타 스윙 준비 위치")]
        [SerializeField] private Vector3 secondSwingPreparationPosition = new Vector3(0.15f, -0.85f, 0f);

        [Tooltip("오른쪽을 바라볼 때의 2타 스윙 준비 회전각")]
        [SerializeField] private float secondSwingPreparationRotation = -170f;

        [Tooltip("오른쪽을 바라볼 때의 2타 스윙 종료 위치")]
        [SerializeField] private Vector3 secondSwingEndPosition = new Vector3(-0.75f, 0.45f, 0f);

        [Tooltip("오른쪽을 바라볼 때의 2타 스윙 종료 회전각")]
        [SerializeField] private float secondSwingEndRotation = 60f;

        [Tooltip("각 콤보 공격의 종료 자세에서 다음 공격을 기다리는 시간(초)")]
        [SerializeField, Min(0f)] private float comboHoldDuration = 0.3f;

        [Tooltip("콤보 입력 시간이 지난 뒤 기본 자세로 돌아가는 시간(초)")]
        [SerializeField, Min(0.01f)] private float comboReturnDuration = 0.18f;

        [Header("Spear Thrust Combo Settings")]
        [Tooltip("2타 스윙 종료 자세에서 첫 찌르기 준비 자세까지 이동하는 시간(초)")]
        [SerializeField, Min(0.01f)] private float swingToThrustTransitionDuration = 0.08f;

        [Tooltip("짧은 찌르기 한 번의 전체 진행 시간(초)")]
        [SerializeField, Min(0.01f)] private float shortThrustDuration = 0.16f;

        [Tooltip("짧은 찌르기 시작 시 공격 방향으로 창끝을 맞추는 시간(초)")]
        [SerializeField, Min(0f)] private float shortThrustAimDuration = 0.06f;

        [Tooltip("짧은 찌르기에서 창을 뒤로 당기는 데 사용하는 시간 비율")]
        [SerializeField, Range(0.05f, 0.6f)] private float shortThrustWindupRatio = 0.32f;

        [Tooltip("짧은 찌르기에서 최대 전진 지점까지 뻗는 데 사용하는 시간 비율")]
        [SerializeField, Range(0.05f, 0.8f)] private float shortThrustStrikeRatio = 0.38f;

        [Tooltip("1~3번째 짧은 찌르기의 뒤로 당기는 거리")]
        [SerializeField] private Vector3 shortThrustPullDistances = new Vector3(0.12f, 0.2f, 0.3f);

        [Tooltip("짧은 찌르기가 기본 위치에서 앞으로 뻗는 거리")]
        [SerializeField, Min(0f)] private float shortThrustDistance = 0.8f;

        [Tooltip("3번째 짧은 찌르기의 시전 시간 배율. 1~2타보다 약간 느리고 힘차게 보이도록 사용합니다.")]
        [SerializeField, Range(0.5f, 2f)] private float thirdThrustDurationMultiplier = 1.3f;

        [Header("Atlas Relic Settings")]
        [Tooltip("붉은 창 깃의 1레벨 찌르기 거리 증가율")]
        [SerializeField, Min(0f)] private float redSpearPennonRangeBonus = 0.2f;

        [Tooltip("붉은 창 깃의 추가 레벨당 찌르기 거리 증가율")]
        [SerializeField, Min(0f)] private float redSpearPennonRangeBonusPerLevel = 0.05f;

        [Tooltip("찌르기 교본의 1레벨 찌르기 속도 증가율")]
        [SerializeField, Min(0f)] private float thrustingManualSpeedBonus = 0.3f;

        [Tooltip("찌르기 교본의 추가 레벨당 찌르기 속도 증가율")]
        [SerializeField, Min(0f)] private float thrustingManualSpeedBonusPerLevel = 0.1f;

        [Header("Spear Effect Settings")]
        [Tooltip("공격 시 활성화할 자식 이펙트 오브젝트 (미지정 시 Animator가 있는 자식을 자동 탐색)")]
        [SerializeField] private GameObject slashEffectObject;

        [Tooltip("이펙트 오브젝트의 Animator (미지정 시 slashEffectObject에서 자동 획득)")]
        [SerializeField] private Animator slashEffectAnimator;

        [Tooltip("실행할 이펙트 애니메이션의 State 이름")]
        [SerializeField] private string effectStateName = "AttackEffect";

        [Tooltip("공격 속도와 무관하게 이펙트 애니메이션 전체를 재생할 시간(초)")]
        [SerializeField, Min(0.01f)] private float effectPlaybackDuration = 0.3f;

        [Tooltip("애니메이션 완료 후 마지막 프레임을 유지할 시간(초)")]
        [SerializeField, Min(0f)] private float effectFinalFrameHoldDuration = 0.05f;

        [Tooltip("공격 시작 후 무기를 따라갈 이펙트 프레임 수")]
        [SerializeField, Min(0)] private int effectFollowFrameCount = 2;

        [Tooltip("완전 차징 공격 이펙트의 최대 로컬 스케일")]
        [SerializeField, Min(0.01f)] private float maxChargedEffectScale = 2f;

        [Tooltip("짧은 찌르기 1~2타에 사용할 Atlas_Thrust_Effect 컨트롤러")]
        [SerializeField] private RuntimeAnimatorController shortThrustEffectController;

        [Tooltip("짧은 찌르기 3타와 차징 찌르기에 사용할 AtlasThrust 컨트롤러")]
        [SerializeField] private RuntimeAnimatorController chargedThrustEffectController;

        [Tooltip("짧은 찌르기 1~2타 Atlas_Thrust_Effect의 원본 대비 크기")]
        [SerializeField, Min(0.01f)] private float shortThrustEffectScaleMultiplier = 0.45f;

        [Tooltip("짧은 찌르기 3타 AtlasThrust의 원본 대비 크기")]
        [SerializeField, Min(0.01f)] private float thirdThrustEffectScaleMultiplier = 1.6f;

        // 무기 방향 설정을 강제하기 위해 true로 재정의
        public override bool OverrideRotation => true;

        [Header("Spear Threshold Settings")]
        [SerializeField] private float chargeThresholdTime = 0.3f;

        private PlayerController playerController;
        private SpriteRenderer spriteRenderer;
        private Coroutine attackCoroutine;
        private Coroutine effectCoroutine;
        private RuntimeAnimatorController originalEffectController;
        private Transform effectOriginalParent;
        private Vector3 effectOriginalLocalPosition;
        private Quaternion effectOriginalLocalRotation;
        private Vector3 effectOriginalLocalScale;
        private bool isAttacking = false;
        private int nextComboStep = 1;
        private int activeComboStep;
        private int activeComboLength;
        private bool hasLatchedComboMode;
        private bool comboUsesThrustOnly;
        private bool isHoldingComboPose;
        private bool isReturningToIdle;
        private bool isComboContinuationPress;
        private bool comboFacingRight;
        private float comboPoseReleaseTime;
        private float comboReturnStartTime;
        private float heldComboRotation;
        private float comboReturnStartRotation;
        private Vector3 heldComboPosition;
        private Vector3 comboReturnStartPosition;

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
            if (isPressing || isCharging || isAttacking)
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

            // 초기 어깨걸이 위치 및 회전 설정
            UpdateIdlePose();
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
                    Transform effectTransform = slashEffectObject.transform;
                    effectOriginalParent = effectTransform.parent;
                    effectOriginalLocalPosition = effectTransform.localPosition;
                    effectOriginalLocalRotation = effectTransform.localRotation;
                    effectOriginalLocalScale = effectTransform.localScale;
                    originalEffectController = slashEffectAnimator != null
                        ? slashEffectAnimator.runtimeAnimatorController
                        : null;
                    if (chargedThrustEffectController == null)
                    {
                        chargedThrustEffectController = originalEffectController;
                    }
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
            // 공격 중이 아닐 때는 콤보 종료 자세를 유지하거나 어깨걸이 자세로 복귀
            if (!isAttacking)
            {
                if (isHoldingComboPose)
                {
                    UpdateComboHoldPose();
                }
                else
                {
                    UpdateIdlePose();
                }
            }

            // 마우스 버튼이 유지되고 있을 때 누적 시간 및 차징 판단 업데이트
            if (IsChargingEnabled() && isPressing && !isComboContinuationPress && !isAttacking)
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

        private void UpdateIdlePose()
        {
            if (playerController == null)
            {
                return;
            }

            bool facingRight = playerController.IsFacingRight;
            transform.localScale = new Vector3(1f, facingRight ? 1f : -1f, 1f);
            transform.localPosition = GetShoulderIdleOffset(facingRight);
            transform.localRotation = Quaternion.Euler(0f, 0f, GetShoulderIdleRotation(facingRight));
        }

        public override void Attack(Vector2 direction, Vector3 targetPosition = default)
        {
            try
            {
                if (!CanAttack())
                {
                    return;
                }

                if (IsChargingEnabled())
                {
                    // 이미 차징 중이거나 공격 중인 경우 무시
                    if (isCharging || isAttacking)
                    {
                        return;
                    }

                    isComboContinuationPress = CanContinueCombo(playerController.IsFacingRight);
                    isPressing = true;
                    pressTime = 0f;
                    isCharging = false;
                    currentChargeTime = 0f;
                }
                else
                {
                    // 차징 미적용 무기면 즉시 일반 휘두르기 실행
                    ExecuteComboAttack(1f);
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

        private void ExecuteSpearAttack(float finalDamageMultiplier, float finalThrustDistance, float effectChargePercent)
        {
            try
            {
                lastAttackTime = Time.time;
                ResetComboState();

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

                    speedMultiplier *= GetThrustSpeedMultiplier();
                    finalThrustDistance *= GetThrustRangeMultiplier();

                    float currentAttackDuration = attackDuration / speedMultiplier;

                    attackCoroutine = StartCoroutine(ThrustRoutine(
                        finalAimAngle,
                        currentAttackDuration,
                        finalThrustDistance,
                        speedMultiplier,
                        finalDamageMultiplier,
                        effectChargePercent));
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Spear] ExecuteSpearAttack Exception: " + e.Message + "\n" + e.StackTrace);
            }
        }

        private void ExecuteComboAttack(float finalDamageMultiplier)
        {
            try
            {
                lastAttackTime = Time.time;

                if (attackCoroutine != null)
                {
                    StopCoroutine(attackCoroutine);
                }

                if (playerController == null)
                {
                    return;
                }

                bool facingRight = playerController.IsFacingRight;
                bool canContinueCombo = CanContinueCombo(facingRight)
                    || IsReservedComboContinuation(facingRight);

                int comboStep = canContinueCombo ? nextComboStep : 1;
                if (!canContinueCombo)
                {
                    ResetComboState();
                    UpdateIdlePose();
                    comboUsesThrustOnly = GetActiveRelicLevel(ThrustingManualRelicId) > 0;
                    hasLatchedComboMode = true;
                }

                // 콤보 도중 유물 스냅샷이 갱신돼도 현재 콤보의 공격 구성이 섞이지 않도록
                // 첫 타에서 정한 모드를 콤보가 끝날 때까지 유지합니다.
                bool thrustOnly = hasLatchedComboMode && comboUsesThrustOnly;
                if (thrustOnly && comboStep > 3)
                {
                    comboStep = 1;
                }

                float speedMultiplier = playerController.PlayerData != null
                    ? Mathf.Max(0.1f, playerController.PlayerData.meleeSpeed)
                    : 1f;

                isHoldingComboPose = false;
                isReturningToIdle = false;
                int comboLength = thrustOnly ? 3 : 5;
                activeComboStep = comboStep;
                activeComboLength = comboLength;

                if (!thrustOnly && comboStep <= 2)
                {
                    attackCoroutine = StartCoroutine(SwingRoutine(
                        comboStep,
                        swingPreparationDuration,
                        finalDamageMultiplier,
                        facingRight));
                }
                else
                {
                    int thrustStep = thrustOnly ? comboStep : comboStep - 2;
                    float thrustDurationMultiplier = thrustStep >= 3
                        ? thirdThrustDurationMultiplier
                        : 1f;
                    float thrustSpeedMultiplier = speedMultiplier * GetThrustSpeedMultiplier();
                    attackCoroutine = StartCoroutine(ShortThrustRoutine(
                        thrustStep,
                        shortThrustDuration * thrustDurationMultiplier / thrustSpeedMultiplier,
                        thrustSpeedMultiplier,
                        thrustDurationMultiplier,
                        finalDamageMultiplier,
                        facingRight,
                        !thrustOnly && comboStep == 3));
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Spear] ExecuteComboAttack Exception: " + e.Message + "\n" + e.StackTrace);
            }
        }

        private void PlaySlashEffect(
            float effectChargePercent,
            RuntimeAnimatorController effectController = null,
            float effectScaleMultiplier = 1f)
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
                    ResetSlashEffect();
                }

                effectCoroutine = StartCoroutine(PlaySlashEffectRoutine(
                    effectChargePercent,
                    effectController,
                    effectScaleMultiplier));
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Spear] PlaySlashEffect Exception: " + e.Message + "\n" + e.StackTrace);
            }
        }

        private IEnumerator PlaySlashEffectRoutine(
            float effectChargePercent,
            RuntimeAnimatorController effectController,
            float effectScaleMultiplier)
        {
            RestoreSlashEffectParent();
            RuntimeAnimatorController controllerToPlay = effectController != null
                ? effectController
                : chargedThrustEffectController;
            if (controllerToPlay != null)
            {
                slashEffectAnimator.runtimeAnimatorController = controllerToPlay;
            }

            Vector3 baseEffectScale = effectOriginalLocalScale * Mathf.Max(0.01f, effectScaleMultiplier);
            slashEffectObject.transform.localScale = Vector3.Lerp(
                baseEffectScale,
                Vector3.one * maxChargedEffectScale,
                Mathf.Clamp01(effectChargePercent));
            slashEffectObject.SetActive(true);

            // 공격 속도와 별개로 설정한 고정 시간 동안 클립 전체 재생
            float safePlaybackDuration = Mathf.Max(0.01f, effectPlaybackDuration);
            float effectClipFrameRate;
            float effectClipDuration = GetEffectClipDuration(out effectClipFrameRate);
            slashEffectAnimator.speed = effectClipDuration > 0f
                ? effectClipDuration / safePlaybackDuration
                : 1f;
            
            // 애니메이션을 강제로 처음부터 재생하고 프레임 즉시 업데이트
            slashEffectAnimator.Play(effectStateName, 0, 0f);
            slashEffectAnimator.Update(0f);

            // 지정한 초기 프레임 동안만 무기를 따라가고, 이후 월드 위치와 방향을 고정
            float followDuration = 0f;
            if (effectFollowFrameCount > 0 && effectClipFrameRate > 0f && slashEffectAnimator.speed > 0f)
            {
                followDuration = effectFollowFrameCount / (effectClipFrameRate * slashEffectAnimator.speed);
                followDuration = Mathf.Min(followDuration, safePlaybackDuration);
                yield return new WaitForSeconds(followDuration);
            }

            slashEffectObject.transform.SetParent(null, true);

            float remainingDuration = safePlaybackDuration + effectFinalFrameHoldDuration - followDuration;
            if (remainingDuration > 0f)
            {
                yield return new WaitForSeconds(remainingDuration);
            }

            ResetSlashEffect();
        }

        private void ResetSlashEffect()
        {
            if (slashEffectAnimator != null)
            {
                slashEffectAnimator.speed = 1f;
                RuntimeAnimatorController controllerToRestore = chargedThrustEffectController != null
                    ? chargedThrustEffectController
                    : originalEffectController;
                if (controllerToRestore != null)
                {
                    slashEffectAnimator.runtimeAnimatorController = controllerToRestore;
                }
            }

            if (slashEffectObject != null)
            {
                slashEffectObject.SetActive(false);
                RestoreSlashEffectParent();
            }

            effectCoroutine = null;
        }

        private void RestoreSlashEffectParent()
        {
            if (slashEffectObject == null || effectOriginalParent == null)
            {
                return;
            }

            Transform effectTransform = slashEffectObject.transform;
            if (effectTransform.parent != effectOriginalParent)
            {
                effectTransform.SetParent(effectOriginalParent, false);
            }

            effectTransform.localPosition = effectOriginalLocalPosition;
            effectTransform.localRotation = effectOriginalLocalRotation;
            effectTransform.localScale = effectOriginalLocalScale;
        }

        private float GetEffectClipDuration(out float clipFrameRate)
        {
            clipFrameRate = 0f;
            if (slashEffectAnimator == null || slashEffectAnimator.runtimeAnimatorController == null)
            {
                return 0f;
            }

            AnimationClip[] clips = slashEffectAnimator.runtimeAnimatorController.animationClips;
            for (int i = 0; i < clips.Length; i++)
            {
                AnimationClip clip = clips[i];
                if (clip != null && clip.name == effectStateName)
                {
                    clipFrameRate = clip.frameRate;
                    return clip.length;
                }
            }

            if (clips.Length > 0 && clips[0] != null)
            {
                clipFrameRate = clips[0].frameRate;
                return clips[0].length;
            }

            return 0f;
        }

        private IEnumerator SwingRoutine(
            int comboStep,
            float currentPreparationDuration,
            float finalDamageMultiplier,
            bool facingRight)
        {
            isAttacking = true;
            spearHitPoolClear();
            DisableHitbox();

            transform.localScale = new Vector3(1f, facingRight ? 1f : -1f, 1f);

            Vector3 idlePosition = GetShoulderIdleOffset(facingRight);
            Vector3 startPosition = comboStep == 2 ? heldComboPosition : idlePosition;
            float startRotation = comboStep == 2 ? heldComboRotation : GetShoulderIdleRotation(facingRight);
            SetSwingPose(startPosition, startRotation, facingRight);

            Vector3 preparationPosition = GetFacingSwingPosition(
                comboStep == 1
                    ? firstSwingPreparationPosition
                    : secondSwingPreparationPosition,
                facingRight);
            float preparationRotation = GetFacingSwingRotation(
                comboStep == 1
                    ? firstSwingPreparationRotation
                    : secondSwingPreparationRotation,
                facingRight);
            Vector3 endPosition = GetFacingSwingPosition(
                comboStep == 1
                    ? firstSwingEndPosition
                    : secondSwingEndPosition,
                facingRight);
            float endRotation = GetFacingSwingRotation(
                comboStep == 1
                    ? firstSwingEndRotation
                    : secondSwingEndRotation,
                facingRight);

            // 기본 자세 또는 1타 종료 자세에서 다음 준비 자세까지는 자연스럽게 연결합니다.
            yield return InterpolateAimPose(
                startPosition,
                preparationPosition,
                startRotation,
                preparationRotation,
                Mathf.Max(0.01f, currentPreparationDuration),
                facingRight);

            // 1타는 기존과 반대인 긴 방향, 2타는 그 반대 방향으로 휘두릅니다.
            float pathEndRotation = comboStep == 1
                ? endRotation + (facingRight ? -360f : 360f)
                : endRotation;
            EnableHitbox();

            float visibleStartRatio = Mathf.Clamp(swingVisibleStartRatio, 0.05f, 0.4f);
            float visibleEndRatio = Mathf.Clamp(swingVisibleEndRatio, 0.05f, 0.4f);
            float finishStartProgress = 1f - visibleEndRatio;

            // 초반은 서서히 가속하고, 지정한 전체 원호를 계속 따라갑니다.
            yield return InterpolateSwingPathSegment(
                preparationPosition,
                endPosition,
                preparationRotation,
                pathEndRotation,
                0f,
                visibleStartRatio,
                swingVisibleStartDuration,
                true,
                false,
                facingRight,
                finalDamageMultiplier);

            // 중앙도 순간이동하지 않고, 매우 짧은 시간 동안 연속된 프레임으로 통과합니다.
            yield return InterpolateSwingPathSegment(
                preparationPosition,
                endPosition,
                preparationRotation,
                pathEndRotation,
                visibleStartRatio,
                finishStartProgress,
                swingFastMiddleDuration,
                false,
                false,
                facingRight,
                finalDamageMultiplier);

            // 종료 직전은 같은 원호 위에서 서서히 감속하며 지정한 종료 자세에 도착합니다.
            yield return InterpolateSwingPathSegment(
                preparationPosition,
                endPosition,
                preparationRotation,
                pathEndRotation,
                finishStartProgress,
                1f,
                swingVisibleEndDuration,
                false,
                true,
                facingRight,
                finalDamageMultiplier);

            DisableHitbox();
            SetSwingPose(endPosition, endRotation, facingRight);

            HoldComboPose(endPosition, endRotation, facingRight);
        }

        private IEnumerator ShortThrustRoutine(
            int thrustStep,
            float currentThrustDuration,
            float speedMultiplier,
            float durationMultiplier,
            float finalDamageMultiplier,
            bool facingRight,
            bool transitionFromSwing)
        {
            isAttacking = true;
            spearHitPoolClear();
            DisableHitbox();

            transform.localScale = new Vector3(1f, facingRight ? 1f : -1f, 1f);

            Vector3 startPosition = transform.localPosition;
            float startRotation = transform.localEulerAngles.z;
            float aimAngle = GetAttackAimAngle(facingRight);
            float attackRotation = GetAttackRotation(aimAngle, facingRight);
            Vector3 thrustDirection = new Vector3(
                Mathf.Cos(aimAngle * Mathf.Deg2Rad),
                Mathf.Sin(aimAngle * Mathf.Deg2Rad),
                0f);
            Vector3 basePosition = GetAlignedThrustBasePosition(facingRight, thrustDirection);
            float pullDistance = GetShortThrustPullDistance(thrustStep);
            Vector3 pullPosition = basePosition - thrustDirection * pullDistance;
            float activeThrustDistance = shortThrustDistance * GetThrustRangeMultiplier();
            Vector3 strikePosition = basePosition + thrustDirection * activeThrustDistance;
            Vector3 aimPosition = thrustStep == 1 ? basePosition : strikePosition;
            float currentAimDuration = transitionFromSwing
                ? swingToThrustTransitionDuration
                : shortThrustAimDuration
                    * durationMultiplier
                    / Mathf.Max(0.1f, speedMultiplier);

            // 일반 콤보의 첫 찌르기는 2타 스윙 종료 자세에서 빠르고 부드럽게 연결합니다.
            // 2~3타는 앞 타격의 전진 위치를 유지한 채 새 공격 방향만 맞춥니다.
            yield return InterpolateAimPose(
                startPosition,
                aimPosition,
                startRotation,
                attackRotation,
                currentAimDuration,
                facingRight);

            currentThrustDuration = Mathf.Max(0.01f, currentThrustDuration);
            float windupDuration = currentThrustDuration * shortThrustWindupRatio;
            float strikeDuration = currentThrustDuration * shortThrustStrikeRatio;

            // 창끝을 공격 방향에 둔 채, 타수가 높을수록 더 멀리 당깁니다.
            yield return InterpolateAimPose(
                aimPosition,
                pullPosition,
                attackRotation,
                attackRotation,
                windupDuration,
                facingRight);

            RuntimeAnimatorController thrustEffectController = thrustStep >= 3
                ? chargedThrustEffectController
                : shortThrustEffectController;
            float thrustEffectScaleMultiplier = thrustStep >= 3
                ? thirdThrustEffectScaleMultiplier
                : shortThrustEffectScaleMultiplier;
            PlaySlashEffect(0f, thrustEffectController, thrustEffectScaleMultiplier);

            EnableHitbox();
            yield return InterpolateSwingPose(
                pullPosition,
                strikePosition,
                attackRotation,
                attackRotation,
                strikeDuration,
                facingRight,
                true,
                finalDamageMultiplier);
            DisableHitbox();

            // 모든 찌르기는 최대 전진 위치를 유지하고, 콤보 대기 시간이 끝나면 기본 자세로 복귀합니다.
            HoldComboPose(strikePosition, attackRotation, facingRight);
        }

        private IEnumerator InterpolateAimPose(
            Vector3 startPosition,
            Vector3 endPosition,
            float startRotation,
            float endRotation,
            float duration,
            bool facingRight)
        {
            bool isAlreadyAtTarget = (endPosition - startPosition).sqrMagnitude <= 0.000001f
                && Mathf.Abs(Mathf.DeltaAngle(startRotation, endRotation)) <= 0.01f;
            if (duration <= 0f || isAlreadyAtTarget)
            {
                SetSwingPose(endPosition, endRotation, facingRight);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                Vector3 position = Vector3.Lerp(startPosition, endPosition, t);
                float rotation = Mathf.LerpAngle(startRotation, endRotation, t);
                SetSwingPose(position, rotation, facingRight);
                yield return null;
            }

            SetSwingPose(endPosition, endRotation, facingRight);
        }

        private float GetShortThrustPullDistance(int thrustStep)
        {
            switch (Mathf.Clamp(thrustStep, 1, 3))
            {
                case 1:
                    return Mathf.Max(0f, shortThrustPullDistances.x);
                case 2:
                    return Mathf.Max(0f, shortThrustPullDistances.y);
                default:
                    return Mathf.Max(0f, shortThrustPullDistances.z);
            }
        }

        private Vector3 GetAlignedThrustBasePosition(bool facingRight, Vector3 thrustDirection)
        {
            Vector3 shoulderPosition = GetShoulderIdleOffset(facingRight);
            if (thrustDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                return shoulderPosition;
            }

            Vector3 normalizedDirection = thrustDirection.normalized;
            float distanceAlongAim = Vector3.Dot(shoulderPosition, normalizedDirection);
            Vector3 alignedPosition = normalizedDirection * distanceAlongAim;
            alignedPosition.z = shoulderPosition.z;
            return alignedPosition;
        }

        private void HoldComboPose(Vector3 position, float rotation, bool facingRight)
        {
            CompleteActiveComboStep();
            heldComboPosition = position;
            heldComboRotation = rotation;
            comboFacingRight = facingRight;
            comboPoseReleaseTime = Time.time + comboHoldDuration;
            isHoldingComboPose = true;
            isReturningToIdle = false;
            isAttacking = false;
            attackCoroutine = null;
        }

        private IEnumerator InterpolateSwingPose(
            Vector3 startPosition,
            Vector3 endPosition,
            float startRotation,
            float endRotation,
            float duration,
            bool facingRight,
            bool checkCollision,
            float finalDamageMultiplier)
        {
            if (duration <= 0f)
            {
                SetSwingPose(endPosition, endRotation, facingRight);
                if (checkCollision)
                {
                    Physics2D.SyncTransforms();
                    CheckManualCollision(finalDamageMultiplier, GetSpearTipDirection(endRotation, facingRight));
                }
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                Vector3 position = Vector3.Lerp(startPosition, endPosition, t);
                // 180도를 넘는 휘두르기 방향을 유지해야 하므로 LerpAngle을 사용하지 않습니다.
                float rotation = Mathf.Lerp(startRotation, endRotation, t);
                SetSwingPose(position, rotation, facingRight);

                if (checkCollision)
                {
                    Physics2D.SyncTransforms();
                    CheckManualCollision(finalDamageMultiplier, GetSpearTipDirection(rotation, facingRight));
                }

                yield return null;
            }

            SetSwingPose(endPosition, endRotation, facingRight);
        }

        private IEnumerator InterpolateSwingPathSegment(
            Vector3 pathStartPosition,
            Vector3 pathEndPosition,
            float pathStartRotation,
            float pathEndRotation,
            float segmentStartProgress,
            float segmentEndProgress,
            float duration,
            bool easeIn,
            bool easeOut,
            bool facingRight,
            float finalDamageMultiplier)
        {
            if (duration <= 0f)
            {
                SetSwingPathPose(
                    pathStartPosition,
                    pathEndPosition,
                    pathStartRotation,
                    pathEndRotation,
                    segmentEndProgress,
                    facingRight,
                    finalDamageMultiplier);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float localProgress = Mathf.Clamp01(elapsed / duration);
                if (easeIn)
                {
                    localProgress *= localProgress;
                }
                else if (easeOut)
                {
                    float remainingProgress = 1f - localProgress;
                    localProgress = 1f - remainingProgress * remainingProgress;
                }

                float pathProgress = Mathf.Lerp(
                    segmentStartProgress,
                    segmentEndProgress,
                    localProgress);
                SetSwingPathPose(
                    pathStartPosition,
                    pathEndPosition,
                    pathStartRotation,
                    pathEndRotation,
                    pathProgress,
                    facingRight,
                    finalDamageMultiplier);
                yield return null;
            }

            SetSwingPathPose(
                pathStartPosition,
                pathEndPosition,
                pathStartRotation,
                pathEndRotation,
                segmentEndProgress,
                facingRight,
                finalDamageMultiplier);
        }

        private void SetSwingPathPose(
            Vector3 pathStartPosition,
            Vector3 pathEndPosition,
            float pathStartRotation,
            float pathEndRotation,
            float progress,
            bool facingRight,
            float finalDamageMultiplier)
        {
            float clampedProgress = Mathf.Clamp01(progress);
            float rotation = Mathf.Lerp(pathStartRotation, pathEndRotation, clampedProgress);
            Vector3 position = EvaluateSwingPathPosition(
                pathStartPosition,
                pathEndPosition,
                pathStartRotation,
                pathEndRotation,
                clampedProgress,
                facingRight);
            SetSwingPose(position, rotation, facingRight);
            Physics2D.SyncTransforms();
            CheckManualCollision(
                finalDamageMultiplier,
                GetSpearTipDirection(rotation, facingRight));
        }

        private Vector3 EvaluateSwingPathPosition(
            Vector3 pathStartPosition,
            Vector3 pathEndPosition,
            float pathStartRotation,
            float pathEndRotation,
            float progress,
            bool facingRight)
        {
            Vector3 orbitCenter = GetFacingSwingPosition(swingOrbitCenterOffset, facingRight);
            Vector3 startOffset = pathStartPosition - orbitCenter;
            Vector3 endOffset = pathEndPosition - orbitCenter;
            float startRadius = startOffset.magnitude;
            float endRadius = endOffset.magnitude;
            if (startRadius <= Mathf.Epsilon || endRadius <= Mathf.Epsilon)
            {
                return Vector3.Lerp(pathStartPosition, pathEndPosition, progress);
            }

            float startOrbitAngle = Mathf.Atan2(startOffset.y, startOffset.x) * Mathf.Rad2Deg;
            float endOrbitAngle = Mathf.Atan2(endOffset.y, endOffset.x) * Mathf.Rad2Deg;
            float orbitAngleDelta = Mathf.DeltaAngle(startOrbitAngle, endOrbitAngle);
            float weaponRotationDelta = pathEndRotation - pathStartRotation;
            if (weaponRotationDelta < 0f && orbitAngleDelta > 0f)
            {
                orbitAngleDelta -= 360f;
            }
            else if (weaponRotationDelta > 0f && orbitAngleDelta < 0f)
            {
                orbitAngleDelta += 360f;
            }

            float orbitAngle = (startOrbitAngle + orbitAngleDelta * progress) * Mathf.Deg2Rad;
            float radius = Mathf.Lerp(startRadius, endRadius, progress);
            Vector3 position = orbitCenter + new Vector3(
                Mathf.Cos(orbitAngle) * radius,
                Mathf.Sin(orbitAngle) * radius,
                0f);
            position.z = Mathf.Lerp(pathStartPosition.z, pathEndPosition.z, progress);
            return position;
        }

        private Vector3 GetFacingSwingPosition(Vector3 rightFacingPosition, bool facingRight)
        {
            if (!facingRight)
            {
                rightFacingPosition.x = -rightFacingPosition.x;
            }

            return rightFacingPosition;
        }

        private float GetAttackAimAngle(bool facingRight)
        {
            float centerAngle = facingRight ? 0f : 180f;
            if (InputManager.Instance == null || Camera.main == null)
            {
                return centerAngle;
            }

            Vector2 mouseScreenPosition = InputManager.Instance.MousePosition;
            Vector3 mouseWorldPosition = Camera.main.ScreenToWorldPoint(
                new Vector3(mouseScreenPosition.x, mouseScreenPosition.y, 0f));
            mouseWorldPosition.z = 0f;

            Vector3 origin = transform.parent != null ? transform.parent.position : transform.position;
            Vector3 targetDirection = mouseWorldPosition - origin;
            if (targetDirection.sqrMagnitude <= Mathf.Epsilon)
            {
                return centerAngle;
            }

            float targetAngle = Mathf.Atan2(targetDirection.y, targetDirection.x) * Mathf.Rad2Deg;
            float angleDifference = Mathf.Clamp(
                Mathf.DeltaAngle(centerAngle, targetAngle),
                -maxAimAngle,
                maxAimAngle);
            return centerAngle + angleDifference;
        }

        private float GetFacingSwingRotation(float rightFacingRotation, bool facingRight)
        {
            return facingRight ? rightFacingRotation : 180f - rightFacingRotation;
        }

        private void SetSwingPose(Vector3 localPosition, float localRotation, bool facingRight)
        {
            transform.localScale = new Vector3(1f, facingRight ? 1f : -1f, 1f);
            transform.localPosition = localPosition;
            transform.localRotation = Quaternion.Euler(0f, 0f, localRotation);
        }

        private Vector3 GetSpearTipDirection(float localRotation, bool facingRight)
        {
            float tipAngle = localRotation + (facingRight ? spriteForwardAngle : -spriteForwardAngle);
            float tipAngleRadians = tipAngle * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(tipAngleRadians), Mathf.Sin(tipAngleRadians), 0f);
        }

        private void UpdateComboHoldPose()
        {
            if (playerController == null)
            {
                return;
            }

            if (playerController.IsFacingRight != comboFacingRight)
            {
                ResetComboState();
                UpdateIdlePose();
                return;
            }

            // 제한 시간 안에 다음 입력을 누르기 시작했다면 버튼을 떼는 순간까지
            // 종료 자세와 콤보 단계를 예약해 둡니다. 릴리즈 프레임으로 콤보가 갈리지 않게 합니다.
            if (isPressing && isComboContinuationPress)
            {
                SetSwingPose(heldComboPosition, heldComboRotation, comboFacingRight);
                return;
            }

            if (Time.time <= comboPoseReleaseTime)
            {
                SetSwingPose(heldComboPosition, heldComboRotation, comboFacingRight);
                return;
            }

            if (!isReturningToIdle)
            {
                // 복귀 동작이 시작되는 즉시 콤보를 만료시켜, 기본 자세에서 찌르기가 이어지지 않게 합니다.
                nextComboStep = 1;
                isReturningToIdle = true;
                comboReturnStartTime = Time.time;
                comboReturnStartPosition = transform.localPosition;
                comboReturnStartRotation = transform.localEulerAngles.z;
            }

            float returnProgress = Mathf.Clamp01(
                (Time.time - comboReturnStartTime) / Mathf.Max(0.01f, comboReturnDuration));
            float easedProgress = Mathf.SmoothStep(0f, 1f, returnProgress);
            Vector3 idlePosition = GetShoulderIdleOffset(comboFacingRight);
            float idleRotation = GetShoulderIdleRotation(comboFacingRight);
            Vector3 currentPosition = Vector3.Lerp(comboReturnStartPosition, idlePosition, easedProgress);
            float currentRotation = Mathf.LerpAngle(comboReturnStartRotation, idleRotation, easedProgress);
            SetSwingPose(currentPosition, currentRotation, comboFacingRight);

            if (returnProgress >= 1f)
            {
                ResetComboState();
            }
        }

        private void ResetComboState()
        {
            nextComboStep = 1;
            activeComboStep = 0;
            activeComboLength = 0;
            hasLatchedComboMode = false;
            comboUsesThrustOnly = false;
            isHoldingComboPose = false;
            isReturningToIdle = false;
        }

        private bool CanContinueCombo(bool facingRight)
        {
            return nextComboStep > 1
                && isHoldingComboPose
                && comboFacingRight == facingRight
                && Time.time <= comboPoseReleaseTime;
        }

        private bool IsReservedComboContinuation(bool facingRight)
        {
            return isComboContinuationPress
                && nextComboStep > 1
                && isHoldingComboPose
                && comboFacingRight == facingRight;
        }

        private void CompleteActiveComboStep()
        {
            if (activeComboStep <= 0 || activeComboLength <= 0)
            {
                nextComboStep = 1;
            }
            else
            {
                nextComboStep = activeComboStep >= activeComboLength
                    ? 1
                    : activeComboStep + 1;
            }

            activeComboStep = 0;
            activeComboLength = 0;
        }

        private IEnumerator ThrustRoutine(
            float aimAngle,
            float currentAttackDuration,
            float activeThrustDistance,
            float speedMultiplier,
            float finalDamageMultiplier,
            float effectChargePercent)
        {
            isAttacking = true;
            spearHitPoolClear(); // 타격했던 대상 캐시 초기화

            // 1. 공격 입력 순간의 방향과 어깨걸이 위치를 고정
            bool facingRight = playerController.IsFacingRight;
            transform.localScale = new Vector3(1f, facingRight ? 1f : -1f, 1f);
            Vector3 thrustDir = new Vector3(Mathf.Cos(aimAngle * Mathf.Deg2Rad), Mathf.Sin(aimAngle * Mathf.Deg2Rad), 0f);
            Vector3 targetIdlePos = GetAlignedThrustBasePosition(facingRight, thrustDir);
            transform.localPosition = targetIdlePos;

            // 2. 어깨에 걸친 현재 각도에서 창끝이 공격 방향을 향할 때까지 먼저 회전
            float startRotationZ = transform.localEulerAngles.z;
            float attackRotationZ = GetAttackRotation(aimAngle, facingRight);
            float currentAimTransitionDuration = aimTransitionDuration / Mathf.Max(0.1f, speedMultiplier);
            float aimElapsed = 0f;

            while (aimElapsed < currentAimTransitionDuration)
            {
                aimElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(aimElapsed / currentAimTransitionDuration);
                t = Mathf.SmoothStep(0f, 1f, t);
                float rotationZ = Mathf.LerpAngle(startRotationZ, attackRotationZ, t);
                transform.localRotation = Quaternion.Euler(0f, 0f, rotationZ);
                yield return null;
            }

            transform.localRotation = Quaternion.Euler(0f, 0f, attackRotationZ);

            // 3. 회전이 끝난 시점부터 공격 이펙트와 판정, 찌르기 동작 시작
            PlaySlashEffect(effectChargePercent);

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
                    CheckManualCollision(finalDamageMultiplier, thrustDir);
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

        private Vector3 GetShoulderIdleOffset(bool facingRight)
        {
            Vector3 leftFacingOffset = idleOffset;
            if (weaponData != null && weaponData.visualPositionOffset != Vector3.zero)
            {
                leftFacingOffset = weaponData.visualPositionOffset;
            }

            // Atlas 데이터는 왼쪽 수평 자세를 기준으로 작성하고 오른쪽은 좌우 대칭 처리
            if (facingRight)
            {
                leftFacingOffset.x = -leftFacingOffset.x;
            }

            return leftFacingOffset;
        }

        private float GetShoulderIdleRotation(bool facingRight)
        {
            float leftFacingRotation = weaponData != null ? weaponData.spriteRotationOffset : 0f;

            // Y축 플립을 사용하는 왼쪽 포즈를 월드 Y축 기준으로 반사한 오른쪽 각도
            return facingRight ? 180f - leftFacingRotation : leftFacingRotation;
        }

        private float GetAttackRotation(float aimAngle, bool facingRight)
        {
            // 좌측에서는 Y축 반전이 적용되므로 스프라이트 전방각 보정의 부호도 반전
            return aimAngle + (facingRight ? -spriteForwardAngle : spriteForwardAngle);
        }

        private void spearHitPoolClear()
        {
            spearHitTargets.Clear();
        }

        private void CheckManualCollision(float finalDamageMultiplier, Vector3 thrustDir)
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
                            Vector3 hitPoint = other.transform.position;
                            ColliderDistance2D dist = col.Distance(other);
                            if (dist.isValid)
                            {
                                hitPoint = (Vector3)dist.pointB;
                            }
                            WeaponVFXHelper.PlayHitEffect(weaponData.hitEffectPrefab, hitPoint, direction: thrustDir);
                        }
                    }
                }
            }
        }

        private bool IsChargingEnabled()
        {
            if (isChargeable) return true;

            string targetRelicId = weaponData != null && !string.IsNullOrEmpty(weaponData.requiredRelicId) ? weaponData.requiredRelicId : "ChargeRelic";

            if (playerController != null)
            {
                PlayerRelicManager playerRelicManager = playerController.GetComponent<PlayerRelicManager>();
                if (playerRelicManager != null && playerRelicManager.IsRelicActive(targetRelicId))
                {
                    return true;
                }
            }

            return false;
        }

        private int GetActiveRelicLevel(string relicId)
        {
            if (playerController == null)
            {
                return 0;
            }

            PlayerRelicManager playerRelicManager = playerController.GetComponent<PlayerRelicManager>();
            return playerRelicManager != null
                ? playerRelicManager.CombatModifiers.GetActiveLevel(relicId)
                : 0;
        }

        private float GetThrustRangeMultiplier()
        {
            int relicLevel = GetActiveRelicLevel(RedSpearPennonRelicId);
            if (relicLevel <= 0)
            {
                return 1f;
            }

            return 1f
                + redSpearPennonRangeBonus
                + redSpearPennonRangeBonusPerLevel * (relicLevel - 1);
        }

        private float GetThrustSpeedMultiplier()
        {
            int relicLevel = GetActiveRelicLevel(ThrustingManualRelicId);
            if (relicLevel <= 0)
            {
                return 1f;
            }

            return 1f
                + thrustingManualSpeedBonus
                + thrustingManualSpeedBonusPerLevel * (relicLevel - 1);
        }

        public override void AttackEnd()
        {
            try
            {
                if (IsChargingEnabled() && isPressing)
                {
                    isPressing = false;

                    if (!isCharging)
                    {
                        // 임계값 미만으로 누르고 뗀 경우 -> 기본 콤보 또는 찌르기 교본의 3연속 찌르기
                        ExecuteComboAttack(1f);
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

                        ExecuteSpearAttack(finalDamageMultiplier, finalThrustDistance, chargePercent);
                        currentChargeTime = 0f;
                    }

                    isComboContinuationPress = false;
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
            }

            isAttacking = false;
            isPressing = false;
            isComboContinuationPress = false;
            isCharging = false;
            currentChargeTime = 0f;
            ResetComboState();
            SetChargingEffect(false);
            ResetChargingMaterial();

            ResetSlashEffect();

            DisableHitbox();
        }
    }
}
