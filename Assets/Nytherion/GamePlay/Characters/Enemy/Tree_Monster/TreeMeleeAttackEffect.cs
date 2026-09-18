using System.Collections;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    [DisallowMultipleComponent]
    public sealed class TreeMeleeAttackEffect : MonoBehaviour
    {
        [SerializeField] private GameObject effectObject;
        [SerializeField] private Animator effectAnimator;
        [SerializeField, Min(0.01f)] private float effectDuration = 1.34f;

        private Coroutine hideRoutine;

        private void Awake()
        {
            HideEffect();
        }

        public void PlayTreeMeleeEffect()
        {
            if (effectObject == null || effectAnimator == null)
                return;

            if (hideRoutine != null)
                StopCoroutine(hideRoutine);

            effectObject.SetActive(true);
            effectAnimator.Play(0, 0, 0f);
            effectAnimator.Update(0f);
            hideRoutine = StartCoroutine(HideAfterDuration());
        }

        private IEnumerator HideAfterDuration()
        {
            yield return new WaitForSeconds(effectDuration);
            hideRoutine = null;
            HideEffect();
        }

        private void OnDisable()
        {
            if (hideRoutine != null)
            {
                StopCoroutine(hideRoutine);
                hideRoutine = null;
            }

            HideEffect();
        }

        private void HideEffect()
        {
            if (effectObject != null)
                effectObject.SetActive(false);
        }

        private void OnValidate()
        {
            effectDuration = Mathf.Max(0.01f, effectDuration);
        }
    }
}
