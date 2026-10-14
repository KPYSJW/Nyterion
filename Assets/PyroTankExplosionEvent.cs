using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.GamePlay.Skills;

public class PyroTankExplosionEvent : MonoBehaviour
{
    [SerializeField]private PyroTankController pyroTankController;


    public void OnExplosionEnd()
    {
        if (pyroTankController != null)
        {
            // 부모의 함수를 대신 실행해줍니다.
            pyroTankController.OnExplosionEnd(); 
        }
    }
}
