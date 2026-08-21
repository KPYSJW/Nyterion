using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Skill;
using UnityEngine;

namespace Nytherion.GamePlay.Skills
{
    /// <summary>
    /// 플레이어의 마우스 위치에 오브젝트 풀링을 활용하여 블랙홀 투사체를 소환하는 스킬 
    /// </summary>
    public class BlackholeSkill : SkillBase
    {
        [SerializeField] private string poolTag = "Blackhole";

        protected override void Activate()
        {
            if (skillData != null)
            {
                //  소환될 마우스 월드 위치 계산
                Vector3 spawnPosition = GetTargetPosition();

                // 풀 매니저를 통해 블랙홀 객체 획득
                GameObject blackholeInstance = ObjectPoolManager.Instance.SpawnFromPool(poolTag, spawnPosition, Quaternion.identity);

                if (blackholeInstance != null && blackholeInstance.TryGetComponent(out BlackholeField projectile))
                {
                    blackholeInstance.SetActive(true);

                    // 블랙홀 투사체 초기화
                    if (skillData is BlackholeSkillData bhData)
                    {
                        projectile.Initialize(bhData.damage, bhData.range, bhData.pullForce, bhData.duration, bhData.tickRate, bhData.enemyLayer, poolTag);
                    }
                    else
                    {
                        Debug.LogError("[BlackholeSkill] 할당된 skillData가 BlackholeSkillData가 아닙니다!");
                    }
                }
                else
                {
                    Debug.LogError($"[BlackholeSkill] 풀에서 오브젝트를 가져오지 못했거나 BlackholeField이 없습니다!");
                }
            }
        }

        /// <summary>
        /// 현재 마우스 커서의 픽셀 좌표를 게임 월드 좌표로 변환하여 반환
        /// </summary>
        private Vector3 GetTargetPosition()
        {
            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

            mouseWorldPos.z = 0f;

            return mouseWorldPos;
        }
    }
}