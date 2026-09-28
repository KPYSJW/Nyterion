using UnityEngine;

namespace Nytherion.GamePlay.Skills
{
    /// <summary>
    /// Visual 오브젝트의 루티 애니메이션 이벤트를 부모 포탑에 전달합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RootiTurretAnimationEventRelay : MonoBehaviour
    {
        private RootiTurretController rooti;

        public void LaunchSeed()
        {
            ResolveRooti();
            rooti?.LaunchSeed();
        }

        public void CompleteLanding()
        {
            ResolveRooti();
            rooti?.CompleteLanding();
        }

        public void CompleteAttack()
        {
            ResolveRooti();
            rooti?.CompleteAttack();
        }

        private void ResolveRooti()
        {
            if (rooti == null) rooti = GetComponentInParent<RootiTurretController>();
        }
    }
}
