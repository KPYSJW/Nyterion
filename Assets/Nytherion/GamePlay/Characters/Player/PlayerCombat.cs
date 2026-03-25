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
        
        private InputManager _inputManager;
        private PlayerManager _playerManager;
        
        [Inject]
        public void Construct(InputManager inputManager)
        {
            _inputManager = inputManager;
             
        }

        private void Awake()
        {
            _playerManager = GetComponent<PlayerManager>();
            if (_playerManager == null)
            {
                Debug.LogError("PlayerManager 컴포넌트를 찾을 수 없습니다!");
            }
        }

        private void Start()
        {
            

            if (_inputManager != null)
            {
                _inputManager.onAttackDown += Attack;
                _inputManager.onAttackUp += AttackEnd;
               
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

            WeaponEngravingSynergyData synergy = _playerManager.playerEngravingManager.synergyEvaluator.EvaluateSynergy(weapon.weaponData, _playerManager.playerEngravingManager.GetCurrentEngravings());
            if (synergy != null)
            {
                Debug.Log($"시너지 발동: {synergy.weaponName} + {synergy.engravingName}");
            }
            else
            {
                Debug.Log("시너지 없음.");
            }
            currentWeapon = Instantiate(weapon, weaponPoint.position, Quaternion.identity, weaponPoint);
        }

        public void Attack()
        {
            
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
            if (_inputManager != null)
            {
                _inputManager.onAttackDown -= Attack;
                _inputManager.onAttackUp -= AttackEnd;
            }
        }
    }
}