using Nytherion.Core.Managers;
using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    [DisallowMultipleComponent]
    public class PooledVFXAnimationEvent : MonoBehaviour
    {
        [SerializeField] private string poolTag;
        [SerializeField] private string[] animationStates;
        private Animator effectAnimator;

        private void Awake()
        {
            effectAnimator = GetComponent<Animator>();
            if (string.IsNullOrEmpty(poolTag))
            {
                poolTag = gameObject.name.Replace("(Clone)", "").Trim();
            }
        }

        private void OnEnable()
        {
            if (effectAnimator == null || effectAnimator.runtimeAnimatorController == null) return;
            // 풀에서 재사용할 때도 마지막 상태가 남지 않도록 처음부터 재생합니다.
            effectAnimator.Rebind();
            if (animationStates != null && animationStates.Length > 0)
            {
                // 풀에서 꺼낼 때마다 타격 애니메이션을 같은 확률로 선택합니다.
                string state = animationStates[Random.Range(0, animationStates.Length)];
                effectAnimator.Play(state, 0, 0f);
            }
            else effectAnimator.Play(0, 0, 0f);
            effectAnimator.Update(0f);
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
