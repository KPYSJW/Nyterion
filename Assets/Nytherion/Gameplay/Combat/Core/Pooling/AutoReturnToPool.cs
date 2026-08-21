using UnityEngine;
using System.Collections;
using Nytherion.Core.Managers;

namespace Nytherion.GamePlay.Combat
{
    public class AutoReturnToPool : MonoBehaviour
    {
        [Header("Pool Settings")]
        [Tooltip("오브젝트 풀에서 식별할 태그 (프리팹 이름과 일치 권장)")]
        [SerializeField] private string poolTag = "Spark";

        [Tooltip("몇 초 뒤에 풀로 돌려보낼지 지정")]
        [SerializeField] private float returnDelay = 0.5f;

        private void Awake()
        {
            // (Clone) 접미사를 제거한 오리지널 프리팹 이름으로 태그 자동 동기화
            poolTag = gameObject.name.Replace("(Clone)", "").Trim();
        }

        public void InitializeDelay(float delay)
        {
            this.returnDelay = delay;
        }

        private void OnEnable()
        {
            StartCoroutine(ReturnToPoolAfterDelay());
        }

        private void OnDisable()
        {
            StopAllCoroutines();
        }

        private IEnumerator ReturnToPoolAfterDelay()
        {
            Animator animator = GetComponent<Animator>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (animator != null && animator.isActiveAndEnabled)
            {
                // 애니메이터가 상태를 올바르게 초기화할 수 있도록 한 프레임 대기합니다.
                yield return null;

                AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                if (!stateInfo.loop)
                {
                    float duration = stateInfo.length;
                    yield return new WaitForSeconds(duration);
                }
                else
                {
                    yield return new WaitForSeconds(returnDelay);
                }
            }
            else
            {
                yield return new WaitForSeconds(returnDelay);
            }

            if (ObjectPoolManager.Instance != null)
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
