using UnityEngine;
using System;
using System.Collections.Generic;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Data;
using Nytherion.Data.ScriptableObjects.Skill;

namespace Nytherion.Core.Managers
{
    public class SkillDataManager : BaseManager, ISaveable
    {
        [Header("Database")]
        public SkillDatabaseSO skillDatabase; 

        [Header("Runtime Data")]
        public SkillData[] storageSkills = new SkillData[12];
        public SkillData[] equippedSkills = new SkillData[3];

        public List<SkillData> defaultStartingSkills = new List<SkillData>();

        public event Action OnSkillDataChanged;

        public void AcquireSkill(SkillData newSkill)
        {
            bool alreadyOwned = false;
            foreach (var s in equippedSkills) if (s == newSkill) alreadyOwned = true;
            foreach (var s in storageSkills) if (s == newSkill) alreadyOwned = true;

            if (!alreadyOwned)
            {
                for (int i = 0; i < storageSkills.Length; i++)
                {
                    if (storageSkills[i] == null)
                    {
                        storageSkills[i] = newSkill;
                        OnSkillDataChanged?.Invoke();
                        return;
                    }
                }
            }
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
            saveData.ownedSkillIds.Clear();
            saveData.equippedSkillIds.Clear();

            for (int i = 0; i < storageSkills.Length; i++)
                saveData.ownedSkillIds.Add(storageSkills[i] != null ? storageSkills[i].name : "");

            for (int i = 0; i < equippedSkills.Length; i++)
                saveData.equippedSkillIds.Add(equippedSkills[i] != null ? equippedSkills[i].name : "");
        }

        public override void LoadFromSaveData(SaveData saveData)
        {
            if (skillDatabase == null) return;

            Array.Clear(storageSkills, 0, storageSkills.Length);
            Array.Clear(equippedSkills, 0, equippedSkills.Length);

            if (saveData.ownedSkillIds.Count == 0 && defaultStartingSkills.Count > 0)
            {
                for (int i = 0; i < defaultStartingSkills.Count; i++)
                {
                    if (i < storageSkills.Length) storageSkills[i] = defaultStartingSkills[i];
                }
            }
            else
            {
                for (int i = 0; i < saveData.ownedSkillIds.Count; i++)
                {
                    if (i >= storageSkills.Length) break;
                    string id = saveData.ownedSkillIds[i];
                    storageSkills[i] = string.IsNullOrEmpty(id) ? null : skillDatabase.GetSkillById(id);
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