using System.Collections.Generic;
using UnityEngine;
using Nytherion.Core.Enums;
using Nytherion.GamePlay.Relics;
using Nytherion.Core.Data;
using Nytherion.Gameplay.Relics.Modules;
using Nytherion.Core.Utils;

namespace Nytherion.Data.ScriptableObjects.Relics
{
    [System.Serializable]
    public class InfluenceZone
    {
        [Tooltip("중심(0,0)으로부터의 상대 위치. x, y 모두 -1 ~ 1 사이의 값만 유효")]
        public Vector2Int offset;
        [Tooltip("해당 위치에 부여할 효과 종류 (레벨 업/다운)")]
        public InfluenceType type;

        [Range(1, 3)]
        [Tooltip("레벨 업/다운 변동량입니다. 레벨 영향이 아닌 특수 칸에서는 사용하지 않습니다.")]
        public int levelAmount = 1;

        public int GetLevelAmount()
        {
            return Mathf.Clamp(levelAmount <= 0 ? 1 : levelAmount, 1, 3);
        }
    }

    /// <summary>
    /// 유물 등급별 영향 범위를 한 곳에서 관리합니다.
    /// 일반 유물은 최대 다섯 칸 안에서 레벨 업/다운을 함께 사용하고,
    /// 침묵 또는 시너지 연결을 가진 특수 유물은 고유 영향권을 유지합니다.
    /// </summary>
    public static class RelicInfluencePolicy
    {
        private static readonly InfluenceZone[] CommonZones =
        {
            CreateZone(1, 0, InfluenceType.LevelUp, 1),
            CreateZone(0, 1, InfluenceType.LevelDown, 1),
            CreateZone(-1, 0, InfluenceType.LevelDown, 1),
            CreateZone(0, -1, InfluenceType.LevelDown, 1)
        };

        private static readonly InfluenceZone[] UncommonZones =
        {
            CreateZone(1, 0, InfluenceType.LevelUp, 1),
            CreateZone(0, 1, InfluenceType.LevelDown, 1),
            CreateZone(-1, 0, InfluenceType.LevelDown, 1)
        };

        private static readonly InfluenceZone[] RareZones =
        {
            CreateZone(0, 1, InfluenceType.LevelUp, 1),
            CreateZone(1, 0, InfluenceType.LevelUp, 1),
            CreateZone(0, -1, InfluenceType.LevelDown, 1),
            CreateZone(-1, 0, InfluenceType.LevelDown, 1)
        };

        private static readonly InfluenceZone[] EpicZones =
        {
            CreateZone(0, 1, InfluenceType.LevelUp, 2),
            CreateZone(1, 0, InfluenceType.LevelUp, 1),
            CreateZone(0, -1, InfluenceType.LevelUp, 1),
            CreateZone(-1, 0, InfluenceType.LevelDown, 1),
            CreateZone(-1, -1, InfluenceType.LevelDown, 1)
        };

        private static readonly InfluenceZone[] LegendaryZones =
        {
            CreateZone(0, 1, InfluenceType.LevelUp, 3),
            CreateZone(1, 0, InfluenceType.LevelUp, 2),
            CreateZone(0, -1, InfluenceType.LevelUp, 1),
            CreateZone(-1, 0, InfluenceType.LevelDown, 1)
        };

        private static readonly Vector2Int[] SpecialOffsets =
        {
            new Vector2Int(-1, 1),
            Vector2Int.up,
            Vector2Int.one,
            Vector2Int.left,
            Vector2Int.right,
            new Vector2Int(-1, -1),
            Vector2Int.down,
            new Vector2Int(1, -1)
        };

        public static IReadOnlyList<InfluenceZone> GetDefaultZones(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Common:
                    return CommonZones;
                case Rarity.Uncommon:
                    return UncommonZones;
                case Rarity.Rare:
                    return RareZones;
                case Rarity.Epic:
                    return EpicZones;
                case Rarity.Legendary:
                    return LegendaryZones;
                default:
                    return CommonZones;
            }
        }

