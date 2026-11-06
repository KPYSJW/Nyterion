using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nytherion.Core.Data;
using Nytherion.Core.Enums;
using Nytherion.Data.ScriptableObjects.Gacha;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.Gameplay.Relics.Modules;
using UnityEditor;
using UnityEngine;

namespace Nytherion.Editor
{
    /// <summary>스킬 유물 아이콘 교체와 기존 아이콘을 사용하는 능력치 유물을 설정합니다.</summary>
    [InitializeOnLoad]
    public static class SkillRelicIconReplacementSetup
    {
        private const string DataRoot = "Assets/Nytherion/Data/ScriptableObjects/";
        private const string Output = "output/skill-relic-icons";

        private sealed class Entry
        {
            public string relic, icon, id, korean, english, descriptionKr, descriptionEn;
            public StatType stat;
            public float value, growth;
            public bool percentage;
            public Rarity rarity;
        }

        private static readonly Entry[] Entries =
        {
            new Entry { relic = "GravityStone", icon = "SkillRelicBlackhole", id = "DenseStone", korean = "응축된 돌", english = "Dense Stone", stat = StatType.MaxMana, value = 20, growth = 5, rarity = Rarity.Common, descriptionKr = "최대 마나 +20 (레벨당 +5)", descriptionEn = "Max Mana +20 (+5 per level)" },
            new Entry { relic = "Relic_Decoy", icon = "SkillRelicDecoy", id = "TrainingEffigy", korean = "수련용 허수아비", english = "Training Effigy", stat = StatType.Defense, value = 3, growth = 1, rarity = Rarity.Common, descriptionKr = "방어력 +3 (레벨당 +1)", descriptionEn = "Defense +3 (+1 per level)" },
            new Entry { relic = "Meteorite", icon = "SkillRelicMeteor", id = "FallenStar", korean = "떨어진 별", english = "Fallen Star", stat = StatType.SkillDamage, value = 0.15f, growth = 0.05f, rarity = Rarity.Rare, descriptionKr = "스킬 피해 +15% (레벨당 +5%p)", descriptionEn = "Skill damage +15% (+5pp per level)" },
            new Entry { relic = "ScrollofShadowClone", icon = "SkillRelicShadowClone", id = "ShadowScript", korean = "그림자 비급", english = "Shadow Script", stat = StatType.CritChance, value = 0.04f, growth = 0.01f, rarity = Rarity.Rare, descriptionKr = "치명타 확률 +4%p (레벨당 +1%p)", descriptionEn = "Critical chance +4pp (+1pp per level)" },
            new Entry { relic = "Rooti", icon = "SkillRelicRooti", id = "SproutCharm", korean = "새싹 부적", english = "Sprout Charm", stat = StatType.MaxHealth, value = 30, growth = 6, rarity = Rarity.Uncommon, descriptionKr = "최대 체력 +30 (레벨당 +6)", descriptionEn = "Max health +30 (+6 per level)" },
            new Entry { relic = "Relic_ChainIgnition", icon = "SkillRelicChainIgnition", id = "EmberCube", korean = "불씨 큐브", english = "Ember Cube", stat = StatType.RangedDamage, value = 3, growth = 1, rarity = Rarity.Uncommon, descriptionKr = "원거리 공격력 +3 (레벨당 +1)", descriptionEn = "Ranged attack +3 (+1 per level)" }
        };

