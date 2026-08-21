using System;
using UnityEngine;
using Nytherion.Core.Managers;
using Nytherion.Core.Interfaces;

namespace Nytherion.GamePlay.Combat.Weapon
{
    public class BlackSword : MeleeWeapon
    {
        [Header("BlackSword Settings")]
        [Tooltip("공격 시 마우스 방향에 소환할 이펙트 프리팹 (지정되지 않으면 WeaponData의 projectilePrefab 사용)")]
        [SerializeField] private GameObject customSlashEffectPrefab;
        [Tooltip("플레이어 중심에서 조준 방향으로 이동한 검기 원호 중심 거리")]
        [SerializeField, Min(0f)] private float slashCenterForwardOffset = 0.08f;


        [Header("Animator Settings (Optional)")]
        [SerializeField] private string attackTriggerName = "Attack";

        [Header("Aim Follow Settings")]
        [Tooltip("마우스가 오른쪽에 있을 때 플레이어 중심 기준 손잡이 위치")]
        [SerializeField] private Vector3 rightHandOffset = new Vector3(0.2f, -0.13f, 0f);
        [Tooltip("마우스가 왼쪽에 있을 때 플레이어 중심 기준 손잡이 위치")]
        [SerializeField] private Vector3 leftHandOffset = new Vector3(-0.3f, 0f, 0f);
        [Tooltip("수직 조준 부근에서 좌우 손 위치가 반복 전환되지 않도록 하는 X축 임계값")]
        [SerializeField, Range(0f, 0.5f)] private float handSideSwitchThreshold = 0.08f;
        [Tooltip("조준 반대 방향을 향하는 검 이미지의 미세 회전 보정값")]
        [SerializeField] private float aimAngleOffset = 0f;
        [Tooltip("마우스 방향을 따라가는 속도")]
        [SerializeField, Min(0f)] private float aimFollowSpeed = 24f;
        [SerializeField, Min(0f)] private float rootScale = 1f;

        [Header("Solis-style Swing Settings")]
        [Tooltip("스윙 중 손잡이가 손에서 벗어나지 않도록 위치 애니메이션을 줄이는 비율")]
        [SerializeField, Range(0f, 1f)] private float swingPositionInfluence = 0.15f;
        [Tooltip("1타 준비 동작의 기준 각도")]
        [SerializeField, Range(90f, 160f)] private float normalSwingHalfAngle = 110f;
        [Tooltip("1·2타 종료각 보정값. 음수로 낮추면 칼끝이 반대쪽으로 더 진행합니다")]
        [SerializeField, Range(-90f, 90f)] private float swingEndExtensionAngle = -65f;
        [Tooltip("준비 동작이 끝나고 검기와 판정이 생성되는 Swing 정규화 시간")]
        [SerializeField, Range(0.2f, 0.85f)] private float strikeNormalizedTime = 0.55f;
        [Tooltip("2·3타 준비 동작에서 뒤로 더 젖히는 각도")]
        [SerializeField, Range(0f, 45f)] private float followupWindupAngle = 15f;
        [Tooltip("스윙 방향 결정 경계에서 연속 입력마다 방향이 바뀌는 것을 막는 여유값")]
        [SerializeField, Range(0f, 0.5f)] private float swingDirectionDecisionThreshold = 0.05f;
        [Tooltip("공격 종료 자세에서 이 시간이 지나면 연속 공격이 다시 1타부터 시작됩니다")]
        [SerializeField, Min(0f)] private float comboResetDuration = 1.1f;
        [Tooltip("콤보 연결 시간이 끝난 뒤 원위치로 돌아오는 시간")]
        [SerializeField, Min(0.01f)] private float comboReturnDuration = 0.14f;
        [Tooltip("마지막 3타 자세를 잠깐 유지하는 시간")]
        [SerializeField, Min(0f)] private float finalPoseHoldDuration = 0.12f;

        [Header("Slash Hit Settings")]
        [Tooltip("검기 콜라이더가 타격할 적 레이어")]
        [SerializeField] private LayerMask enemyLayerMask = 1 << 3;
        [Tooltip("3타 검기 시각 크기 배율")]
        [SerializeField, Min(1f)] private float thirdSlashVisualScale = 1.25f;