        public static bool IsSpecialRelic(RelicData relic)
        {
            if (relic == null || relic.influenceZones == null) return false;

            foreach (InfluenceZone zone in relic.influenceZones)
            {
                if (zone != null &&
                    (zone.type == InfluenceType.Silence || zone.type == InfluenceType.SynergyLink))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool ContainsOffset(RelicData relic, Rarity rarity, Vector2Int offset)
        {
            if (IsSpecialRelic(relic))
            {
                for (int i = 0; i < SpecialOffsets.Length; i++)
                {
                    if (SpecialOffsets[i] == offset) return true;
                }

                return false;
            }

            foreach (InfluenceZone zone in GetDefaultZones(rarity))
            {
                if (zone.offset == offset) return true;
            }

            return false;
        }

        public static bool Normalize(RelicData relic)
        {
            if (relic == null) return false;

            if (relic.influenceZones == null)
            {
                relic.influenceZones = new List<InfluenceZone>();
            }

            if (IsSpecialRelic(relic))
            {
                bool changedSpecialAmount = false;
                foreach (InfluenceZone zone in relic.influenceZones)
                {
                    if (zone == null ||
                        (zone.type != InfluenceType.LevelUp && zone.type != InfluenceType.LevelDown))
                    {
                        continue;
                    }

                    int normalizedAmount = zone.GetLevelAmount();
                    if (zone.levelAmount != normalizedAmount)
                    {
                        zone.levelAmount = normalizedAmount;
                        changedSpecialAmount = true;
                    }
                }

                return changedSpecialAmount;
            }

            IReadOnlyList<InfluenceZone> defaultZones = GetDefaultZones(relic.rarity);
            bool isAlreadyNormalized = relic.influenceZones.Count == defaultZones.Count;
            for (int i = 0; isAlreadyNormalized && i < defaultZones.Count; i++)
            {
                InfluenceZone currentZone = relic.influenceZones[i];
                InfluenceZone normalizedZone = defaultZones[i];
                isAlreadyNormalized = currentZone != null &&
                                      currentZone.offset == normalizedZone.offset &&
                                      currentZone.type == normalizedZone.type &&
                                      currentZone.GetLevelAmount() == normalizedZone.levelAmount;
            }

            if (isAlreadyNormalized) return false;

            relic.influenceZones = new List<InfluenceZone>(defaultZones.Count);
            foreach (InfluenceZone zone in defaultZones)
            {
                relic.influenceZones.Add(CreateZone(
                    zone.offset.x,
                    zone.offset.y,
                    zone.type,
                    zone.levelAmount));
            }

            return true;
        }

        private static InfluenceZone CreateZone(int x, int y, InfluenceType type, int levelAmount)
        {
            return new InfluenceZone
            {
                offset = new Vector2Int(x, y),
                type = type,
                levelAmount = levelAmount
            };
        }
    }

    [CreateAssetMenu(fileName = "NewRelicData", menuName = "Data/Relic")]
    public class RelicData : ScriptableObject
    {
        [Header("기본정보")]
        public string relicName; // 영어 이름 
        public string koreanName;    // 한국어 이름 
        [TextArea] public string description_KR;
        [TextArea] public string description_EN;

        public string DisplayName => LocalizationText.Get(
            LocalizationTables.Relics,
            LocalizationKeys.RelicName(relicName),
            koreanName,
            relicName);
        public string Description => LocalizationText.Get(
            LocalizationTables.Relics,
            LocalizationKeys.RelicDescription(relicName),
            description_KR,
            description_EN);

        public Sprite Image;
        public Rarity rarity;

        [Header("레벨 정보")]
        public int level = 1;
        [HideInInspector] public bool isDisabled = false; 

        [Header("복합 효과 및 조건 모듈")]
        public List<RelicEffectModule> effectModules = new List<RelicEffectModule>();

        [Header("전투 보정")]
        [Tooltip("활성화 중인 동안 모든 원거리 투사체에 유도 기능을 부여합니다.")]
        public bool grantsProjectileHoming;

        [Header("각인 모양 (1x1 고정)")]
        public List<Vector2Int> shape = new List<Vector2Int> { Vector2Int.zero };

        [Header("영향 범위 설정 (등급별 고정)")]
        [Tooltip("일반 유물은 최대 다섯 칸의 레벨 업/다운 조합을 사용합니다. 침묵·시너지 연결 유물은 고유 영향권을 유지합니다.")]
        public List<InfluenceZone> influenceZones = new List<InfluenceZone>();

        [Header("시너지 설정")]
        [Tooltip("같은 계열의 시너지 부품임을 식별하는 ID (비어있으면 시너지 없음)")]
        public string synergySeriesId;

        [Tooltip("같은 계열 유물들이 공유하는 세트 보너스 데이터")]
        public RelicSetBonusData synergySetBonusData;

        [Header("해금 설정")]
        [Tooltip("이 유물을 해금하기 위해 필요한 마일스톤 ID (비어있으면 기본 해금)")]
        public string unlockMilestoneID;

        private void OnValidate()
        {
            if (shape == null)
            {
                shape = new List<Vector2Int>();
            }

            if (shape.Count != 1 || shape[0] != Vector2Int.zero)
            {
                shape.Clear();
                shape.Add(Vector2Int.zero);
            }

            RelicInfluencePolicy.Normalize(this);
        }
    }
}
