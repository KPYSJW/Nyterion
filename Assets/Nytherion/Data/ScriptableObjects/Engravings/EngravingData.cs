using System.Collections.Generic;
using UnityEngine;
using Nytherion.Core.Enums;
using Nytherion.GamePlay.Engravings;
using Nytherion.Core.Data;

namespace Nytherion.Data.ScriptableObjects.Engravings
{
    [System.Serializable]
    public class InfluenceZone
    {
        [Tooltip("중심(0,0)으로부터의 상대 위치. x, y 모두 -1 ~ 1 사이의 값만 유효합니다.")]
        public Vector2Int offset;
        [Tooltip("해당 위치에 부여할 효과 종류 (레벨 업/다운)")]
        public InfluenceType type;
    }

    [CreateAssetMenu(fileName = "NewEngravingData", menuName = "Data/Engraving")]
    public class EngravingData : ScriptableObject
    {
        [Header("기본정보")]
        public string engravingName;
        [TextArea] public string description;
        public Sprite Image;
        public Rarity rarity;
        public bool isCursed;

        [Header("레벨 정보")]
        public int level = 1;

        [Header("능력치 증가")]
        public List<StatModifier> statModifiers = new List<StatModifier>();

        [Header("각인 모양 (1x1 고정)")]
        public List<Vector2Int> shape = new List<Vector2Int> { Vector2Int.zero };

        [Header("영향 범위 설정 (고정)")]
        [Tooltip("이 각인이 주변에 영향을 미칠 영역의 목록입니다.")]
        public List<InfluenceZone> influenceZones = new List<InfluenceZone>();

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
                    Debug.LogWarning($"'{engravingName}'의 영향 범위 offset은 (0,0)이 될 수 없습니다.");
                }
            }
        }
    }
}