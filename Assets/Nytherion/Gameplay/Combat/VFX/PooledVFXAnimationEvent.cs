using Nytherion.Core.Managers;
using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    [DisallowMultipleComponent]
    public class PooledVFXAnimationEvent : MonoBehaviour
    {
        [SerializeField] private string poolTag;

        private void Awake()
        {
            if (string.IsNullOrEmpty(poolTag))
            {
                poolTag = gameObject.name.Replace("(Clone)", "").Trim();
            }
        }

        public void AnimationFinished()
        {
            if (ObjectPoolManager.Instance != null && !string.IsNullOrEmpty(poolTag))
            {
                ObjectPoolManager.Instance.ReturnToPool(poolTag, gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