        static SkillRelicIconReplacementSetup() => EditorApplication.update += Update;

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists(Output + "/rename.request"))
            {
                File.Delete(Output + "/rename.request");
                try { RenameSkillRelics(); }
                catch (Exception error) { File.WriteAllText(Output + "/rename-result.txt", "FAIL " + error); Debug.LogException(error); }
                return;
            }
            if (!File.Exists(Output + "/apply.request")) return;
            File.Delete(Output + "/apply.request");
            try { Apply(); }
            catch (Exception error) { File.WriteAllText(Output + "/result.txt", "FAIL " + error); Debug.LogException(error); }
        }

        private static void RenameSkillRelics()
        {
            string[] korean = { "블랙홀 마법 책", "허수아비 마법 책", "메테오 마법 책", "그림자 분신 마법 책", "루티 마법 책", "연쇄 점화 마법 책" };
            string[] english = { "Black Hole Magic Book", "Decoy Magic Book", "Meteor Magic Book", "Shadow Clone Magic Book", "Rooti Magic Book", "Chain Ignition Magic Book" };
            for (int i = 0; i < Entries.Length; i++)
            {
                RelicData relic = AssetDatabase.LoadAssetAtPath<RelicData>(DataRoot + "Relics/SkillRelics/" + Entries[i].relic + ".asset");
                Require(relic != null, "스킬 유물 누락: " + Entries[i].relic);
                relic.koreanName = korean[i];
                relic.englishName = english[i];
                ManaRelicSetup.Localize(relic);
                EditorUtility.SetDirty(relic);
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText(Output + "/rename-result.txt", "PASS 스킬 유물 6종 한국어·영어 표시 이름 변경");
        }

        [MenuItem("Tools/Nytherion/Relics/스킬 유물 아이콘 교체 및 능력치 유물 설정")]
        public static void Apply()
        {
            Directory.CreateDirectory(Output);
            RelicDatabaseSO database = AssetDatabase.LoadAssetAtPath<RelicDatabaseSO>(DataRoot + "Relics/RelicDatabase.asset");
            Require(database != null && database.allRelics != null, "유물 데이터베이스 누락");
            const string folder = DataRoot + "Relics/Stat";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(DataRoot + "Relics", "Stat");

            foreach (Entry entry in Entries)
            {
                RelicData skillRelic = AssetDatabase.LoadAssetAtPath<RelicData>(DataRoot + "Relics/SkillRelics/" + entry.relic + ".asset");
                Require(skillRelic != null && skillRelic.Image != null && skillRelic.effectModules != null &&
                    skillRelic.effectModules.Where(m => m != null && m.effects != null).SelectMany(m => m.effects).OfType<GrantSkillEffect>().Any(e => e.skillData != null),
                    "스킬 유물 또는 기존 아이콘 누락: " + entry.relic);
                Sprite newIcon = ManaRelicSetup.ImportSprite(entry.icon);
                Require(newIcon != null, "새 아이콘 누락: " + entry.icon);
                string path = folder + "/" + entry.id + ".asset";
                SimpleStatRelicData relic = AssetDatabase.LoadAssetAtPath<SimpleStatRelicData>(path);
                if (relic == null)
                {
                    Require(skillRelic.Image != newIcon, "기존 아이콘을 확인할 수 없습니다: " + entry.relic);
                    Require(!database.allRelics.Any(r => r != null && r.relicName == entry.id), "유물 ID 중복: " + entry.id);
                    relic = ScriptableObject.CreateInstance<SimpleStatRelicData>();
                    relic.relicName = entry.id;
                    relic.Image = skillRelic.Image;
                    AssetDatabase.CreateAsset(relic, path);
                }
                relic.koreanName = entry.korean;
                relic.englishName = entry.english;
                relic.description_KR = entry.descriptionKr;
                relic.description_EN = entry.descriptionEn;
                relic.rarity = entry.rarity;
                relic.level = 1;
                relic.isDisabled = false;
                relic.simpleStatModifiers = new List<StatModifier> { new StatModifier { stat = entry.stat, value = entry.value, valuePerLevel = entry.growth, isPercentage = entry.percentage } };
                relic.InitializeSimpleStats(true);
                RelicInfluencePolicy.Normalize(relic);
                if (!database.allRelics.Contains(relic)) database.allRelics.Add(relic);
                GachaPoolSO pool = AssetDatabase.LoadAssetAtPath<GachaPoolSO>(DataRoot + "Gacha/GachaPool/Relic/" + entry.rarity + "_Relic.asset");
                Require(pool != null && pool.items != null, "가챠 풀 누락: " + entry.rarity);
                if (!pool.items.Any(item => item.item == relic)) pool.items.Add(new GachaItemRate { item = relic, weight = 100 });
                skillRelic.Image = newIcon;
                ManaRelicSetup.Localize(relic);
                EditorUtility.SetDirty(relic);
                EditorUtility.SetDirty(skillRelic);
                EditorUtility.SetDirty(pool);
                Require(relic.Image != null && relic.Image != newIcon && relic.effectModules.SelectMany(m => m.effects).OfType<StatRelicEffect>().Count() == 1 &&
                    !relic.effectModules.SelectMany(m => m.effects).OfType<GrantSkillEffect>().Any(), "능력치 유물 검증 실패: " + entry.id);
            }
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            File.WriteAllText(Output + "/result.txt", "PASS 스킬 유물 아이콘 6개, 기존 아이콘 능력치 유물 6개, 유물 DB와 가챠 연결");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
