using UnityEngine;

namespace Nytherion.UI.Controllers
{
    /// <summary>
    /// 애니메이터의 연출 State가 재생을 마치고 종료되는 순간(OnStateExit)
    /// 부모 Layer/Hierarchy에서 GachaUIController를 찾아 연출 완료를 알리는 StateMachineBehaviour
    /// </summary>
    public class GachaAnimationStateBehaviour : StateMachineBehaviour
    {
        override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (animator == null) return;

            GachaUIController controller = animator.GetComponentInParent<GachaUIController>();
            if (controller != null)
            {
                controller.OnGachaAnimationFinished();
            }
        }
    }
}
