using System;
using System.Collections.Generic;
using Nytherion.Data.ScriptableObjects.Relics;

namespace Nytherion.GamePlay.Combat
{
    /// <summary>
    /// 장착 유물이 바뀔 때만 다시 계산되는 전투 보정값입니다.
    /// 발사·충돌·피격 경로에서는 목록 순회 없이 이 스냅샷만 조회합니다.
    /// </summary>
    public sealed class CombatModifierSnapshot
    {
        public static readonly CombatModifierSnapshot Empty =
            new CombatModifierSnapshot(new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase), false);

        private readonly Dictionary<string, int> activeRelicLevels;

        public bool HasProjectilePiercing { get; }
        public bool HasProjectileBounce { get; }
        public bool HasProjectileHoming { get; }

        private CombatModifierSnapshot(Dictionary<string, int> activeRelicLevels, bool hasProjectileHoming)
        {
            this.activeRelicLevels = activeRelicLevels;
            HasProjectilePiercing = IsAnyActive("Piercing", "관통", "TangledYarn", "꼬인 실타래");
            HasProjectileBounce = IsAnyActive("Bounce", "튕김", "SqueakyGear", "삐걱이는 톱니");
            HasProjectileHoming = hasProjectileHoming;
        }

        public static CombatModifierSnapshot Create(IReadOnlyList<RelicData> relics)
        {
            Dictionary<string, int> levels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            bool hasProjectileHoming = false;
            if (relics != null)
            {
                for (int i = 0; i < relics.Count; i++)
                {
                    RelicData relic = relics[i];
                    if (relic == null || relic.isDisabled) continue;

                    AddLevel(levels, relic.name, relic.level);
                    AddLevel(levels, relic.relicName, relic.level);
                    AddLevel(levels, relic.koreanName, relic.level);
                    hasProjectileHoming |= relic.grantsProjectileHoming;
                }
            }

            return new CombatModifierSnapshot(levels, hasProjectileHoming);
        }

        public bool IsActive(string relicId)
        {
            return GetActiveLevel(relicId) > 0;
        }

        public int GetActiveLevel(string relicId)
        {
            if (string.IsNullOrWhiteSpace(relicId)) return 0;
            return activeRelicLevels.TryGetValue(relicId.Trim(), out int level) ? level : 0;
        }

        private bool IsAnyActive(params string[] relicIds)
        {
            for (int i = 0; i < relicIds.Length; i++)
            {
                if (IsActive(relicIds[i])) return true;
            }
            return false;
        }

        private static void AddLevel(Dictionary<string, int> levels, string relicId, int level)
        {
            if (string.IsNullOrWhiteSpace(relicId)) return;

            string normalizedId = relicId.Trim();
            int normalizedLevel = Math.Max(1, level);
            if (!levels.TryGetValue(normalizedId, out int currentLevel) || normalizedLevel > currentLevel)
            {
                levels[normalizedId] = normalizedLevel;
            }
        }
    }
}
