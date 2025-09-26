using TMPro;
using UnityEngine;
using Nytherion.Core.Managers;
using Nytherion.Core.Enums;
using VContainer;
using VContainer.Unity;
using System.Collections;

public class CurrencyDisplay : MonoBehaviour, IInitializable
{
    [Header("Currency Settings")]
    [SerializeField] private CurrencyType type;
    [SerializeField] private TMP_Text amountText;
    public TMP_Text AmountText => amountText;
    public CurrencyType CurrencyType => type;

    [Header("Display Options")]
    [SerializeField] private string prefix = "";
    [SerializeField] private string suffix = "";
    [SerializeField] private bool useThousandsSeparator = false;
    private CurrencyManager currencyManager;
    
    [Inject]
    public void Construct(CurrencyManager currencyManager)
    {
        this.currencyManager = currencyManager;
        if (currencyManager == null)
        {
            Debug.LogError($"[CurrencyDisplay] {gameObject.name} - CurrencyManager 주입 실패!");
        }
    }

    public void Initialize()
    {

        if (currencyManager != null)
        {
            currencyManager.onCurrencyChanged += OnCurrencyChanged;

            int currentAmount = currencyManager.GetCurrency(type);

            UpdateUI(currentAmount);
            if (currentAmount == 0)
            {
                StartCoroutine(DelayedUpdateUI());
            }
        }
        else
        {
            Debug.LogError($"[CurrencyDisplay] {gameObject.name} - VContainer 의존성 주입 실패!");
        }
    }

    private IEnumerator DelayedUpdateUI()
    {
        yield return null;
        yield return new WaitForSeconds(0.1f);

        if (currencyManager != null)
        {
            int currentAmount = currencyManager.GetCurrency(type);
            UpdateUI(currentAmount);
        }
    }

    private void OnDestroy()
    {
        if (currencyManager != null)
        {
            currencyManager.onCurrencyChanged -= OnCurrencyChanged;
        }
    }

    private void OnCurrencyChanged(CurrencyType changedType, int newAmount)
    {
        if (changedType == type)
        {
            UpdateUI(newAmount);
        }
    }

    private void UpdateUI(int amount)
    {
        if (amountText == null)
        {
            Debug.LogError($"[CurrencyDisplay] {gameObject.name} - amountText가 null입니다!");
            return;
        }

        string formattedAmount;
        if (useThousandsSeparator)
        {
            formattedAmount = amount.ToString("N0");
        }
        else
        {
            formattedAmount = amount.ToString();
        }

        string newText = prefix + formattedAmount + suffix;
        amountText.text = newText;
    }

    public void UpdateAmount(int amount)
    {
        UpdateUI(amount);
    }

    public void ShowGainEffect(int amount)
    {
        // TODO: Add visual effect for currency gain
    }

    public void ShowLossEffect(int amount)
    {
        // TODO: Add visual effect for currency loss
    }

    public int GetDisplayedAmount()
    {
        if (currencyManager != null)
        {
            return currencyManager.GetCurrency(type);
        }
        return 0;
    }

    public void SetDisplayType(CurrencyType newType)
    {
        if (currencyManager != null)
        {
            currencyManager.onCurrencyChanged -= OnCurrencyChanged;
        }

        type = newType;

        if (currencyManager != null)
        {
            currencyManager.onCurrencyChanged += OnCurrencyChanged;
            UpdateUI(currencyManager.GetCurrency(type));
        }
    }
}
