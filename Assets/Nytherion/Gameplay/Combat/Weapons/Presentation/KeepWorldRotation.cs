using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KeepWorldRotation : MonoBehaviour
{
    [SerializeField] private Vector3 fixedEulerAngles = Vector3.zero;

    private void LateUpdate()
    {
        transform.rotation = Quaternion.Euler(fixedEulerAngles);
    }
}
