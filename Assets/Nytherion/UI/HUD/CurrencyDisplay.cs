using TMPro;
using UnityEngine;
using Nytherion.Core.Managers;
using Nytherion.Core.Enums;
using VContainer;
using VContainer.Unity;
using System.Collections;
using Nytherion.Core.Interfaces;

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
    private CurrencyDataManager currencyDataManager;
    private WaitForSeconds updateWait = new WaitForSeconds(0.1f);
    
    [Inject]
    public void Construct(CurrencyDataManager currencyManager)
    {
        this.currencyDataManager = currencyManager;
        if (currencyManager == null)
        {
            Debug.LogError($"[CurrencyDisplay] {gameObject.name} - CurrencyManager 주입 실패!");
        }
    }

    public void Initialize()
    {

        if (currencyDataManager != null)
        {
            currencyDataManager.OnDataChanged += OnCurrencyDataChanged;

            int currentAmount = currencyDataManager.GetCurrency(type);

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
        yield return updateWait;

        if (currencyDataManager != null)
        {
            int currentAmount = currencyDataManager.GetCurrency(type);
            UpdateUI(currentAmount);
        }
    }

    private void OnDestroy()
    {
        if (currencyDataManager != null)
        {
            currencyDataManager.OnDataChanged -= OnCurrencyDataChanged;
        }
    }

    private void OnCurrencyDataChanged(CurrencyChangeData data)
    {
        if (data.currencyType == this.type)
        {
            UpdateUI(data.newAmount);
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
        if (currencyDataManager != null)
        {
            return currencyDataManager.GetCurrency(type);
        }
        return 0;
    }

    public void SetDisplayType(CurrencyType newType)
    {
        if (currencyDataManager != null)
        {
            currencyDataManager.OnDataChanged -= OnCurrencyDataChanged;
        }

        type = newType;

        if (currencyDataManager != null)
        {
            currencyDataManager.OnDataChanged += OnCurrencyDataChanged;
            UpdateUI(currencyDataManager.GetCurrency(type));
        }
    }
}
