using UnityEngine;
using System.Collections.Generic;
using Nytherion.Gameplay.Relics.Modules;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Relics;
using VContainer;

namespace Nytherion.GamePlay.Characters.Player
{
    /// <summary>
    /// 플레이어에 장착된 각인들의 조건을 실시간으로 감시하고,
    /// 효과의 적용 및 해제 라이프사이클을 전담하는 컨트롤러
    /// </summary>
    public class RelicEffectController : MonoBehaviour
    {
        private PlayerManager playerManager;
        private PlayerRelicManager relicManager;
        private RelicManager globalRelicManager;

        // 현재 장착된 각인 모듈들 중 활성화(조건 만족)된 모듈과 그 레벨을 추적
        private Dictionary<RelicEffectModule, int> activeModuleLevels = new Dictionary<RelicEffectModule, int>();
        private readonly Dictionary<RelicEffectModule, EquippedModuleData> currentlyEquippedModules = new();
        private readonly HashSet<RelicSetBonusData> equippedSetBonuses = new();
        private readonly HashSet<RelicTranscendenceData> linkedTranscendenceEffects = new();
        private readonly List<RelicEffectModule> activeModules = new();
        private readonly Dictionary<(Object, ChainSynergyMeasure, string), int> maxSatisfiedChains = new();

        // 디버그 강제 활성 상태는 플레이어 수명 동안만 유지하며 저장하지 않는다.
        private readonly Dictionary<RelicSetBonusData, int> debugSetRequirements = new();
        private readonly HashSet<RelicTranscendenceData> debugTranscendences = new();

        public int GetDebugSetRequirement(RelicSetBonusData setBonus) =>
            setBonus != null && debugSetRequirements.TryGetValue(setBonus, out int requirement) ? requirement : 0;

        public bool IsDebugTranscendenceActive(RelicTranscendenceData transcendence) =>
            transcendence != null && debugTranscendences.Contains(transcendence);

        public void SetDebugSetBonus(RelicSetBonusData setBonus, int requirement)
        {
            if (setBonus == null) return;
            if (requirement > 0) debugSetRequirements[setBonus] = requirement;
            else debugSetRequirements.Remove(setBonus);
            RefreshDebugCriticalChance();
            ReevaluateAllConditions();
        }

        public void SetDebugTranscendence(RelicTranscendenceData transcendence, bool active)
        {
            if (transcendence == null) return;
            if (active) debugTranscendences.Add(transcendence);
            else debugTranscendences.Remove(transcendence);
            ReevaluateAllConditions();
        }

        public void ResetDebugEffects()
        {
            debugSetRequirements.Clear();
            debugTranscendences.Clear();
            RefreshDebugCriticalChance();
            ReevaluateAllConditions();
        }

        private void RefreshDebugCriticalChance()
        {
            if (playerManager == null) return;
            bool guaranteeCritical = false;
            foreach (var entry in debugSetRequirements)
            {
                if (entry.Key != null && entry.Key.synergySeriesId == "StormArcherSeries" && entry.Value > 0)
                {
                    guaranteeCritical = true;
                    break;
                }
            }
            playerManager.SetDebugGuaranteedCritical(guaranteeCritical);
        }

        private readonly struct EquippedModuleData
        {
            public readonly Object Owner;
            public readonly int Level;
            public readonly bool IsDisabled;

            public EquippedModuleData(Object owner, int level, bool isDisabled)
            {
                Owner = owner;
                Level = level;
                IsDisabled = isDisabled;
            }
        }

        private bool isEvaluating = false;

        [Inject]
        public void Construct(RelicManager relicManager)
        {
            globalRelicManager = relicManager;
        }

        private void Awake()
        {
            playerManager = GetComponent<PlayerManager>();
            relicManager = GetComponent<PlayerRelicManager>();
        }

        private void Start()
        {
            if (relicManager != null)
            {
                relicManager.OnRelicEffectsChanged += HandleRelicsChanged;
            }

            // 조건 재검사를 위한 이벤트 구독
            Nytherion.GamePlay.Characters.Player.PlayerHealth.OnHealthChanged += HandleHealthChanged;
            
            if (playerManager != null && playerManager.PlayerCombat != null)
            {
                playerManager.PlayerCombat.OnWeaponEquipped += HandleWeaponEquipped;
            }

            // 저장된 장착 상태가 Start 이전에 동기화되어도 최초 효과를 적용한다.
            ReevaluateAllConditions();
        }

