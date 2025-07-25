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