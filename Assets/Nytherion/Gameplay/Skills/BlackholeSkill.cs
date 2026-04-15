using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Skill;
using UnityEditor.EditorTools;
using UnityEngine;
using VContainer;

namespace Nytherion.GamePlay.Skills
{
    public class BlackholeSkill : SkillBase
    {
        [SerializeField] private string poolTag = "Blackhole";

        private ObjectPoolManager poolManager;

        [Inject]
        public void Construct(ObjectPoolManager poolManager)
        {
            this.poolManager = poolManager;
        }
        protected override void Activate()
        {
            if (skillData != null)
            {
                Vector3 spawnPosition = GetTargetPosition();

                GameObject blackholeInstance = poolManager.SpawnFromPool(poolTag, spawnPosition, Quaternion.identity);

                if (blackholeInstance != null && blackholeInstance.TryGetComponent(out BlackholeProjectile projectile))
                {
                    blackholeInstance.SetActive(true);

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
                    Debug.LogError($"[BlackholeSkill] 풀에서 오브젝트를 가져오지 못했거나 BlackholeProjectile이 없습니다!");
                }
            }
        }
        private Vector3 GetTargetPosition()
        {
            Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

            mouseWorldPos.z = 0f;

            return mouseWorldPos;
        }
    }
}