using System.Collections;
using UnityEngine;
using Nytherion.Core.Managers;
using Nytherion.GamePlay.Characters.Player;

namespace Nytherion.GamePlay.Combat.Weapons
{
    public class ShortDagger : MeleeWeapon
    {
        [Header("Dagger Aim Settings")]
        [Tooltip("플레이어 기준 위아래 최대 조준 각도 (부채꼴의 절반 크기)")]
        [SerializeField] private float maxAimAngle = 60f;

        [Tooltip("대기 상태에서 플레이어 중심 대비 무기의 오프셋")]
        [SerializeField] private Vector3 idleOffset = new Vector3(0.15f, -0.05f, 0f);

        [Tooltip("왼쪽 수평 조준 시 무기 중심 위치. 오른쪽에서는 X축으로 대칭됩니다")]
        [SerializeField] private Vector3 leftFacingOffset = new Vector3(0.328f, -0.284f, 0f);
        [Tooltip("왼쪽 수평 조준 시 루트 회전각")]
        [SerializeField] private float leftFacingRotation = 23.5f;
        [Tooltip("Frostbite 스프라이트 중심에서 손잡이까지의 로컬 위치")]
        [SerializeField] private Vector3 gripOffset = new Vector3(0f, -0.625f, 0f);

        [Header("Dagger Attack Settings")]
        [Tooltip("찌르기 동작 시 앞으로 뻗어나갈 최대 거리")]
        [SerializeField] private float thrustDistance = 0.7f;

        [Tooltip("준비 동작과 검기 재생을 포함한 한 번의 공격 시간(초)")]
        [SerializeField] private float attackDuration = 0.12f;

        [Header("Instant Swing Settings")]
        [Tooltip("공격 시간 중 준비 동작에 사용하는 비율")]
        [SerializeField, Range(0.05f, 0.4f)] private float windupRatio = 0.2f;
        [Tooltip("첫 베기를 준비할 때 칼끝을 위로 들어 올리는 각도")]
        [SerializeField] private float swingWindupAngle = 15f;
        [Tooltip("준비 자세에서 타격 종료까지 검기로 표현하는 회전각")]
        [SerializeField, Range(1f, 360f)] private float swingSweepAngle = 340f;
        [Tooltip("연속 베기를 준비할 때 종료 자세에서 추가로 젖히는 각도")]
        [SerializeField, Min(0f)] private float followupWindupAngle = 15f;
        [Tooltip("콤보 연결 시간이 지난 뒤 대기 자세로 복귀하는 시간")]
        [SerializeField, Min(0.01f)] private float comboReturnDuration = 0.14f;

        [Header("Circular Slash Effect")]
        [SerializeField] private Material swingArcMaterial;
        [SerializeField, Min(0.1f)] private float swingArcRadius = 1.5f;
        [SerializeField, Min(0.01f)] private float swingArcWidth = 0.18f;
        [SerializeField] private Color swingArcColor = new Color(0.6f, 1f, 0.95f, 0.9f);

        [Header("Combo Effect GameObjects")]
        [Tooltip("1타 찌르기용 이펙트 오브젝트")]
        [SerializeField] private GameObject thrustEffectObject;
        [Tooltip("내리베기용 이펙트 오브젝트")]
        [SerializeField] private GameObject swingEffectObject;
        [Tooltip("올려베기용 이펙트 오브젝트")]
        [SerializeField] private GameObject swingUpEffectObject;

        private Animator thrustEffectAnimator;
        private Animator swingEffectAnimator;
        private Animator swingUpEffectAnimator;

        private Collider2D thrustEffectCollider;
        private Collider2D swingEffectCollider;
        private Collider2D swingUpEffectCollider;

        private Vector3 thrustEffectInitialPos;
        private Quaternion thrustEffectInitialRot;
        private Vector3 thrustEffectInitialScale;

        private Vector3 swingEffectInitialPos;
        private Quaternion swingEffectInitialRot;
        private Vector3 swingEffectInitialScale;

        private Vector3 swingUpEffectInitialPos;
        private Quaternion swingUpEffectInitialRot;
        private Vector3 swingUpEffectInitialScale;

