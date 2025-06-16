using Nytherion.Core;
using System.Collections;
using System.Collections.Generic;
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
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            playerCombat = GetComponent<PlayerCombat>();
            playerHealth = GetComponent<PlayerHealth>();
        }

    }
}