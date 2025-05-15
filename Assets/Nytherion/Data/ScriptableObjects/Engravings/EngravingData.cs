using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.Data.Enums;

namespace Nytherion.Data.ScriptableObjects.Engravings
{
    [CreateAssetMenu(fileName = "NewEngravingData", menuName = "Data/Engraving")]
    public class EngravingData : ScriptableObject
    {
        public string engravingName;
        public string description;
        public bool isCursed;

        public WeaponSynergyType synergyType;  // 시너지 발동 조건
        public float attackMultiplier = 1f;
        public float cooldownMultiplier = 1f;
        public float speedMultiplier = 1f;

    }
}