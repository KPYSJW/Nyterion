using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class InventoryUI : MonoBehaviour
{
    public GameObject inventoryPanel;
    private bool isOpen = false;
    private PlayerAction playerAction;

    private void Awake()
    {
        playerAction = new PlayerAction();
        playerAction.Player.Enable();

        playerAction.Player.ToggleInventory.performed += _ => ToggleInventory();
        playerAction.Player.CloseInventory.performed += _ => CloseInventory();
    }

    private void OnDisable()
    {
        playerAction.Player.Disable();
    }

    private void ToggleInventory()
    {
        Debug.Log("토글 함수 호출됨");
        isOpen = !isOpen;
        inventoryPanel.SetActive(true);
    }

    private void CloseInventory()
    {
        isOpen = false;
        inventoryPanel.SetActive(false);
    }
}