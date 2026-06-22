using Nytherion.GamePlay.Combat;
using Nytherion.Core.Managers;
using UnityEngine;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Combat.Weapons;
using VContainer;

namespace Nytherion.GamePlay.Characters.Player
{
    public class PlayerCombat : MonoBehaviour
    {
        [SerializeField] private Transform weaponPoint;
        [SerializeField] private Transform meleeWeaponPoint;

        [SerializeField] private float orbitRadius = 1.0f;

        [SerializeField] private float deadZoneRadius = 0.5f;

        [SerializeField] private float orbitSpeed = 15f;

        [SerializeField] private Vector3 centerOffset = new Vector3(0, 0.5f, 0);

        [Header("Aim Settings")]
        private bool useAngleLimit = false; // 360도 회전을 위해 인스펙터 오버라이드를 무시하고 항상 false로 고정
        [SerializeField] private float aimAngleLimit = 45f; // 중심각 기준 좌우 45도 (합 90도 부채꼴)

        public WeaponBase currentWeapon;

        public event System.Action<WeaponBase> OnWeaponEquipped;
        public event System.Action<Vector2, Vector3> OnPlayerAttack;
        public event System.Action OnPlayerAttackEnd;

        private InputManager inputManager;
        private PlayerManager playerManager;
        private PlayerController playerController;

        private float currentAngle = 0f;
        private bool isAttackHeld = false;

        [Inject]
        public void Construct(InputManager inputManager)
        {
            this.inputManager = inputManager;
        }

        private void Awake()
        {
            playerManager = GetComponent<PlayerManager>();
            playerController = GetComponent<PlayerController>();
        }

        private void Start()
        {
            if (inputManager != null)
            {
                inputManager.onAttackDown += HandleAttackDown;
                inputManager.onAttackUp += HandleAttackUp;
            }
        }

        private void HandleAttackDown()
        {
            isAttackHeld = true;
            Attack();
        }

        private void HandleAttackUp()
        {
            isAttackHeld = false;
            AttackEnd();
        }

        public void EquipWeapon(WeaponBase weaponPrefab, WeaponData data = null)
        {
            if (currentWeapon != null)
            {
                Destroy(currentWeapon.gameObject);
                currentWeapon = null;
            }

            if (weaponPrefab == null)
            {
                OnWeaponEquipped?.Invoke(null);
                return;
            }

            WeaponType type = (data != null) ? data.weaponType : (weaponPrefab.weaponData != null ? weaponPrefab.weaponData.weaponType : WeaponType.Ranged);

            if (type == WeaponType.Melee)
            {
                if (meleeWeaponPoint != null)
                {
                    currentWeapon = Instantiate(weaponPrefab, meleeWeaponPoint, false);
                }
            }
            else
            {
                if (weaponPoint != null)
                {
                    currentWeapon = Instantiate(weaponPrefab, weaponPoint, false);
                }
            }

            if (currentWeapon != null)
            {
                // Instantiate(..., false)에 의해 프리팹의 원래 localPosition이 유지됩니다.
                // 만약 WeaponData에 오버라이드용 visualPositionOffset이 명시되어 있다면 그것을 덮어씁니다.
                if (type != WeaponType.Melee)
                {
                    Vector3 posOffset = currentWeapon.transform.localPosition;
                    if (data != null && data.visualPositionOffset != Vector3.zero)
                    {
                        posOffset = data.visualPositionOffset;
                    }
                    else if (currentWeapon.weaponData != null && currentWeapon.weaponData.visualPositionOffset != Vector3.zero)
                    {
                        posOffset = currentWeapon.weaponData.visualPositionOffset;
                    }

                    currentWeapon.transform.localPosition = posOffset;
                }
                else
                {
                    Vector3 posOffset = currentWeapon.transform.localPosition;
                    if (data != null && data.visualPositionOffset != Vector3.zero)
                    {
                        posOffset = data.visualPositionOffset;
                    }
                    else if (currentWeapon.weaponData != null && currentWeapon.weaponData.visualPositionOffset != Vector3.zero)
                    {
                        posOffset = currentWeapon.weaponData.visualPositionOffset;
                    }
                    currentWeapon.transform.localPosition = posOffset;
                }

                // Animator Controller 런타임 주입 (원거리 무기만 적용)
                if (type != WeaponType.Melee)
                {
                    Animator weaponAnimator = currentWeapon.GetComponent<Animator>();
                    if (weaponAnimator == null)
                    {
                        weaponAnimator = currentWeapon.GetComponentInChildren<Animator>();
                    }

                    if (weaponAnimator != null)
                    {
                        RuntimeAnimatorController controller = null;
                        if (data != null)
                        {
                            controller = data.animatorController;
                        }
                        else if (currentWeapon.weaponData != null)
                        {
                            controller = currentWeapon.weaponData.animatorController;
                        }
                        weaponAnimator.runtimeAnimatorController = controller;
                    }
                }

                if (data != null)
                {
                    currentWeapon.Initialize(data);
                }

                if (type != WeaponType.Melee)
                {
                    float rotationOffset = 0f;
                    if (data != null)
                    {
                        rotationOffset = data.spriteRotationOffset;
                    }
                    else if (currentWeapon.weaponData != null)
                    {
                        rotationOffset = currentWeapon.weaponData.spriteRotationOffset;
                    }

                    currentWeapon.transform.localRotation = Quaternion.Euler(0f, 0f, rotationOffset);
                }
                else
                {
                    currentWeapon.transform.localRotation = Quaternion.identity;
                }
            }
            
            OnWeaponEquipped?.Invoke(currentWeapon);
        }

