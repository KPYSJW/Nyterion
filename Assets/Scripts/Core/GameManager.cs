using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance;
        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }
}

