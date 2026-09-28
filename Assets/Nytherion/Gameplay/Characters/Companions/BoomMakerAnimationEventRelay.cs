using UnityEngine;

namespace Nytherion.GamePlay.Characters.Companions
{
    /// <summary>
    /// Visual 오브젝트의 애니메이션 이벤트를 부모 소환수에게 전달합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoomMakerAnimationEventRelay : MonoBehaviour
    {
        private BoomMaker boomMaker;

        public void DeployExplosiveRobot()
        {
            if (boomMaker == null)
            {
                boomMaker = GetComponentInParent<BoomMaker>();
            }

            boomMaker?.DeployExplosiveRobot();
        }
    }
}
