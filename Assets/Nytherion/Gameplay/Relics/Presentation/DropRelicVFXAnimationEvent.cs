using UnityEngine;
using Nytherion.Core.Managers;

namespace Nytherion.GamePlay.Relics
{
    [DisallowMultipleComponent]
    public class DropRelicVFXAnimationEvent : MonoBehaviour
    {
        private ObjectPoolManager returnPool;
        private string poolTag;
        private bool isReturning;

        public void SetPool(ObjectPoolManager pool, string tag)
        {
            returnPool = pool;
            poolTag = tag;
            isReturning = false;
        }

        // DropRelicEffect 애니메이션 마지막 프레임의 Animation Event에서 호출한다.
        public void AnimationFinished()
        {
            if (isReturning || !gameObject.activeSelf) return;
            isReturning = true;
            if (returnPool != null && !string.IsNullOrEmpty(poolTag))
            {
                returnPool.ReturnToPool(poolTag, gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
