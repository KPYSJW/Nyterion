using UnityEngine;

namespace Nytherion.GamePlay.Characters.Companions
{
    /// <summary>
    /// Visual 오브젝트의 Opapa 애니메이션 이벤트를 부모 소환수에게 전달합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OpapaAnimationEventRelay : MonoBehaviour
    {
        private OpapaCompanion opapa;

        public void BeginRolling()
        {
            ResolveCompanion();
            opapa?.BeginRolling();
        }

        public void CompleteRollAttack()
        {
            ResolveCompanion();
            opapa?.CompleteRollAttack();
        }

        private void ResolveCompanion()
        {
            if (opapa == null)
            {
                opapa = GetComponentInParent<OpapaCompanion>();
            }
        }
    }
}
