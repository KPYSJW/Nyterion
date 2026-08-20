using UnityEngine;
using System;
using System.Collections.Generic;
using Nytherion.Core.Utils;

namespace Nytherion.Gameplay.Relics.Modules
{
    [Serializable]
    public class RelicEffectModule
    {
        [Tooltip("UI에 표시할 한국어 효과 설명")]
        [TextArea] public string description_KR;

        [Tooltip("UI에 표시할 영어 효과 설명")]
        [TextArea] public string description_EN;

        public string Description => !string.IsNullOrEmpty(description_KR) ? description_KR : description_EN;

        [Tooltip("효과가 발동되기 위한 조건. 비워두면 항상 발동")]
        [SerializeReference, SubclassSelector] public RelicConditionBase condition;

        [Tooltip("조건이 맞을 때 발동할 효과들의 목록. 버프와 디버프 혼합 가능")]
        [SerializeReference, SubclassSelector] public List<RelicEffectBase> effects = new List<RelicEffectBase>();
    }
}
