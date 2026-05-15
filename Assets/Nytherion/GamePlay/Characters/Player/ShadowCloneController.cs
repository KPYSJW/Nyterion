using UnityEngine;
using System.Collections;
using Nytherion.GamePlay.Combat;

namespace Nytherion.GamePlay.Characters.Player
{
    /// <summary>
    /// 실제 분신의 움직임, 애니메이션, 공격 동기화를 담당하는 컨트롤러
    /// </summary>
    public class ShadowCloneController : MonoBehaviour
    {
        [Header("Clone References")]
        public Transform weaponPoint;          // 분신의 무기가 장착될 트랜스폼
        public SpriteRenderer cloneSprite;     // 분신의 메인 스프라이트
        public Animator cloneAnimator;         // 분신의 애니메이터
        public float cloneAttackDelay = 0.05f;

        private WaitForSeconds attackDelayWait;
        private WaitForSeconds fixedDelayWait;

        private PlayerCombat playerCombat;     // 본체(플레이어)의 전투 컴포넌트
        private Animator playerAnimator;       // 본체의 애니메이터
        private SpriteRenderer playerSprite;   // 본체의 스프라이트
        private WeaponBase cloneWeapon;        // 분신이 현재 장착 중인 복제된 무기
        private float currentDamageRatio = 0.3f; // 본체 대비 데미지 비율

        private SpriteRenderer[] allCloneSprites; // 분신 하위의 모든 스프라이트 캐싱용

        private void Awake()
        {
            attackDelayWait = new WaitForSeconds(cloneAttackDelay);
            fixedDelayWait = new WaitForSeconds(0.15f);

            // 컴포넌트 자동 할당 및 초기 비활성화 상태 설정 
            if (cloneSprite == null) cloneSprite = GetComponentInChildren<SpriteRenderer>();
            if (cloneAnimator == null) cloneAnimator = GetComponentInChildren<Animator>();

            allCloneSprites = GetComponentsInChildren<SpriteRenderer>();

            enabled = false;
            foreach (var sr in allCloneSprites)
            {
                if (sr != null) sr.enabled = false;
            }
            if (weaponPoint != null) weaponPoint.gameObject.SetActive(false);
        }
        /// <summary>
        /// 분신의 시각적 요서를 활성화하는 메서드. 스킬 발동 시 호출되어 분신이 나타나도록 처리
        /// </summary>
        public void ActivateVisuals()
        {
            enabled = true;
            if (allCloneSprites != null)
            {
                foreach (var sr in allCloneSprites)
                {
                    if (sr != null) sr.enabled = true;
                }
            }
            if (weaponPoint != null) weaponPoint.gameObject.SetActive(true);
        }
        /// <summary>
        /// 분신의 시각적 요소를 비활성화
        /// </summary>
        public void DeactivateVisuals()
        {
            enabled = false;
            if (allCloneSprites != null)
            {
                foreach (var sr in allCloneSprites)
                {
                    if (sr != null) sr.enabled = false;
                }
            }
            if (weaponPoint != null) weaponPoint.gameObject.SetActive(false);
        }

        /// <summary>
        /// 분신을 초기화하고 플레이어의 공격 이벤트를 구독
        /// </summary>
        public void Initialize(PlayerCombat combat, float damageRatio)
        {
            // 기존 이벤트 구독 해제 (중복 구독 방지)
            if (playerCombat != null)
            {
                playerCombat.OnPlayerAttack -= PerformAttack;
                playerCombat.OnPlayerAttackEnd -= PerformAttackEnd;
            }

            playerCombat = combat;
            currentDamageRatio = damageRatio;

            // 플레이어의 애니메이터 컨트롤러와 위치를 동기화 
            if (playerCombat != null)
            {
                playerAnimator = playerCombat.GetComponentInChildren<Animator>();
                playerSprite = playerCombat.GetComponentInChildren<SpriteRenderer>();

                if (playerAnimator != null && cloneAnimator != null)
                {
                    cloneAnimator.runtimeAnimatorController = playerAnimator.runtimeAnimatorController;
                }

                transform.position = playerCombat.transform.position;
            }

            // 분신 시각 효과 설정
            if (cloneSprite != null)
            {
                cloneSprite.color = new Color(0f, 0f, 0f, 0.7f);
            }

            // 플레이어 공격 이벤트 구독
            if (playerCombat != null)
            {
                playerCombat.OnPlayerAttack += PerformAttack;
                playerCombat.OnPlayerAttackEnd += PerformAttackEnd;
            }
        }
        /// <summary>
        /// 분신의 기능을 정지시키고 메모리를 정리 
        /// </summary>
        public void Deactivate()
        {
            if (playerCombat != null)
            {
                playerCombat.OnPlayerAttack -= PerformAttack;
                playerCombat.OnPlayerAttackEnd -= PerformAttackEnd;
            }

            if (cloneWeapon != null)
            {
                Destroy(cloneWeapon.gameObject);
                cloneWeapon = null;
            }
        }

