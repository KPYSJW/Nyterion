using UnityEngine;
using System.Collections.Generic;
using Nytherion.Core.Enums;
using Nytherion.Core.Data;


namespace Nytherion.Data.ScriptableObjects.Items
{
    public abstract class EquipmentData : ItemData
    {
        [Header("Equipment Settings")]
        public EquipmentType equipmentType;
        public Rarity rarity;

        [Header("Synergy & Traits")]
        [Tooltip("이 장비가 가지고 있는 특성(태그)들. 시너지 및 조건 발동에 사용됩니다.")]
        public List<EquipmentTrait> traits = new List<EquipmentTrait>();

        [Header("Stat Modifiers")]
        public List<StatModifier> statModifiers;

        /// <summary>
        /// 등급별 스탯 및 가격을 보정하기 위한 가상 메소드
        /// </summary>
        public virtual void ApplyRarityStats(Rarity targetRarity)
        {
            rarity = targetRarity;
        }

        protected virtual void OnEnable()
        {
            isStackable = false;
            maxStack = 1;
        }
    }
}