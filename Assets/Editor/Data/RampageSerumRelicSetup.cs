using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nytherion.Core.Data;
using Nytherion.Core.Enums;
using Nytherion.Data.ScriptableObjects.Gacha;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.Gameplay.Relics.Modules;
using UnityEditor;
using UnityEngine;

namespace Nytherion.Editor
{
    /// <summary>오버드라이브를 폭주 혈청으로 옮기고 맨드레이크를 체력 유물로 전환합니다.</summary>
    [InitializeOnLoad]
    public static class RampageSerumRelicSetup
    {
        private const string Root = "Assets/Nytherion/Data/ScriptableObjects/";
        private const string SerumPath = Root + "Relics/SkillRelics/RampageSerum.asset";

        static RampageSerumRelicSetup() => EditorApplication.update += Update;

        private static void Update()
        {
            const string request = "output/rampage-serum/apply.request";
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(request)) return;
            File.Delete(request);
            try { Apply(); }
            catch (Exception error)
            {
                File.WriteAllText("output/rampage-serum/result.txt", "FAIL " + error);
                Debug.LogException(error);
            }
        }

        [MenuItem("Tools/Nytherion/Relics/폭주 혈청 설정")]
        public static void Apply()
        {
            RelicData mandrake = AssetDatabase.LoadAssetAtPath<RelicData>(Root + "Relics/SkillRelics/Mandrake.asset");
            SkillData skill = AssetDatabase.LoadAssetAtPath<SkillData>(Root + "Skill/Overdrive_Skill.asset");
            RelicDatabaseSO database = AssetDatabase.LoadAssetAtPath<RelicDatabaseSO>(Root + "Relics/RelicDatabase.asset");
            Require(mandrake != null && skill != null && database != null, "기존 유물·스킬·DB 누락");
            Sprite icon = ManaRelicSetup.ImportSprite("RampageSerum");
            Require(icon != null, "폭주 혈청 아이콘 누락");

            RelicData serum = AssetDatabase.LoadAssetAtPath<RelicData>(SerumPath);
            if (serum == null)
            {
                // 기존 스킬 유물의 등급과 고유 영향 칸을 이어받습니다.
                serum = UnityEngine.Object.Instantiate(mandrake);
                serum.name = "RampageSerum";
                serum.relicName = "RampageSerum";
                AssetDatabase.CreateAsset(serum, SerumPath);
            }
            serum.koreanName = "폭주 혈청";
            serum.englishName = "Rampage Serum";
            serum.Image = icon;
            serum.isDisabled = false;
            serum.effectModules = new List<RelicEffectModule>
            {
                new RelicEffectModule { effects = new List<RelicEffectBase> { new GrantSkillEffect { skillData = skill } } }
            };
            RelicDescriptionSetup.GetSkillDescriptions(skill, out serum.description_KR, out serum.description_EN);

            // 저장 ID와 GUID, 기존 아이콘 및 영향 칸은 그대로 유지합니다.
            mandrake.koreanName = "맨드레이크";
            mandrake.englishName = "Mandrake";
            mandrake.description_KR = "최대 체력 +60 (레벨당 +12).";
            mandrake.description_EN = "Max health +60 (+12 per level).";
            mandrake.effectModules = new List<RelicEffectModule>
            {
                new RelicEffectModule { effects = new List<RelicEffectBase>
                {
                    new StatRelicEffect { statModifiers = new List<StatModifier>
                    {
                        new StatModifier { stat = StatType.MaxHealth, value = 60f, valuePerLevel = 12f }
                    } }
                } }
            };

            if (!database.allRelics.Contains(serum)) database.allRelics.Add(serum);
            GachaPoolSO pool = AssetDatabase.LoadAssetAtPath<GachaPoolSO>(Root + "Gacha/GachaPool/Relic/" + serum.rarity + "_Relic.asset");
            Require(pool != null && pool.items != null, "가챠 풀 누락");
            if (!pool.items.Any(item => item.item == serum)) pool.items.Add(new GachaItemRate { item = serum, weight = 100 });
            ManaRelicSetup.Localize(mandrake);
            ManaRelicSetup.Localize(serum);
            foreach (UnityEngine.Object asset in new UnityEngine.Object[] { mandrake, serum, database, pool })
                EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            Require(mandrake.relicName == "Mandrake" && mandrake.Image != null && mandrake.Image != serum.Image,
                "맨드레이크 ID·아이콘 검증 실패");
            Require(mandrake.effectModules.SelectMany(module => module.effects).Single() is StatRelicEffect,
                "맨드레이크 능력치 효과 검증 실패");
            Require(serum.effectModules.SelectMany(module => module.effects).Single() is GrantSkillEffect grant && grant.skillData == skill,
                "폭주 혈청 스킬 연결 검증 실패");
            Require(database.allRelics.Count(relic => relic == serum) == 1 && pool.items.Count(item => item.item == serum) == 1,
                "유물 DB·가챠 중복 등록");
            Directory.CreateDirectory("output/rampage-serum");
            File.WriteAllText("output/rampage-serum/result.txt",
                "PASS 폭주 혈청: 전용 아이콘, 한영 표시, 오버드라이브, DB·가챠 연결\n" +
                "PASS 맨드레이크: 기존 ID·GUID 유지, 최대 체력 +60 / 레벨당 +12, 스킬 부여 제거\n");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