        private void OnDestroy()
        {
            Deactivate();
        }

        private Vector3 currentVelocity;
        private Vector3 targetOffset = new Vector3(-0.2f, 0f, 0f);

        private void Update()
        {
            if (playerCombat == null) return;

            // 플레이어의 방향에 따라 분신의 위치 오프셋 조정 (좌우 반전)
            float facingDirection = 1f;
            if (playerSprite != null && playerSprite.flipX)
            {
                facingDirection = -1f;
            }
            else if (playerCombat.transform.localScale.x < 0)
            {
                facingDirection = -1f;
            }

            targetOffset = new Vector3(-0.2f * facingDirection, 0f, 0f);

            // 플레이어 뒤를 자연스럽게 따라가도록 이동 처리
            Vector3 targetPos = playerCombat.transform.position + targetOffset;
            transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref currentVelocity, 0.15f);

            // 레이어 정렬 및 Z 축 위치 동기화
            if (cloneSprite != null && playerSprite != null)
            {
                cloneSprite.sortingOrder = 1;
            }

            transform.position = new Vector3(transform.position.x, transform.position.y, playerCombat.transform.position.z);

            // 플레이어의 무기 상태와 애니메이션 동기화
            SyncWeapon();

            if (cloneWeapon != null && playerCombat.currentWeapon != null && weaponPoint != null && playerCombat.currentWeapon.transform.parent != null)
            {
                weaponPoint.rotation = playerCombat.currentWeapon.transform.parent.rotation;
                weaponPoint.localScale = playerCombat.currentWeapon.transform.parent.localScale;
            }

            if (playerAnimator != null && cloneAnimator != null)
            {
                // 플레이어가 재생 중인 애니메이션과 진행도를 그대로 복사
                AnimatorStateInfo stateInfo = playerAnimator.GetCurrentAnimatorStateInfo(0);

                cloneAnimator.Play(stateInfo.fullPathHash, 0, stateInfo.normalizedTime);

                if (playerSprite != null && cloneSprite != null)
                {
                    cloneSprite.flipX = playerSprite.flipX;
                }
            }
        }

