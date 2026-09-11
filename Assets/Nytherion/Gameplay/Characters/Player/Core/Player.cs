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
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            playerCombat = GetComponent<PlayerCombat>();
            playerHealth = GetComponent<PlayerHealth>();
        }

    }
}