        private Nytherion.GamePlay.Characters.Player.PlayerController playerController;
        private Transform visualTransform;
        private Vector2 currentAimDirection = Vector2.right;
        private Vector2 lockedAttackDirection = Vector2.right;
        private bool isUsingRightHandOffset = true;
        private float activeSwingDirectionSign = 1f;
        private int nextComboStep;
        private int activeComboStep;
        private bool wasSwinging;
        private bool swingRequested;
        private bool isHoldingComboPose;
        private bool isReturningToIdle;
        private float heldSwingAngle;
        private float currentHeldSwingAngle;
        private float comboPoseReleaseTime;
        private float returnStartTime;
        private float returnStartAngle;
        private GameObject pendingSlashEffectPrefab;
        private bool isStrikePending;

        private float NormalSwingEndAngle => normalSwingHalfAngle + swingEndExtensionAngle;

        public override void Start()
        {
            base.Start();
            playerController = GetComponentInParent<Nytherion.GamePlay.Characters.Player.PlayerController>();
            if (animator == null)
            {
                animator = GetComponent<Animator>();
                if (animator == null)
                {
                    animator = GetComponentInChildren<Animator>();
                }
            }
            if (animator != null)
            {
                visualTransform = animator.transform;
            }

            if (playerController != null)
            {
                currentAimDirection = playerController.IsFacingRight ? Vector2.right : Vector2.left;
                lockedAttackDirection = currentAimDirection;
                isUsingRightHandOffset = playerController.IsFacingRight;
                activeSwingDirectionSign = ResolveSwingDirectionSign(currentAimDirection);
            }
        }

        private void LateUpdate()
        {
            if (playerController == null || visualTransform == null || animator == null) return;

            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            bool isSwinging = stateInfo.IsName("Swing");

            if (isSwinging)
            {
                swingRequested = false;
                isHoldingComboPose = false;
                isReturningToIdle = false;
                wasSwinging = true;

                ApplyRootAimPose(lockedAttackDirection);
                if (isStrikePending && stateInfo.normalizedTime >= strikeNormalizedTime)
                {
                    SpawnPendingSlashEffect();
                }
                ApplySwingPose(activeComboStep, stateInfo.normalizedTime);
                return;
            }

            if (wasSwinging)
            {
                FinishSwingPose();
                wasSwinging = false;
            }

            // Trigger가 처리되어 Swing 상태로 진입하기 전 한 프레임 동안 이전 자세를 유지합니다.
            if (swingRequested)
            {
                ApplyRootAimPose(lockedAttackDirection);
                if (isHoldingComboPose)
                {
                    ApplyHeldPose(currentHeldSwingAngle);
                }
                else
                {
                    ApplyIdlePose();
                }
                return;
            }

            if (isHoldingComboPose)
            {
                ApplyRootAimPose(lockedAttackDirection);
                UpdateComboHoldPose();
                return;
            }

            UpdateAimDirection();
            ApplyRootAimPose(currentAimDirection);
            ApplyIdlePose();
        }

        private void UpdateAimDirection()
        {
            if (InputManager.Instance == null || Camera.main == null) return;

            Vector2 mouseScreenPosition = InputManager.Instance.MousePosition;
            Vector3 mouseWorldPosition = Camera.main.ScreenToWorldPoint(
                new Vector3(mouseScreenPosition.x, mouseScreenPosition.y, 0f));
            Vector2 targetDirection = mouseWorldPosition - playerController.transform.position;
            if (targetDirection.sqrMagnitude <= 0.0001f) return;

            targetDirection.Normalize();
            float followAmount = 1f - Mathf.Exp(-aimFollowSpeed * Time.deltaTime);
            Vector2 smoothedDirection = Vector2.Lerp(currentAimDirection, targetDirection, followAmount);
            currentAimDirection = smoothedDirection.sqrMagnitude > 0.0001f
                ? smoothedDirection.normalized
                : targetDirection;
        }

