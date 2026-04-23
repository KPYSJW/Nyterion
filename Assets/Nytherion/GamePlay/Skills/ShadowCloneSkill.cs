using UnityEngine;
using System.Collections;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Characters.Player;

namespace Nytherion.GamePlay.Skills
{
    /// <summary>
    /// 분신 스킬을 사용했을 때, 발동 로직을 처리하는 클래스
    /// </summary>
    public class ShadowCloneSkill : SkillBase
    {
        private ShadowCloneSkillData cloneData;
        private ShadowCloneController currentClone;

        /// <summary>
        /// 스킬 발동 시 호출되는 메서드
        /// </summary>
        protected override void Activate()
        {
            // 스킬 데이터 캐싱 및 타입 검증
            if (cloneData == null)
            {
                cloneData = skillData as ShadowCloneSkillData;
                // 타입이 맞지 않으면 에러 로그 출력 후 종료
                if (cloneData == null)
                {
                    Debug.LogError("[ShadowCloneSkill] SkillData가 ShadowCloneSkillData 타입이 아닙니다.");
                    return;
                }
            }

            // 컨트롤러 컴포넌트 캐싱
            if (currentClone == null)
            {
                currentClone = GetComponent<ShadowCloneController>();
                // 컨트롤러 컴포넌트가 없으면 에러 로그 출력 후 종료
                if (currentClone == null)
                {
                    Debug.LogError("[ShadowCloneSkill] ShadowCloneController 컴포넌트를 찾을 수 없습니다.");
                    return;
                }
            }

            // 최종 데미지 계산 : 기본 데미지 비율 + (레벨업 당 증가하는 비율 * (현재 스킬 레벨 - 1))
            float finalDamageRatio = cloneData.baseDamageRatio + (cloneData.damageRatioPerLevel * Mathf.Max(0, cloneData.skillLevel - 1));

            // 분신 시각 효과 활성화 및 초기화 
            currentClone.ActivateVisuals();
            PlayerCombat pCombat = caster.GetComponent<PlayerCombat>();
            currentClone.Initialize(pCombat, finalDamageRatio);
            
            // 이전 타이머를 초기화하고 지속 시간 후에 분신 제거 코루틴 시작
            StopAllCoroutines();
            StartCoroutine(DestroyCloneRoutine(cloneData.duration));
        }

        /// <summary>
        /// 지속 시간이 지난 후 분신을 비활성화하는 코루틴
        /// </summary>
        /// <param name="duration">지속 시간</param>
        /// <returns></returns>
        private IEnumerator DestroyCloneRoutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            if (currentClone != null)
            {
                currentClone.Deactivate();
                currentClone.DeactivateVisuals();
            }
        }
    }
}
