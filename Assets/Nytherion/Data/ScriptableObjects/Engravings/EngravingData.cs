using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.Data.Enums;

namespace Nytherion.Data.ScriptableObjects.Engravings
{
    [CreateAssetMenu(fileName = "NewEngravingData", menuName = "Data/Engraving")]
    public class EngravingData : ScriptableObject
    {
        [Header("기본정보")]
        public string engravingName;
        [TextArea] public string description;
        public Sprite Image;
        public Rarity rarity;
        public bool isCursed;

        [Header("각인 모양 (블록 좌표)")]
        public List<Vector2Int> shape = new List<Vector2Int> { Vector2Int.zero };

        [Header("시너지 정보")]
        public WeaponSynergyType synergyType;
        public float attackMultiplier = 1f;
        public float cooldownMultiplier = 1f;
        public float speedMultiplier = 1f;
    }
}