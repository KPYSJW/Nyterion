using Nytherion.GamePlay.Characters.Player;
using UnityEngine;

public class DashState : PlayerState
{
    private float dashTimer;

    public override void Enter(PlayerController playerController)
    {
        playerController.PlayAnimation("Dash");
        playerController.IsDashing = true;
        playerController.LastDashTime = Time.time;
        dashTimer = playerController.PlayerData.dashDuration;

        playerController.ApplyDashVelocity();
    }

    public override void Execute(PlayerController playerController)
    {
        Animator animator = playerController.GetComponent<Animator>();
        if (animator != null)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            
            // 현재 애니메이션이 "Dash"이고 재생 완료율이 100% (1.0f) 이상이면 상태 종료
            if (stateInfo.IsName("Dash") && stateInfo.normalizedTime >= 1.0f)
            {
                playerController.ChangeState(new IdleState());
                return;
            }
        }
        else
        {
            // 애니메이터가 없는 경우 작동할 안전장치 (타이머 예외 처리)
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0)
            {
                playerController.ChangeState(new IdleState());
                return;
            }
        }
    }

    public override void Exit(PlayerController playerController)
    {
        playerController.IsDashing = false;
        playerController.GetComponent<Rigidbody2D>().velocity = Vector2.zero;
    }
}