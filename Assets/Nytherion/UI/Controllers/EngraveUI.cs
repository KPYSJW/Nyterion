using Nytherion.Data.ScriptableObjects.Engravings;
using Nytherion.GamePlay.Characters.Player;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Nytherion.UI.Controllers
{
    public class EngraveUI : MonoBehaviour
    {
        public Image[] engraveSlots;
        
        private PlayerManager _playerManager;
        
        [Inject]
        public void Construct(PlayerManager playerManager)
        {
            _playerManager = playerManager;
        }

        private void Update()
        {
            UpdateEngraveUI();
        }

        public void UpdateEngraveUI()
        {
            if (_playerManager == null) return;
            
            foreach (Image slot in engraveSlots)
            {
                slot.gameObject.SetActive(false);
            }
            List<EngravingData> engrave = _playerManager.playerEngravingManager.GetCurrentEngravings();
            for (int i=0;i< engrave.Count;++i)
            {
                engraveSlots[i].gameObject.SetActive(true);
                engraveSlots[i].sprite = engrave[i].Image;
            }
        }
    }
}

