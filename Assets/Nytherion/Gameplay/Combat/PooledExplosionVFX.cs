using UnityEngine;
using Nytherion.Core.Managers;
using VContainer;

namespace Nytherion.GamePlay.Combat
{
    public class PooledExplosionVFX : MonoBehaviour
    {
        [Header("Pool Settings")]
        public string poolTag = "ExplosionVFX";

        private ObjectPoolManager poolManager;

        [Inject]
        public void Construct(ObjectPoolManager poolManager)
        {
            this.poolManager = poolManager;
        }
        public void AnimationFinished()
        {
            if (poolManager != null)
            {
                poolManager.ReturnToPool(poolTag, gameObject);
            }
        }
    }
}