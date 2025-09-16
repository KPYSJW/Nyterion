using Nytherion.GamePlay.Characters.Player;
using UnityEngine;

public class IdleState : PlayerState
{
    public override void Enter(PlayerController playerController)
    {
        playerController.PlayAnimation("Idle");
        playerController.GetComponent<Rigidbody2D>().velocity = UnityEngine.Vector2.zero;
    }

    public override void Execute(PlayerController playerController)
    {   
        Vector2 moveInput = playerController.MoveInput;
        
        if (playerController.IsDashPressed &&
            UnityEngine.Time.time >= playerController.LastDashTime + playerController.PlayerData.dashCooldown)
        {
            playerController.ChangeState(new DashState());
            return;
        }

        if (moveInput.magnitude > 0)
        {
            playerController.ChangeState(new WalkState());
            return;
        }
    }

    public override void Exit(PlayerController playerController)
    {
    }
}
