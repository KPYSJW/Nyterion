using UnityEngine;
using System.Collections.Generic;
using Nytherion.Gameplay.Engravings.Modules;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Engravings;

namespace Nytherion.GamePlay.Characters.Player
{
    /// <summary>
    /// 플레이어에 장착된 각인들의 조건을 실시간으로 감시하고,
    /// 효과의 적용 및 해제 라이프사이클을 전담하는 컨트롤러
    /// </summary>
    public class EngravingEffectController : MonoBehaviour
    {
        private PlayerManager playerManager;
        private PlayerEngravingManager engravingManager;

        // 현재 장착된 각인 모듈들 중 활성화(조건 만족)된 모듈과 그 레벨을 추적
        private Dictionary<EngravingEffectModule, int> activeModuleLevels = new Dictionary<EngravingEffectModule, int>();

        private void Awake()
        {
            playerManager = GetComponent<PlayerManager>();
            engravingManager = GetComponent<PlayerEngravingManager>();
        }

        private void Start()
        {
            if (engravingManager != null)
            {
                engravingManager.OnEngravingsChanged += HandleEngravingsChanged;
            }
            
            // 전역 이벤트(OnEngravingStateChanged 등)를 통해 레벨 변동 감지
            var globalEngravingManager = FindObjectOfType<EngravingManager>();
            if (globalEngravingManager != null)
            {
                globalEngravingManager.OnEngravingStateChanged += HandleEngravingsChanged;
            }

            // 조건 재검사를 위한 이벤트 구독
            Nytherion.GamePlay.Characters.Player.PlayerHealth.OnHealthChanged += HandleHealthChanged;
            
            if (playerManager != null && playerManager.PlayerCombat != null)
            {
                playerManager.PlayerCombat.OnWeaponEquipped += HandleWeaponEquipped;
            }
        }

        private void OnDestroy()
        {
            if (engravingManager != null)
            {
                engravingManager.OnEngravingsChanged -= HandleEngravingsChanged;
            }
            var globalEngravingManager = FindObjectOfType<EngravingManager>();
            if (globalEngravingManager != null)
            {
                globalEngravingManager.OnEngravingStateChanged -= HandleEngravingsChanged;
            }
            
            Nytherion.GamePlay.Characters.Player.PlayerHealth.OnHealthChanged -= HandleHealthChanged;
            
            if (playerManager != null && playerManager.PlayerCombat != null)
            {
                playerManager.PlayerCombat.OnWeaponEquipped -= HandleWeaponEquipped;
            }
            
            RemoveAllActiveEffects();
        }

        private void HandleHealthChanged(float current, float max)
        {
            ReevaluateAllConditions();
        }

        private void HandleWeaponEquipped(Nytherion.GamePlay.Combat.WeaponBase weapon)
        {
            ReevaluateAllConditions();
        }

        private void HandleEngravingsChanged()
        {
            ReevaluateAllConditions();
        }

        /// <summary>
        /// 체력 변경, 무기 변경, 각인 장착/해제/레벨 변동 등 상태가 변할 때마다 전체 각인의 발동 조건을 다시 검사
        /// </summary>
        public void ReevaluateAllConditions()
        {
            if (playerManager == null || engravingManager == null) return;

            // 현재 장착된 모든 각인의 모든 모듈과 해당 각인의 레벨을 수집
            Dictionary<EngravingEffectModule, int> currentlyEquippedModules = new Dictionary<EngravingEffectModule, int>();
            foreach (var engraving in engravingManager.GetCurrentEngravings())
            {
                if (engraving != null && engraving.effectModules != null)
                {
                    foreach (var module in engraving.effectModules)
                    {
                        // 동일한 모듈이 여러 개 있을 수 있는 경우는 드물지만, 가장 높은 레벨을 덮어쓰도록 처리
                        currentlyEquippedModules[module] = engraving.level;
                    }
                }
            }

            // 1. 기존에 활성화되었던 모듈 중, 장착 해제되었거나, 레벨이 변했거나, 조건이 불충족된 모듈 비활성화 (Remove)
            List<EngravingEffectModule> modulesToRemove = new List<EngravingEffectModule>();
            foreach (var kvp in activeModuleLevels)
            {
                var activeModule = kvp.Key;
                var oldLevel = kvp.Value;

                bool isStillEquipped = currentlyEquippedModules.TryGetValue(activeModule, out int currentLevel);
                // 장착 해제된 경우 혹은 레벨이 바뀐 경우에는 일단 비활성화 (레벨이 바뀌었으면 나중에 다시 활성화됨)
                if (!isStillEquipped || oldLevel != currentLevel)
                {
                    modulesToRemove.Add(activeModule);
                }
                else
                {
                    bool isConditionMet = activeModule.condition == null || activeModule.condition.IsConditionMet(playerManager, currentLevel);
                    if (!isConditionMet)
                    {
                        modulesToRemove.Add(activeModule);
                    }
                }
            }

            foreach (var module in modulesToRemove)
            {
                DeactivateModule(module, activeModuleLevels[module]);
            }

            // 2. 현재 장착된 모듈 중, 새롭게 조건이 충족되었거나 레벨이 갱신되어 활성화해야 할 모듈 처리 (Apply)
            foreach (var kvp in currentlyEquippedModules)
            {
                var module = kvp.Key;
                var currentLevel = kvp.Value;

                if (!activeModuleLevels.ContainsKey(module))
                {
                    bool isConditionMet = module.condition == null || module.condition.IsConditionMet(playerManager, currentLevel);
                    if (isConditionMet)
                    {
                        ActivateModule(module, currentLevel);
                    }
                }
            }
        }

        private void ActivateModule(EngravingEffectModule module, int level)
        {
            if (module == null || module.effects == null) return;

            foreach (var effect in module.effects)
            {
                if (effect != null)
                {
                    effect.ApplyEffect(playerManager, level);
                }
            }
            activeModuleLevels[module] = level;
        }

        private void DeactivateModule(EngravingEffectModule module, int level)
        {
            if (module == null || module.effects == null) return;

            foreach (var effect in module.effects)
            {
                if (effect != null)
                {
                    effect.RemoveEffect(playerManager, level);
                }
            }
            activeModuleLevels.Remove(module);
        }

        private void RemoveAllActiveEffects()
        {
            List<EngravingEffectModule> modulesToDeactivate = new List<EngravingEffectModule>(activeModuleLevels.Keys);
            foreach (var module in modulesToDeactivate)
            {
                DeactivateModule(module, activeModuleLevels[module]);
            }
            activeModuleLevels.Clear();
        }
    }
}