using UnityEngine;
using Nytherion.Core.Managers;
using System.Collections.Generic;
using Nytherion.Core.Enums;

namespace Nytherion.GamePlay.Combat.Weapon
{
    /// <summary>
    /// Abaddon 무기 본체 컴포넌트입니다.
    /// 공격 시 3가지의 슬래시 이펙트 프리팹을 순차적으로 생성하여 공격 콤보를 구현합니다.
    /// </summary>
    public class Abaddon : MeleeWeapon
    {
        [Header("Abaddon Combat Settings")]
        [Tooltip("공격 시 순서대로 순환하며 스폰할 3가지 슬래시 이펙트 프리팹 (동적 판정용)")]
        [SerializeField] private GameObject[] slashEffectPrefabs = new GameObject[3];
        [Tooltip("이펙트 소환 거리 (0 이하일 경우 플레이어 중심에 바로 생성합니다)")]
        [SerializeField] private float spawnDistance = 0f;
        [Tooltip("이펙트 소환 중심 Y축 오프셋 (플레이어 발밑 기준 조절용)")]
        [SerializeField] private float effectYOffset = 0f;
        [Tooltip("슬래시 이펙트 생성 시 Z축에 적용할 랜덤 각도 오프셋 범위 (-값 ~ +값)")]
        [SerializeField] private float randomAngleRange = 15f;
 
        [Header("Abaddon Weapon Visual Settings")]
        [Tooltip("우측 조준 공격 시 활성화할 자식 이펙트 오브젝트 (무기 자식 비주얼)")]
        [SerializeField] private GameObject rightEffectObject;
        [Tooltip("좌측 조준 공격 시 활성화할 자식 이펙트 오브젝트 (무기 자식 비주얼)")]
        [SerializeField] private GameObject leftEffectObject;
        [Tooltip("자식 이펙트 제어용 공용 Animator")]
        [SerializeField] private Animator localEffectAnimator;
        [Tooltip("재생할 이펙트 애니메이션 이름")]
        [SerializeField] private string effectClipName = "SwingEffect";
        [Tooltip("각 이펙트의 기본 활성화 지속 시간 (애니메이션 정보 누락 시의 예비용)")]
        [SerializeField] private float effectDuration = 0.5f;

        [Header("Animator Settings (Optional)")]
        [SerializeField] private string attackTriggerName = "Attack";

        [Header("Idle Position & Rotation Settings")]
        [SerializeField] private Vector3 rightIdlePosition = new Vector3(0.22f, 0.44f, 0f);
        [SerializeField] private float rightIdleRotationZ = 90f;
        [SerializeField] private Vector3 leftIdlePosition = new Vector3(-0.46f, 0.13f, 0f);
        [SerializeField] private float leftIdleRotationZ = 45f;

        [Header("Sprite Settings")]
        [Tooltip("우측을 바라볼 때 사용할 무기 이미지")]
        [SerializeField] private Sprite rightFacingSprite;
        [Tooltip("좌측을 바라볼 때 사용할 무기 이미지")]
        [SerializeField] private Sprite leftFacingSprite;
        [Tooltip("무기 이미지를 렌더링하는 SpriteRenderer (미지정 시 자체/자식 오브젝트에서 탐색)")]
        [SerializeField] private SpriteRenderer weaponSpriteRenderer;

        private Nytherion.GamePlay.Characters.Player.PlayerController playerController;
        private Transform visualTransform;
        private int currentEffectIndex = 0;
        private bool waitingForRelease = false;
        private bool lastFacingRight = true;
        private Coroutine localEffectRoutine;



        public override void Start()
        {
            base.Start();
            
            if (rightEffectObject != null)
            {
                rightEffectObject.SetActive(false);
            }
            if (leftEffectObject != null)
            {
                leftEffectObject.SetActive(false);
            }

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

            if (weaponSpriteRenderer == null)
            {
                weaponSpriteRenderer = GetComponent<SpriteRenderer>();
                if (weaponSpriteRenderer == null)
                {
                    weaponSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
                }
            }

            if (playerController != null)
            {
                lastFacingRight = playerController.IsFacingRight;
                UpdateSprite(lastFacingRight);
            }

            if (InputManager.Instance != null)
            {
                InputManager.Instance.onAttackUp += HandleAttackUp;
            }
        }

        private void OnDestroy()
        {
            if (InputManager.Instance != null)
            {
                InputManager.Instance.onAttackUp -= HandleAttackUp;
            }
        }

