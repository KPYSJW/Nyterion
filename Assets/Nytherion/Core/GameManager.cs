using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }
    }
}