        [Header("Combo Effect States")]
        [Tooltip("1타 찌르기 시 실행할 이펙트 애니메이션 State 이름")]
        [SerializeField] private string thrustEffectStateName = "AttackEffect";
        [Tooltip("2타 내리베기 시 실행할 이펙트 애니메이션 State 이름")]
        [SerializeField] private string swingEffectStateName = "SwingEffect";
        [Tooltip("3타 올려베기 시 실행할 이펙트 애니메이션 State 이름")]
        [SerializeField] private string swingUpEffectStateName = "SwingUpEffect";

        // 무기 방향 설정을 강제하기 위해 true로 재정의
        public override bool OverrideRotation => true;

        private PlayerController playerController;
        private SpriteRenderer spriteRenderer;
        private Coroutine attackCoroutine;
        private bool isAttacking = false;
        private Vector3 visualRestPosition;
        private Quaternion visualRestRotation;
        private bool weaponAnimatorWasEnabled;
        private bool hasVisualRestPose;

        private Quaternion idleRotation;
        private Vector3 idleScale;

        [Header("Smooth Settings")]
        [Tooltip("위치 및 회전 보간 속도 (높을수록 빠르게 쫓아갑니다)")]
        [SerializeField] private float smoothSpeed = 25f;

        private Vector3 targetLocalPos;
        private Quaternion targetLocalRot;
        private Vector3 targetLocalScale;

        [Header("Combo Settings")]
        [Tooltip("공격 종료 자세를 유지하는 시간. 이 안에 다시 공격하면 반대 방향으로 벱니다")]
        [SerializeField] private float comboResetTime = 1.0f;
        [Tooltip("휘두르기 동작 시 앞으로 반원을 그리며 뻗어 나갈 거리")]
        [SerializeField] private float swingDistance = 0.5f;

        [Header("Weapon Animator Settings")]
        [Tooltip("무기 자체의 찌르기/휘두르기 애니메이션 재생을 담당하는 Animator")]
        [SerializeField] private Animator weaponAnimator;
        [SerializeField] private string thrustTriggerName = "Thrust";
        [SerializeField] private string swingTriggerName = "Swing";
        [SerializeField] private string swingUpTriggerName = "SwingUp";

        private int attackComboStep = 1;
        private bool isHoldingComboPose;
        private float comboPoseReleaseTime;
        private float currentSwingAngle;
        private float heldSwingAngle;
        private Vector3 localGripPosition;
        private LineRenderer swingArc;
        private Material runtimeArcMaterial;

        // 물리 프레임 업데이트 주기 문제로 인한 트리거 누락을 방지하기 위한 수동 충돌 타겟 관리
        private System.Collections.Generic.HashSet<Nytherion.Core.Interfaces.IDamageable> daggerHitTargets = new System.Collections.Generic.HashSet<Nytherion.Core.Interfaces.IDamageable>();

        public override bool CanAttack()
        {
            if (isAttacking)
            {
                return false;
            }

            if (weaponData == null)
            {
                return true;
            }

            return base.CanAttack();
        }

        private void InitializeEffectObjects()
        {
            if (thrustEffectObject != null)
            {
                thrustEffectAnimator = thrustEffectObject.GetComponent<Animator>();
                if (thrustEffectAnimator == null) thrustEffectAnimator = thrustEffectObject.GetComponentInChildren<Animator>();
                if (thrustEffectAnimator != null) thrustEffectAnimator.keepAnimatorStateOnDisable = false;
                thrustEffectCollider = thrustEffectObject.GetComponent<Collider2D>();
                thrustEffectObject.SetActive(false);
            }
            if (swingEffectObject != null)
            {
                swingEffectAnimator = swingEffectObject.GetComponent<Animator>();
                if (swingEffectAnimator == null) swingEffectAnimator = swingEffectObject.GetComponentInChildren<Animator>();
                if (swingEffectAnimator != null) swingEffectAnimator.keepAnimatorStateOnDisable = false;
                swingEffectCollider = swingEffectObject.GetComponent<Collider2D>();
                swingEffectObject.SetActive(false);
            }
            if (swingUpEffectObject != null)
            {
                swingUpEffectAnimator = swingUpEffectObject.GetComponent<Animator>();
                if (swingUpEffectAnimator == null) swingUpEffectAnimator = swingUpEffectObject.GetComponentInChildren<Animator>();
                if (swingUpEffectAnimator != null) swingUpEffectAnimator.keepAnimatorStateOnDisable = false;
                swingUpEffectCollider = swingUpEffectObject.GetComponent<Collider2D>();
                swingUpEffectObject.SetActive(false);
            }
        }

