using UnityEngine;

namespace Nytherion.GamePlay.Characters.Player
{
    public class Player : MonoBehaviour
    {
        public static Player Instance { get; private set; }

        public PlayerCombat playerCombat;
        public PlayerHealth playerHealth;

        private void Awake()
        {
            Debug.Log($"🔵 [Player.cs] Awake() 호출됨 - 오브젝트: {this.gameObject.name}");

            if (Instance == null)
            {
                Instance = this;
                Debug.Log($"🔵 [Player.cs] 싱글톤 인스턴스로 등록되었습니다.");
            }
            else
            {
                Debug.LogWarning($"🔴 [Player.cs] 이미 다른 Player 인스턴스가 존재하여 이 오브젝트({this.gameObject.name})를 파괴합니다.");
                Destroy(gameObject);
                return; // 파괴 후 아래 코드가 실행되지 않도록 함
            }

            playerCombat = GetComponent<PlayerCombat>();
            playerHealth = GetComponent<PlayerHealth>();
        }

    }
}