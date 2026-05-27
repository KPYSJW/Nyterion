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

        // 거리를 속도로 나누어 대쉬 지속 시간을 계산합니다.
        float calculatedDuration = playerController.PlayerData.dashDistance / playerController.PlayerData.dashSpeed;
        dashTimer = calculatedDuration;

        playerController.ApplyDashVelocity();
    }

    public override void Execute(PlayerController playerController)
    {
        dashTimer -= Time.deltaTime;
        if (dashTimer <= 0)
        {
            playerController.ChangeState(new IdleState());
            return;
        }
    }

    public override void Exit(PlayerController playerController)
    {
        playerController.IsDashing = false;
        playerController.GetComponent<Rigidbody2D>().velocity = Vector2.zero;
    }
}