        private void Update()
        {
            RotateWeaponToMouse();

            if (isAttackHeld && currentWeapon != null)
            {
                // 차징 무기가 아닌 경우 꾹 누르고 있으면 쿨다운에 맞춰 자동 연사 (Auto-fire)
                if (!(currentWeapon is ChargeableRangedWeapon))
                {
                    if (currentWeapon.CanAttack())
                    {
                        Attack();
                    }
                }
            }
        }

        private void RotateWeaponToMouse()
        {
            if (inputManager == null || weaponPoint == null) return;

            if (currentWeapon != null && currentWeapon.OverrideRotation)
            {
                weaponPoint.localPosition = Vector3.zero;
                weaponPoint.localRotation = Quaternion.identity;
                weaponPoint.localScale = Vector3.one;

                if (meleeWeaponPoint != null)
                {
                    meleeWeaponPoint.localPosition = Vector3.zero;
                    meleeWeaponPoint.localRotation = Quaternion.identity;
                    meleeWeaponPoint.localScale = Vector3.one;
                }
                return;
            }

            Vector2 mouseScreenPos = inputManager.MousePosition;

            if (Camera.main != null)
            {
                Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, 0f));
                mouseWorldPos.z = 0f;

                Vector3 playerCenter = transform.position + centerOffset;
                Vector2 mouseVector = mouseWorldPos - playerCenter;

                float targetAngle = currentAngle;
                if (mouseVector.magnitude >= deadZoneRadius)
                {
                    targetAngle = Mathf.Atan2(mouseVector.y, mouseVector.x) * Mathf.Rad2Deg;
                }

                if (useAngleLimit && playerController != null)
                {
                    float centerAngle = playerController.IsFacingRight ? 0f : 180f;
                    float angleDiff = Mathf.DeltaAngle(centerAngle, targetAngle);
                    angleDiff = Mathf.Clamp(angleDiff, -aimAngleLimit, aimAngleLimit);
                    targetAngle = centerAngle + angleDiff;
                }

                currentAngle = Mathf.LerpAngle(currentAngle, targetAngle, Time.deltaTime * orbitSpeed);

                Vector2 currentDirection = new Vector2(Mathf.Cos(currentAngle * Mathf.Deg2Rad), Mathf.Sin(currentAngle * Mathf.Deg2Rad));

                weaponPoint.position = playerCenter + (Vector3)(currentDirection * orbitRadius);
                weaponPoint.rotation = Quaternion.Euler(0, 0, currentAngle);

                float normalizedAngle = Mathf.DeltaAngle(0, currentAngle);
                if (Mathf.Abs(normalizedAngle) > 90f)
                {
                    weaponPoint.localScale = new Vector3(1f, -1f, 1f);
                }
                else
                {
                    weaponPoint.localScale = new Vector3(1f, 1f, 1f);
                }
            }
        }

        public void Attack()
        {
            if (currentWeapon != null)
            {
                Vector2 fireDirection = weaponPoint.right;
                Vector2 mouseScreenPos = inputManager.MousePosition;
                Vector3 targetWorldPos = Camera.main.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, 0f));
                targetWorldPos.z = 0f;
                currentWeapon.Attack(fireDirection, targetWorldPos);

                OnPlayerAttack?.Invoke(fireDirection, targetWorldPos);
            }
        }

        public void AttackEnd()
        {
            if (currentWeapon != null)
            {
                currentWeapon.AttackEnd();
                OnPlayerAttackEnd?.Invoke();
            }
        }

        private void OnDisable()
        {
            if (inputManager != null)
            {
                inputManager.onAttackDown -= HandleAttackDown;
                inputManager.onAttackUp -= HandleAttackUp;
            }
        }
    }
}