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

        [Header("Dagger Attack Settings")]
        [Tooltip("찌르기 동작 시 앞으로 뻗어나갈 최대 거리")]
        [SerializeField] private float thrustDistance = 0.7f;

        [Tooltip("찌르기 애니메이션 전체 진행 시간(초)")]
        [SerializeField] private float attackDuration = 0.12f;

        [Header("Combo Effect GameObjects")]
        [Tooltip("1타 찌르기용 이펙트 오브젝트")]
        [SerializeField] private GameObject thrustEffectObject;
        [Tooltip("2타 내리베기용 이펙트 오브젝트")]
        [SerializeField] private GameObject swingEffectObject;
        [Tooltip("3타 올려베기용 이펙트 오브젝트")]
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

        private Quaternion idleRotation;
        private Vector3 idleScale;

        [Header("Smooth Settings")]
        [Tooltip("위치 및 회전 보간 속도 (높을수록 빠르게 쫓아갑니다)")]
        [SerializeField] private float smoothSpeed = 25f;

        private Vector3 targetLocalPos;
        private Quaternion targetLocalRot;
        private Vector3 targetLocalScale;

        [Header("Combo Settings")]
        [Tooltip("공격을 이 시간 동안 하지 않으면 콤보가 1타(찌르기)로 리셋됩니다")]
        [SerializeField] private float comboResetTime = 1.0f;
        [Tooltip("휘두르기 동작 시 앞으로 반원을 그리며 뻗어 나갈 거리")]
        [SerializeField] private float swingDistance = 0.5f;

        [Header("Weapon Animator Settings")]
        [Tooltip("무기 자체의 찌르기/휘두르기 애니메이션 재생을 담당하는 Animator")]
        [SerializeField] private Animator weaponAnimator;
        [SerializeField] private string thrustTriggerName = "Thrust";
        [SerializeField] private string swingTriggerName = "Swing";
        [SerializeField] private string swingUpTriggerName = "SwingUp";

        private int attackComboStep = 0;
        private float lastAttackInputTime = 0f;

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
            float elapsedSinceAttack = Time.time - lastAttackTime;

            // 공격 시작 직후 짧은 시간(0.15초) 내에 날아오는 이벤트는
            // 이전 콤보 단계에서 전이 딜레이로 인해 늦게 배달된 찌꺼기 이벤트이므로 무시합니다.
            if (elapsedSinceAttack < 0.15f)
            {
                return;
            }

            isAttacking = false;

            if (weaponAnimator != null)
            {
                weaponAnimator.CrossFade("Idle", 0.1f);
            }

            if (thrustEffectObject != null) thrustEffectObject.SetActive(false);
            if (swingEffectObject != null) swingEffectObject.SetActive(false);
            if (swingUpEffectObject != null) swingUpEffectObject.SetActive(false);

            DisableHitbox();
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

            if (!isAttacking)
            {
                // 공격 중이 아닐 때는 마우스 조준 없이 플레이어가 바라보는 방향(좌/우)에 따라 대칭 타겟만 설정
                Vector3 targetPos = idleOffset;

                if (!facingRight)
                {
                    targetPos.x = -idleOffset.x;
                    targetLocalScale = new Vector3(idleScale.x, -idleScale.y, idleScale.z);
                    // 좌측을 바라볼 때의 회전: Y축을 뒤집었으므로 기본 회전각도 좌우 대칭이 되도록 처리
                    targetLocalRot = Quaternion.Euler(0f, 0f, 180f - idleRotation.eulerAngles.z);
                }
                else
                {
                    targetLocalScale = idleScale;
                    targetLocalRot = idleRotation;
                }

                targetLocalPos = targetPos;
            }

            // 매 프레임 타겟 트랜스폼으로 부드럽게 보간 (Lerp / Slerp)
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetLocalPos, Time.deltaTime * smoothSpeed);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, targetLocalRot, Time.deltaTime * smoothSpeed);
            transform.localScale = Vector3.Lerp(transform.localScale, targetLocalScale, Time.deltaTime * smoothSpeed);
        }

        private void UpdateWeaponAim()
        {
            // 초기 1회만 타겟 위치를 즉시 갱신
            if (playerController != null)
            {
                bool facingRight = playerController.IsFacingRight;
                Vector3 targetPos = idleOffset;
                if (!facingRight)
                {
                    targetPos.x = -idleOffset.x;
                    targetLocalScale = new Vector3(idleScale.x, -idleScale.y, idleScale.z);
                    targetLocalRot = Quaternion.Euler(0f, 0f, 180f - idleRotation.eulerAngles.z);
                }
                else
                {
                    targetLocalScale = idleScale;
                    targetLocalRot = idleRotation;
                }
                targetLocalPos = targetPos;

                // 즉각 대입하여 튀지 않게 초기화
                transform.localPosition = targetLocalPos;
                transform.localRotation = targetLocalRot;
                transform.localScale = targetLocalScale;
            }
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

                // 콤보 타임아웃 검사: 마지막 공격 입력 후 일정 시간이 지났으면 콤보 리셋
                if (Time.time - lastAttackInputTime > comboResetTime)
                {
                    attackComboStep = 0;
                }
                lastAttackInputTime = Time.time;

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

                    // 무기 자체 애니메이터 속도도 플레이어의 공격 속도 스탯과 동기화
                    if (weaponAnimator != null)
                    {
                        weaponAnimator.speed = speedMultiplier;
                    }

                    // 트리거 누적으로 인한 전이 꼬임 방지를 위해 이전 트리거들을 초기화
                    ResetAllWeaponTriggers();

                    // 콤보 단계에 따라 찌르기(1타) -> 내리베기(2타) -> 올려베기(3타) 실행
                    if (attackComboStep == 0)
                    {
                        attackCoroutine = StartCoroutine(ThrustRoutine(finalAimAngle, currentAttackDuration, thrustDistance, speedMultiplier));
                        attackComboStep = 1;
                    }
                    else if (attackComboStep == 1)
                    {
                        attackCoroutine = StartCoroutine(SwingRoutine(finalAimAngle, currentAttackDuration, swingDistance, speedMultiplier));
                        attackComboStep = 2;
                    }
                    else
                    {
                        attackCoroutine = StartCoroutine(SwingUpRoutine(finalAimAngle, currentAttackDuration, swingDistance, speedMultiplier));
                        attackComboStep = 0;
                    }

                    string targetEffectState = thrustEffectStateName;
                    if (currentStep == 1)
                    {
                        targetEffectState = swingEffectStateName;
                    }
                    else if (currentStep == 2)
                    {
                        targetEffectState = swingUpEffectStateName;
                    }

                    PlaySlashEffect(currentAttackDuration, speedMultiplier, currentStep, targetEffectState);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("[ShortDagger] ExecuteDaggerAttack Exception: " + e.Message + "\n" + e.StackTrace);
            }
        }

        private void PlaySlashEffect(float currentAttackDuration, float speedMultiplier, int comboStep, string effectState)
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

                // 타겟 이펙트 활성화 및 위치 초기화
                targetObj.SetActive(true);
                targetObj.transform.localPosition = initialPos;
                targetObj.transform.localRotation = initialRot;
                targetObj.transform.localScale = initialScale;

                targetAnimator.speed = speedMultiplier;
                targetAnimator.Play(effectState, 0, 0f);
                targetAnimator.Update(0f);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[ShortDagger] PlaySlashEffect Exception: " + e.Message + "\n" + e.StackTrace);
            }
        }

        private IEnumerator ThrustRoutine(float aimAngle, float currentAttackDuration, float activeThrustDistance, float speedMultiplier)
        {
            isAttacking = true;
            daggerHitTargets.Clear();

            // 1. 공격 시작 시 타겟 각도 및 스케일 지정
            bool facingRight = playerController.IsFacingRight;
            targetLocalScale = new Vector3(idleScale.x, facingRight ? idleScale.y : -idleScale.y, idleScale.z);
            float finalRotationZ = aimAngle + (facingRight ? -45f : 45f);
            targetLocalRot = Quaternion.Euler(0f, 0f, finalRotationZ);

            Vector3 targetIdlePos = idleOffset;
            if (!facingRight)
            {
                targetIdlePos.x = -idleOffset.x;
            }
            targetLocalPos = targetIdlePos;

            // 2. 무기 애니메이터에 찌르기 상태 즉시 재생
            if (weaponAnimator != null)
            {
                weaponAnimator.Play(thrustTriggerName, 0, 0f);
            }

            float elapsed = 0f;
            EnableHitbox();

            // 공격 진행 루프 (Update에서 매 프레임 타겟으로 보간 처리)
            while (elapsed < currentAttackDuration)
            {
                elapsed += Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / currentAttackDuration);

                // 찌르기 공격 동작 시간 동안 피격 판정 검사 실행
                if (normalizedTime < 0.75f)
                {
                    CheckManualCollision(thrustEffectCollider);
                }
                else
                {
                    DisableHitbox();
                }

                // 위치는 고정 대기 상태를 유지하며, 자식 비주얼의 키프레임 애니메이션에 움직임을 맡깁니다.
                targetLocalPos = targetIdlePos;
                yield return null;
            }

            DisableHitbox();

            // 🌟 예외 방지 안전 타이머: 에디터에서 애니메이션 이벤트를 누락했거나 소실되었을 경우 2.0초 후 강제 해제하여 콤보가 먹통이 되는 것을 방지합니다.
            yield return new WaitForSeconds(2.0f);
            if (isAttacking)
            {
                OnAttackAnimationEnd();
            }
            attackCoroutine = null;
        }

        private IEnumerator SwingRoutine(float aimAngle, float currentAttackDuration, float activeSwingDistance, float speedMultiplier)
        {
            isAttacking = true;
            daggerHitTargets.Clear();

            bool facingRight = playerController.IsFacingRight;

            // 1. 공격 시작 시 마우스 조준 방향으로 루트 회전만 순간 조준
            targetLocalScale = new Vector3(idleScale.x, facingRight ? idleScale.y : -idleScale.y, idleScale.z);
            float finalRotationZ = aimAngle + (facingRight ? -45f : 45f);
            targetLocalRot = Quaternion.Euler(0f, 0f, finalRotationZ);

            Vector3 targetIdlePos = idleOffset;
            if (!facingRight)
            {
                targetIdlePos.x = -idleOffset.x;
            }
            targetLocalPos = targetIdlePos;

            // 2. 무기 애니메이터에 휘두르기 상태 즉시 재생
            if (weaponAnimator != null)
            {
                weaponAnimator.Play(swingTriggerName, 0, 0f);
            }

            float elapsed = 0f;
            EnableHitbox();

            while (elapsed < currentAttackDuration)
            {
                elapsed += Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / currentAttackDuration);

                // 휘두르는 공격 동작 시간 동안 피격 판정 검사 실행
                if (normalizedTime < 0.75f)
                {
                    CheckManualCollision(swingEffectCollider);
                }
                else
                {
                    DisableHitbox();
                }

                targetLocalPos = targetIdlePos;
                yield return null;
            }

            DisableHitbox();

            // 🌟 예외 방지 안전 타이머: 에디터에서 애니메이션 이벤트를 누락했거나 소실되었을 경우 2.0초 후 강제 해제하여 콤보가 먹통이 되는 것을 방지합니다.
            yield return new WaitForSeconds(2.0f);
            if (isAttacking)
            {
                OnAttackAnimationEnd();
            }
            attackCoroutine = null;
        }

        private IEnumerator SwingUpRoutine(float aimAngle, float currentAttackDuration, float activeSwingDistance, float speedMultiplier)
        {
            isAttacking = true;
            daggerHitTargets.Clear();

            bool facingRight = playerController.IsFacingRight;

            // 1. 공격 시작 시 마우스 조준 방향으로 루트 회전만 순간 조준
            targetLocalScale = new Vector3(idleScale.x, facingRight ? idleScale.y : -idleScale.y, idleScale.z);
            float finalRotationZ = aimAngle + (facingRight ? -45f : 45f);
            targetLocalRot = Quaternion.Euler(0f, 0f, finalRotationZ);

            Vector3 targetIdlePos = idleOffset;
            if (!facingRight)
            {
                targetIdlePos.x = -idleOffset.x;
            }
            targetLocalPos = targetIdlePos;

            // 2. 무기 애니메이터에 3타 올려베기 상태 즉시 재생
            if (weaponAnimator != null)
            {
                weaponAnimator.Play(swingUpTriggerName, 0, 0f);
            }

            float elapsed = 0f;
            EnableHitbox();

            while (elapsed < currentAttackDuration)
            {
                elapsed += Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / currentAttackDuration);

                // 올려베는 공격 동작 시간 동안 피격 판정 검사 실행
                if (normalizedTime < 0.75f)
                {
                    CheckManualCollision(swingUpEffectCollider);
                }
                else
                {
                    DisableHitbox();
                }

                targetLocalPos = targetIdlePos;
                yield return null;
            }

            DisableHitbox();

            // 🌟 예외 방지 안전 타이머
            yield return new WaitForSeconds(2.0f);
            if (isAttacking)
            {
                OnAttackAnimationEnd();
            }
            attackCoroutine = null;
        }

        private void CheckManualCollision(Collider2D targetCol)
        {
            if (targetCol == null) targetCol = col;
            if (targetCol == null) return;

            ContactFilter2D filter = new ContactFilter2D();
            filter.useTriggers = true;

            Collider2D[] results = new Collider2D[15];
            int count = targetCol.OverlapCollider(filter, results);

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
                    if (target != null && !daggerHitTargets.Contains(target))
                    {
                        daggerHitTargets.Add(target);
                        
                        float baseDmg = weaponData != null ? weaponData.damage : 8f;
                        target.TakeDamage(baseDmg);
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
