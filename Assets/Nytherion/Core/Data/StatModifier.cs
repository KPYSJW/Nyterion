using System;
using Nytherion.Core.Enums;
using UnityEngine;

namespace Nytherion.Core.Data
{
    [Serializable]
    public class StatModifier
    {
        public StatType stat;
        public float value;
        [Tooltip("레벨업 시 추가로 증가하는 수치 (기본 1레벨 제외)")]
        public float valuePerLevel;
        public bool isPercentage;
    }
}