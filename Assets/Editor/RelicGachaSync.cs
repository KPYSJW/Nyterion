using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.Data.ScriptableObjects.Gacha;
using Nytherion.Core.Enums;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class RelicGachaSync
    {
        private const string GACHA_POOL_BASE_PATH = "Assets/Nytherion/Data/ScriptableObjects/Gacha/GachaPool/Relic";

        static RelicGachaSync()
        {
            // Assembly reload 시 자동으로 1회 동기화 수행
            // 메인 스레드 대기 후 실행되도록 지연 호출
            EditorApplication.delayCall += () =>
            {
                SyncRelicsToGachaPools(false);
            };
        }

        [MenuItem("Nytherion/Sync Relics to Gacha Pools")]
        public static void SyncManual()
        {
            SyncRelicsToGachaPools(true);
        }

        public static void SyncRelicsToGachaPools(bool forceShowDialog)
        {
            string[] relicGuids = AssetDatabase.FindAssets("t:RelicData");
            if (relicGuids.Length == 0)
            {
                if (forceShowDialog)
                {
                    EditorUtility.DisplayDialog("Gacha Sync", "프로젝트에서 유물 데이터를 찾지 못했습니다.", "확인");
                }
                return;
            }

            // 등급별 가챠 풀SO 로드
            Dictionary<Rarity, GachaPoolSO> pools = new Dictionary<Rarity, GachaPoolSO>();
            bool anyPoolModified = false;

            foreach (Rarity rarity in Enum.GetValues(typeof(Rarity)))
            {
                string poolPath = $"{GACHA_POOL_BASE_PATH}/{rarity}_Relic.asset";
                GachaPoolSO pool = AssetDatabase.LoadAssetAtPath<GachaPoolSO>(poolPath);
                if (pool != null)
                {
                    pools[rarity] = pool;
                    if (pool.items == null)
                    {
                        pool.items = new List<GachaItemRate>();
                    }
                }
            }

            // 모든 유물 검색하여 해당하는 등급의 가챠 풀에 매핑
            Dictionary<Rarity, List<RelicData>> relicsByRarity = new Dictionary<Rarity, List<RelicData>>();
            foreach (Rarity rarity in Enum.GetValues(typeof(Rarity)))
            {
                relicsByRarity[rarity] = new List<RelicData>();
            }

            foreach (string guid in relicGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                RelicData relic = AssetDatabase.LoadAssetAtPath<RelicData>(path);
                if (relic != null)
                {
                    relicsByRarity[relic.rarity].Add(relic);
                }
            }

            // 각 가챠 풀 리스트 동기화
            int updatedCount = 0;
            foreach (KeyValuePair<Rarity, GachaPoolSO> pair in pools)
            {
                Rarity rarity = pair.Key;
                GachaPoolSO pool = pair.Value;
                List<RelicData> targetRelics = relicsByRarity[rarity];

                // 정렬을 기준삼아 기존 리스트와 비교
                List<RelicData> sortedTargetRelics = targetRelics.OrderBy(r => r.relicName).ToList();
                List<RelicData> currentRelicsInPool = pool.items
                    .Where(i => i.item != null)
                    .Select(i => i.item as RelicData)
                    .OrderBy(r => r.relicName)
                    .ToList();

                // 가물의 개수나 포함 내용이 다른지 감지
                bool listChanged = sortedTargetRelics.Count != currentRelicsInPool.Count;
                if (!listChanged)
                {
                    for (int i = 0; i < sortedTargetRelics.Count; i++)
                    {
                        if (sortedTargetRelics[i] != currentRelicsInPool[i])
                        {
                            listChanged = true;
                            break;
                        }
                    }
                }

                if (listChanged)
                {
                    Undo.RecordObject(pool, "Sync Gacha Pool Relics");

                    // 기존 유물들 중 여전히 유효한 것들만 필터링 혹은 아예 새 리스트 빌드
                    List<GachaItemRate> newItems = new List<GachaItemRate>();
                    foreach (RelicData relic in sortedTargetRelics)
                    {
                        GachaItemRate existingRate = pool.items.FirstOrDefault(itemRate => itemRate.item == relic);
                        int weight = existingRate != null ? existingRate.weight : 100;
                        newItems.Add(new GachaItemRate { item = relic, weight = weight });
                    }

                    pool.items = newItems;
                    EditorUtility.SetDirty(pool);
                    anyPoolModified = true;
                    updatedCount += sortedTargetRelics.Count;
                }
            }

            if (anyPoolModified)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"[RelicGachaSync] 유물 가챠 풀 동기화 완료: 총 {updatedCount}개 유물 등록.");
                if (forceShowDialog)
                {
                    EditorUtility.DisplayDialog("Gacha Sync 완료", $"총 {updatedCount}개의 유물이 등급별 가챠 풀에 동기화되었습니다.", "확인");
                }
            }
            else
            {
                if (forceShowDialog)
                {
                    EditorUtility.DisplayDialog("Gacha Sync", "이미 모든 가챠 풀이 최신 상태입니다.", "확인");
                }
            }
        }
    }
}
