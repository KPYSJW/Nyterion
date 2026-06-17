using UnityEngine;
using System.Collections;
using Nytherion.Core.Managers;

namespace Nytherion.GamePlay.Combat.Weapon
{
    public class FlambergeWeapon : MeleeWeapon
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

            // 5. 검기 이펙트 생성
            if (slashEffectPrefab != null)
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

                    // [각도 설정] 마우스 방향 정렬 + 미세한 랜덤 각도 추가 (-15도 ~ +15도)
                    float randomRotation = Random.Range(-15f, 15f);
                    effect.transform.rotation = Quaternion.Euler(0f, 0f, baseAngle + randomRotation);

                    // [스케일 설정] 플레이어가 보는 좌우 방향 및 랜덤 Y 반전(상하 뒤집어 베기 연출)
                    float flipX = (attackDir.x >= 0) ? 1.0f : -1.0f;
                    float randomFlipY = (Random.value > 0.5f) ? 1.0f : -1.0f;

                    Vector3 originalScale = slashEffectPrefab.transform.localScale;
                    effect.transform.localScale = new Vector3(originalScale.x * flipX, originalScale.y * randomFlipY, originalScale.z);

                    if (effect.TryGetComponent<CollisionObject>(out CollisionObject collisionObj))
                    {
                        collisionObj.damage = weaponData.damage * damageMultiplier;
                        collisionObj.traits = GetTraits();
                        
                        if (collisionObj.hitEffectPrefab == null)
                        {
                            collisionObj.hitEffectPrefab = weaponData.hitEffectPrefab;
                        }
                    }
                    else if (effect.TryGetComponent<FlambergeCollision>(out FlambergeCollision flambergeCol))
                    {
                        flambergeCol.damage = weaponData.damage * damageMultiplier;
                        
                        if (flambergeCol.hitEffectPrefab == null)
                        {
                            flambergeCol.hitEffectPrefab = weaponData.hitEffectPrefab;
                        }
                    }
                }
            }

            lastAttackTime = Time.time;
        }

        public override void AttackEnd()
        {
            // 필요 시 추가적인 공격 종료 처리
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