        private void OnDestroy()
        {
            if (relicManager != null)
            {
                relicManager.OnRelicEffectsChanged -= HandleRelicsChanged;
            }
            
            Nytherion.GamePlay.Characters.Player.PlayerHealth.OnHealthChanged -= HandleHealthChanged;
            
            if (playerManager != null && playerManager.PlayerCombat != null)
            {
                playerManager.PlayerCombat.OnWeaponEquipped -= HandleWeaponEquipped;
            }
            
            if (playerManager != null) playerManager.SetDebugGuaranteedCritical(false);
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

        private void HandleRelicsChanged()
        {
            ReevaluateAllConditions();
        }

        /// <summary>
        /// 체력 변경, 무기 변경, 각인 장착/해제/레벨 변동 등 상태가 변할 때마다 전체 각인의 발동 조건을 다시 검사
        /// </summary>
        public void ReevaluateAllConditions()
        {
            if (playerManager == null || relicManager == null || isEvaluating || relicManager.IsSynchronizingRelics) return;

            try
            {
                isEvaluating = true;

                // 현재 장착된 모든 각인의 모든 모듈과 해당 각인의 레벨을 수집
                currentlyEquippedModules.Clear();
                equippedSetBonuses.Clear();
                linkedTranscendenceEffects.Clear();
                maxSatisfiedChains.Clear();
                
                foreach (RelicData relic in relicManager.GetCurrentRelics())
                {
                    if (relic != null && relic.effectModules != null)
                    {
                        foreach (RelicEffectModule module in relic.effectModules)
                        {
                            if (module == null) continue;
                            currentlyEquippedModules[module] = new EquippedModuleData(relic, relic.level, relic.isDisabled);
                        }
                    }

                    if (relic != null && relic.synergySetBonusData != null &&
                        relic.synergySeriesId == relic.synergySetBonusData.synergySeriesId)
                    {
                        equippedSetBonuses.Add(relic.synergySetBonusData);
                    }
                }

                equippedSetBonuses.UnionWith(debugSetRequirements.Keys);
                linkedTranscendenceEffects.UnionWith(debugTranscendences);

                // 공유 세트 모듈은 여러 유물이 참조해도 세트당 한 번만 등록한다.
                foreach (RelicSetBonusData setBonus in equippedSetBonuses)
                {
                    if (setBonus == null || setBonus.bonusModules == null) continue;

                    foreach (RelicEffectModule module in setBonus.bonusModules)
                    {
                        if (module == null) continue;
                        currentlyEquippedModules[module] = new EquippedModuleData(setBonus, 1, false);
                    }

                    if (setBonus.linkedTranscendenceEffects == null) continue;
                    foreach (RelicTranscendenceData transcendence in setBonus.linkedTranscendenceEffects)
                    {
                        if (transcendence != null)
                        {
                            linkedTranscendenceEffects.Add(transcendence);
                        }
                    }
                }

                // 초월 효과는 모든 하위 세트의 요구 수량을 만족한 동안만 효과 모듈을 등록한다.
                foreach (RelicTranscendenceData transcendence in linkedTranscendenceEffects)
                {
                    if (transcendence == null ||
                        (!debugTranscendences.Contains(transcendence) && !transcendence.IsActive(globalRelicManager)) ||
                        transcendence.effectModules == null)
                    {
                        continue;
                    }

                    foreach (RelicEffectModule module in transcendence.effectModules)
                    {
                        if (module == null) continue;
                        currentlyEquippedModules[module] = new EquippedModuleData(transcendence, 1, false);
                    }
                }

                // 유물 또는 공유 세트별로 조건을 만족한 가장 높은 단계만 활성화한다.
                foreach (KeyValuePair<RelicEffectModule, EquippedModuleData> pair in currentlyEquippedModules)
                {
                    if (!(pair.Key.condition is ChainSynergyCondition chainCondition))
                    {
                        continue;
                    }

                    bool isSatisfied = pair.Value.Owner is RelicSetBonusData setBonus &&
                        debugSetRequirements.TryGetValue(setBonus, out int debugRequirement)
                        ? chainCondition.requiredChainLength <= debugRequirement
                        : chainCondition.IsConditionMet(playerManager, pair.Value.Level);
                    if (!isSatisfied) continue;

                    var tierGroup = GetChainTierGroup(pair.Value.Owner, chainCondition);
                    if (!maxSatisfiedChains.TryGetValue(tierGroup, out int currentMaximum) ||
                        chainCondition.requiredChainLength > currentMaximum)
                    {
                        maxSatisfiedChains[tierGroup] = chainCondition.requiredChainLength;
                    }
                }

                // 1. 기존에 활성화되었던 모듈 중, 장착 해제되었거나, 레벨이 변했거나, 비활성화되었거나, 조건이 불충족된 모듈 비활성화 (Remove)
                // Dictionary를 순회하면서 DeactivateModule을 통해 요소를 제거하므로, Key 목록을 복사하여 순회
                activeModules.Clear();
                activeModules.AddRange(activeModuleLevels.Keys);
                foreach (RelicEffectModule activeModule in activeModules)
                {
                    int oldLevel = activeModuleLevels[activeModule];

                    bool isStillEquipped = currentlyEquippedModules.TryGetValue(activeModule, out EquippedModuleData data);
                    // 장착 해제, 레벨 변경, 혹은 비활성화 상태가 된 경우 제거 대상으로 지정
                    if (!isStillEquipped || oldLevel != data.Level || data.IsDisabled)
                    {
                        DeactivateModule(activeModule, oldLevel);
                    }
                    else
                    {
                        if (!IsModuleConditionMet(activeModule, data, maxSatisfiedChains))
                        {
                            DeactivateModule(activeModule, oldLevel);
                        }
                    }
                }

                // 2. 현재 장착된 모듈 중, 새롭게 조건이 충족되었거나 레벨이 갱신되어 활성화해야 할 모듈 처리 (Apply)
                foreach (KeyValuePair<RelicEffectModule, EquippedModuleData> kvp in currentlyEquippedModules)
                {
                    RelicEffectModule module = kvp.Key;
                    EquippedModuleData data = kvp.Value;
                    int currentLevel = data.Level;

                    if (!activeModuleLevels.ContainsKey(module) && !data.IsDisabled)
                    {
                        if (IsModuleConditionMet(module, data, maxSatisfiedChains))
                        {
                            ActivateModule(module, currentLevel);
                        }
                    }
                }
            }
            finally
            {
                currentlyEquippedModules.Clear();
                equippedSetBonuses.Clear();
                linkedTranscendenceEffects.Clear();
                maxSatisfiedChains.Clear();
                activeModules.Clear();
                isEvaluating = false;
            }
        }

        private bool IsModuleConditionMet(
            RelicEffectModule module,
            EquippedModuleData data,
            Dictionary<(Object, ChainSynergyMeasure, string), int> maxSatisfiedChains)
        {
            if (data.Owner is RelicTranscendenceData transcendence && debugTranscendences.Contains(transcendence))
                return true;

            if (module.condition == null) return true;

            if (module.condition is ChainSynergyCondition chainCondition)
            {
                return maxSatisfiedChains.TryGetValue(GetChainTierGroup(data.Owner, chainCondition), out int maximumRequirement) &&
                       chainCondition.requiredChainLength == maximumRequirement;
            }

            return module.condition.IsConditionMet(playerManager, data.Level);
        }

        private static (Object, ChainSynergyMeasure, string) GetChainTierGroup(Object owner, ChainSynergyCondition condition)
        {
            return (owner, condition.measure, condition.targetSeriesId ?? string.Empty);
        }

        private void ActivateModule(RelicEffectModule module, int level)
        {
            if (module == null || module.effects == null) return;

            // 방어 코드: 이미 이 레벨로 활성화된 상태라면 무시 (무한 루프 방지)
            if (activeModuleLevels.TryGetValue(module, out int existingLevel) && existingLevel == level)
            {
                return;
            }

            // 순서 변경: 상태를 먼저 갱신하여, 이펙트 발동 중 발생하는 이벤트가 다시 여기로 돌아왔을 때 중복 실행을 막음
            activeModuleLevels[module] = level;

            foreach (var effect in module.effects)
            {
                if (effect != null)
                {
                    effect.ApplyEffect(playerManager, level);
                }
            }
        }

        private void DeactivateModule(RelicEffectModule module, int level)
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
            List<RelicEffectModule> modulesToDeactivate = new List<RelicEffectModule>(activeModuleLevels.Keys);
            foreach (var module in modulesToDeactivate)
            {
                DeactivateModule(module, activeModuleLevels[module]);
            }
            activeModuleLevels.Clear();
        }
    }
}
