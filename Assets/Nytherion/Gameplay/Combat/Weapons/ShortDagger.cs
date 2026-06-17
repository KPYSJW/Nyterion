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

        [Tooltip("최대 거리에 도달하는 타이밍 비율 (0.0 ~ 1.0)")]
        [SerializeField] private float peakTimePercent = 0.3f;

        [Header("Dagger Effect Settings")]
        [Tooltip("공격 시 활성화할 자식 이펙트 오브젝트")]
        [SerializeField] private GameObject slashEffectObject;

        [Tooltip("이펙트 오브젝트의 Animator")]
        [SerializeField] private Animator slashEffectAnimator;

        [Tooltip("실행할 이펙트 애니메이션의 State 이름")]
        [SerializeField] private string effectStateName = "AttackEffect";

        // 무기 방향 설정을 강제하기 위해 true로 재정의
        public override bool OverrideRotation => true;

        private PlayerController playerController;
        private SpriteRenderer spriteRenderer;
        private Coroutine attackCoroutine;
        private Coroutine effectCoroutine;
        private bool isAttacking = false;

        private Quaternion idleRotation;
        private Vector3 idleScale;

        private Vector3 effectInitialLocalPos;
        private Quaternion effectInitialLocalRot;
        private Vector3 effectInitialLocalScale;

        [Header("Smooth Settings")]
        [Tooltip("위치 및 회전 보간 속도 (높을수록 빠르게 쫓아갑니다)")]
        [SerializeField] private float smoothSpeed = 25f;

        private Vector3 targetLocalPos;
        private Quaternion targetLocalRot;
        private Vector3 targetLocalScale;

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

        public override void Start()
        {
            DisableHitbox();
            
            WeaponAniRelay weaponAniRelay = GetComponentInParent<WeaponAniRelay>();
            if (weaponAniRelay != null)
            {
                weaponAniRelay.currentWeapon = this;
            }

            playerController = GetComponentInParent<PlayerController>();
            
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

            // 자식 이펙트 오브젝트 자동 캐싱
            InitializeEffectObjects();

            // 이펙트의 초기 로컬 트랜스폼 값을 캐싱합니다.
            if (slashEffectObject != null)
            {
                effectInitialLocalPos = slashEffectObject.transform.localPosition;
                effectInitialLocalRot = slashEffectObject.transform.localRotation;
                effectInitialLocalScale = slashEffectObject.transform.localScale;
            }

            // 초기 위치 및 회전 설정
            UpdateWeaponAim();
        }

        private void InitializeEffectObjects()
        {
            try
            {
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
            }
            catch (System.Exception e)
            {
                Debug.LogError("[ShortDagger] InitializeEffectObjects Exception: " + e.Message);
            }
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

                    attackCoroutine = StartCoroutine(ThrustRoutine(finalAimAngle, currentAttackDuration, thrustDistance, speedMultiplier));
                    PlaySlashEffect(currentAttackDuration, speedMultiplier);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("[ShortDagger] ExecuteDaggerAttack Exception: " + e.Message + "\n" + e.StackTrace);
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
                Debug.LogError("[ShortDagger] PlaySlashEffect Exception: " + e.Message + "\n" + e.StackTrace);
            }
        }

        private IEnumerator PlaySlashEffectRoutine(float currentAttackDuration, float speedMultiplier)
        {
            if (slashEffectObject == null) yield break;

            slashEffectObject.SetActive(true);
            
            // 사용자의 세팅 의도에 맞춰 다시 무기 공격 속도 배율에 이펙트 재생 속도를 연동시킵니다.
            slashEffectAnimator.speed = speedMultiplier;
            slashEffectAnimator.Play(effectStateName, 0, 0f);
            
            // 애니메이터 상태 즉시 갱신
            slashEffectAnimator.Update(0f);

            // 재생 시간도 속도 배율을 반영하여 보정합니다.
            AnimatorStateInfo stateInfo = slashEffectAnimator.GetCurrentAnimatorStateInfo(0);
            float effectDuration = stateInfo.length / speedMultiplier;

            // 최소 대기 시간 보장
            if (effectDuration <= 0f)
            {
                effectDuration = 0.3f;
            }

            float elapsed = 0f;
            while (elapsed < effectDuration)
            {
                elapsed += Time.deltaTime;

                // 매 프레임 이펙트의 로컬 트랜스폼을 캐싱된 최초 설정 값으로 강제 고정합니다.
                // 이를 통해 플레이어가 이동하거나 무기가 움직여도 이펙트가 어긋나지 않고 밀착 추적합니다.
                if (slashEffectObject != null)
                {
                    slashEffectObject.transform.localPosition = effectInitialLocalPos;
                    slashEffectObject.transform.localRotation = effectInitialLocalRot;
                    slashEffectObject.transform.localScale = effectInitialLocalScale;
                }

                yield return null;
            }

            slashEffectAnimator.speed = 1f;
            slashEffectObject.SetActive(false);
            effectCoroutine = null;
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

            // 제자리 공격이므로 위치 타겟은 대기 위치 유지
            Vector3 targetIdlePos = idleOffset;
            if (!facingRight)
            {
                targetIdlePos.x = -idleOffset.x;
            }
            targetLocalPos = targetIdlePos;

            Vector3 thrustDir = new Vector3(Mathf.Cos(aimAngle * Mathf.Deg2Rad), Mathf.Sin(aimAngle * Mathf.Deg2Rad), 0f);

            float elapsed = 0f;
            EnableHitbox();

            // 공격 진행 루프 (Update에서 매 프레임 타겟으로 보간 처리)
            while (elapsed < currentAttackDuration)
            {
                elapsed += Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / currentAttackDuration);
                float currentThrustDistance = 0f;

                if (normalizedTime < peakTimePercent)
                {
                    float t = normalizedTime / peakTimePercent;
                    currentThrustDistance = Mathf.Lerp(0f, activeThrustDistance, t);

                    CheckManualCollision();
                }
                else
                {
                    DisableHitbox();

                    float t = (normalizedTime - peakTimePercent) / (1f - peakTimePercent);
                    currentThrustDistance = Mathf.Lerp(activeThrustDistance, 0f, t);
                }

                // 타겟 위치를 찌르기 궤적에 맞추어 실시간으로 갱신 (Update가 부드럽게 쫓아갑니다)
                targetLocalPos = targetIdlePos + thrustDir * currentThrustDistance;
                yield return null;
            }

            DisableHitbox();

            // 2. 공격 완료 후 상태 초기화 (Update의 !isAttacking 분기가 대기 상태 복원 보간을 수행합니다)
            isAttacking = false;
            attackCoroutine = null;
        }

        private void CheckManualCollision()
        {
            if (col == null) return;

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

        private void OnDisable()
        {
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

            if (slashEffectObject != null)
            {
                slashEffectObject.SetActive(false);
            }

            DisableHitbox();
        }
    }
}
