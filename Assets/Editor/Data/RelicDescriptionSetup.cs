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
                ["TargetMaker"] = ("불그늘·수호자의 지팡이·연쇄 점화의 공격 중심을 조준 지점으로 변경", "Centers Blazeshade, Guardian's Staff and Chain Ignition attacks on the aimed position."),
                ["WindStone"] = ("적중 시 20% 확률로 풍석 발사\n0.25초마다 적중 피해의 50% (레벨당 +10%p)", "On hit: 20% chance to fire a Wind Stone.\nDeals 50% of hit damage every 0.25s (+10pp per level)."),
                ["Zenith"] = ("파이로폴 추가 화염구가 화면 내 적을 자동 조준", "Pyrofall's extra fireballs automatically target enemies in view."),
                ["Rubber Ball"] = ("투사체가 주변 적에게 최대 3회 도탄", "Projectiles bounce to nearby enemies up to 3 times."),
                ["Globe"] = ("투사체가 적을 관통", "Projectiles pierce enemies."),
                ["ChargeRelic"] = ("무기 차징 공격 활성화", "Enables charged weapon attacks."),
                ["Haste Rune"] = ("충전 시간 -10%", "Charge time -10%."),
                ["Fire Pepper"] = ("화염 상태이상 피해 주기 -30%", "Burn damage interval -30%."),
                ["Trigger Piece"] = ("추가 투사체 +1", "Extra projectiles +1."),
                ["First Pip"] = ("치명타 확률 +4%p (레벨당 +1%p)\n눈 6종 장착 시 효과 2배", "Critical chance +4pp (+1pp per level).\nEquip all 6 Pips to double their effects."),
                ["Second Pip"] = ("치명타 피해 +15% (레벨당 +4%)\n눈 6종 장착 시 효과 2배", "Critical damage +15% (+4% per level).\nEquip all 6 Pips to double their effects."),
                ["Third Pip"] = ("근접 공격력 +3 (레벨당 +1)\n눈 6종 장착 시 효과 2배", "Melee attack +3 (+1 per level).\nEquip all 6 Pips to double their effects."),
                ["Fourth Pip"] = ("원거리 공격력 +3 (레벨당 +1)\n눈 6종 장착 시 효과 2배", "Ranged attack +3 (+1 per level).\nEquip all 6 Pips to double their effects."),
                ["Fifth Pip"] = ("근접 공격 속도 +10% (레벨당 +2%)\n눈 6종 장착 시 효과 2배", "Melee attack speed +10% (+2% per level).\nEquip all 6 Pips to double their effects."),
                ["Six Pip"] = ("원거리 공격 속도 +10% (레벨당 +2%)\n눈 6종 장착 시 효과 2배", "Ranged attack speed +10% (+2% per level).\nEquip all 6 Pips to double their effects.")
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

        public static void GetSkillDescriptions(SkillData skill, out string korean, out string english)
        {
            switch (skill.skillID)
            {
                case "skill_meteor_strike":
                    korean = "화면 내 모든 적에게 운석을 순차 낙하";
                    english = "Drops meteors on all enemies in view in succession.";
                    return;
                case "skill_decoy":
                    korean = "주변 적을 유인하는 더미 소환";
                    english = "Summons a decoy that taunts nearby enemies.";
                    return;
                case "skill_portal_gate":
                    korean = "[F]로 왕복하는 차원문 생성";
                    english = "Creates portals you can travel between with [F].";
                    return;
                case "skill_projectile_gate":
                    korean = "투사체를 분열시키는 게이트 생성";
                    english = "Creates a gate that splits projectiles.";
                    return;
            }

            korean = skill.description == null ? "" : skill.description.Trim();
            english = "";
            if (string.IsNullOrEmpty(skill.skillID)) return;
            if (LocalizationTranslationCatalog.SkillKoreanDescriptions.TryGetValue(skill.skillID, out string summary))
                korean = summary;
            if (LocalizationTranslationCatalog.SkillEnglishDescriptions.TryGetValue(skill.skillID, out string translation))
                english = translation;
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
                Undo.RecordObject(relic, "유물 설명 간소화");
                ApplyDescription(relic);
                if (relic.effectModules != null)
                    foreach (RelicEffectModule module in relic.effectModules)
                    {
                        if (module?.effects == null) continue;
                        foreach (RelicEffectBase effect in module.effects)
                            if (effect is GrantSkillEffect grant && grant.skillData != null && grant.skillData.IsRuntimeAvailable)
                                GetSkillDescriptions(grant.skillData, out relic.description_KR, out relic.description_EN);
                    }
                if (beforeKorean != relic.description_KR || beforeEnglish != relic.description_EN)
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