        private void ApplyRootAimPose(Vector2 aimDirection)
        {
            if (aimDirection.sqrMagnitude <= 0.0001f)
            {
                aimDirection = Vector2.right;
            }

            aimDirection.Normalize();

            // 손잡이는 좌·우 고정 위치를 사용하고 검의 회전만 마우스를 따라갑니다.
            // 수직 조준 부근에서는 기존 손 위치를 유지해 좌우로 떨리는 현상을 방지합니다.
            UpdateHandOffsetSide(aimDirection);

            Vector3 handOffset = isUsingRightHandOffset ? rightHandOffset : leftHandOffset;
            Vector3 playerPosition = playerController.transform.position;
            transform.position = playerPosition + handOffset;

            // 원본 스프라이트가 수직 위를 향하므로 조준각에 90도를 더하면
            // 오른쪽 조준 시 칼끝은 왼쪽, 왼쪽 조준 시 칼끝은 오른쪽을 향합니다.
            float aimAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, aimAngle + 90f + aimAngleOffset);
            transform.localScale = Vector3.one * rootScale;
        }

        private void UpdateHandOffsetSide(Vector2 aimDirection)
        {
            if (aimDirection.x > handSideSwitchThreshold)
            {
                isUsingRightHandOffset = true;
            }
            else if (aimDirection.x < -handSideSwitchThreshold)
            {
                isUsingRightHandOffset = false;
            }
        }

        private float ResolveSwingDirectionSign(Vector2 aimDirection)
        {
            if (aimDirection.sqrMagnitude <= 0.0001f)
            {
                return activeSwingDirectionSign;
            }

            aimDirection.Normalize();
            float handSign = isUsingRightHandOffset ? 1f : -1f;
            float directionValue = -aimDirection.x + handSign * aimDirection.y;
            if (Mathf.Abs(directionValue) <= swingDirectionDecisionThreshold)
            {
                return activeSwingDirectionSign;
            }

            return directionValue > 0f ? 1f : -1f;
        }

        private void ApplyIdlePose()
        {
            Vector3 localScale = visualTransform.localScale;
            localScale.x = Mathf.Abs(localScale.x);
            visualTransform.localPosition = Vector3.zero;
            visualTransform.localRotation = Quaternion.identity;
            visualTransform.localScale = localScale;
        }

        private void ApplySwingPose(int comboStep, float normalizedTime)
        {
            // Animator의 위치와 스케일만 사용하고, 회전은 콤보가 정확히 이어지도록 직접 계산합니다.
            Vector3 animationOffset = visualTransform.localPosition;
            Vector3 animationScale = visualTransform.localScale;
            float swingAngle = EvaluateSwingAngle(comboStep, normalizedTime);

            if (comboStep == 1)
            {
                animationOffset.x = -animationOffset.x;
            }

            animationScale.x = Mathf.Abs(animationScale.x);
            visualTransform.localPosition = animationOffset * swingPositionInfluence;
            visualTransform.localScale = animationScale;
            visualTransform.localRotation = Quaternion.Euler(0f, 0f, swingAngle);
        }

        private float EvaluateSwingAngle(int comboStep, float normalizedTime)
        {
            float time = Mathf.Clamp01(normalizedTime);
            float windupProgress = Mathf.InverseLerp(0f, strikeNormalizedTime, time);
            windupProgress = Mathf.SmoothStep(0f, 1f, windupProgress);

            float swingAngle;
            if (comboStep == 0)
            {
                swingAngle = time < strikeNormalizedTime
                    ? Mathf.Lerp(0f, normalSwingHalfAngle, windupProgress)
                    : -NormalSwingEndAngle;
            }
            else if (comboStep == 1)
            {
                swingAngle = time < strikeNormalizedTime
                    ? Mathf.Lerp(-NormalSwingEndAngle, -NormalSwingEndAngle - followupWindupAngle, windupProgress)
                    : NormalSwingEndAngle;
            }
            else
            {
                swingAngle = time < strikeNormalizedTime
                    ? Mathf.Lerp(NormalSwingEndAngle, normalSwingHalfAngle, windupProgress)
                    : -NormalSwingEndAngle;
            }

            return swingAngle * activeSwingDirectionSign;
        }

        private void FinishSwingPose()
        {
            // 낮은 프레임에서도 타격 시점을 건너뛰지 않도록 종료 시 한 번 더 보장합니다.
            if (isStrikePending)
            {
                SpawnPendingSlashEffect();
            }

            heldSwingAngle = GetSwingEndAngle(activeComboStep);
            currentHeldSwingAngle = heldSwingAngle;
            isHoldingComboPose = true;
            isReturningToIdle = false;

            comboPoseReleaseTime = activeComboStep == 2
                ? Time.time + finalPoseHoldDuration
                : Time.time + comboResetDuration;
        }