        /// <summary>
        /// 플레이어가 무기를 변경하거나 장착했을 때 분신도 동일한 무기를 복제하여 장착 
        /// </summary>
        private void SyncWeapon()
        {
            WeaponBase playerWeapon = playerCombat.currentWeapon;

            // 플레이어 무기가 있고 분신 무기가 없거나 다르다면 무기 교체
            if (playerWeapon != null && (cloneWeapon == null || cloneWeapon.weaponData != playerWeapon.weaponData))
            {
                if (cloneWeapon != null) Destroy(cloneWeapon.gameObject);

                // 기존 로직: 분신의 weaponPoint에 무기 생성 
                /*
                cloneWeapon = Instantiate(playerWeapon, weaponPoint);
                cloneWeapon.transform.localPosition = Vector3.zero;
                cloneWeapon.transform.localRotation = Quaternion.identity;
                */

                // 플레이어 무기와 동일한 위치(본체)에 생성하고, 시각적으로 숨김
                cloneWeapon = Instantiate(playerWeapon, playerCombat.currentWeapon.transform.parent);
                cloneWeapon.transform.localPosition = Vector3.zero;
                cloneWeapon.transform.localRotation = Quaternion.identity;

                // 분신의 데미지 패널티 적용
                cloneWeapon.damageMultiplier = currentDamageRatio;

                cloneWeapon.Initialize(playerWeapon.weaponData);

                // 시각적 요소 숨기기
                SpriteRenderer[] renderers = cloneWeapon.GetComponentsInChildren<SpriteRenderer>();
                foreach (var r in renderers)
                {
                    r.enabled = false;
                }

                // 원거리 무기인 경우 발사 지점도 동기화
                if (cloneWeapon is RangedWeapon rw)
                {
                    // 기존 로직: 복제된 무기의 FirePoint 사용
                    /*
                    Transform clonedFirePoint = cloneWeapon.transform.Find("FirePoint") ?? cloneWeapon.transform;
                    rw.firePoint = clonedFirePoint;
                    */

                    // 플레이어 본체의 FirePoint 사용
                    if (playerWeapon is RangedWeapon playerRw)
                    {
                        rw.firePoint = playerRw.firePoint;
                    }
                }
            }
            // 플레이어가 무기를 해제한 경우 분신 무기도 제거 
            else if (playerWeapon == null && cloneWeapon != null)
            {
                Destroy(cloneWeapon.gameObject);
                cloneWeapon = null;
            }
        }

        /// <summary>
        /// 플레이어가 공격을 시작할 때 트리거되는 이벤트 핸들러
        /// </summary>
        private void PerformAttack(Vector2 direction, Vector3 targetPosition)
        {
            if (cloneWeapon != null)
            {
                // 기존 로직
                /*
                if (!cloneWeapon.gameObject.activeInHierarchy)
                {
                    cloneWeapon.gameObject.SetActive(true);
                }

                // 무기 공격 실행
                bool canAttack = cloneWeapon.CanAttack();
                cloneWeapon.Attack(direction, targetPosition);
                */

                //  시간차(Echo) 발사를 위해 코루틴 실행
                StartCoroutine(DelayedAttackRoutine(direction, targetPosition));
            }
            else
            {
                Debug.LogWarning("[ShadowClone] 분신의 무기(cloneWeapon)가 없습니다.");
            }
        }

        private IEnumerator DelayedAttackRoutine(Vector2 direction, Vector3 targetPosition)
        {
            // 
            yield return fixedDelayWait;

            if (cloneWeapon != null)
            {
                if (!cloneWeapon.gameObject.activeInHierarchy)
                {
                    cloneWeapon.gameObject.SetActive(true);
                }

                // 처음 조준했던 방향(direction)과 위치(targetPosition)를 그대로 사용하여
                // 플레이어가 지연 시간 동안 방향을 돌려도 원래 의도한 방향으로 발사되게 함
                cloneWeapon.Attack(direction, targetPosition);
            }
        }

        /// <summary>
        /// 플레이어의 공격이 끝났을 때 트리거되는 이벤트 핸들러
        /// </summary>
        private void PerformAttackEnd()
        {
            if (cloneWeapon != null)
            {
                // 기존 로직 (주석 처리)
                /*
                if (!cloneWeapon.gameObject.activeInHierarchy)
                {
                    cloneWeapon.gameObject.SetActive(true);
                }

                cloneWeapon.AttackEnd();
                */

                // 공격 종료(AttackEnd)에도 동일한 딜레이를 주어 싱크를 맞춤
                StartCoroutine(DelayedAttackEndRoutine());
            }
        }

        private IEnumerator DelayedAttackEndRoutine()
        {
            // 플레이어 공격 보다 분신 공격은 일정 시간 이후 발사
            yield return attackDelayWait;

            if (cloneWeapon != null)
            {
                if (!cloneWeapon.gameObject.activeInHierarchy)
                {
                    cloneWeapon.gameObject.SetActive(true);
                }

                cloneWeapon.AttackEnd();
            }
        }
    }
}
