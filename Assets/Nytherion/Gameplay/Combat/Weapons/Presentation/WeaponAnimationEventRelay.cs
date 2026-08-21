using UnityEngine;

namespace Nytherion.GamePlay.Combat.Weapons
{
    public class WeaponAnimationEventRelay : MonoBehaviour
    {
        private ShortDagger parentDagger;

        private void Awake()
        {
            EnsureParentCached();
        }

        private void Start()
        {
            EnsureParentCached();
        }

        private void EnsureParentCached()
        {
            if (parentDagger == null)
            {
                parentDagger = GetComponentInParent<ShortDagger>();
            }
        }

        /// <summary>
        /// 자식 visual 객체의 Animator 애니메이션 이벤트로부터 호출받아
        /// 부모 단검 스크립트의 OnAttackAnimationEnd를 실행해 줍니다.
        /// </summary>
        public void OnAttackAnimationEnd()
        {
            EnsureParentCached();

            if (parentDagger != null)
            {
                parentDagger.OnAttackAnimationEnd();
            }
            else
            {
                Debug.LogWarning("[WeaponAnimationEventRelay] parentDagger를 찾을 수 없습니다. 부모 오브젝트에 ShortDagger 컴포넌트가 있는지 확인해 주세요.");
            }
        }
    }
}