        private float GetSwingEndAngle(int comboStep)
        {
            if (comboStep == 1) return NormalSwingEndAngle * activeSwingDirectionSign;
            return -NormalSwingEndAngle * activeSwingDirectionSign;
        }

        private float GetSwingStartAngle(int comboStep)
        {
            if (comboStep == 0) return 0f;
            if (comboStep == 1) return -NormalSwingEndAngle * activeSwingDirectionSign;
            return NormalSwingEndAngle * activeSwingDirectionSign;
        }

        private void UpdateComboHoldPose()
        {
            if (Time.time < comboPoseReleaseTime)
            {
                ApplyHeldPose(heldSwingAngle);
                return;
            }

            if (!isReturningToIdle)
            {
                isReturningToIdle = true;
                returnStartTime = Time.time;
                returnStartAngle = currentHeldSwingAngle;
            }

            float returnProgress = Mathf.Clamp01((Time.time - returnStartTime) / comboReturnDuration);
            returnProgress = Mathf.SmoothStep(0f, 1f, returnProgress);
            currentHeldSwingAngle = Mathf.Lerp(returnStartAngle, 0f, returnProgress);
            ApplyHeldPose(currentHeldSwingAngle);

            if (returnProgress >= 1f)
            {
                isHoldingComboPose = false;
                isReturningToIdle = false;
                nextComboStep = 0;
                currentAimDirection = lockedAttackDirection;
            }
        }

        private void ApplyHeldPose(float swingAngle)
        {
            Vector3 localScale = visualTransform.localScale;
            localScale.x = Mathf.Abs(localScale.x);
            visualTransform.localPosition = Vector3.zero;
            visualTransform.localRotation = Quaternion.Euler(0f, 0f, swingAngle);
            visualTransform.localScale = localScale;
        }

        public override void Attack(Vector2 direction, Vector3 targetPosition = default)
        {
            Debug.Log($"[BlackSword Debug] Attack Called! targetPosition: {targetPosition}, CanAttack: {CanAttack()}");

            if (!CanAttack()) 
            {
                Debug.LogWarning($"[BlackSword Debug] Attack blocked by Cooldown. Time since last attack: {Time.time - lastAttackTime}s / Cooldown: {weaponData.cooldown}s");
                return;
            }

            Vector3 attackOrigin = playerController != null
                ? playerController.transform.position
                : transform.position;
            Vector2 attackDirection = targetPosition != default
                ? (Vector2)(targetPosition - attackOrigin)
                : direction;
            if (attackDirection.sqrMagnitude <= 0.0001f)
            {
                attackDirection = direction;
            }
            if (attackDirection.sqrMagnitude <= 0.0001f)
            {
                attackDirection = currentAimDirection;
            }

            bool canContinueCombo = nextComboStep > 0
                && isHoldingComboPose
                && !isReturningToIdle
                && Time.time <= comboPoseReleaseTime;

            if (!canContinueCombo)
            {
                nextComboStep = 0;
                isHoldingComboPose = false;
                isReturningToIdle = false;
            }

            // 콤보 자세를 유지하고 있어도 다음 타격은 클릭한 순간의 마우스 방향을 사용합니다.
            lockedAttackDirection = attackDirection.normalized;
            currentAimDirection = lockedAttackDirection;
            UpdateHandOffsetSide(lockedAttackDirection);
            activeSwingDirectionSign = ResolveSwingDirectionSign(lockedAttackDirection);

            activeComboStep = nextComboStep;
            nextComboStep = (nextComboStep + 1) % 3;
            if (canContinueCombo)
            {
                heldSwingAngle = GetSwingStartAngle(activeComboStep);
                currentHeldSwingAngle = heldSwingAngle;
            }

            // 1. 공격 애니메이션 재생 (무기 자체 비주얼)
            if (animator != null)
            {
                Debug.Log($"[BlackSword Debug] Animator found. Sending Trigger: {attackTriggerName}");
                swingRequested = true;
                animator.SetTrigger(attackTriggerName);
            }
            else
            {
                Debug.LogError("[BlackSword Debug] Animator component is NULL! Please attach an Animator or link it in the Inspector.");
            }

            // 2. 이펙트 프리팹 결정
            GameObject effectPrefab = customSlashEffectPrefab != null ? customSlashEffectPrefab : weaponData.projectilePrefab;
            if (effectPrefab == null)
            {
                Debug.LogError("[BlackSword Debug] Slash effect prefab is NULL! Make sure to assign it in customSlashEffectPrefab or WeaponData.projectilePrefab.");
                lastAttackTime = Time.time;
                return;
            }
            else
            {
                Debug.Log($"[BlackSword Debug] Effect prefab resolved successfully: {effectPrefab.name}");
            }

            // 검기는 준비 동작이 끝나는 타격 프레임까지 생성하지 않습니다.
            pendingSlashEffectPrefab = effectPrefab;
            isStrikePending = true;

            if (animator == null)
            {
                SpawnPendingSlashEffect();
            }

            lastAttackTime = Time.time;
        }

