using System.Collections.Generic;
using UnityEngine;
using Nytherion.Core.Enums;
using Nytherion.GamePlay.Relics;
using Nytherion.Core.Data;
using Nytherion.Gameplay.Relics.Modules;

namespace Nytherion.Data.ScriptableObjects.Relics
{
    [System.Serializable]
    public class InfluenceZone
    {
        [Tooltip("중심(0,0)으로부터의 상대 위치. x, y 모두 -1 ~ 1 사이의 값만 유효")]
        public Vector2Int offset;
        [Tooltip("해당 위치에 부여할 효과 종류 (레벨 업/다운)")]
        public InfluenceType type;
    }

    [CreateAssetMenu(fileName = "NewRelicData", menuName = "Data/Relic")]
    public class RelicData : ScriptableObject
    {
        [Header("기본정보")]
        public string relicName; // 영어 이름 (또는 ID 용도)
        public string koreanName;    // 한국어 이름 (UI 출력용)
        [TextArea] public string description_KR;
        [TextArea] public string description_EN;

        public string Description => !string.IsNullOrEmpty(description_KR) ? description_KR : description_EN;

        public Sprite Image;
        public Rarity rarity;

        [Header("레벨 정보")]
        public int level = 1;
        [HideInInspector] public bool isDisabled = false; // 실시간 비활성화 상태 (Silence 영향)

        [Header("복합 효과 및 조건 모듈")]
        public List<RelicEffectModule> effectModules = new List<RelicEffectModule>();

        [Header("각인 모양 (1x1 고정)")]
        public List<Vector2Int> shape = new List<Vector2Int> { Vector2Int.zero };

        [Header("영향 범위 설정 (고정)")]
        [Tooltip("이 각인이 주변에 영향을 미칠 영역의 목록")]
        public List<InfluenceZone> influenceZones = new List<InfluenceZone>();

        [Header("해금 설정")]
        [Tooltip("이 유물을 해금하기 위해 필요한 마일스톤 ID (비어있으면 기본 해금)")]
        public string unlockMilestoneID;

        private void OnValidate()
        {
            if (shape.Count != 1 || shape[0] != Vector2Int.zero)
            {
                shape.Clear();
                shape.Add(Vector2Int.zero);
            }

            foreach (var zone in influenceZones)
            {
                zone.offset.x = Mathf.Clamp(zone.offset.x, -1, 1);
                zone.offset.y = Mathf.Clamp(zone.offset.y, -1, 1);
                if (zone.offset == Vector2Int.zero)
                {
                    Debug.LogWarning($"'{relicName}'의 영향 범위 offset은 (0,0)이 될 수 없습니다.");
                }
            }
        }
    }
}