using UnityEngine;
using Nytherion.GamePlay.Characters.Player;

public class DebugHealthTest : MonoBehaviour
{
    [Header("테스트 설정")]
    [Tooltip("한 번 클릭할 때마다 깎일 체력량")]
    public float damageAmount = 10f;

    private PlayerHealth playerHealth;

    private void Start()
    {
        playerHealth = FindObjectOfType<PlayerHealth>();

        if (playerHealth == null)
        {
            Debug.LogError("[DebugHealthTest] 씬에서 PlayerHealth를 찾을 수 없습니다!");
        }
    }

    public void OnDamageButtonClicked()
    {
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damageAmount);
            Debug.Log($"[테스트] 플레이어에게 {damageAmount}의 데미지를 입혔습니다! 현재 체력: {playerHealth.CurrentHealth}");
        }
    }
}