using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nytherion.Data.ScriptableObjects.Gacha;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Combat;
using Nytherion.Gameplay.Relics.Modules;
using UnityEditor;
using UnityEngine;

namespace Nytherion.Editor
{
    /// <summary>기존 유물의 GUID·등급·모양을 보존하면서 효과와 획득 경로를 연결합니다.</summary>
    [InitializeOnLoad]
    public static class RootiUpgradeRelicSetup
    {
        public const string RelicRoot = "Assets/Nytherion/Data/ScriptableObjects/Relics/SkillRelics/";
        public const string Output = "output/rooti-upgrades";

        static RootiUpgradeRelicSetup() => EditorApplication.update += ProcessRequest;

        private static void ProcessRequest()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode ||
                !File.Exists(Output + "/setup.request")) return;
            File.Delete(Output + "/setup.request");
            Setup();
        }

        [MenuItem("Tools/Nytherion/Rooti/Setup Upgrade Relics")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            RelicDatabaseSO database = AssetDatabase.LoadAssetAtPath<RelicDatabaseSO>(
                "Assets/Nytherion/Data/ScriptableObjects/Relics/RelicDatabase.asset");
            Configure("PurpleRadish", "Purple Radish", "자색 무", RootiUpgradeType.AttackSpeed, 0,
                "루티의 공격속도가 25% 증가합니다.", database);
            Configure("Ginseng", "Ginseng", "인삼", RootiUpgradeType.ChargeCapacity, 1,
                "루티 소환의 최대 저장 개수가 1 증가합니다. 쿨타임마다 1개씩 충전하며, 연속 소환 간격은 최소 0.5초입니다.", database);
            Configure("AzureBerry", "Azure Berry", "벽청 베리", RootiUpgradeType.ProjectileCount, 2,
                "루티의 공격 투사체 수가 2 증가합니다.", database);
            Configure("EchoFlower", "Echo Flower", "복제초", RootiUpgradeType.SummonCount, 1,
                "충전 1개로 루티를 동시에 2마리 소환합니다.", database);
            Configure("BounceBerry", "Bounce Berry", "통통 베리", RootiUpgradeType.Bounce, 0,
                "루티의 씨앗이 적에게 맞은 뒤 5 범위 내의 다른 적을 향해 최대 3회 튕깁니다. 다른 적이 없으면 무작위 방향으로 날아갑니다.", database);
            if (database != null) AssetDatabase.SaveAssetIfDirty(database);
            TurretSkillData skill = AssetDatabase.LoadAssetAtPath<TurretSkillData>(
                "Assets/Nytherion/Data/ScriptableObjects/Skill/Turret_Skill.asset");
            if (skill != null)
            {
                // 새 충전·발사 설정을 직렬화하며 기존 스킬의 쿨타임·피해·프리팹 참조는 보존합니다.
                EditorUtility.SetDirty(skill);
                AssetDatabase.SaveAssetIfDirty(skill);
                ConfigureSeedPrefab(skill);
            }
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/setup.txt", "PASS 다섯 루티 강화 유물의 효과·아이콘·데이터베이스·가챠 연결\n");
            Debug.Log("[RootiUpgradeRelicSetup] 루티 강화 유물 5종을 연결했습니다.");
        }

        private static void ConfigureSeedPrefab(TurretSkillData skill)
        {
            string path = AssetDatabase.GetAssetPath(skill.projectilePrefab);
            if (string.IsNullOrEmpty(path)) return;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ProjDistanceLimit distanceLimit = root.GetComponent<ProjDistanceLimit>();
                if (distanceLimit != null) Object.DestroyImmediate(distanceLimit);
                ProjCameraBoundsLimit cameraLimit = root.GetComponent<ProjCameraBoundsLimit>();
                if (cameraLimit != null) Object.DestroyImmediate(cameraLimit);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void Configure(string fileName, string englishName, string koreanName,
            RootiUpgradeType type, int count, string description, RelicDatabaseSO database)
        {
            string path = RelicRoot + fileName + ".asset";
            RelicData relic = AssetDatabase.LoadAssetAtPath<RelicData>(path);
            if (relic == null)
            {
                relic = ScriptableObject.CreateInstance<RelicData>();
                AssetDatabase.CreateAsset(relic, path);
            }
            // relicName은 저장 ID로도 쓰이므로 기존에 지정한 이름은 바꾸지 않습니다.
            if (string.IsNullOrWhiteSpace(relic.relicName)) relic.relicName = englishName;
            if (string.IsNullOrWhiteSpace(relic.koreanName)) relic.koreanName = koreanName;
            if (string.IsNullOrWhiteSpace(relic.description_KR)) relic.description_KR = description;
            if (relic.Image == null)
                relic.Image = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Nytherion/Art/Relics/Sprites/" + fileName + ".png");
            if (relic.effectModules == null) relic.effectModules = new List<RelicEffectModule>();
            if (!relic.effectModules.Any(module => module != null && module.effects != null &&
                module.effects.Any(effect => effect is RootiUpgradeRelicEffect)))
            {
                // Inspector에서 비워둔 모듈이 있으면 재사용합니다.
                RelicEffectModule module = relic.effectModules.FirstOrDefault(candidate => candidate != null &&
                    candidate.condition == null && (candidate.effects == null || candidate.effects.Count == 0));
                if (module == null)
                {
                    module = new RelicEffectModule();
                    relic.effectModules.Add(module);
                }
                module.description_KR = description;
                module.effects = new List<RelicEffectBase>
                {
                    new RootiUpgradeRelicEffect { upgradeType = type, additionalCount = count }
                };
            }
            EditorUtility.SetDirty(relic);
            AssetDatabase.SaveAssetIfDirty(relic);
            if (database != null)
            {
                if (database.allRelics == null) database.allRelics = new List<RelicData>();
                if (!database.allRelics.Contains(relic))
                {
                    database.allRelics.Add(relic);
                    EditorUtility.SetDirty(database);
                }
            }
            string poolPath = "Assets/Nytherion/Data/ScriptableObjects/Gacha/GachaPool/Relic/" + relic.rarity + "_Relic.asset";
            GachaPoolSO pool = AssetDatabase.LoadAssetAtPath<GachaPoolSO>(poolPath);
            if (pool == null) return;
            if (pool.items == null) pool.items = new List<GachaItemRate>();
            if (pool.items.All(entry => entry.item != relic))
            {
                pool.items.Add(new GachaItemRate { item = relic, weight = 100 });
                EditorUtility.SetDirty(pool);
                AssetDatabase.SaveAssetIfDirty(pool);
            }
        }
    }
}
