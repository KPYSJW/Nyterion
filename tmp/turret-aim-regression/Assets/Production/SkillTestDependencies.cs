using Nytherion.Data.ScriptableObjects.Skill;
using UnityEngine;

namespace Nytherion.Core.Interfaces
{
    public interface IDamageable { }
}

namespace Nytherion.Data.ScriptableObjects.Skill
{
    public class SkillData : ScriptableObject
    {
        public float coolDown;
        public float damage = 10f;
        public float range = 4f;
    }
}

namespace Nytherion.GamePlay.Skills
{
    public class TurretController : MonoBehaviour
    {
        protected float attackRange;
        protected float damage;
        public virtual void Initialize(TurretSkillData data)
        {
            attackRange = data.range;
            damage = data.damage;
        }
        public virtual void Deploy(Vector3 launchPosition, Vector3 landingPosition) { }
        protected virtual void Update() { }
        protected virtual void PerformAttack() { }
        protected void LaunchProjectileAtTarget(Transform target) { }
    }
}