        private void OnDisable()
        {
            if (attackCoroutine != null)
            {
                StopCoroutine(attackCoroutine);
                attackCoroutine = null;
            }

            isAttacking = false;
            isHoldingComboPose = false;
            attackComboStep = 1;
            currentSwingAngle = 0f;
            if (swingArc != null) swingArc.enabled = false;

            RestoreWeaponVisual();

            if (thrustEffectObject != null) thrustEffectObject.SetActive(false);
            if (swingEffectObject != null) swingEffectObject.SetActive(false);
            if (swingUpEffectObject != null) swingUpEffectObject.SetActive(false);

            DisableHitbox();
        }

        /// <summary>
        /// 유니티 애니메이션 클립 끝에서 호출할 이벤트 수신 메서드
        /// </summary>
        public void OnAttackAnimationEnd()
        {
            // 빠른 공격의 종료는 코루틴이 담당하며, 이전 애니메이션 이벤트는 무시한다.
        }

        public override void EnableHitbox()
        {
            base.EnableHitbox();
            if (thrustEffectCollider != null) thrustEffectCollider.enabled = true;
            if (swingEffectCollider != null) swingEffectCollider.enabled = true;
            if (swingUpEffectCollider != null) swingUpEffectCollider.enabled = true;
        }

        public override void DisableHitbox()
        {
            base.DisableHitbox();
            if (thrustEffectCollider != null) thrustEffectCollider.enabled = false;
            if (swingEffectCollider != null) swingEffectCollider.enabled = false;
            if (swingUpEffectCollider != null) swingUpEffectCollider.enabled = false;
            ResetHitTargets();
        }

        public override void Start()
        {
            DisableHitbox();
            
            WeaponAniRelay weaponAniRelay = GetComponentInParent<WeaponAniRelay>();
            if (weaponAniRelay != null)
            {
                weaponAniRelay.currentWeapon = this;
            }

            playerController = GetComponentInParent<PlayerController>();

            if (weaponAnimator == null)
            {
                weaponAnimator = GetComponent<Animator>();
                if (weaponAnimator == null)
                {
                    weaponAnimator = GetComponentInChildren<Animator>();
                }
            }
            
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (weaponAnimator != null)
            {
                visualRestPosition = weaponAnimator.transform.localPosition;
                visualRestRotation = weaponAnimator.transform.localRotation;
                weaponAnimatorWasEnabled = weaponAnimator.enabled;
                hasVisualRestPose = true;
                localGripPosition = visualRestPosition + visualRestRotation * gripOffset;
            }

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
                col.isTrigger = true;
            }

            DisableHitbox();

            // 에디터 혹은 PlayerCombat에서 설정해 준 초기의 localPosition과 localRotation, localScale을 idle 상태용으로 캐싱합니다.
            idleOffset = transform.localPosition;
            idleRotation = transform.localRotation;
            idleScale = transform.localScale;

            // 초기 타겟 설정
            targetLocalPos = idleOffset;
            targetLocalRot = idleRotation;
            targetLocalScale = idleScale;

            // 자식 이펙트 오브젝트 자동 캐싱 및 초기화
            InitializeEffectObjects();

            // 각 이펙트의 초기 로컬 트랜스폼 값을 캐싱합니다.
            if (thrustEffectObject != null)
            {
                thrustEffectInitialPos = thrustEffectObject.transform.localPosition;
                thrustEffectInitialRot = thrustEffectObject.transform.localRotation;
                thrustEffectInitialScale = thrustEffectObject.transform.localScale;
            }
            if (swingEffectObject != null)
            {
                swingEffectInitialPos = swingEffectObject.transform.localPosition;
                swingEffectInitialRot = swingEffectObject.transform.localRotation;
                swingEffectInitialScale = swingEffectObject.transform.localScale;
            }
            if (swingUpEffectObject != null)
            {
                swingUpEffectInitialPos = swingUpEffectObject.transform.localPosition;
                swingUpEffectInitialRot = swingUpEffectObject.transform.localRotation;
                swingUpEffectInitialScale = swingUpEffectObject.transform.localScale;
            }

