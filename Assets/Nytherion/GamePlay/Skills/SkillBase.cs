using Nytherion.Data.ScriptableObjects.Skill;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Nytherion.GamePlay.Skills
{
    /// <summary>
    /// 게임 내 모든 스킬의 뼈대가 되는 추상 기본 클래스
    /// 스킬의 데이터 참조, 시전자 정보, 쿨다운 관리 및 기본 실행 흐름을 담당
    /// </summary>
    public abstract class SkillBase : MonoBehaviour
    {
        /// <summary> 스킬의 기본 정보와 능력치를 담고 있는 데이터/// </summary>
        public SkillData skillData;

        /// <summary> 스킬을 시전 한 주체의 Transform/// </summary>
        [System.NonSerialized] public Transform caster;

        /// <summary> 발사체 기반  스킬인 경우 발사되는 기준 위치/// </summary>
        [System.NonSerialized] public Transform firePoint;

        /// <summary> 마지막으로 스킬을 사용한 시간 (초기값은 게임 시작 즉시 사용 가능하도록 음수 무한대로 설정/// </summary>
        [System.NonSerialized] private float lastUseTime = -Mathf.Infinity;


        /// <summary>
        ///  스킬 사용을 시도. 사용 가능하다면 스킬을 활성화하고 쿨다운을 초기화
        /// </summary>
        /// <returns></returns>
        public bool CanUse() => Time.time > lastUseTime + skillData.coolDown;

        /// <summary>
        /// 실제 스킬의 효과나 로직이 구현되는 메서드
        /// </summary>
        public void TryUse()
        {

            if (CanUse())
            {
                Activate();
                lastUseTime = Time.time;
            }
        }

        /// <summary>
        /// 실제 스킬의 효과나 로직이 구현되는 메서드
        /// </summary>
        protected abstract void Activate();

        /// <summary>
        /// 스킬의 전체 쿨다운 시간을 반환
        /// </summary>
        public float GetCooldownTime() => skillData.coolDown;

        /// <summary>
        /// 스킬이 다시 사용 가능해질 때까지 남은 시간을 반환
        /// </summary>
        /// <returns>남은 쿨다운 시간(최소 0초)</returns>
        public float GetRemainingCooldown()
        {
            float remaining = (lastUseTime + skillData.coolDown) - Time.time;
            return Mathf.Max(0f, remaining);
        }
    }
}

