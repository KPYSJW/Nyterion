using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nytherion.Core.Data;
using Nytherion.Core.Enums;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;
using Nytherion.Data.ScriptableObjects.Gacha;
using Nytherion.Data.ScriptableObjects.Player;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.Gameplay.Relics.Modules;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class ManaRelicSetup
    {
        private const string Output = "output/mana-relics";
        private const string DataRoot = "Assets/Nytherion/Data/ScriptableObjects/";
        private const string SpriteRoot = "Assets/Nytherion/Art/Relics/Sprites/";
        private static readonly string[] Ids = { "BlueFish", "BlueApple", "Stardust", "ManaCrystal", "BottledDawn", "PollutedPlant", "Astril", "WraithKey" };
        private static readonly string[] Names = { "청어", "청색 사과", "별빛가루", "자수정", "별칩이 담긴 병", "오염된 식물", "아스트릴", "망령의 열쇠" };
        private static readonly string[] English = { "Blue Fish", "Blue Apple", "Stardust", "Amethyst", "Bottled Star Chips", "Polluted Plant", "Astril", "Wraith Key" };
        private static readonly string[] Icons = { "BlueFish", "BlueApple", "ManaRelic03", "ManaRelic04", "ManaRelic05", "PollutedPlant", "Astril", "WraithKey" };
        private static readonly Rarity[] Rarities = { Rarity.Common, Rarity.Uncommon, Rarity.Rare, Rarity.Rare, Rarity.Epic, Rarity.Uncommon, Rarity.Rare, Rarity.Epic };
        private static readonly float[] Capacity = { 20f, 0f, 15f, 50f, 0f, -20f, 25f, 0.3f };
        private static readonly float[] CapacityGrowth = { 5f, 0f, 3f, 10f, 0f, 0f, 5f, 0.05f };
        private static readonly bool[] CapacityPercentage = { false, false, false, false, false, false, false, true };
        private static readonly float[] Regeneration = { 0f, 1f, 0.5f, 0f, 2f, 2f, 1f, 0f };
        private static readonly float[] RegenerationGrowth = { 0f, 0.25f, 0.1f, 0f, 0.4f, 0.5f, 0.2f, 0f };

        static ManaRelicSetup() => EditorApplication.update += Update;
        private static void Update()
        {
            if (!EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode &&
                File.Exists(Output + "/names.request"))
            {
                File.Delete(Output + "/names.request");
                try
                {
                    for (int i = 3; i <= 4; i++)
                    {
                        RelicData relic = AssetDatabase.LoadAssetAtPath<RelicData>(DataRoot + "Relics/Mana/" + Ids[i] + ".asset");
                        Require(relic != null && relic.relicName == Ids[i], "기존 유물 ID 확인");
                        relic.koreanName = Names[i];
                        relic.englishName = English[i];
                        Localize(relic);
                        EditorUtility.SetDirty(relic);
                    }
                    AssetDatabase.SaveAssets();
                    File.WriteAllText(Output + "/names.txt", "PASS 자수정 / 별칩이 담긴 병 표시 이름 변경, 기존 ID·효과·아이콘 유지");
                }
                catch (Exception error) { File.WriteAllText(Output + "/names.txt", "FAIL " + error); Debug.LogException(error); }
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            bool additional = File.Exists(Output + "/additional.request");
            if (!additional && !File.Exists(Output + "/setup.request")) return;
            File.Delete(Output + (additional ? "/additional.request" : "/setup.request"));
            try { ApplyFrom(additional ? 5 : 0); }
            catch (Exception error) { File.WriteAllText(Output + "/result.txt", "FAIL " + error); Debug.LogException(error); }
        }

        [MenuItem("Tools/Nytherion/Relics/마나 유물 및 WarpGear 설정")]
        public static void Apply()
        {
            ApplyFrom(0);
        }

        private static void ApplyFrom(int firstIndex)
        {
            Directory.CreateDirectory(Output);
            string folder = DataRoot + "Relics/Mana";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(DataRoot + "Relics", "Mana");
            RelicDatabaseSO database = AssetDatabase.LoadAssetAtPath<RelicDatabaseSO>(DataRoot + "Relics/RelicDatabase.asset");
            Require(database != null, "유물 데이터베이스 누락");
            var report = new System.Text.StringBuilder();
            for (int i = firstIndex; i < Ids.Length; i++)
            {
                string path = folder + "/" + Ids[i] + ".asset";
                SimpleStatRelicData relic = AssetDatabase.LoadAssetAtPath<SimpleStatRelicData>(path);
                if (relic == null)
                {
                    relic = ScriptableObject.CreateInstance<SimpleStatRelicData>();
                    relic.relicName = Ids[i];
                    AssetDatabase.CreateAsset(relic, path);
                }
                relic.koreanName = Names[i];
                relic.englishName = English[i];
                relic.Image = ImportSprite(Icons[i]);
                relic.rarity = Rarities[i];
                relic.level = 1;
                relic.isDisabled = false;
                relic.simpleStatModifiers = new List<StatModifier>();
                var korean = new List<string>();
                var english = new List<string>();
                if (Capacity[i] != 0f)
                {
                    relic.simpleStatModifiers.Add(new StatModifier { stat = StatType.MaxMana, value = Capacity[i], valuePerLevel = CapacityGrowth[i], isPercentage = CapacityPercentage[i] });
                    float factor = CapacityPercentage[i] ? 100f : 1f;
                    string suffix = CapacityPercentage[i] ? "%" : "";
                    string amount = (Capacity[i] * factor).ToString("+0.##;-0.##;0") + suffix;
                    string growth = (CapacityGrowth[i] * factor).ToString("+0.##;-0.##;0") + suffix;
                    korean.Add("최대 마나 " + amount + (CapacityGrowth[i] != 0f ? " (레벨당 " + growth + ")" : ""));
                    english.Add("Max Mana " + amount + (CapacityGrowth[i] != 0f ? " (" + growth + " per level)" : ""));
                }
                if (Regeneration[i] > 0f)
                {
                    relic.simpleStatModifiers.Add(new StatModifier { stat = StatType.ManaRegenPerSecond, value = Regeneration[i], valuePerLevel = RegenerationGrowth[i] });
                    korean.Add($"초당 마나 회복 +{Regeneration[i]} (레벨당 +{RegenerationGrowth[i]})");
                    english.Add($"Mana Regen +{Regeneration[i]}/sec (+{RegenerationGrowth[i]} per level)");
                }
                relic.description_KR = string.Join("\n", korean);
                relic.description_EN = string.Join("\n", english);
                relic.InitializeSimpleStats(true);
                if (Ids[i] == "Stardust")
                {
                    relic.simpleStatModifiers.Clear();
                    relic.effectModules = new List<RelicEffectModule> { new RelicEffectModule {
                        effects = new List<RelicEffectBase> { new ManaOnKillEffect() } } };
                    RelicDescriptionSetup.ApplyDescription(relic);
                }
                RelicInfluencePolicy.Normalize(relic);
                if (!database.allRelics.Contains(relic)) database.allRelics.Add(relic);
                Require(database.allRelics.Count(entry => entry != null && entry.relicName == Ids[i]) == 1, "유물 ID 중복: " + Ids[i]);
                GachaPoolSO pool = AssetDatabase.LoadAssetAtPath<GachaPoolSO>(DataRoot + "Gacha/GachaPool/Relic/" + relic.rarity + "_Relic.asset");
                Require(pool != null, "가챠 풀 누락");
                if (!pool.items.Any(rate => rate.item == relic)) pool.items.Add(new GachaItemRate { item = relic, weight = 100 });
                EditorUtility.SetDirty(pool);
                EditorUtility.SetDirty(relic);
                Localize(relic);
                VerifyStats(relic, i);
                report.AppendLine($"PASS {Names[i]} / {relic.rarity} / {relic.description_KR.Replace("\n", " / ")} / 아이콘·DB·가챠·1/3레벨 효과 적용 및 해제");
            }
            if (firstIndex == 0)
            {
                Sprite warp = ImportSprite("WarpGear");
                RelicData portal = AssetDatabase.LoadAssetAtPath<RelicData>(PortalGateSkillSetup.RelicPath);
                Require(portal != null, "차원문 유물 누락");
                portal.Image = warp;
                Require(portal.effectModules.Any(module => module.effects.Any(effect => effect is GrantSkillEffect grant &&
                    grant.skillData != null && grant.skillData.skillID == "skill_portal_gate")), "차원문 스킬 획득 효과");
                EditorUtility.SetDirty(portal);
                report.AppendLine("PASS WarpGear 차원문 유물 아이콘 연결, 기존 스킬 획득 효과 유지");
            }
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            report.AppendLine("PASS 마나 증가 시 현재량 유지 / 감소 시 상한 적용");
            File.WriteAllText(Output + "/result.txt", report.ToString());
        }

        internal static Sprite ImportSprite(string name)
        {
            string path = SpriteRoot + name + ".png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Require(importer != null, "아이콘 누락: " + name);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32f;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = true;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        internal static void Localize(RelicData relic)
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(LocalizationTables.Relics);
            if (collection == null) return;
            foreach (var table in collection.StringTables)
            {
                bool english = table.LocaleIdentifier.Code.StartsWith("en", StringComparison.OrdinalIgnoreCase);
                foreach (var pair in new[] {
                    new KeyValuePair<string, string>(LocalizationKeys.RelicName(relic.relicName), english ? relic.englishName : relic.koreanName),
                    new KeyValuePair<string, string>(LocalizationKeys.RelicDescription(relic.relicName), english ? relic.description_EN : relic.description_KR) })
                {
                    var entry = table.GetEntry(pair.Key) ?? table.AddEntry(pair.Key, pair.Value);
                    entry.Value = pair.Value;
                }
                EditorUtility.SetDirty(table);
            }
            EditorUtility.SetDirty(collection.SharedData);
        }

        private static void VerifyStats(SimpleStatRelicData source, int index)
        {
            if (Ids[index] == "Stardust")
            {
                Require(source.simpleStatModifiers.Count == 0 && source.effectModules.Count == 1 &&
                    source.effectModules[0].effects.Single() is ManaOnKillEffect,
                    "별빛가루는 상시 능력치 대신 처치 시 마나 회복 효과를 사용합니다.");
                return;
            }
            GameObject root = new GameObject("마나 유물 검증");
            PlayerData basis = ScriptableObject.CreateInstance<PlayerData>();
            SimpleStatRelicData relic = UnityEngine.Object.Instantiate(source);
            PlayerManager player = root.AddComponent<PlayerManager>();
            try
            {
                PlayerSkillManager mana = root.AddComponent<PlayerSkillManager>();
                var serialized = new SerializedObject(player);
                serialized.FindProperty("basePlayerData").objectReferenceValue = basis;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                player.Initialize();
                var effect = relic.effectModules.SelectMany(module => module.effects).OfType<StatRelicEffect>().Single();
                Require(mana.TrySpendMana(40f), "마나 소비 준비");
                foreach (int level in new[] { 1, 3 })
                {
                    effect.ApplyEffect(player, level);
                    float bonus = Capacity[index] + CapacityGrowth[index] * (level - 1);
                    float maximum = CapacityPercentage[index] ? 100f * (1f + bonus) : 100f + bonus;
                    float regeneration = 5f + Regeneration[index] + RegenerationGrowth[index] * (level - 1);
                    Require(Mathf.Approximately(mana.MaxMana, maximum) && Mathf.Approximately(mana.ManaRegenPerSecond, regeneration), "마나 능력치 적용");
                    Require(Mathf.Approximately(player.currentPlayerData.maxMana, maximum) && Mathf.Approximately(player.currentPlayerData.manaRegenPerSecond, regeneration), "인벤토리 데이터 동기화");
                    Require(mana.CurrentMana == 60f, "장착 시 임의 충전 방지");
                    effect.RemoveEffect(player, level);
                    Require(mana.MaxMana == 100f && mana.ManaRegenPerSecond == 5f && mana.CurrentMana == 60f, "해제 시 원복 및 미충전");
                }
                if (CapacityPercentage[index])
                {
                    var flatBonus = new StatModifier { stat = StatType.MaxMana, value = 50f };
                    player.AddTemporaryStatModifier(flatBonus);
                    effect.ApplyEffect(player, 1);
                    Require(Mathf.Approximately(mana.MaxMana, 150f * (1f + Capacity[index])), "고정·비율 마나 보너스 중첩");
                    effect.RemoveEffect(player, 1);
                    player.RemoveTemporaryStatModifier(flatBonus);
                    Require(mana.MaxMana == 100f && mana.CurrentMana == 60f, "중첩 효과 제거 후 복원");
                }
                mana.InitializeMana(150f, 5f);
                mana.UpdateManaStats(100f, 5f);
                Require(mana.CurrentMana == 100f, "최대 마나 감소 시 상한");
            }
            finally
            {
                PlayerData runtime = player.currentPlayerData;
                UnityEngine.Object.DestroyImmediate(root);
                if (runtime != null) UnityEngine.Object.DestroyImmediate(runtime);
                UnityEngine.Object.DestroyImmediate(basis);
                UnityEngine.Object.DestroyImmediate(relic);
            }
        }

        private static void Require(bool result, string message)
        {
            if (!result) throw new InvalidOperationException(message);
        }
    }
}