        private void SpawnPendingSlashEffect()
        {
            GameObject effectPrefab = pendingSlashEffectPrefab;
            pendingSlashEffectPrefab = null;
            isStrikePending = false;
            if (effectPrefab == null) return;

            Vector3 playerPos = playerController != null
                ? playerController.transform.position
                : transform.position;
            Vector3 spawnDirection = lockedAttackDirection;
            Vector3 spawnPos = playerPos + spawnDirection * slashCenterForwardOffset;
            float aimAngle = Mathf.Atan2(lockedAttackDirection.y, lockedAttackDirection.x) * Mathf.Rad2Deg;
            Quaternion spawnRotation = Quaternion.Euler(0f, 0f, aimAngle);

            Debug.Log($"[BlackSword Debug] Strike! Spawning trail effect at: {spawnPos}");

            GameObject effectInstance = null;
            if (ObjectPoolManager.Instance != null)
            {
                effectInstance = ObjectPoolManager.Instance.SpawnFromPool(effectPrefab, spawnPos, spawnRotation);
                Debug.Log($"[BlackSword Debug] Spawned via ObjectPoolManager: {effectInstance != null}");
            }
            else
            {
                effectInstance = Instantiate(effectPrefab, spawnPos, spawnRotation);
                Debug.LogWarning("[BlackSword Debug] ObjectPoolManager not found. Instantiated directly.");
            }

            if (effectInstance != null)
            {
                BlackSwordCollision slashEffect = effectInstance.GetComponent<BlackSwordCollision>();
                if (slashEffect == null)
                {
                    slashEffect = effectInstance.AddComponent<BlackSwordCollision>();
                    Debug.LogWarning("[BlackSword Debug] BlackSword visual controller was missing on the effect prefab. Added dynamically.");
                }

                float damage = weaponData != null
                    ? weaponData.damage * EffectiveDamageMultiplier
                    : 0f;
                GameObject hitEffectPrefab = weaponData != null ? weaponData.hitEffectPrefab : null;
                Vector2 hitDirection = lockedAttackDirection;
                slashEffect.ConfigureHitbox(
                    enemyLayerMask,
                    (hitCollider, target) => ApplySlashHit(
                        hitCollider,
                        target,
                        damage,
                        hitEffectPrefab,
                        hitDirection));
                slashEffect.ConfigureFollowTarget(
                    playerController != null ? playerController.transform : null,
                    spawnDirection * slashCenterForwardOffset);
                slashEffect.ConfigureVisual(
                    activeComboStep,
                    thirdSlashVisualScale,
                    activeSwingDirectionSign);
            }
        }

        private void ApplySlashHit(
            Collider2D hitCollider,
            IDamageable target,
            float damage,
            GameObject hitEffectPrefab,
            Vector2 hitDirection)
        {
            if (target == null) return;

            target.TakeDamage(damage);
            ApplyStatusEffects(target);

            Vector2 hitOrigin = playerController != null
                ? playerController.transform.position
                : transform.position;
            Vector2 hitPoint = hitCollider != null
                ? hitCollider.ClosestPoint(hitOrigin)
                : hitOrigin;
            if (hitCollider != null && hitPoint == hitOrigin)
            {
                hitPoint = hitCollider.bounds.center;
            }

            WeaponEffectHelper.PlayHitEffect(
                hitEffectPrefab,
                hitPoint,
                direction: hitDirection);
        }

        public override void AttackEnd()
        {
            // 필요 시 추가 처리
        }
    }
}
