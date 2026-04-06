using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using UnityEngine;
using Nytherion.GamePlay.Combat;
using Nytherion.GamePlay.Skills;

namespace Nytherion.GamePlay.Characters.Skill
{
    public class SpiralSkill : SkillBase
    {
        [Header("Spiral Skill Settings")]
        public string projectilePoolTag = "SpiralProjectile";

        public int projectileCount = 3;

        protected override void Activate()
        {
            if (caster == null) return;

            float angleStep = 360f / projectileCount;

            for (int i = 0; i < projectileCount; i++)
            {
                GameObject proj = ObjectPoolManager.Instance.SpawnFromPool(projectilePoolTag, caster.position, Quaternion.identity);

                if (proj != null)
                {
                    if (proj.TryGetComponent<CollisionObject>(out var col))
                    {
                        col.damage = skillData.damage;
                    }

                    if (proj.TryGetComponent<SpiralMovement>(out var spiral))
                    {
                        spiral.SetupSpiral(angleStep * i, caster.position);
                    }
                }
            }
        }
    }
}