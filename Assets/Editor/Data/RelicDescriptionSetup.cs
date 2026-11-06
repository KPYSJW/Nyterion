using System;
using System.Collections.Generic;
using System.IO;
using Nytherion.Core.Utils;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.Editor.Localization;
using Nytherion.Gameplay.Relics.Modules;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace Nytherion.Editor
{
    /// <summary>유물 설명에는 발동 조건과 핵심 효과만 표시합니다.</summary>
    [InitializeOnLoad]
    public static class RelicDescriptionSetup
    {
        private const string Output = "output/relic-descriptions";
        private static readonly Dictionary<string, (string Korean, string English)> Descriptions =
            new Dictionary<string, (string, string)>
            {
                ["Bloom Ring"] = ("스킬 사용 후 3초 동안 공격 속도 +20% (레벨당 +4%p). 재발동 시 지속 시간 갱신.", "After using a skill, attack speed +20% for 3 seconds (+4pp per level). Repeated triggers refresh the duration."),
                ["Stardust"] = ("적 처치 시 마나 3 회복 (레벨당 +1).", "Restore 3 mana on kill (+1 per level)."),
                ["Beast Scale"] = ("피격 시 4초 동안 방어력 +3 (레벨당 +1), 최대 3중첩. 재발동 시 지속 시간 갱신.", "Taking damage grants +3 defense for 4 seconds (+1 per level), up to 3 stacks. Repeated triggers refresh the duration."),
                ["Mossy Stone"] = ("최대 체력 +45 (레벨당 +9).", "Max health +45 (+9 per level)."),
                ["Wraith Stone"] = ("저주받은 적 처치 시 반경 3 안의 저주받지 않은 적에게 남은 저주 전파 (레벨당 반경 +0.25).", "When a cursed enemy dies, spread its remaining curse to uncursed enemies within 3 units (+0.25 radius per level)."),
                ["Grimoire"] = ("저주 장비 장착 시 공격력 +15% (레벨당 +5%p).", "With Cursed equipment, attack +15% (+5pp per level)."),
                ["CatEyes"] = ("공격 시 일반 투사체를 발사하는 소환물 생성", "Summons a companion that fires normal projectiles when you attack."),
                ["Daemon"] = ("공격 시 마성 투사체를 발사하는 소환물 생성", "Summons a companion that fires demonic projectiles when you attack."),
                ["Fulmen"] = ("공격 시 번개 투사체를 발사하는 소환물 생성", "Summons a companion that fires lightning projectiles when you attack."),
                ["Glacia"] = ("공격 시 얼음 투사체를 발사하는 소환물 생성", "Summons a companion that fires ice projectiles when you attack."),
                ["Ignis"] = ("공격 시 화염 투사체를 발사하는 소환물 생성", "Summons a companion that fires fire projectiles when you attack."),
                ["Lumen"] = ("공격 시 신성 투사체를 발사하는 소환물 생성", "Summons a companion that fires holy projectiles when you attack."),
                ["Malleus"] = ("공격 시 저주 투사체를 발사하는 소환물 생성", "Summons a companion that fires cursed projectiles when you attack."),
                ["Venenus"] = ("공격 시 독 투사체를 발사하는 소환물 생성", "Summons a companion that fires poison projectiles when you attack."),
                ["BoomMaker"] = ("적을 추적하는 폭발 로봇을 내보내는 BoomMaker 소환", "Summons BoomMaker to deploy robots that chase enemies and explode."),
                ["Comrade Blast"] = ("장착 중 가까운 적에게 미사일을 발사하는 블래스트 소환", "While equipped, summons Blast to fire missiles at nearby enemies."),
                ["Opapa"] = ("장착 중 적을 관통하고 벽에서 튕기는 오파파 소환", "While equipped, summons Opapa to pierce enemies and bounce off walls."),
                ["Frog"] = ("장착 중 적을 자동 공격하는 개구리 소환", "While equipped, summons a frog that automatically attacks enemies."),
                ["Slime Specimen"] = ("장착 중 적을 자동 공격하는 슬라임 소환", "While equipped, summons a slime that automatically attacks enemies."),
                ["Azure Berry"] = ("루티 투사체 +2", "Rooti projectiles +2."),
                ["Purple Radish"] = ("루티 공격 속도 +25%", "Rooti attack speed +25%."),
                ["Ginseng"] = ("루티 소환 저장량 +1", "Rooti summon capacity +1."),
                ["Echo Flower"] = ("충전 1개로 루티 2마리 소환", "Summons two Rooti with one charge."),
                ["Bounce Berry"] = ("루티 씨앗이 주변 적에게 최대 3회 도탄", "Rooti seeds bounce to nearby enemies up to 3 times."),
                ["TargetMaker"] = ("불그늘·단절·연쇄 점화의 공격 중심을 조준 지점으로 변경", "Centers Blazeshade, Isolation and Chain Ignition attacks on the aimed position."),
                ["WindStone"] = ("적중 시 20% 확률로 풍석 발사\n0.25초마다 적중 피해의 50% (레벨당 +10%p)", "On hit: 20% chance to fire a Wind Stone.\nDeals 50% of hit damage every 0.25s (+10pp per level)."),
                ["PowerDevice"] = ("광란을 풀차징한 뒤 다음 공격 버튼을 누르는 동안 계속 연사합니다.", "After fully charging Frenzy, hold the next attack press to fire continuously."),
                ["Zenith"] = ("파이로폴 추가 화염구가 화면 내 적을 자동 조준", "Pyrofall's extra fireballs automatically target enemies in view."),
                ["Rubber Ball"] = ("투사체가 주변 적에게 최대 3회 도탄", "Projectiles bounce to nearby enemies up to 3 times."),
                ["Globe"] = ("투사체가 적을 관통", "Projectiles pierce enemies."),
                ["ManaCharger"] = ("무기 차징 공격 활성화", "Enables charged weapon attacks."),
                ["Haste Rune"] = ("충전 시간 -10%", "Charge time -10%."),
                ["Fire Pepper"] = ("화염 상태이상 피해 주기 -30%", "Burn damage interval -30%."),
                ["Trigger Piece"] = ("추가 투사체 +1", "Extra projectiles +1."),
                ["First Pip"] = ("치명타 확률 +4%p (레벨당 +1%p)\n추가 피해: 2세트 -10~20% / 4세트 0~40% / 6세트 20~60%", "Critical chance +4pp (+1pp per level).\nBonus damage: 2 dice -10% to +20% / 4 dice 0% to +40% / 6 dice +20% to +60%."),
                ["Second Pip"] = ("치명타 피해 +15% (레벨당 +4%)\n추가 피해: 2세트 -10~20% / 4세트 0~40% / 6세트 20~60%", "Critical damage +15% (+4% per level).\nBonus damage: 2 dice -10% to +20% / 4 dice 0% to +40% / 6 dice +20% to +60%."),
                ["Third Pip"] = ("스킬 피해 +15% (레벨당 +5%p)\n추가 피해: 2세트 -10~20% / 4세트 0~40% / 6세트 20~60%", "Skill damage +15% (+5pp per level).\nBonus damage: 2 dice -10% to +20% / 4 dice 0% to +40% / 6 dice +20% to +60%."),
                ["Fourth Pip"] = ("원거리 공격력 +3 (레벨당 +1)\n추가 피해: 2세트 -10~20% / 4세트 0~40% / 6세트 20~60%", "Ranged attack +3 (+1 per level).\nBonus damage: 2 dice -10% to +20% / 4 dice 0% to +40% / 6 dice +20% to +60%."),
                ["Fifth Pip"] = ("스킬 쿨타임 -10% (레벨당 -2%p)\n추가 피해: 2세트 -10~20% / 4세트 0~40% / 6세트 20~60%", "Skill cooldown -10% (-2pp per level).\nBonus damage: 2 dice -10% to +20% / 4 dice 0% to +40% / 6 dice +20% to +60%."),
                ["Six Pip"] = ("원거리 공격 속도 +10% (레벨당 +2%)\n추가 피해: 2세트 -10~20% / 4세트 0~40% / 6세트 20~60%", "Ranged attack speed +10% (+2% per level).\nBonus damage: 2 dice -10% to +20% / 4 dice 0% to +40% / 6 dice +20% to +60%.")
            };

        static RelicDescriptionSetup() => EditorApplication.update += Update;

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            string request = Output + "/simplify.request";
            if (!File.Exists(request)) return;
            File.Delete(request);
            try { SimplifyDescriptions(); }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/result.txt", "FAIL " + error);
                Debug.LogException(error);
            }
        }

        public static string GetSkillRelicName(SkillData skill, string currentName = null)
        {
            switch (skill.skillID)
            {
                case "skill_blackhole": return "블랙홀 마법 책";
                case "skill_bubble_shield": return "비눗방울";
                case "skill_chain_ignition": return "연쇄 점화 마법 책";
                case "skill_decoy": return "허수아비 마법 책";
                case "skill_fire_bolt": return "파이어볼 마법 책";
                case "skill_meteor_strike": return "메테오 마법 책";
                case "skill_shadow_clone": return "그림자 분신 마법 책";
                case "skill_turret": return "루티 마법 책";
                case "skill_spiral": return "나선";
            }
            string name = string.IsNullOrWhiteSpace(currentName) ? skill.skillName : currentName;
            return name == null ? skill.name : name.Replace(" 각인", "").Trim();
        }

        // 스킬 자체의 효과 설명과 구분하여 유물로 스킬을 얻는다는 점을 표시합니다.
        public static void GetSkillDescriptions(SkillData skill, out string korean, out string english)
        {
            switch (skill.skillID)
            {
                case "skill_all_stat_up":
                    korean = "일정 시간 모든 능력치를 높이는 스킬을 얻는다.";
                    english = "Grants a skill that temporarily boosts all stats.";
                    return;
                case "skill_blackhole":
                    korean = "적을 끌어당기며 피해를 주는 블랙홀 스킬을 얻는다.";
                    english = "Grants a black hole skill that pulls in and damages enemies.";
                    return;
                case "skill_bubble_shield":
                    korean = "피해를 흡수하는 비눗방울 스킬을 얻는다.";
                    english = "Grants a bubble shield skill that absorbs damage.";
                    return;
                case "skill_chain_ignition":
                    korean = "조준 방향으로 연속 화염 폭발을 일으키는 스킬을 얻는다.";
                    english = "Grants a skill that unleashes successive fire explosions toward your aim.";
                    return;
                case "skill_decoy":
                    korean = "주변 적을 유인하는 허수아비 스킬을 얻는다.";
                    english = "Grants a Decoy skill that taunts nearby enemies.";
                    return;
                case "skill_fire_bolt":
                    korean = "적에게 피해와 화상을 주는 파이어볼 스킬을 얻는다.";
                    english = "Grants a fireball skill that damages and burns enemies.";
                    return;
                case "skill_ice_shot":
                    korean = "적에게 피해와 빙결을 주는 아이스샷 스킬을 얻는다.";
                    english = "Grants an ice shot skill that damages and chills enemies.";
                    return;
                case "skill_lifesteal":
                    korean = "공격 피해로 체력을 회복하는 생명력 흡수 스킬을 얻는다.";
                    english = "Grants a lifesteal skill that restores health by dealing damage.";
                    return;
                case "skill_meteor_strike":
                    korean = "화면 내 모든 적에게 운석을 떨어뜨리는 스킬을 얻는다.";
                    english = "Grants a skill that drops meteors on all enemies in view.";
                    return;
                case "skill_multishot":
                    korean = "추가 투사체를 발사하는 다중 사격 스킬을 얻는다.";
                    english = "Grants a multi shot skill that fires extra projectiles.";
                    return;
                case "skill_overdrive":
                    korean = "일정 시간 공격력, 공격 속도와 대시 속도를 높이는 스킬을 얻는다.";
                    english = "Grants a skill that temporarily boosts attack, attack speed and dash speed.";
                    return;
                case "skill_portal_gate":
                    korean = "서로 왕복할 수 있는 차원문 스킬을 얻는다.";
                    english = "Grants a portal skill that lets you travel between linked portals.";
                    return;
                case "skill_projectile_gate":
                    korean = "투사체를 분열시키는 분광 게이트 스킬을 얻는다.";
                    english = "Grants a gate skill that splits projectiles.";
                    return;
                case "skill_pyro_tank":
                    korean = "적에게 돌진해 폭발하는 폭열 전차 스킬을 얻는다.";
                    english = "Grants a tank skill that charges at enemies and explodes.";
                    return;
                case "skill_shadow_clone":
                    korean = "함께 공격하는 그림자 분신 스킬을 얻는다.";
                    english = "Grants a shadow clone skill that attacks alongside you.";
                    return;
                case "skill_souleater":
                    korean = "적을 처치할수록 강해지는 투사체 스킬을 얻는다.";
                    english = "Grants a projectile skill that grows stronger with each kill.";
                    return;
                case "skill_spiral":
                    korean = "회전 구체로 주변 적을 공격하는 나선 스킬을 얻는다.";
                    english = "Grants a skill that summons spinning orbs to attack nearby enemies.";
                    return;
                case "skill_turret":
                    korean = "적을 자동으로 공격하는 루티 소환 스킬을 얻는다.";
                    english = "Grants a skill that summons Rooti to automatically attack enemies.";
                    return;
            }
            string name = GetSkillRelicName(skill);
            korean = $"{name} 스킬을 얻는다.";
            string englishName = LocalizationTranslationCatalog.Skills.TryGetValue(skill.skillID, out var names)
                ? names.English : skill.skillName;
            english = $"Grants the {englishName} skill.";
        }

        public static void ApplyDescription(RelicData relic)
        {
            if (!Descriptions.TryGetValue(relic.relicName, out var description)) return;
            relic.description_KR = description.Korean;
            relic.description_EN = description.English;
        }

        [MenuItem("Tools/Nytherion/Relics/Simplify Descriptions")]
        public static void SimplifyDescriptions()
        {
            var results = new List<string>();
            var collection = LocalizationEditorSettings.GetStringTableCollection(LocalizationTables.Relics);
            foreach (string guid in AssetDatabase.FindAssets("t:RelicData",
                new[] { "Assets/Nytherion/Data/ScriptableObjects/Relics" }))
            {
                RelicData relic = AssetDatabase.LoadAssetAtPath<RelicData>(AssetDatabase.GUIDToAssetPath(guid));
                if (!relic.IsRuntimeAvailable) continue;
                string beforeKorean = relic.description_KR;
                string beforeEnglish = relic.description_EN;
                string beforeName = relic.koreanName;
                Undo.RecordObject(relic, "유물 설명 간소화");
                ApplyDescription(relic);
                if (relic.effectModules != null)
                    foreach (RelicEffectModule module in relic.effectModules)
                    {
                        if (module?.effects == null) continue;
                        foreach (RelicEffectBase effect in module.effects)
                            if (effect is GrantSkillEffect grant && grant.skillData != null && grant.skillData.IsRuntimeAvailable)
                            {
                                relic.koreanName = GetSkillRelicName(grant.skillData, relic.koreanName);
                                GetSkillDescriptions(grant.skillData, out relic.description_KR, out relic.description_EN);
                            }
                    }
                if (beforeKorean != relic.description_KR || beforeEnglish != relic.description_EN || beforeName != relic.koreanName)
                {
                    EditorUtility.SetDirty(relic);
                    AssetDatabase.SaveAssetIfDirty(relic);
                }
                if (collection != null)
                    foreach (StringTable table in collection.StringTables)
                    {
                        string code = table.LocaleIdentifier.Code;
                        if (!code.StartsWith("ko", StringComparison.OrdinalIgnoreCase) &&
                            !code.StartsWith("en", StringComparison.OrdinalIgnoreCase)) continue;
                        table.AddEntry(LocalizationKeys.RelicDescription(relic.relicName),
                            code.StartsWith("ko", StringComparison.OrdinalIgnoreCase) ? relic.description_KR : relic.description_EN);
                        table.AddEntry(LocalizationKeys.RelicName(relic.relicName),
                            code.StartsWith("ko", StringComparison.OrdinalIgnoreCase) ? relic.koreanName : relic.EnglishName);
                        EditorUtility.SetDirty(table);
                        AssetDatabase.SaveAssetIfDirty(table);
                    }
                results.Add(relic.relicName + ": " + relic.description_KR);
            }
            if (collection != null)
            {
                EditorUtility.SetDirty(collection.SharedData);
                AssetDatabase.SaveAssetIfDirty(collection.SharedData);
            }
            Directory.CreateDirectory(Output);
            File.WriteAllLines(Output + "/result.txt", results);
            Debug.Log("[RelicDescriptionSetup] 유물 효과 설명 간소화 완료");
        }
    }
}
