using UnityEditor;
using Nytherion.Editor.Localization;
using UnityEngine;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.Gameplay.Relics.Modules;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class GenerateSkillRelics
    {
        private const string SKILL_RELIC_SAVE_DIR = "Assets/Nytherion/Data/ScriptableObjects/Relics/SkillRelics";
        private const string RELIC_DATABASE_PATH = "Assets/Nytherion/Data/ScriptableObjects/Relics/RelicDatabase.asset";

        // 기본 제공 스킬 설명 딕셔너리
        private static readonly Dictionary<string, string> DefaultSkillDescriptions = new Dictionary<string, string>
        {
            { "AS_Skill", "일정 시간 공격 속도 증가." },
            { "All_Stat_Skill", "모든 능력치 증가." },
            { "Atk_Buff_Skill", "일정 시간 공격력 증가." },
            { "Aura_Skill", "주변 적에게 지속 피해." },
            { "Blackhole_Skill", "블랙홀로 적을 끌어당기며 지속 피해." },
            { "CallLightning_Skill", "하늘에서 강력한 벼락을 내리쳐 적들에게 고위력의 전기 피해를 입힙니다." },
            { "Dash_CD_Skill", "대시 쿨타임 감소." },
            { "FireWave_Skill", "전방으로 번지는 거대한 화염 파도를 발사하여 범위 안의 적들에게 화염 피해를 입힙니다." },
            { "FrostNova_Skill", "주변의 넓은 영역에 냉기 피해를 입히고 잠시 동안 빙결 상태로 만듭니다." },
            { "IceShard_Skill", "날카로운 얼음 파편들을 부채꼴 모양으로 발사하여 적들을 꿰뚫고 피해를 입힙니다." },
            { "Laser_Skill", "관통 레이저로 직선상의 적 공격." },
            { "Lifesteal_Skill", "일정 시간 가한 피해의 일부를 체력으로 회복." },
            { "MeteorStrike_Skill", "운석을 떨어뜨려 범위 피해." },
            { "MultiShot_Skill", "투사체 발사 시 추가 투사체 발사." },
            { "Overdrive_Skill", "일정 시간 공격 속도, 대시 속도 증가." },
            { "Shadow_Clone_Skill", "함께 공격하는 그림자 분신 소환." },
            { "Soul_Eater_Skill", "적 처치 시 체력 회복 및 일시적 능력 강화." },
            { "Spiral_Skill", "회전 구체로 주변 적에게 지속 피해." },
            { "Turret_Skill", "루티 포탑을 배치해 범위 내 적 공격." }
        };

        static GenerateSkillRelics()
        {
            // 에디터 로드 시 자동 실행
            EditorApplication.delayCall += () =>
            {
                UpdateAllSkillDataDescriptions(silent: true);
                UpdateAllSkillRelicDescriptions(silent: true);
            };
        }

        [MenuItem("Nytherion/Update Skill & Relic Descriptions")]
        public static void UpdateAllDescriptionsMenu()
        {
            UpdateAllSkillDataDescriptions(silent: false);
            UpdateAllSkillRelicDescriptions(silent: false);
        }

        [MenuItem("Nytherion/Generate Skill Relics for All Skills")]
        public static void GenerateAllSkillRelics()
        {
            UpdateAllSkillDataDescriptions(silent: true);

            if (!Directory.Exists(SKILL_RELIC_SAVE_DIR))
            {
                Directory.CreateDirectory(SKILL_RELIC_SAVE_DIR);
            }

            string[] skillGuids = AssetDatabase.FindAssets("t:SkillData");
            List<SkillData> allSkills = new List<SkillData>();

            foreach (string guid in skillGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                SkillData skill = AssetDatabase.LoadAssetAtPath<SkillData>(path);
                if (skill != null && !allSkills.Contains(skill))
                {
                    allSkills.Add(skill);
                }
            }

            Debug.Log($"[GenerateSkillRelics] 총 {allSkills.Count}개의 스킬 데이터를 찾았습니다.");

            RelicDatabaseSO relicDB = AssetDatabase.LoadAssetAtPath<RelicDatabaseSO>(RELIC_DATABASE_PATH);
            if (relicDB == null)
            {
                string[] dbGuids = AssetDatabase.FindAssets("t:RelicDatabaseSO");
                if (dbGuids.Length > 0)
                {
                    relicDB = AssetDatabase.LoadAssetAtPath<RelicDatabaseSO>(AssetDatabase.GUIDToAssetPath(dbGuids[0]));
                }
            }

            if (relicDB != null)
            {
                Undo.RecordObject(relicDB, "Generate Skill Relics");
                if (relicDB.allRelics == null)
                {
                    relicDB.allRelics = new List<RelicData>();
                }
            }

            int createdCount = 0;
            int updatedCount = 0;

            foreach (SkillData skill in allSkills)
            {
                string cleanName = string.IsNullOrEmpty(skill.skillName) ? skill.name : skill.skillName;
                cleanName = cleanName.Replace(" ", "_");
                string assetName = $"Relic_{cleanName}.asset";
                string fullPath = Path.Combine(SKILL_RELIC_SAVE_DIR, assetName).Replace("\\", "/");

                RelicData relicData = AssetDatabase.LoadAssetAtPath<RelicData>(fullPath);
                bool isNew = false;

                if (relicData == null)
                {
                    relicData = ScriptableObject.CreateInstance<RelicData>();
                    isNew = true;
                }

                string skillName = string.IsNullOrEmpty(skill.skillName) ? skill.name : skill.skillName;

                relicData.relicName = $"Relic of {cleanName}";
                relicData.koreanName = $"{skillName} 각인";
                GetSkillRelicDescriptions(skill, out string descriptionKR, out string descriptionEN);
                relicData.description_KR = descriptionKR;
                relicData.description_EN = descriptionEN;
                relicData.Image = skill.icon;
                relicData.rarity = Core.Enums.Rarity.Rare;
                relicData.level = 1;
                relicData.shape = new List<Vector2Int> { Vector2Int.zero };

                // GrantSkillEffect 생성 및 연결
                GrantSkillEffect grantEffect = new GrantSkillEffect
                {
                    skillData = skill
                };

                RelicEffectModule module = new RelicEffectModule
                {
                    condition = null, // 조건 없이 항시 부여
                    effects = new List<RelicEffectBase> { grantEffect }
                };

                relicData.effectModules = new List<RelicEffectModule> { module };

                if (isNew)
                {
                    AssetDatabase.CreateAsset(relicData, fullPath);
                    createdCount++;
                }
                else
                {
                    EditorUtility.SetDirty(relicData);
                    updatedCount++;
                }

                if (relicDB != null && !relicDB.allRelics.Contains(relicData))
                {
                    relicDB.allRelics.Add(relicData);
                }
            }

            if (relicDB != null)
            {
                EditorUtility.SetDirty(relicDB);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[GenerateSkillRelics] 완료! 신규 생성: {createdCount}개, 갱신: {updatedCount}개. RelicDatabase 등록 완료.");
            EditorUtility.DisplayDialog("완료", $"총 {allSkills.Count}개 스킬에 대한 유물이 생성/업데이트 되었습니다.\n(신규: {createdCount}, 갱신: {updatedCount})", "확인");
        }

        // 에디터 재로드와 유물 생성 시 같은 간결한 설명을 사용합니다.
        private static void GetSkillRelicDescriptions(SkillData skill, out string korean, out string english)
        {
            string koreanName = string.IsNullOrEmpty(skill.skillName) ? skill.name : skill.skillName;
            string englishName = koreanName;
            string englishDescription = "";
            if (!string.IsNullOrEmpty(skill.skillID))
            {
                if (LocalizationTranslationCatalog.Skills.TryGetValue(skill.skillID, out var names))
                {
                    koreanName = names.Korean;
                    englishName = names.English;
                }
                LocalizationTranslationCatalog.SkillEnglishDescriptions.TryGetValue(skill.skillID, out englishDescription);
            }

            korean = $"[{koreanName}] 획득";
            english = $"Unlocks [{englishName}]";
            if (!string.IsNullOrWhiteSpace(skill.description))
                korean += "\n" + skill.description.Trim();
            if (!string.IsNullOrWhiteSpace(englishDescription))
                english += "\n" + englishDescription.Trim();
        }

        /// <summary>
        /// 비어 있는 SkillData의 description 필드를 기본 설명으로 자동 등록
        /// </summary>
        public static void UpdateAllSkillDataDescriptions(bool silent = false)
        {
            string[] skillGuids = AssetDatabase.FindAssets("t:SkillData");
            int updateCount = 0;

            foreach (string guid in skillGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                SkillData skill = AssetDatabase.LoadAssetAtPath<SkillData>(path);
                if (skill == null) continue;

                string assetName = Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrEmpty(skill.description))
                {
                    if (DefaultSkillDescriptions.TryGetValue(assetName, out string defaultDesc))
                    {
                        skill.description = defaultDesc;
                        EditorUtility.SetDirty(skill);
                        updateCount++;
                    }
                    else if (!string.IsNullOrEmpty(skill.skillName) && DefaultSkillDescriptions.TryGetValue(skill.skillName, out string defaultDescByName))
                    {
                        skill.description = defaultDescByName;
                        EditorUtility.SetDirty(skill);
                        updateCount++;
                    }
                }
            }

            if (updateCount > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[GenerateSkillRelics] {updateCount}개의 스킬 데이터 설정을 기본 설명으로 채웠습니다.");
            }

            if (!silent)
            {
                EditorUtility.DisplayDialog("스킬 설명 채우기 완료", $"{updateCount}개의 스킬 데이터 설명이 채워졌습니다.", "확인");
            }
        }

        /// <summary>
        /// 프로젝트 내 모든 스킬 유물 에셋의 설명 문구를 새로운 기능에 맞춰 일괄 업데이트
        /// </summary>
        public static void UpdateAllSkillRelicDescriptions(bool silent = false)
        {
            string[] relicGuids = AssetDatabase.FindAssets("t:RelicData");
            int updateCount = 0;

            foreach (string guid in relicGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                RelicData relic = AssetDatabase.LoadAssetAtPath<RelicData>(path);
                if (relic == null || relic.effectModules == null) continue;

                foreach (RelicEffectModule module in relic.effectModules)
                {
                    if (module == null || module.effects == null) continue;

                    foreach (RelicEffectBase effect in module.effects)
                    {
                        if (effect is GrantSkillEffect grantEffect && grantEffect.skillData != null)
                        {
                            GetSkillRelicDescriptions(grantEffect.skillData, out string newKR, out string newEN);

                            if (relic.description_KR != newKR || relic.description_EN != newEN)
                            {
                                relic.description_KR = newKR;
                                relic.description_EN = newEN;
                                EditorUtility.SetDirty(relic);
                                updateCount++;
                            }
                        }
                    }
                }
            }

            if (updateCount > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[GenerateSkillRelics] {updateCount}개의 스킬 유물 설정을 새로운 설명 포맷으로 일괄 수정하였습니다.");
            }

            if (!silent)
            {
                EditorUtility.DisplayDialog("스킬 유물 설명 업데이트 완료", $"{updateCount}개의 스킬 유물 설명 문구가 요청하신 포맷으로 변경되었습니다.", "확인");
            }
        }
    }
}
