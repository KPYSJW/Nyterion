using Nytherion.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Player

{
    public class Player : MonoBehaviour
    {
        public static Player Instance;
        public PlayerCombat playerCombat;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            playerCombat = GetComponent<PlayerCombat>();
        }

    }
}


