using UnityEngine;
using System;
using System.Collections.Generic;
using Nytherion.Core.Utils;

namespace Nytherion.Gameplay.Engravings.Modules
{
    [Serializable]
    public class EngravingEffectModule
    {
        [Tooltip("효과가 발동되기 위한 조건. 비워두면 항상 발동")]
        [SerializeReference, SubclassSelector] public EngravingConditionBase condition;

        [Tooltip("조건이 맞을 때 발동할 효과들의 목록. 버프와 디버프 혼합 가능")]
        [SerializeReference, SubclassSelector] public List<EngravingEffectBase> effects = new List<EngravingEffectBase>();
    }
}