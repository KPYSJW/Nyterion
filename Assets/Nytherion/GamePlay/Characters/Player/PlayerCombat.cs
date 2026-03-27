using Nytherion.GamePlay.Combat;
using Nytherion.Core.Managers;
using UnityEngine;
using Nytherion.Data.ScriptableObjects.Synergy;
using VContainer;

namespace Nytherion.GamePlay.Characters.Player
{
    public class PlayerCombat : MonoBehaviour
    {
        [Tooltip("무기가 생성될 위치를 지정하는 트랜스폼")]
        [SerializeField] private Transform weaponPoint;

        [Tooltip("현재 플레이어가 장착한 무기")]
        public WeaponBase currentWeapon;
        
        private InputManager inputManager;
        private PlayerManager playerManager;
        
        [Inject]
        public void Construct(InputManager inputManager)
        {
            this.inputManager = inputManager;
        }

        private void Awake()
        {
            playerManager = GetComponent<PlayerManager>();
            if (playerManager == null)
            {
                Debug.LogError("PlayerManager 컴포넌트를 찾을 수 없습니다!");
            }
        }

        private void Start()
        {
            if (inputManager != null)
            {
                inputManager.onAttackDown += Attack;
                inputManager.onAttackUp += AttackEnd;
            }
        }
        public void EquipWeapon(WeaponBase weapon)
        {
            if (currentWeapon != null)
            {
                Destroy(currentWeapon.gameObject);
                currentWeapon = null;
            }

            if (weapon == null)
            {
                Debug.Log("무기 장착 해제됨.");
                return;
            }

            if (playerManager != null &&
                playerManager.playerEngravingManager != null &&
                playerManager.playerEngravingManager.synergyEvaluator != null &&
                weapon.weaponData != null)
            {
                WeaponEngravingSynergyData synergy = playerManager.playerEngravingManager.synergyEvaluator.EvaluateSynergy(weapon.weaponData, playerManager.playerEngravingManager.GetCurrentEngravings());

                if (synergy != null)
                {
                    Debug.Log($"시너지 발동: {synergy.weaponName} + {synergy.engravingName}");
                }
                else
                {
                    Debug.Log("시너지 없음.");
                }
            }
            else
            {
                Debug.Log("[PlayerCombat] 로딩 중이거나 시너지 평가기가 준비되지 않아 시너지 계산을 건너뜁니다.");
            }

            currentWeapon = Instantiate(weapon, weaponPoint.position, Quaternion.identity, weaponPoint);
        }

        public void Attack()
        {
            if(currentWeapon != null) 
            {
                currentWeapon.Attack(Vector2.right);
            }
        }

        public void AttackEnd()
        {
            if (currentWeapon != null)
            {
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