        private void HandleAttackUp()
        {
            waitingForRelease = false;
        }

        private void UpdateSprite(bool isFacingRight)
        {
            if (weaponSpriteRenderer != null)
            {
                if (isFacingRight)
                {
                    if (rightFacingSprite != null)
                    {
                        weaponSpriteRenderer.sprite = rightFacingSprite;
                    }
                }
                else
                {
                    if (leftFacingSprite != null)
                    {
                        weaponSpriteRenderer.sprite = leftFacingSprite;
                    }
                }
            }
        }

        private void LateUpdate()
        {
            if (playerController == null || visualTransform == null || animator == null) return;

            // 플레이어 방향이 변경되었을 때 스프라이트 동적 교체 (공격 중이든 대기 중이든 즉각 반응)
            if (playerController.IsFacingRight != lastFacingRight)
            {
                lastFacingRight = playerController.IsFacingRight;
                UpdateSprite(lastFacingRight);
            }

            // 루트 스케일의 X는 무조건 양수로 고정하여 회전 행렬 꼬임을 방지합니다.
            Vector3 parentScale = transform.localScale;
            parentScale.x = Mathf.Abs(parentScale.x);
            transform.localScale = parentScale;

            // 공격(Swing) 중이든 대기(Idle) 중이든 동일한 위치 및 회전을 강제 고정합니다.
            // (Swing 애니메이션 클립은 위치/회전 변경 없이 오직 Material만 변화시키기 때문입니다.)
            Vector3 localPosIdle = visualTransform.localPosition;
            Vector3 localScaleIdle = visualTransform.localScale;
            float targetZRotation = 0f;

            if (playerController.IsFacingRight)
            {
                localPosIdle = rightIdlePosition;
                localScaleIdle.x = Mathf.Abs(localScaleIdle.x);
                targetZRotation = rightIdleRotationZ;
            }
            else
            {
                localPosIdle = leftIdlePosition;
                localScaleIdle.x = Mathf.Abs(localScaleIdle.x);
                targetZRotation = leftIdleRotationZ;
            }

            visualTransform.localPosition = localPosIdle;
            visualTransform.localScale = scaleOverride(localScaleIdle);
            visualTransform.localRotation = Quaternion.Euler(0f, 0f, targetZRotation);
        }

        private Vector3 scaleOverride(Vector3 baseScale)
        {
            // visual scale 강제 보정용 헬퍼 (필요시 조절)
            return baseScale;
        }

