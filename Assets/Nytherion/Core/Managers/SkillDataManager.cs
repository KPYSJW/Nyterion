using UnityEngine;
using System;
using System.Collections.Generic;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Data;
using Nytherion.Data.ScriptableObjects.Skill;

namespace Nytherion.Core.Managers
{
    public class SkillState
    {
        public int level = 1;
        public int exp = 0;

        public void AddExp(int amount)
        {
            exp += amount;
            CheckLevelUp();
        }

        private void CheckLevelUp()
        {
            int maxLevel = 10; 
            while (level < maxLevel && exp >= GetRequiredExp(level))
            {
                exp -= GetRequiredExp(level);
                level++;
                Debug.Log($"스킬 레벨업! 현재 레벨: {level}");
            }
        }

        public int GetRequiredExp(int currentLevel)
        {
            return 1 << (currentLevel - 1);
        }
    }
    public class SkillDataManager : BaseManager, ISaveable
    {
        [Header("Database")]
        public SkillDatabaseSO skillDatabase; 

        [Header("Runtime Data")]
        public SkillData[] storageSkills = new SkillData[12];
        public SkillData[] equippedSkills = new SkillData[3];

        public Dictionary<string, SkillState> skillStates = new Dictionary<string, SkillState>();

        public List<SkillData> defaultStartingSkills = new List<SkillData>();

        public event Action OnSkillDataChanged;

        public void AcquireSkill(SkillData newSkill)
        {
            if (skillStates.ContainsKey(newSkill.skillID))
            {
                skillStates[newSkill.skillID].AddExp(1); 
                Debug.Log($"{newSkill.skillName} 스킬 중복 획득! 현재 경험치: {skillStates[newSkill.skillID].exp}");
                OnSkillDataChanged?.Invoke();
                return;
            }

            for (int i = 0; i < storageSkills.Length; i++)
            {
                if (storageSkills[i] == null)
                {
                    storageSkills[i] = newSkill;
                    skillStates[newSkill.skillID] = new SkillState(); 
                    OnSkillDataChanged?.Invoke();
                    return;
                }
            }

            Debug.LogWarning("스킬 보관함이 가득 찼습니다.");
        }

        public void UpdateSkills(SkillData[] newEquipped, SkillData[] newStorage)
        {
            for (int i = 0; i < equippedSkills.Length; i++)
                equippedSkills[i] = (i < newEquipped.Length) ? newEquipped[i] : null;

            for (int i = 0; i < storageSkills.Length; i++)
                storageSkills[i] = (i < newStorage.Length) ? newStorage[i] : null;
        }

        public override void PopulateSaveData(SaveData saveData)
        {
            saveData.ownedSkills.Clear();
            saveData.equippedSkillIds.Clear();

            for (int i = 0; i < storageSkills.Length; i++)
            {
                if (storageSkills[i] != null)
                {
                    string id = storageSkills[i].skillID; 

                    if (skillStates.TryGetValue(id, out var state))
                    {
                        saveData.ownedSkills.Add(new SkillEntry { skillId = id, level = state.level, exp = state.exp });
                    }
                    else
                    {
                        saveData.ownedSkills.Add(new SkillEntry { skillId = id, level = 1, exp = 0 });
                    }
                }
                else
                {
                    saveData.ownedSkills.Add(new SkillEntry { skillId = "", level = 1, exp = 0 });
                }
            }

            for (int i = 0; i < equippedSkills.Length; i++)
            {
                saveData.equippedSkillIds.Add(equippedSkills[i] != null ? equippedSkills[i].skillID : "");
            }
        }
        public override void LoadFromSaveData(SaveData saveData)
        {
            if (skillDatabase == null) return;

            Array.Clear(storageSkills, 0, storageSkills.Length);
            Array.Clear(equippedSkills, 0, equippedSkills.Length);
            skillStates.Clear();

            if (saveData.ownedSkills.Count == 0 && defaultStartingSkills.Count > 0)
            {
                for (int i = 0; i < defaultStartingSkills.Count; i++)
                {
                    if (i < storageSkills.Length)
                    {
                        storageSkills[i] = defaultStartingSkills[i];
                        skillStates[storageSkills[i].skillID] = new SkillState();
                    }
                }
            }
            else
            {
                for (int i = 0; i < saveData.ownedSkills.Count; i++)
                {
                    if (i >= storageSkills.Length) break;

                    SkillEntry entry = saveData.ownedSkills[i];
                    string id = entry.skillId;

                    if (!string.IsNullOrEmpty(id))
                    {
                        storageSkills[i] = skillDatabase.GetSkillById(id);

                        if (storageSkills[i] != null)
                        {
                            skillStates[id] = new SkillState { level = entry.level, exp = entry.exp };
                        }
                    }
                }

                for (int i = 0; i < saveData.equippedSkillIds.Count; i++)
                {
                    if (i >= equippedSkills.Length) break;
                    string id = saveData.equippedSkillIds[i];
                    equippedSkills[i] = string.IsNullOrEmpty(id) ? null : skillDatabase.GetSkillById(id);
                }
            }

            OnSkillDataChanged?.Invoke();
        }
    }
}