using UnityEngine;
using Nytherion.Core.Managers;

namespace Nytherion.GamePlay.Combat.Weapon
{
    public class BlackSword : MeleeWeapon
    {
        [Header("BlackSword Settings")]
        [Tooltip("공격 시 마우스 방향에 소환할 이펙트 프리팹 (지정되지 않으면 WeaponData의 projectilePrefab 사용)")]
        [SerializeField] private GameObject customSlashEffectPrefab;
        [Tooltip("이펙트 소환 거리 (0 이하일 경우 WeaponData의 range 값을 사용합니다)")]
        [SerializeField] private float spawnDistance = 0f;


        [Header("Animator Settings (Optional)")]
        [SerializeField] private string attackTriggerName = "Attack";

        [Header("Idle Position & Rotation Settings")]
        [SerializeField] private Vector3 rightIdlePosition = new Vector3(0.22f, 0.44f, 0f);
        [SerializeField] private float rightIdleRotationZ = 90f;
        [SerializeField] private Vector3 leftIdlePosition = new Vector3(-0.46f, 0.13f, 0f);
        [SerializeField] private float leftIdleRotationZ = 45f;

        [Header("Swing Direction Settings")]
        [Tooltip("좌측 스윙 시 X축 위치 추가 오프셋 (칼날의 스윙 중심을 플레이어 손 위치에 맞추기 위해 조절합니다)")]
        [SerializeField] private float leftSwingXOffset = -0.2f;

        private Nytherion.GamePlay.Characters.Player.PlayerController playerController;
        private Transform visualTransform;

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
        }

        private void LateUpdate()
        {
            if (playerController == null || visualTransform == null || animator == null) return;

            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);

            // 1. Swing(공격) 애니메이션이 진행 중일 때
            if (stateInfo.IsName("Swing"))
            {
                // 스케일을 음수로 뒤집으면 유니티 행렬 연산 상 각도가 꼬이는 현상이 발생합니다.
                // 따라서 루트의 localScale.x는 무조건 양수로 유지하고, 좌측일 때의 궤적 좌표와 회전각을 수학적으로 덮어씁니다.
                Vector3 parentScale = transform.localScale;
                parentScale.x = Mathf.Abs(parentScale.x);
                transform.localScale = parentScale;

                if (!playerController.IsFacingRight)
                {
                    Vector3 localPos = visualTransform.localPosition;
                    Vector3 localEuler = visualTransform.localEulerAngles;

                    // 좌측 스윙 X축 대칭 및 오프셋 보정
                    localPos.x = -localPos.x + leftSwingXOffset;

                    // Z축 회전 각도를 수학적으로 대칭 반전 (180도 - 현재 각도)
                    float symmetricAngle = 180f - localEuler.z;

                    visualTransform.localPosition = localPos;
                    visualTransform.localRotation = Quaternion.Euler(0f, 0f, symmetricAngle);
                }
                
                return;
            }

            // 2. Idle(대기) 상태일 때
            Vector3 normalParentScale = transform.localScale;
            normalParentScale.x = Mathf.Abs(normalParentScale.x);
            transform.localScale = normalParentScale;

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
                localScaleIdle.x = Mathf.Abs(localScaleIdle.x); // 좌측 Idle 시에도 뒤집지 않고 양수 유지
                targetZRotation = leftIdleRotationZ;
            }

            visualTransform.localPosition = localPosIdle;
            visualTransform.localScale = localScaleIdle;
            visualTransform.localRotation = Quaternion.Euler(0f, 0f, targetZRotation);
        }


        public override void Attack(Vector2 direction, Vector3 targetPosition = default)
        {
            Debug.Log($"[BlackSword Debug] Attack Called! targetPosition: {targetPosition}, CanAttack: {CanAttack()}");

            if (!CanAttack()) 
            {
                Debug.LogWarning($"[BlackSword Debug] Attack blocked by Cooldown. Time since last attack: {Time.time - lastAttackTime}s / Cooldown: {weaponData.cooldown}s");
                return;
            }

            // 1. 공격 애니메이션 재생 (무기 자체 비주얼)
            if (animator != null)
            {
                Debug.Log($"[BlackSword Debug] Animator found. Sending Trigger: {attackTriggerName}");
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

            // 3. 스폰 위치 계산
            Vector3 playerPos = transform.position;
            Vector3 toMouse = targetPosition - playerPos;
            toMouse.z = 0f;

            Vector3 spawnDirection = toMouse.normalized;
            if (spawnDirection == Vector3.zero)
            {
                spawnDirection = (Vector3)direction;
            }

            float finalDistance = spawnDistance > 0f ? spawnDistance : weaponData.range;
            Vector3 spawnPos = playerPos + spawnDirection * finalDistance;
            Debug.Log($"[BlackSword Debug] Spawning effect at: {spawnPos} (Distance: {finalDistance})");

            // 4. 회전 각도 계산
            float aimAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Quaternion spawnRotation = Quaternion.Euler(0f, 0f, aimAngle);

            // 5. ObjectPoolManager를 통한 스폰
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

            // 6. 데미지 및 속성 설정
            if (effectInstance != null)
            {
                BlackSwordCollision collisionComp = effectInstance.GetComponent<BlackSwordCollision>();
                if (collisionComp == null)
                {
                    collisionComp = effectInstance.AddComponent<BlackSwordCollision>();
                    Debug.LogWarning("[BlackSword Debug] BlackSwordCollision component was missing on the effect prefab. Added dynamically.");
                }

                collisionComp.damage = weaponData.damage * EffectiveDamageMultiplier;
                collisionComp.traits = GetTraits();
                if (collisionComp.hitEffectPrefab == null)
                {
                    collisionComp.hitEffectPrefab = weaponData.hitEffectPrefab;
                }
            }

            lastAttackTime = Time.time;
        }

        public override void AttackEnd()
        {
            // 필요 시 추가 처리
        }
    }
}
