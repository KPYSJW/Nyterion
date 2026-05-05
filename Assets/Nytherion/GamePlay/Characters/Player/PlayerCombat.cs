using Nytherion.GamePlay.Combat;
using Nytherion.Core.Managers;
using UnityEngine;
using Nytherion.Data.ScriptableObjects.Synergy;
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

        public WeaponBase currentWeapon;

        public event System.Action<WeaponBase> OnWeaponEquipped;
        public event System.Action<Vector2, Vector3> OnPlayerAttack;
        public event System.Action OnPlayerAttackEnd;

        private InputManager inputManager;
        private PlayerManager playerManager;

        private float currentAngle = 0f;
        [Inject]
        public void Construct(InputManager inputManager)
        {
            this.inputManager = inputManager;
        }

        private void Awake()
        {
            playerManager = GetComponent<PlayerManager>();
        }

        private void Start()
        {
            if (inputManager != null)
            {
                inputManager.onAttackDown += Attack;
                inputManager.onAttackUp += AttackEnd;
            }
        }

        public void EquipWeapon(WeaponBase newWeapon)
        {
            if (currentWeapon != null)
            {
                Destroy(currentWeapon.gameObject);
            }
            if(newWeapon.weaponData.weaponType==WeaponType.Melee)
            {
                    if (newWeapon != null && meleeWeaponPoint != null)
                {
                    currentWeapon = Instantiate(newWeapon, meleeWeaponPoint);

                    currentWeapon.transform.localPosition = Vector3.zero;
                    currentWeapon.transform.localRotation = Quaternion.identity;
                }
            }
            else
            {
                    if (newWeapon != null && weaponPoint != null)
                {
                    currentWeapon = Instantiate(newWeapon, weaponPoint);

                    currentWeapon.transform.localPosition = Vector3.zero;
                    currentWeapon.transform.localRotation = Quaternion.identity;
                }
            }
            
            OnWeaponEquipped?.Invoke(currentWeapon);
        }

        private void Update()
        {
            RotateWeaponToMouse();
        }

        private void RotateWeaponToMouse()
        {
            if (inputManager == null || weaponPoint == null) return;

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
                inputManager.onAttackDown -= Attack;
                inputManager.onAttackUp -= AttackEnd;
            }
        }
    }
}