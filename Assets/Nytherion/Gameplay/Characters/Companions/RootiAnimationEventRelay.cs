using UnityEngine;

namespace Nytherion.GamePlay.Characters.Companions
{
    /// <summary>
    /// Visual 오브젝트의 루티 애니메이션 이벤트를 부모 소환수에 전달합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RootiAnimationEventRelay : MonoBehaviour
    {
        private RootiCompanion rooti;

        public void LaunchSeed()
        {
            if (rooti == null)
            {
                rooti = GetComponentInParent<RootiCompanion>();
            }

            rooti?.LaunchSeed();
        }
    }
}
