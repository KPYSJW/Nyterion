using UnityEngine;
using Nytherion.Core.Managers;

namespace Nytherion.GamePlay.Combat
{
    public class PooledExplosionVFX : MonoBehaviour
    {
        [Header("Pool Settings")]
        public string poolTag = "ExplosionVFX";

        public void AnimationFinished()
        {
            if (ObjectPoolManager.Instance != null)
            {
                ObjectPoolManager.Instance.ReturnToPool(poolTag, gameObject);
            }
        }
    }
}