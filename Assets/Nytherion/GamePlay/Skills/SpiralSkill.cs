using Nytherion.Core.Managers;
using UnityEngine;
using Nytherion.GamePlay.Combat;
using Nytherion.GamePlay.Skills;
using VContainer;

namespace Nytherion.GamePlay.Characters.Skill
{
    /// <summary>
    /// 시전자를 중심으로 여러 개의 투사체를 나선형으로 발사하는 스킬
    /// </summary>
    public class SpiralSkill : SkillBase
    {
        [Header("Spiral Skill Settings")]
        public string projectilePoolTag = "SpiralProjectile";

        public int projectileCount = 3;

        /// <summary>
        /// 스킬이 사용될 때 호출되어 투사체들을 360도 방향으로 나누어 발사
        /// </summary>
        protected override void Activate()
        {
            if (caster == null) return;
            
            // 투사체 개수에 따라 360도를 등분하여 발사 각도 간격을 계산 
            float angleStep = 360f / projectileCount;

            for (int i = 0; i < projectileCount; i++)
            {
                // 오브젝트 풀에서 투사체 생성 (시전자 위치)
                GameObject proj = ObjectPoolManager.Instance.SpawnFromPool(projectilePoolTag, caster.position, Quaternion.identity);

                if (proj != null)
                {
                    // 충돌체 컴포넌트에 스킬 데이터의 데미지 적용
                    if (proj.TryGetComponent<CollisionObject>(out var col))
                    {
                        col.damage = skillData.damage;
                    }

                    // 나선형 움직임을 담당하는 컴포넌트 초기화
                    if (proj.TryGetComponent<SpiralMovement>(out var spiral))
                    {
                        // 각 투사체마다 angleStep만큼 회전된 각도를 할당
                        spiral.SetupSpiral(angleStep * i, caster.position);
                    }
                }
            }
        }
    }
}