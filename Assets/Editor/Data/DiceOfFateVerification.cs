using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Nytherion.Core.Data;
using Nytherion.Core.Enums;
using Nytherion.Core.Managers;
using Nytherion.Core.Utils;
using Nytherion.Data.ScriptableObjects.Enemy;
using Nytherion.Data.ScriptableObjects.Player;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Combat;
using Nytherion.Gameplay.Relics.Modules;
using Nytherion.GamePlay.Relics;
using Nytherion.UI.RelicBoard;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Nytherion.Editor
{
    /// <summary>실제 보드의 세트 활성화와 별도 피해 대상의 무작위 피해를 검증한다.</summary>
    [InitializeOnLoad]
    public static class DiceOfFateVerification
    {
        private const string Output = "output/dice-of-fate";
        private const string Pending = "Nytherion.DiceOfFate.Verify";
        private const string Root = "Assets/Nytherion/Data/ScriptableObjects/Relics/";
        private static readonly string[] Files = { "Dice1", "Dice2", "Dice3", "Dice4", "Dice5", "Dice6" };
        private static readonly List<string> results = new List<string>();
        private static readonly List<Object> temporary = new List<Object>();
        private static double readyAt;

        static DiceOfFateVerification()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
                    readyAt = EditorApplication.timeSinceStartup + 5d;
            };
        }

        [MenuItem("Tools/Nytherion/Relics/운명의 육면체 검증")]
        public static void Request()
        {
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/verify.request", "");
        }

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (File.Exists(Output + "/verify.request"))
            {
                File.Delete(Output + "/verify.request");
                SessionState.SetBool(Pending, true);
                SessionState.SetBool(Pending + ".Exit", !EditorApplication.isPlaying);
                readyAt = EditorApplication.timeSinceStartup + 5d;
                if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
            }
            if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying ||
                EditorApplication.timeSinceStartup < readyAt) return;
            SessionState.SetBool(Pending, false);
            results.Clear();
            Random.State previousRandom = Random.state;
            SupportedLanguage previousLanguage = LocalizationText.CurrentLanguage;
            try
            {
                VerifyBoard();
                VerifyDamage();
            }
            catch (Exception error) { results.Add("FAIL " + error); }
            finally
            {
                Random.state = previousRandom;
                LocalizationText.SetTemporaryLanguage(previousLanguage);
                for (int i = temporary.Count - 1; i >= 0; i--)
                    if (temporary[i] != null) Object.DestroyImmediate(temporary[i]);
                temporary.Clear();
                Directory.CreateDirectory(Output);
                File.WriteAllLines(Output + "/verification.txt", results);
                if (SessionState.GetBool(Pending + ".Exit", false)) EditorApplication.isPlaying = false;
            }
        }

        private static void VerifyBoard()
        {
            RelicManager manager = Object.FindObjectOfType<RelicManager>();
            PlayerManager player = Object.FindObjectOfType<PlayerManager>();
            Require(manager != null && player != null, "GameScene의 실제 유물 매니저와 플레이어 참조");
            RelicEffectController effects = player.GetComponent<RelicEffectController>();
            Require(effects != null, "실제 유물 효과 컨트롤러 참조");
            RelicSetBonusData set = AssetDatabase.LoadAssetAtPath<RelicSetBonusData>(Root + "DiceOfFate.asset");
            RelicData[] relics = Files.Select(file => AssetDatabase.LoadAssetAtPath<RelicData>(Root + "DiceOfFate/" + file + ".asset")).ToArray();
            Require(relics.All(relic => relic != null && relic.IsRuntimeAvailable && relic.synergySetBonusData == set),
                "주사위 6종 획득 가능 및 공유 세트 Inspector 참조");
            Require(relics.All(relic => !relic.effectModules.Any(module => module.effects.Any(effect => effect is PipCompletionEffect))),
                "개별 주사위의 이전 완성 효과 제거");
            SaveData original = new SaveData();
            manager.PopulateSaveData(original);
            try
            {
                Load(manager, relics);
                effects.ReevaluateAllConditions();
                PipCompletionRuntime runtime = player.GetComponent<PipCompletionRuntime>();
                Require(runtime != null && Mathf.Approximately(runtime.MinimumBonusRatio, 0.2f) &&
                    Mathf.Approximately(runtime.MaximumBonusRatio, 0.6f) && set.IsAnyTierActive(manager),
                    "6세트 장착 시 20~60% 추가 피해 활성화");
                string stats = JsonUtility.ToJson(player.currentPlayerData);
                for (int i = 0; i < 4; i++) effects.ReevaluateAllConditions();
                Require(Mathf.Approximately(runtime.MinimumBonusRatio, 0.2f) && Mathf.Approximately(runtime.MaximumBonusRatio, 0.6f) &&
                    stats == JsonUtility.ToJson(player.currentPlayerData),
                    "조건 반복 평가 시 효과 중복 및 능력치 2배 적용 없음");
                SaveData saved = new SaveData();
                manager.PopulateSaveData(saved);
                manager.LoadFromSaveData(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(saved)));
                effects.ReevaluateAllConditions();
                Require(manager.GetEquippedRelics().Select(relic => relic.relicName).OrderBy(id => id)
                    .SequenceEqual(relics.Select(relic => relic.relicName).OrderBy(id => id)) &&
                    Mathf.Approximately(runtime.MinimumBonusRatio, 0.2f) && Mathf.Approximately(runtime.MaximumBonusRatio, 0.6f),
                    "기존 ID 저장/로드 후 6세트 효과 복원");
                float[] minimums = { 0f, 0f, -0.1f, -0.1f, 0f, 0f, 0.2f };
                float[] maximums = { 0f, 0f, 0.2f, 0.2f, 0.4f, 0.4f, 0.6f };
                foreach (int count in new[] { 0, 1, 2, 3, 4, 5, 6, 5, 3, 1, 0, 4, 2, 6 })
                {
                    Load(manager, relics.Take(count).ToArray());
                    effects.ReevaluateAllConditions();
                    for (int repeat = 0; repeat < 3; repeat++) effects.ReevaluateAllConditions();
                    Require(Mathf.Approximately(runtime.MinimumBonusRatio, minimums[count]) &&
                        Mathf.Approximately(runtime.MaximumBonusRatio, maximums[count]) &&
                        set.IsAnyTierActive(manager) == (count >= 2), count + "개 장착 시 최고 단계 한 개만 적용 및 단계 전환 정상");
                }
                for (int missing = 0; missing < relics.Length; missing++)
                {
                    Load(manager, relics.Where((relic, index) => index != missing).ToArray());
                    effects.ReevaluateAllConditions();
                    Require(runtime.MinimumBonusRatio == 0f && Mathf.Approximately(runtime.MaximumBonusRatio, 0.4f) && set.IsAnyTierActive(manager),
                        "주사위 " + (missing + 1) + " 해제 시 4세트 단계로 전환");
                }
                RelicTooltip tooltip = Object.FindObjectOfType<RelicTooltip>(true);
                Require(tooltip != null, "실제 씬의 툴팁 Inspector 참조");
                try
                {
                    foreach (SupportedLanguage language in new[] { SupportedLanguage.Korean, SupportedLanguage.English })
                    {
                        LocalizationText.SetTemporaryLanguage(language);
                        for (int i = 0; i < relics.Length; i++)
                        {
                            tooltip.Show(new RelicBlock(relics[i]));
                            string badge = ((TextMeshProUGUI)Get(tooltip, "setText")).text;
                            string body = ((TextMeshProUGUI)Get(tooltip, "descriptionText")).text;
                            string title = ((TextMeshProUGUI)Get(tooltip, "nameText")).text;
                            string expectedSet = language == SupportedLanguage.Korean ? "운명의 육면체" : "Dice of Fate";
                            string expectedName = (language == SupportedLanguage.Korean ? "주사위 " : "Dice ") + (i + 1);
                            Require(badge == expectedSet && title == expectedName &&
                                body.Contains(language == SupportedLanguage.Korean ? "-10~20%" : "-10% to +20%") &&
                                body.Contains("20%") && body.Contains("40%") && body.Contains("60%") &&
                                !body.Contains("2배") && !body.Contains("double"), language + " 주사위 " + (i + 1) + " 이름/배지/새 효과 설명");
                        }
                    }
                }
                finally { tooltip.Hide(); }
            }
            finally
            {
                manager.LoadFromSaveData(original);
                effects.ReevaluateAllConditions();
            }
        }

        private static void VerifyDamage()
        {
            GameObject root = Track(new GameObject("[운명의 육면체 검증] 플레이어"));
            PlayerManager player = root.AddComponent<PlayerManager>();
            EventManager events = Track(new GameObject("[운명의 육면체 검증] 이벤트")).AddComponent<EventManager>();
            PlayerData data = Track(ScriptableObject.CreateInstance<PlayerData>());
            data.critChance = -1f;
            data.critDamageMultiplier = 2f;
            player.currentPlayerData = data;
            var effect = new PipCompletionEffect { minimumBonusRatio = 0.2f, maximumBonusRatio = 0.6f };
            GameObject target = Track(new GameObject("[운명의 육면체 검증] 피해 대상"));
            TrainingDummy enemy = target.AddComponent<TrainingDummy>();
            EnemyData enemyData = Track(ScriptableObject.CreateInstance<EnemyData>());
            enemyData.maxHealth = 1000000f;
            enemy.Construct(events, null, player);
            enemy.Initialize(enemyData);
            float damage = 0f;
            int hits = 0;
            events.OnEnemyDamagedByPlayerDetailed += hit => { damage = hit.DamageAmount; hits++; };

            enemy.TakeDamage(100f);
            Require(damage == 100f && hits == 1, "세트 미활성 기본 피해 유지");
            int before;
            Vector2[] ranges = { new Vector2(-0.1f, 0.2f), new Vector2(0f, 0.4f), new Vector2(0.2f, 0.6f) };
            for (int tier = 0; tier < ranges.Length; tier++)
            {
                effect.minimumBonusRatio = ranges[tier].x;
                effect.maximumBonusRatio = ranges[tier].y;
                effect.ApplyEffect(player, 1);
                Random.InitState(20261020);
                double sum = 0d;
                float minimum = float.MaxValue;
                float maximum = 0f;
                float lowerBound = 100f * (1f + ranges[tier].x);
                float upperBound = 100f * (1f + ranges[tier].y);
                before = hits;
                for (int i = 0; i < 2048; i++)
                {
                    enemy.TakeDamage(100f);
                    if (damage < lowerBound - 0.001f || damage > upperBound + 0.001f)
                        throw new InvalidOperationException("무작위 피해 범위 초과: " + damage);
                    minimum = Mathf.Min(minimum, damage);
                    maximum = Mathf.Max(maximum, damage);
                    sum += damage;
                }
                double average = sum / 2048d;
                Require(hits == before + 2048 && minimum < lowerBound + 2f && maximum > upperBound - 2f &&
                    Math.Abs(average - (lowerBound + upperBound) / 2d) < 2d,
                    (tier + 1) * 2 + "세트 2048회: 피해 " + lowerBound + "~" + upperBound + ", 평균 " +
                    average.ToString("F2") + ", 적중당 이벤트 1회");
            }
            enemy.TakeDamage(100f, true);
            Require(damage == 100f, "연쇄 추가 피해의 재추첨 없음");
            enemy.TakeDamageOverTime(100f);
            Require(damage == 100f, "지속 피해의 재추첨 없음");
            var fire = new FireEffect(100f, 5f);
            fire.Initialize(enemy, target.GetComponent<StatusEffectManager>(), 5f);
            before = hits;
            fire.OnUpdate(1f);
            Require(damage == 100f && hits == before + 1, "실제 화상 틱에서 재추첨 없음");
            var poison = new PoisonEffect(100f, 5f);
            poison.Initialize(enemy, target.GetComponent<StatusEffectManager>(), 5f);
            before = hits;
            poison.OnUpdate(1f);
            Require(damage == 100f && hits == before + 1, "실제 독 틱에서 재추첨 없음");
            data.critChance = 1f;
            before = hits;
            target.GetComponent<StatusEffectManager>().ApplyEffect(new CurseEffect(1.2f, 10f));
            enemy.TakeDamage(100f);
            Require(damage >= 288f && damage <= 384.001f && hits == before + 1,
                "치명타·받는 피해 배율과 추가 피해를 한 적중에서 한 번만 계산");
            data.critChance = -1f;
            target.GetComponent<StatusEffectManager>().ClearAllEffects();
            effect.ApplyEffect(player, 1);
            Require(Mathf.Approximately(player.GetComponent<PipCompletionRuntime>().MinimumBonusRatio, 0.2f) &&
                Mathf.Approximately(player.GetComponent<PipCompletionRuntime>().MaximumBonusRatio, 0.6f), "중복 적용 시 추가 피해 누적 없음");
            effect.RemoveEffect(player, 1);
            enemy.TakeDamage(100f);
            Require(damage == 100f, "세트 해제 후 기본 피해 복원");
            effect.ApplyEffect(player, 1);
            enemy.Initialize(enemyData);
            enemy.TakeDamage(100f);
            Require(damage >= 120f && damage <= 160.001f, "적 재사용·세트 재장착 후 추가 피해 정상");
            effect.RemoveEffect(player, 1);
        }

        private static void Load(RelicManager manager, RelicData[] relics)
        {
            SaveData save = new SaveData();
            for (int i = 0; i < relics.Length; i++)
                save.relicData.placedBlocks.Add(new RelicGridState.SavedRelicBlock
                { relicId = relics[i].relicName, gridRow = i / 5, gridCol = i % 5 });
            manager.LoadFromSaveData(save);
        }

        private static void Require(bool success, string message)
        {
            if (!success) throw new InvalidOperationException(message);
            results.Add("PASS " + message);
        }
        private static T Track<T>(T value) where T : Object { temporary.Add(value); return value; }
        private static object Get(object target, string field) => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }
}