            // 초기 위치 및 회전 설정
            UpdateWeaponAim();
        }

        private void Update()
        {
            if (playerController == null) return;

            bool facingRight = playerController.IsFacingRight;

            // 타격 중에는 조준 방향을 고정하여 이펙트와 판정이 함께 유지되도록 한다.
            if (isAttacking) return;

            if (isHoldingComboPose)
            {
                // 콤보 대기 중에는 루트 조준도 고정하여 종료 위치를 그대로 유지한다.
                if (Time.time <= comboPoseReleaseTime) return;

                float progress = Mathf.Clamp01((Time.time - comboPoseReleaseTime) / comboReturnDuration);
                // 340도 누적값 그대로 되감지 않고 가장 가까운 대기 각도로 복귀한다.
                SetSwingPose(Mathf.LerpAngle(heldSwingAngle, 0f, Mathf.SmoothStep(0f, 1f, progress)));
                if (progress < 1f) return;

                isHoldingComboPose = false;
                attackComboStep = 1;
                currentSwingAngle = 0f;
                RestoreWeaponVisual();
            }

            SetAimTargets(facingRight, GetAimAngle(facingRight));

            // 매 프레임 타겟 트랜스폼으로 부드럽게 보간 (Lerp / Slerp)
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetLocalPos, Time.deltaTime * smoothSpeed);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, targetLocalRot, Time.deltaTime * smoothSpeed);
            transform.localScale = Vector3.Lerp(transform.localScale, targetLocalScale, Time.deltaTime * smoothSpeed);
        }

        private float GetAimAngle(bool facingRight)
        {
            float centerAngle = facingRight ? 0f : 180f;
            if (InputManager.Instance == null || Camera.main == null) return centerAngle;

            Vector2 mouse = InputManager.Instance.MousePosition;
            Vector3 target = Camera.main.ScreenToWorldPoint(new Vector3(mouse.x, mouse.y, 0f));
            Vector3 origin = transform.parent != null ? transform.parent.position : transform.position;
            Vector2 direction = target - origin;
            if (direction.sqrMagnitude < 0.0001f) return centerAngle;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            return centerAngle + Mathf.Clamp(Mathf.DeltaAngle(centerAngle, angle), -maxAimAngle, maxAimAngle);
        }

        private void SetAimTargets(bool facingRight, float aimAngle)
        {
            targetLocalPos = leftFacingOffset;
            if (facingRight) targetLocalPos.x = -targetLocalPos.x;
            targetLocalScale = new Vector3(Mathf.Abs(idleScale.x),
                facingRight ? Mathf.Abs(idleScale.y) : -Mathf.Abs(idleScale.y), idleScale.z);
            float angle = facingRight
                ? 180f - leftFacingRotation + aimAngle
                : leftFacingRotation + aimAngle - 180f;
            targetLocalRot = Quaternion.Euler(0f, 0f, angle);
        }

        private void UpdateWeaponAim()
        {
            if (playerController == null) return;
            bool facingRight = playerController.IsFacingRight;
            SetAimTargets(facingRight, GetAimAngle(facingRight));
            transform.localPosition = targetLocalPos;
            transform.localRotation = targetLocalRot;
            transform.localScale = targetLocalScale;
        }
        public override void Attack(Vector2 direction, Vector3 targetPosition = default)
        {
            try
            {
                if (!CanAttack())
                {
                    return;
                }

                ExecuteDaggerAttack();
            }
            catch (System.Exception e)
            {
                Debug.LogError("[ShortDagger] Attack Exception: " + e.Message + "\n" + e.StackTrace);
            }
        }

        private void ExecuteDaggerAttack()
        {
            try
            {
                lastAttackTime = Time.time;

                if (attackCoroutine != null)
                {
                    StopCoroutine(attackCoroutine);
                }

                bool canContinueCombo = isHoldingComboPose && Time.time <= comboPoseReleaseTime;
                if (!canContinueCombo)
                {
                    attackComboStep = 1;
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

                    float speedMultiplier = 1f;
                    if (playerController != null && playerController.PlayerData != null)
                    {
                        speedMultiplier = Mathf.Max(0.1f, playerController.PlayerData.meleeSpeed);
                    }

                    float currentAttackDuration = attackDuration / speedMultiplier;

                    int currentStep = attackComboStep;

                    // 트리거 누적으로 인한 전이 꼬임 방지를 위해 이전 트리거들을 초기화
                    ResetAllWeaponTriggers();

                    // 찌르기를 제외하고 내리베기와 올려베기를 번갈아 연결한다.
                    attackComboStep = currentStep == 1 ? 2 : 1;
                    isHoldingComboPose = false;
                    attackCoroutine = StartCoroutine(QuickAttackRoutine(
                        finalAimAngle, currentAttackDuration, speedMultiplier, currentStep, canContinueCombo));
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("[ShortDagger] ExecuteDaggerAttack Exception: " + e.Message + "\n" + e.StackTrace);
            }
        }

        private void PlaySlashEffect(float currentAttackDuration, float speedMultiplier, int comboStep,
            string effectState, float aimAngle)
        {
            try
            {
                GameObject targetObj = null;
                Animator targetAnimator = null;
                Vector3 initialPos = Vector3.zero;
                Quaternion initialRot = Quaternion.identity;
                Vector3 initialScale = Vector3.one;

                if (comboStep == 0)
                {
                    targetObj = thrustEffectObject;
                    targetAnimator = thrustEffectAnimator;
                    initialPos = thrustEffectInitialPos;
                    initialRot = thrustEffectInitialRot;
                    initialScale = thrustEffectInitialScale;
                }
                else if (comboStep == 1)
                {
                    targetObj = swingEffectObject;
                    targetAnimator = swingEffectAnimator;
                    initialPos = swingEffectInitialPos;
                    initialRot = swingEffectInitialRot;
                    initialScale = swingEffectInitialScale;
                }
                else if (comboStep == 2)
                {
                    targetObj = swingUpEffectObject;
                    targetAnimator = swingUpEffectAnimator;
                    initialPos = swingUpEffectInitialPos;
                    initialRot = swingUpEffectInitialRot;
                    initialScale = swingUpEffectInitialScale;
                }

                if (targetObj == null || targetAnimator == null)
                {
                    return;
                }

                // 기존 다른 이펙트들이 켜진 상태로 방치되는 것 방지
                if (thrustEffectObject != null) thrustEffectObject.SetActive(false);
                if (swingEffectObject != null) swingEffectObject.SetActive(false);
                if (swingUpEffectObject != null) swingUpEffectObject.SetActive(false);

                // 이펙트의 원본 전방은 로컬 45도다. 무기의 대기 각도 및 Y 반전과
                // 독립적으로 실제 공격 방향을 로컬 좌표로 변환해 위치와 회전을 함께 보정한다.
                float aimRadians = aimAngle * Mathf.Deg2Rad;
                Vector3 worldAttackDirection = new Vector3(Mathf.Cos(aimRadians), Mathf.Sin(aimRadians), 0f);
                Vector3 localAttackDirection = transform.InverseTransformVector(worldAttackDirection);
                float localAimAngle = Mathf.Atan2(localAttackDirection.y, localAttackDirection.x) * Mathf.Rad2Deg;
                Quaternion effectAimRotation = Quaternion.Euler(0f, 0f, localAimAngle - 45f);

                // 손잡이를 기준으로 기존 이펙트의 거리와 크기는 유지한다.
                targetObj.SetActive(true);
                targetObj.transform.localPosition = localGripPosition + effectAimRotation * initialPos;
                targetObj.transform.localRotation = effectAimRotation * initialRot;
                targetObj.transform.localScale = initialScale;

                targetAnimator.speed = speedMultiplier;
                targetAnimator.Play(effectState, 0, 0f);
                targetAnimator.Update(0f);

                // 생략한 스윙 궤적을 검기로 표현하고 공격 종료 시점에 재생을 마친다.
                AnimatorClipInfo[] clips = targetAnimator.GetCurrentAnimatorClipInfo(0);
                if (clips.Length > 0 && clips[0].clip != null)
                {
                    targetAnimator.speed = clips[0].clip.length / Mathf.Max(0.01f, currentAttackDuration);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("[ShortDagger] PlaySlashEffect Exception: " + e.Message + "\n" + e.StackTrace);
            }
        }

        private IEnumerator QuickAttackRoutine(float aimAngle, float duration, float speedMultiplier,
            int comboStep, bool isFollowup)
        {
            isAttacking = true;
            daggerHitTargets.Clear();
            DisableHitbox();

            bool facingRight = playerController.IsFacingRight;
            SetAimTargets(facingRight, aimAngle);
            transform.localPosition = targetLocalPos;
            transform.localRotation = targetLocalRot;
            transform.localScale = targetLocalScale;

            // 준비 동작만 보간하고 타격 순간에는 중간 회전 없이 종료 자세를 적용한다.
            if (weaponAnimator != null) weaponAnimator.enabled = false;
            duration = Mathf.Max(0.01f, duration);
            float windupEnd = duration * windupRatio;
            float hitboxEnd = duration * 0.7f;
            float startAngle = currentSwingAngle;
            // 왼쪽의 Y 반전까지 고려하면 첫 음수 회전은 칼끝이 위로 향하는 방향이다.
            float swingSign = comboStep == 1 ? -1f : 1f;
            float windupAngle = startAngle + swingSign * (isFollowup ? followupWindupAngle : swingWindupAngle);
            float sweepAngle = swingSign * swingSweepAngle;
            float endAngle = windupAngle + sweepAngle;
            float arcStartAngle = visualRestRotation.eulerAngles.z + 90f + windupAngle;
            float elapsed = 0f;
            bool hasStruck = false;
            string effectState = comboStep == 1 ? swingEffectStateName : swingUpEffectStateName;

            while (true)
            {
                SetSwingPose(elapsed < windupEnd
                    ? Mathf.Lerp(startAngle, windupAngle, Mathf.SmoothStep(0f, 1f, elapsed / windupEnd))
                    : endAngle);
                if (!hasStruck && elapsed >= windupEnd)
                {
                    hasStruck = true;
                    PlaySlashEffect(duration - windupEnd, speedMultiplier, comboStep, effectState, aimAngle);
                    ShowSwingArc(arcStartAngle, sweepAngle);
                    // 기존 박스 판정 대신 원호와 같은 범위를 한 번씩만 타격한다.
                    DisableHitbox();
                    Physics2D.SyncTransforms();
                    CheckSwingArcCollision(arcStartAngle, sweepAngle);
                }
                else if (hasStruck && elapsed < hitboxEnd)
                {
                    CheckSwingArcCollision(arcStartAngle, sweepAngle);
                }

                if (hasStruck) FadeSwingArc(Mathf.InverseLerp(windupEnd, duration, elapsed));

                if (elapsed >= hitboxEnd) DisableHitbox();
                if (elapsed >= duration) break;
                yield return null;
                elapsed += Time.deltaTime;
            }

            if (thrustEffectObject != null) thrustEffectObject.SetActive(false);
            if (swingEffectObject != null) swingEffectObject.SetActive(false);
            if (swingUpEffectObject != null) swingUpEffectObject.SetActive(false);
            if (swingArc != null) swingArc.enabled = false;
            // 판정과 이펙트는 끝내되 무기는 종료 위치에 남겨 다음 반대 베기를 기다린다.
            heldSwingAngle = endAngle;
            SetSwingPose(heldSwingAngle);
            comboPoseReleaseTime = Time.time + comboResetTime;
            isHoldingComboPose = true;
            isAttacking = false;
            attackCoroutine = null;
        }

        private void SetSwingPose(float angle)
        {
            currentSwingAngle = angle;
            if (weaponAnimator == null) return;

            Quaternion swingRotation = Quaternion.Euler(0f, 0f, angle);
            weaponAnimator.transform.localPosition = localGripPosition
                + swingRotation * (visualRestPosition - localGripPosition);
            weaponAnimator.transform.localRotation = swingRotation * visualRestRotation;
        }

        private void RestoreWeaponVisual()
        {
            if (weaponAnimator == null || !hasVisualRestPose) return;
            weaponAnimator.transform.localPosition = visualRestPosition;
            weaponAnimator.transform.localRotation = visualRestRotation;
            weaponAnimator.enabled = weaponAnimatorWasEnabled;
            if (weaponAnimator.isActiveAndEnabled) weaponAnimator.Play("Idle", 0, 0f);
        }

        private void ShowSwingArc(float startAngle, float sweepAngle)
        {
            if (swingArc == null)
            {
                if (swingArcMaterial == null) return;
                var arcObject = new GameObject("GustSwingArc");
                arcObject.transform.SetParent(transform, false);
                swingArc = arcObject.AddComponent<LineRenderer>();
                swingArc.useWorldSpace = false;
                swingArc.loop = false;
                swingArc.numCornerVertices = 2;
                swingArc.numCapVertices = 3;
                swingArc.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                swingArc.receiveShadows = false;
                // 기존 파티클 재질의 셰이더를 재사용하되 공유 에셋은 수정하지 않는다.
                runtimeArcMaterial = new Material(swingArcMaterial);
                runtimeArcMaterial.SetTexture("_BaseMap", Texture2D.whiteTexture);
                runtimeArcMaterial.SetTexture("_MainTex", Texture2D.whiteTexture);
                runtimeArcMaterial.SetFloat("_Cull", 0f);
                swingArc.sharedMaterial = runtimeArcMaterial;
                swingArc.widthCurve = new AnimationCurve(
                    new Keyframe(0f, 0.1f), new Keyframe(0.15f, 1f),
                    new Keyframe(0.85f, 0.8f), new Keyframe(1f, 0f));
            }

            // Quaternion의 최단 회전을 사용하지 않고 부호 있는 340도 전체를 그린다.
            int segments = Mathf.CeilToInt(Mathf.Abs(sweepAngle) / 5f);
            swingArc.positionCount = segments + 1;
            swingArc.widthMultiplier = swingArcWidth;
            for (int i = 0; i <= segments; i++)
            {
                float angle = (startAngle + sweepAngle * i / segments) * Mathf.Deg2Rad;
                swingArc.SetPosition(i, localGripPosition
                    + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * swingArcRadius);
            }
            if (spriteRenderer != null)
            {
                swingArc.sortingLayerID = spriteRenderer.sortingLayerID;
                swingArc.sortingOrder = spriteRenderer.sortingOrder + 1;
            }
            FadeSwingArc(0f);
            swingArc.enabled = true;
        }

        private void FadeSwingArc(float progress)
        {
            if (swingArc == null) return;
            Color color = swingArcColor;
            color.a *= 1f - Mathf.Clamp01(progress);
            swingArc.endColor = color;
            color.a *= 0.35f;
            swingArc.startColor = color;
        }

        private void CheckSwingArcCollision(float startAngle, float sweepAngle)
        {
            Vector3 origin = transform.TransformPoint(localGripPosition);
            float scale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
            Collider2D[] results = Physics2D.OverlapCircleAll(origin, swingArcRadius * scale);
            foreach (Collider2D other in results)
            {
                if (other == null || !other.CompareTag("Enemy")) continue;
                Vector3 closest = other.ClosestPoint(origin);
                Vector2 localDirection = transform.InverseTransformPoint(closest) - localGripPosition;
                if (localDirection.sqrMagnitude > 0.0001f)
                {
                    float angle = Mathf.Atan2(localDirection.y, localDirection.x) * Mathf.Rad2Deg;
                    float swept = Mathf.Repeat((angle - startAngle) * Mathf.Sign(sweepAngle), 360f);
                    if (swept > Mathf.Abs(sweepAngle)) continue;
                }

                var target = other.GetComponent<Nytherion.Core.Interfaces.IDamageable>();
                if (target == null || !daggerHitTargets.Add(target)) continue;
                float damage = weaponData != null ? weaponData.damage * EffectiveDamageMultiplier : 8f;
                target.TakeDamage(damage);
                ApplyStatusEffects(target);
                if (weaponData != null)
                {
                    WeaponVFXHelper.PlayHitEffect(weaponData.hitEffectPrefab, other.transform.position);
                }
            }
        }

        private void OnDestroy()
        {
            if (runtimeArcMaterial != null) Destroy(runtimeArcMaterial);
        }
        public override void AttackEnd()
        {
        }

        private void ResetAllWeaponTriggers()
        {
            if (weaponAnimator == null) return;
            
            weaponAnimator.ResetTrigger(thrustTriggerName);
            weaponAnimator.ResetTrigger(swingTriggerName);
            weaponAnimator.ResetTrigger(swingUpTriggerName);
        }

        private bool HasParameter(Animator animatorComp, string paramName)
        {
            if (animatorComp == null || string.IsNullOrEmpty(paramName)) return false;
            foreach (AnimatorControllerParameter param in animatorComp.parameters)
            {
                if (param.name == paramName)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
