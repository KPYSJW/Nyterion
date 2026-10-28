using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nytherion.Core.Data;
using Nytherion.Core.Enums;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Gacha;
using Nytherion.Data.ScriptableObjects.Player;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.Gameplay.Relics.Modules;
using UnityEditor;
using UnityEngine;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class CursedPortraitSetup
    {
        private const string Output = "output/cursed-portrait";
        private const string Root = "Assets/Nytherion/Data/ScriptableObjects/";
        static CursedPortraitSetup() => EditorApplication.update += Update;
        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode ||
                !File.Exists(Output + "/setup.request")) return;
            File.Delete(Output + "/setup.request");
            try { Apply(); }
            catch (Exception error) { File.WriteAllText(Output + "/result.txt", "FAIL " + error); Debug.LogException(error); }
        }

        [MenuItem("Tools/Nytherion/Relics/망령의 초상화 설정")]
        public static void Apply()
        {
            Directory.CreateDirectory(Output);
            const string path = Root + "Relics/CombatUtility/CursedPortrait.asset";
            RelicData relic = AssetDatabase.LoadAssetAtPath<RelicData>(path);
            if (relic == null)
            {
                relic = ScriptableObject.CreateInstance<RelicData>();
                relic.relicName = "CursedPortrait";
                AssetDatabase.CreateAsset(relic, path);
            }
            relic.koreanName = "망령의 초상화";
            relic.englishName = "Cursed Portrait";
            relic.Image = ManaRelicSetup.ImportSprite("CursedPortrait");
            relic.rarity = Rarity.Rare;
            relic.description_KR = "최대 체력 -15%. 적을 직접 타격하면 마나 2 회복(레벨당 +0.5). 지속 피해와 연쇄 피해는 발동하지 않습니다.";
            relic.description_EN = "Max Health -15%. Direct hits on enemies restore 2 Mana (+0.5 per level). Damage over time and chain damage do not trigger this effect.";
            relic.effectModules = new List<RelicEffectModule> { new RelicEffectModule { effects = new List<RelicEffectBase> {
                new StatRelicEffect { statModifiers = new List<StatModifier> {
                    new StatModifier { stat = StatType.MaxHealth, value = -0.15f, isPercentage = true } } },
                new ManaOnHitEffect() } } };
            RelicInfluencePolicy.Normalize(relic);
            RelicDatabaseSO database = AssetDatabase.LoadAssetAtPath<RelicDatabaseSO>(Root + "Relics/RelicDatabase.asset");
            if (!database.allRelics.Contains(relic)) database.allRelics.Add(relic);
            Require(database.allRelics.Count(entry => entry != null && entry.relicName == relic.relicName) == 1, "유물 ID 중복");
            GachaPoolSO pool = AssetDatabase.LoadAssetAtPath<GachaPoolSO>(Root + "Gacha/GachaPool/Relic/Rare_Relic.asset");
            if (!pool.items.Any(rate => rate.item == relic)) pool.items.Add(new GachaItemRate { item = relic, weight = 100 });
            ManaRelicSetup.Localize(relic);
            foreach (UnityEngine.Object asset in new UnityEngine.Object[] { relic, database, pool }) EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Verify(relic);
            File.WriteAllText(Output + "/result.txt", "PASS 망령의 초상화 / Cursed Portrait / 희귀 / 이미지·DB·가챠 등록\n" +
                "PASS 최대 체력 -15%, 직접 적중 마나 +2, 3레벨 +3, 중복 구독 방지·해제·최대 마나 상한·사망 시 미발동·마나 UI 이벤트\n" +
                "PASS 지속·연쇄·0 피해·대상 없는 이벤트 미발동, 잘못된 회복량 차단");
        }

        private static void Verify(RelicData source)
        {
            GameObject root = new GameObject("초상화 효과 검증");
            RelicData relic = UnityEngine.Object.Instantiate(source);
            PlayerData basis = ScriptableObject.CreateInstance<PlayerData>();
            basis.maxHealth = 100f;
            PlayerManager player = root.AddComponent<PlayerManager>();
            PlayerHealth health = root.AddComponent<PlayerHealth>();
            PlayerSkillManager mana = root.AddComponent<PlayerSkillManager>();
            EventManager events = root.AddComponent<EventManager>();
            GameObject targetObject = new GameObject("초상화 타격 대상");
            TrainingDummy target = targetObject.AddComponent<TrainingDummy>();
            var properties = new SerializedObject(player);
            properties.FindProperty("basePlayerData").objectReferenceValue = basis;
            properties.ApplyModifiedPropertiesWithoutUndo();
            player.Construct(null, null, events, null);
            player.Initialize();
            var effects = relic.effectModules.SelectMany(module => module.effects).ToList();
            ManaOnHitEffect onHit = effects.OfType<ManaOnHitEffect>().Single();
            int notifications = 0;
            mana.ManaChanged += (current, maximum) => notifications++;
            try
            {
                foreach (RelicEffectBase effect in effects) effect.ApplyEffect(player, 1);
                Require(Mathf.Approximately(health.MaxHealth, 85f), "최대 체력 감소");
                Require(mana.TrySpendMana(40f), "마나 소비 준비");
                int before = notifications;
                events.TriggerEnemyDamagedByPlayerWithCrit(10f, false, target, false);
                Require(mana.CurrentMana == 62f && notifications == before + 1, "직접 적중 및 UI 갱신");
                events.TriggerEnemyDamagedByPlayerWithCrit(10f, true, target, false, true);
                events.TriggerEnemyDamagedByPlayerWithCrit(10f, false, target, true);
                events.TriggerEnemyDamagedByPlayerWithCrit(0f, false, target, false);
                events.TriggerEnemyDamagedByPlayerWithCrit(10f, false, null, false);
                Require(mana.CurrentMana == 62f, "지속·연쇄·무효 피해 미발동");
                onHit.ApplyEffect(player, 1);
                events.TriggerEnemyDamagedByPlayerWithCrit(10f, true, target, false);
                Require(mana.CurrentMana == 64f, "재적용 시 중복 회복 방지");
                onHit.RemoveEffect(player, 1);
                events.TriggerEnemyDamagedByPlayerWithCrit(10f, false, target, false);
                Require(mana.CurrentMana == 64f, "유물 해제 후 미발동");
                onHit.ApplyEffect(player, 3);
                events.TriggerEnemyDamagedByPlayerWithCrit(10f, false, target, false);
                Require(mana.CurrentMana == 67f, "레벨별 회복량");
                mana.RestoreMana(32f);
                events.TriggerEnemyDamagedByPlayerWithCrit(10f, false, target, false);
                Require(mana.CurrentMana == 100f, "최대 마나 초과 방지");
                Require(mana.RestoreMana(float.NaN) == 0f && mana.RestoreMana(float.PositiveInfinity) == 0f && mana.RestoreMana(-1f) == 0f, "유효하지 않은 회복량 차단");
                mana.TrySpendMana(50f);
                health.InitializeHealth(0f);
                events.TriggerEnemyDamagedByPlayerWithCrit(10f, false, target, false);
                Require(mana.CurrentMana == 50f, "사망 상태 미발동");
                foreach (RelicEffectBase effect in effects) effect.RemoveEffect(player, 1);
                Require(Mathf.Approximately(health.MaxHealth, 100f), "체력 감소 해제");
            }
            finally
            {
                foreach (RelicEffectBase effect in effects) effect.RemoveEffect(player, 1);
                PlayerData current = player.currentPlayerData;
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(targetObject);
                if (current != null) UnityEngine.Object.DestroyImmediate(current);
                UnityEngine.Object.DestroyImmediate(relic);
                UnityEngine.Object.DestroyImmediate(basis);
            }
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
