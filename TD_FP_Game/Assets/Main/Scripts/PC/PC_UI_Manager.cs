using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class PC_UI_Manager : MonoBehaviour
{
    [Header("Screen Containers")]
    public GameObject MainMenu_Container;
    public GameObject BotUpgrade_Container;
    public GameObject PlayerUpgrade_Container; // Was Upgrade_Container
    public GameObject WeaponList_Container; 
    public GameObject WeaponSpecific_Container; // New specific menu

    [Header("Typewriter")]
    public TypeWriterEffect typewriter;
    [Tooltip("The MAIN text display for both the prompt and the welcome message.")]
    public TMP_Text pcDisplay;
    [TextArea(3, 10)]
    public string pcWelcomeMessage = "Welcome. System ready.\nSelect an option.";

    // --- BOT UI ---
    [Header("Bot UI")]
    public Button btn_UpgradeCapacity;
    public Button btn_UpgradeFlight;
    public Button btn_UpgradeHeal;
    public TextMeshProUGUI botStatusText;

    // --- PLAYER UI ---
    [Header("Player UI")]
    public TextMeshProUGUI btnText_Health;
    public TextMeshProUGUI btnText_Speed;
    // Tower upgrades can live here or be moved to ????
    public TextMeshProUGUI btnText_TowerDmg; 

    // --- WEAPON LIST UI ---
    [Header("Weapon List UI")]
    public WeaponRowUI[] weaponRows; 

    [System.Serializable]
    public class WeaponRowUI
    {
        public string weaponName; 
        public Button selectButton; 
        public Button purchaseButton; 
        public TextMeshProUGUI infoText; 
        public Image selectButtonImage; 
    }

    // --- WEAPON SPECIFIC UI ---
    [Header("Weapon Specific UI")]
    public TextMeshProUGUI header_WeaponName;
    public TextMeshProUGUI btnText_SpecificDmg;
    public TextMeshProUGUI btnText_SpecificRate; 

    // --- INTERNAL STATE ---
    private ScrapCollectorBot _botScript;
    private PlayerStats _playerStats;
    private string _selectedWeaponName; 
    
    private const int COST_CAPACITY = 50;
    private const int COST_FLIGHT = 150;
    private const int COST_HEAL = 300;

    private Coroutine _startupCoroutine;

    void Start()
    {
        ShowScreen(null); 
        pcDisplay.gameObject.SetActive(false);
        _botScript = FindObjectOfType<ScrapCollectorBot>();
        _playerStats = FindObjectOfType<PlayerStats>();
    }
    
    public void StartPCInterface()
    {
        if (_startupCoroutine != null) StopCoroutine(_startupCoroutine);
        _startupCoroutine = StartCoroutine(StartupRoutine());
    }

    public void StopInterface()
    {
        if (typewriter != null) typewriter.StopTyping();
        if (_startupCoroutine != null) { StopCoroutine(_startupCoroutine); _startupCoroutine = null; }
        ShowScreen(null);
        pcDisplay.gameObject.SetActive(false); 
    }

    private IEnumerator StartupRoutine()
    {
        pcDisplay.gameObject.SetActive(true);
        if (typewriter != null && pcDisplay != null) 
        { 
            yield return typewriter.DisplayText(pcDisplay, pcWelcomeMessage); 
            yield return new WaitForSeconds(1f); 
        }
        ShowMainMenu();
        _startupCoroutine = null;
    }

    public void ShowMainMenu() { pcDisplay.gameObject.SetActive(false); ShowScreen(MainMenu_Container); }

    // --- INTERACTION PROMPT ---
    public void ShowInteractPrompt(bool show, string message)
    {
        if (show)
        {
            ShowScreen(null);
            pcDisplay.gameObject.SetActive(true);
            if (typewriter != null) typewriter.DisplayText(pcDisplay, message);
        }
        else
        {
            ShowScreen(null);
            pcDisplay.gameObject.SetActive(false);
            if (typewriter != null) typewriter.StopTyping();
        }
    }

    // --- MAIN MENU BUTTONS ---
    
    public void OnMainMenu_PlayerClicked() 
    { 
        ShowScreen(PlayerUpgrade_Container); 
        UpdatePlayerUI();
        Type("Biometrics Loaded.");
    }

    public void OnMainMenu_WeaponsClicked() 
    { 
        ShowScreen(WeaponList_Container); 
        UpdateWeaponListUI();
        Type("Armory Database Accessed.");
    }

    public void OnMainMenu_BotzClicked() 
    { 
        ShowScreen(BotUpgrade_Container); 
        RefreshBotUI();
        Type("Bot Modification Module Loaded."); 
    }

    public void OnMainMenu_MysteryClicked() 
    { 
        // Placeholder logic for the ???? button
        Type("ERROR: Encrypted Data. Access Denied.");
        // OR show a "Coming Soon" screen
    }

    // --- PLAYER UPGRADES ---
    
    private void UpdatePlayerUI()
    {
        if (UpgradeManager.Instance == null) return;
        UpdateBtnText(UpgradeManager.Instance.healthUpgrade, btnText_Health, "Max Health +25%");
        UpdateBtnText(UpgradeManager.Instance.speedUpgrade, btnText_Speed, "Speed +10%");
        UpdateBtnText(UpgradeManager.Instance.towerDamageGlobal, btnText_TowerDmg, "Tower Damage +20%");
    }

    public void OnBuy_PlayerHealth() => TryBuyGlobal(UpgradeManager.Instance.healthUpgrade, "Health", "Biological enhancement applied.");
    public void OnBuy_PlayerSpeed() => TryBuyGlobal(UpgradeManager.Instance.speedUpgrade, "Speed", "Servos overclocked.");
    public void OnBuy_TowerDamage() => TryBuyGlobal(UpgradeManager.Instance.towerDamageGlobal, "TowerDmg", "Global turret firmware updated.");

    private void TryBuyGlobal(UpgradeManager.UpgradePath path, string key, string msg)
    {
        if (UpgradeManager.Instance.TryBuyGlobalUpgrade(path, key)) { UpdatePlayerUI(); Type(msg); }
        else Type("Insufficient funds.");
    }

    // --- WEAPON LIST LOGIC ---

    private void UpdateWeaponListUI()
    {
        if (UpgradeManager.Instance == null) return;

        foreach (var row in weaponRows)
        {
            bool isOwned = UpgradeManager.Instance.IsWeaponUnlocked(row.weaponName);
            int cost = UpgradeManager.Instance.GetWeaponUnlockCost(row.weaponName);

            var selectTxt = row.selectButton.GetComponentInChildren<TMP_Text>();
            if (selectTxt) selectTxt.text = row.weaponName;
            
            if (isOwned)
            {
                // Normal Color
                row.selectButtonImage.color = Color.white; 
                row.selectButton.interactable = true;
                
                // Purchase Button says "Owned"
                var buyTxt = row.purchaseButton.GetComponentInChildren<TMP_Text>();
                if(buyTxt) buyTxt.text = "OWNED";
                row.purchaseButton.interactable = false; 

                row.infoText.text = "Ready for Upgrade";
            }
            else
            {
                // Dull Color
                row.selectButtonImage.color = Color.gray; 
                row.selectButton.interactable = false; // Can't click to upgrade

                // Purchase Button says Cost
                var buyTxt = row.purchaseButton.GetComponentInChildren<TMP_Text>();
                if(buyTxt) buyTxt.text = $"Purchase\n${cost}";
                row.purchaseButton.interactable = true;

                row.infoText.text = "Must purchase to upgrade";
            }
        }
    }

    public void OnBuy_UnlockWeapon(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= weaponRows.Length) return;
        string wName = weaponRows[rowIndex].weaponName;

        if (UpgradeManager.Instance.TryUnlockWeapon(wName)) 
        { 
            UpdateWeaponListUI(); 
            Type($"{wName} Unlocked."); 
        }
        else 
        { 
            Type("Insufficient Funds."); 
        }
    }

    public void OnSelect_WeaponToUpgrade(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= weaponRows.Length) return;
        _selectedWeaponName = weaponRows[rowIndex].weaponName;
        
        ShowScreen(WeaponSpecific_Container);
        UpdateWeaponSpecificUI();
        
        if (header_WeaponName) header_WeaponName.text = $"Upgrading: {_selectedWeaponName}";
        Type($"Modifying {_selectedWeaponName}...");
    }

    // --- WEAPON SPECIFIC LOGIC ---

    private void UpdateWeaponSpecificUI()
    {
        var wData = UpgradeManager.Instance.GetWeaponData(_selectedWeaponName);
        if (wData == null) return;

        UpdateBtnText(wData.damagePath, btnText_SpecificDmg, "Damage +20%");
        UpdateBtnText(wData.fireRatePath, btnText_SpecificRate, "Fire Rate +10%");
    }

    public void OnBuy_SpecificDmg()
    {
        if(UpgradeManager.Instance.TryBuyWeaponStat(_selectedWeaponName, "Damage")) { UpdateWeaponSpecificUI(); Type("Ballistics improved."); }
        else Type("Insufficient funds.");
    }

    public void OnBuy_SpecificRate()
    {
        if(UpgradeManager.Instance.TryBuyWeaponStat(_selectedWeaponName, "Rate")) { UpdateWeaponSpecificUI(); Type("Mechanism cycled."); }
        else Type("Insufficient funds.");
    }

    // --- BOT LOGIC ---
    private void RefreshBotUI()
    {
        if (_botScript == null) return;
        if(btn_UpgradeCapacity) { btn_UpgradeCapacity.gameObject.SetActive(_botScript.carryCapacity < 5); btn_UpgradeCapacity.GetComponentInChildren<TMP_Text>().text = $"Expand Cargo ({COST_CAPACITY})"; }
        if(btn_UpgradeFlight) { btn_UpgradeFlight.gameObject.SetActive(_botScript.carryCapacity > 1 && !_botScript.canFly); btn_UpgradeFlight.GetComponentInChildren<TMP_Text>().text = $"Flight ({COST_FLIGHT})"; }
        if(btn_UpgradeHeal) { btn_UpgradeHeal.gameObject.SetActive(_botScript.canFly && !_botScript.canHeal); btn_UpgradeHeal.GetComponentInChildren<TMP_Text>().text = $"Medical ({COST_HEAL})"; }
        if(botStatusText) botStatusText.text = $"Load: {_botScript.carryCapacity} | Flight: {(_botScript.canFly?"ON":"OFF")}";
    }
    public void OnBuy_BotCapacity() { if(_playerStats.SpendScrap(COST_CAPACITY)) { _botScript.UpgradeCapacity(_botScript.carryCapacity+2); RefreshBotUI(); Type("Bot Upgraded."); } else Type("No Cash."); }
    public void OnBuy_BotFlight() { if(_playerStats.SpendScrap(COST_FLIGHT)) { _botScript.UpgradeFlight(); RefreshBotUI(); Type("Bot Upgraded."); } else Type("No Cash."); }
    public void OnBuy_BotHeal() { if(_playerStats.SpendScrap(COST_HEAL)) { _botScript.UpgradeHealing(); RefreshBotUI(); Type("Bot Upgraded."); } else Type("No Cash."); }

    // --- HELPERS ---

    private void UpdateBtnText(UpgradeManager.UpgradePath path, TextMeshProUGUI txt, string desc)
    {
        if (txt == null) return;
        if (path.currentLevel >= path.maxLevel) txt.text = $"{desc}\nMAX";
        else txt.text = $"{desc}\nLvl {path.currentLevel}->{path.currentLevel+1} (${path.GetCost()})";
    }

    private void Type(string msg)
    {
        if(typewriter) typewriter.DisplayText(pcDisplay, msg);
    }

    public void BackToMainMenu() { ShowMainMenu(); }
    public void BackToWeaponList() { OnMainMenu_WeaponsClicked(); }

    private void ShowScreen(GameObject screenToShow)
    {
        if(MainMenu_Container) MainMenu_Container.SetActive(false);
        if(BotUpgrade_Container) BotUpgrade_Container.SetActive(false);
        if(PlayerUpgrade_Container) PlayerUpgrade_Container.SetActive(false);
        if(WeaponList_Container) WeaponList_Container.SetActive(false);
        if(WeaponSpecific_Container) WeaponSpecific_Container.SetActive(false);
        
        if (screenToShow != null) screenToShow.SetActive(true);
    }
}