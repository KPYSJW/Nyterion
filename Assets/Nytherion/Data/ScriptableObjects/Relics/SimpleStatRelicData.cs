using UnityEngine;
using System.Collections.Generic;
using Nytherion.Core.Data;
using Nytherion.Gameplay.Relics.Modules;

namespace Nytherion.Data.ScriptableObjects.Relics
{
    /// <summary>
    /// 기획자가 복잡한 모듈 조합 없이 직관적으로 스탯 증가 유물을 만들 수 있도록 돕는 템플릿 클래스입니다.
    /// 에디터에서는 스탯 리스트만 보여주며, 게임 실행 시 자동으로 내부 모듈로 변환됩니다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSimpleStatRelic", menuName = "Data/Relic (간편 스탯용)")]
    public class SimpleStatRelicData : RelicData
    {
        [Header("간편 스탯 설정 (Simple Stat Setup)")]
        [Tooltip("복잡한 Effect Modules 대신 여기에 스탯을 넣으면 자동으로 적용됩니다.")]
        public List<StatModifier> simpleStatModifiers = new List<StatModifier>();

        private bool isInitialized = false;

        private void OnEnable()
        {
            // 스크립터블 오브젝트가 로드될 때 (또는 게임 시작 시) 한 번만 모듈로 변환
            InitializeSimpleStats();
        }

        public void InitializeSimpleStats()
        {
            if (isInitialized) return;
            if (simpleStatModifiers == null || simpleStatModifiers.Count == 0) return;

            // 이미 StatRelicEffect가 있는지 확인
            bool hasStatEffect = false;
            foreach (var module in effectModules)
            {
                if (module.effects != null)
                {
                    foreach (var effect in module.effects)
                    {
                        if (effect is StatRelicEffect)
                        {
                            hasStatEffect = true;
                            break;
                        }
                    }
                }
            }

            // 없다면 자동으로 빈 조건과 함께 StatRelicEffect 모듈을 생성하여 삽입
            if (!hasStatEffect)
            {
                var statEffect = new StatRelicEffect();
                statEffect.statModifiers = new List<StatModifier>(simpleStatModifiers);

                var autoModule = new RelicEffectModule();
                autoModule.condition = null; // 항상 발동
                autoModule.effects.Add(statEffect);

                effectModules.Add(autoModule);
                isInitialized = true;
            }
        }
    }
}