        public override void Attack(Vector2 direction, Vector3 targetPosition = default)
        {
            Debug.Log($"[Abaddon Debug] Attack Called! targetPosition: {targetPosition}, CanAttack: {CanAttack()}, waitingForRelease: {waitingForRelease}");

            if (waitingForRelease)
            {
                return;
            }

            if (!CanAttack()) 
            {
                Debug.LogWarning($"[Abaddon Debug] Attack blocked by Cooldown. Time since last attack: {Time.time - lastAttackTime}s / Cooldown: {weaponData.cooldown}s");
                return;
            }

            waitingForRelease = true;

            // 1. 공격 애니메이션 재생 (무기 자체 비주얼)
            if (animator != null)
            {
                animator.SetTrigger(attackTriggerName);
            }

            // 2. 판정용 슬래시 이펙트 프리팹 동적 스폰 (3단 콤보 순환)
            if (slashEffectPrefabs != null && slashEffectPrefabs.Length > 0)
            {
                int effectIndex = currentEffectIndex % slashEffectPrefabs.Length;
                GameObject effectPrefab = slashEffectPrefabs[effectIndex];
                if (effectPrefab == null)
                {
                    effectPrefab = weaponData.projectilePrefab;
                }

                if (effectPrefab != null)
                {
                    // 스폰 위치 계산 (플레이어 중심 + Y 오프셋)
                    Vector3 playerPos = playerController != null ? (playerController.transform.position + new Vector3(0f, effectYOffset, 0f)) : transform.position;
                    Vector3 spawnPos = playerPos;

                    if (spawnDistance > 0f)
                    {
                        Vector3 toMouse = targetPosition - playerPos;
                        toMouse.z = 0f;
                        Vector3 spawnDirection = toMouse.normalized;
                        if (spawnDirection == Vector3.zero)
                        {
                            spawnDirection = (Vector3)direction;
                        }
                        spawnPos += spawnDirection * spawnDistance;
                    }

                    float randomOffset = UnityEngine.Random.Range(-randomAngleRange, randomAngleRange);
                    float aimAngle = (Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg) + randomOffset;
                    Quaternion spawnRotation = Quaternion.Euler(0f, 0f, aimAngle);

                    // 풀에서 스폰
                    GameObject effectInstance = null;
                    if (ObjectPoolManager.Instance != null)
                    {
                        effectInstance = ObjectPoolManager.Instance.SpawnFromPool(effectPrefab, spawnPos, spawnRotation);
                    }
                    else
                    {
                        effectInstance = Instantiate(effectPrefab, spawnPos, spawnRotation);
                    }

                    // 플레이어를 따라 이동하도록 부모 설정 및 동기화
                    if (effectInstance != null && playerController != null)
                    {
                        effectInstance.transform.SetParent(playerController.transform);
                        effectInstance.transform.localPosition = new Vector3(0f, effectYOffset, 0f);
                        effectInstance.transform.localRotation = spawnRotation;
                    }

                    // 데미지 설정
                    if (effectInstance != null)
                    {
                        AbaddonCollision collisionComp = effectInstance.GetComponent<AbaddonCollision>();
                        if (collisionComp == null)
                        {
                            collisionComp = effectInstance.AddComponent<AbaddonCollision>();
                        }
                        collisionComp.damage = weaponData.damage * EffectiveDamageMultiplier;
                        collisionComp.traits = GetTraits();
                        if (collisionComp.hitEffectPrefab == null)
                        {
                            collisionComp.hitEffectPrefab = weaponData.hitEffectPrefab;
                        }
                    }
                }
            }

            // 3. 무기 자체 비주얼용 자식 이펙트 활성화 (방향별 1회 재생)
            bool isRight = playerController != null ? playerController.IsFacingRight : true;
            GameObject targetEffectObj = isRight ? rightEffectObject : leftEffectObject;
            GameObject otherEffectObj = isRight ? leftEffectObject : rightEffectObject;

            // 반대 방향 이펙트는 무조건 즉시 비활성화
            if (otherEffectObj != null)
            {
                otherEffectObj.SetActive(false);
            }

            if (targetEffectObj != null)
            {
                // 로컬 이펙트 충돌체가 있을 경우에도 판정 세팅
                AbaddonCollision localCollisionComp = targetEffectObj.GetComponent<AbaddonCollision>();
                if (localCollisionComp != null)
                {
                    localCollisionComp.damage = weaponData.damage * EffectiveDamageMultiplier;
                    localCollisionComp.traits = GetTraits();
                    if (localCollisionComp.hitEffectPrefab == null)
                    {
                        localCollisionComp.hitEffectPrefab = weaponData.hitEffectPrefab;
                    }
                }

                PlayLocalEffect(targetEffectObj);
            }

            // 다음 공격을 위해 콤보 인덱스 순환
            if (slashEffectPrefabs != null && slashEffectPrefabs.Length > 0)
            {
                currentEffectIndex = (currentEffectIndex + 1) % slashEffectPrefabs.Length;
            }
            lastAttackTime = Time.time;
        }

        private void PlayLocalEffect(GameObject effectObj)
        {
            if (localEffectRoutine != null)
            {
                StopCoroutine(localEffectRoutine);
            }
            localEffectRoutine = StartCoroutine(PlayLocalEffectRoutine(effectObj));
        }

        private System.Collections.IEnumerator PlayLocalEffectRoutine(GameObject effectObj)
        {
            effectObj.SetActive(true);

            float duration = effectDuration;
            Animator targetAnimator = effectObj.GetComponent<Animator>();
            if (targetAnimator == null)
            {
                targetAnimator = localEffectAnimator;
            }

            if (targetAnimator != null && !string.IsNullOrEmpty(effectClipName))
            {
                targetAnimator.Play(effectClipName, 0, 0f);
                targetAnimator.Update(0f);

                // 1프레임 대기 후 실제 재생 시간 획득
                yield return null; 
                AnimatorStateInfo stateInfo = targetAnimator.GetCurrentAnimatorStateInfo(0);
                duration = stateInfo.length;
            }

            yield return new WaitForSeconds(duration);

            effectObj.SetActive(false);
            localEffectRoutine = null;
        }

        public override void AttackEnd()
        {
            // 필요 시 추가 처리
        }
    }
}
