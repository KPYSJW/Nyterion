using Nytherion.GamePlay.Characters.Player;
using UnityEngine;

public class WalkState : PlayerState
{
    public override void Enter(PlayerController playerController)
    {
        playerController.PlayAnimation("Walk");
    }

    public override void Execute(PlayerController playerController)
    {
        playerController.HandleMovement();

        if (playerController.IsDashPressed && 
            Time.time >= playerController.LastDashTime + playerController.PlayerData.dashCooldown)
        {
            playerController.ChangeState(new DashState());
            return;
        }

        if (playerController.MoveInput.magnitude == 0)
        {
            playerController.ChangeState(new IdleState());
            return;
        }
    }

    public override void Exit(PlayerController playerController)
    {
        playerController.GetComponent<Rigidbody2D>().velocity = Vector2.zero;
    }
}