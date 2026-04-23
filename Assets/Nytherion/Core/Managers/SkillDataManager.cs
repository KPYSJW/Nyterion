using UnityEngine;
using System;
using System.Collections.Generic;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Data;
using Nytherion.Data.ScriptableObjects.Skill;

namespace Nytherion.Core.Managers
{
    /// <summary>
    /// 개별 스킬의 레벨과 경험치 상태를 추적하는 데이터 클래스
    /// </summary>
    public class SkillState
    {
        public int level = 1;
        public int exp = 0;

        /// <summary>
        /// 스킬 경험치를 획득하고 레벨업 조건을 만족했는지 검사
        /// </summary>
        public void AddExp(int amount)
        {
            exp += amount;
            CheckLevelUp();
        }

        /// <summary>
        /// 누적 경험치가 요구 경험치 이사일 경우 레벨 상승
        /// </summary>
        private void CheckLevelUp()
        {
            int maxLevel = 10;
            while (level < maxLevel && exp >= GetRequiredExp(level))
            {
                exp -= GetRequiredExp(level); // 사용된 경험치 차감
                level++;
                Debug.Log($"스킬 레벨업! 현재 레벨: {level}");
            }
        }

        /// <summary>
        /// 특정 레벨에서 다음 레벨로 가기 위한 필요 경험치를 계산 (비트 연산을 사용)
        /// </summary>
        public int GetRequiredExp(int currentLevel)
        {
            return 1 << (currentLevel - 1);
        }
    }

    /// <summary>
    /// 게임 내 플레이어의 전체 스킬 데이터를 관리. 세이브/로드 기능을 지원하는 매니저 클래스
    /// </summary>
    public class SkillDataManager : BaseManager, ISaveable
    {
        [Header("Database")]
        [Tooltip("게임 내 존재하는 모든 스킬이 정의된 데이터베이스")]
        public SkillDatabaseSO skillDatabase;

        [Header("Runtime Data")]
        [Tooltip("보유 중인 스킬 슬롯 데이터")]
        public SkillData[] storageSkills = new SkillData[12];
        [Tooltip("장착 중인 스킬 슬롯 데이터")]
        public SkillData[] equippedSkills = new SkillData[3];

        [Tooltip("스킬 ID를 키로 하여 해당 스킬의 레벨과 경험치를 저장하는 딕셔너리")]
        public Dictionary<string, SkillState> skillStates = new Dictionary<string, SkillState>();

        public List<SkillData> defaultStartingSkills = new List<SkillData>();

        // 스킬 데이터가 변경되었음을 UI 등에 알리는 이벤트
        public event Action OnSkillDataChanged;


        /// <summary>
        /// 새로운 스킬을 획득하면 이미 보유한 스킬이라면 경험치를 증가, 새로운 스킬이라면 보관함 빈자리에 추가
        /// </summary>
        /// <param name="newSkill">획득한 스킬 데이터</param>
        public void AcquireSkill(SkillData newSkill)
        {
            // 이미 보유한 스킬일 경우 중복 획득 처리
            if (skillStates.ContainsKey(newSkill.skillID))
            {
                skillStates[newSkill.skillID].AddExp(1);
                Debug.Log($"{newSkill.skillName} 스킬 중복 획득! 현재 경험치: {skillStates[newSkill.skillID].exp}");
                OnSkillDataChanged?.Invoke();
                return;
            }

            // 새로운 스킬일 경우 보관함의 빈 슬롯을 찾아 할당
            for (int i = 0; i < storageSkills.Length; i++)
            {
                if (storageSkills[i] == null)
                {
                    storageSkills[i] = newSkill;
                    skillStates[newSkill.skillID] = new SkillState(); // 초기 레벨과 경험치 설정
                    OnSkillDataChanged?.Invoke();
                    return;
                }
            }

            Debug.LogWarning("스킬 보관함이 가득 찼습니다.");
        }


        /// <summary>
        /// UI 에서 장착 상태나 보관함 순서가 변경되었을 때 데이터를 최신화
        /// </summary>
        /// <param name="newEquipped">변경된 장착 스킬 배열</param>
        /// <param name="newStorage">변경된 보관함 스킬 배열</param>
        public void UpdateSkills(SkillData[] newEquipped, SkillData[] newStorage)
        {
            // 전달받은 배열 길이만큼 복사하되 범위를 넘으면 null 처리
            for (int i = 0; i < equippedSkills.Length; i++)
                equippedSkills[i] = (i < newEquipped.Length) ? newEquipped[i] : null;

            for (int i = 0; i < storageSkills.Length; i++)
                storageSkills[i] = (i < newStorage.Length) ? newStorage[i] : null;
        }

        /// <summary>
        /// 현재 게임의 스킬 상태를 세이브 데이터 객체에 기록
        /// </summary>
        public override void PopulateSaveData(SaveData saveData)
        {
            // 기존 세이브 데이터 정리
            saveData.ownedSkills.Clear();
            saveData.equippedSkillIds.Clear();

            // 보유 중인 스킬 데이터와 레벨/ 경험치 저장
            for (int i = 0; i < storageSkills.Length; i++)
            {
                if (storageSkills[i] != null)
                {
                    string id = storageSkills[i].skillID;

                    // 상태 딕셔너리에 정보가 있으면 해당 레벨/경험치 저장, 없으면 기본값으로 저장
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
                    // 빈 슬롯은 빈 문자열로 저장
                    saveData.ownedSkills.Add(new SkillEntry { skillId = "", level = 1, exp = 0 });
                }
            }

            // 장착 중인 스킬의 ID 저장
            for (int i = 0; i < equippedSkills.Length; i++)
            {
                saveData.equippedSkillIds.Add(equippedSkills[i] != null ? equippedSkills[i].skillID : "");
            }
        }

        /// <summary>
        /// 세이브 데이터 객체로부터 게임 스킬 상태를 복구
        /// </summary>
        public override void LoadFromSaveData(SaveData saveData)
        {
            if (skillDatabase == null) return;

            // 로드 전 기존 데이터 초기화
            Array.Clear(storageSkills, 0, storageSkills.Length);
            Array.Clear(equippedSkills, 0, equippedSkills.Length);
            skillStates.Clear();
            
            // 저장된 데이터가 없는 경우 기본 스킬 지급
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
                // 보유 스킬 로드 및 상태 복원
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

                // 장착 스킬 로드
                for (int i = 0; i < saveData.equippedSkillIds.Count; i++)
                {
                    if (i >= equippedSkills.Length) break;
                    string id = saveData.equippedSkillIds[i];
                    equippedSkills[i] = string.IsNullOrEmpty(id) ? null : skillDatabase.GetSkillById(id);
                }
            }

            // 로드 완료 후 UI 갱신 이벤트 호출
            OnSkillDataChanged?.Invoke();
        }
    }
}