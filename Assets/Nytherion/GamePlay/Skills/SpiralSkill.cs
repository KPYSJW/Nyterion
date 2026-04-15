using Nytherion.Core.Managers;
using UnityEngine;
using Nytherion.GamePlay.Combat;
using Nytherion.GamePlay.Skills;
using VContainer;

namespace Nytherion.GamePlay.Characters.Skill
{
    public class SpiralSkill : SkillBase
    {
        [Header("Spiral Skill Settings")]
        public string projectilePoolTag = "SpiralProjectile";

        public int projectileCount = 3;

        private ObjectPoolManager poolManager;

        [Inject]
        public void Construct(ObjectPoolManager poolManager)
        {
            this.poolManager = poolManager;
        }
        protected override void Activate()
        {
            if (caster == null) return;

            float angleStep = 360f / projectileCount;

            for (int i = 0; i < projectileCount; i++)
            {
                GameObject proj = poolManager.SpawnFromPool(projectilePoolTag, caster.position, Quaternion.identity);

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