using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class PC_UI_Manager : MonoBehaviour
{
    [Header("Screen Containers")]
    public GameObject MainMenu_Container;
    public GameObject BotUpgrade_Container;
    public GameObject PlayerUpgrade_Container; 
    public GameObject WeaponList_Container; 
    public GameObject WeaponSpecific_Container; 

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
    public TextMeshProUGUI btnText_SpecificMaxAmmo;
    public TextMeshProUGUI btnText_SpecificClip;
    
    [Tooltip("Assign the button for buying Guidance/Special upgrades here.")]
    public Button btn_SpecialUpgrade;
    public TextMeshProUGUI btnText_SpecialUpgrade;

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
        if(pcDisplay) pcDisplay.gameObject.SetActive(false);
        _botScript = FindObjectOfType<ScrapCollectorBot>();
        _playerStats = FindObjectOfType<PlayerStats>();

        if (UpgradeManager.Instance == null)
        {
            Debug.LogError("CRITICAL ERROR: UpgradeManager is missing from the scene! Please create an empty object and attach 'UpgradeManager.cs'.");
        }
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
        if(pcDisplay) pcDisplay.gameObject.SetActive(false); 
    }

    private IEnumerator StartupRoutine()
    {
        if(pcDisplay) pcDisplay.gameObject.SetActive(true);
        if (typewriter != null && pcDisplay != null) 
        { 
            yield return typewriter.DisplayText(pcDisplay, pcWelcomeMessage); 
            yield return new WaitForSeconds(1f); 
        }
        ShowMainMenu();
        _startupCoroutine = null;
    }

    public void ShowMainMenu() { if(pcDisplay) pcDisplay.gameObject.SetActive(false); ShowScreen(MainMenu_Container); }

    public void ShowInteractPrompt(bool show, string message)
    {
        if (show)
        {
            ShowScreen(null);
            if(pcDisplay) 
            {
                pcDisplay.gameObject.SetActive(true);
                if (typewriter != null) typewriter.DisplayText(pcDisplay, message);
            }
        }
        else
        {
            ShowScreen(null);
            if(pcDisplay) pcDisplay.gameObject.SetActive(false);
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
        Type("ERROR: Encrypted Data. Access Denied.");
    }

    // --- PLAYER UPGRADES ---
    
    private void UpdatePlayerUI()
    {
        if (UpgradeManager.Instance == null) return;
        UpdateBtnText(UpgradeManager.Instance.healthUpgrade, btnText_Health, "Max Health");
        UpdateBtnText(UpgradeManager.Instance.speedUpgrade, btnText_Speed, "Move Speed");
        UpdateBtnText(UpgradeManager.Instance.towerDamageGlobal, btnText_TowerDmg, "Tower Dmg");
    }

    public void OnBuy_PlayerHealth() 
    {
        if (UpgradeManager.Instance == null) return;
        TryBuyGlobal(UpgradeManager.Instance.healthUpgrade, "Health", "Biological enhancement applied.");
    }

    public void OnBuy_PlayerSpeed() 
    {
        if (UpgradeManager.Instance == null) return;
        TryBuyGlobal(UpgradeManager.Instance.speedUpgrade, "Speed", "Servos overclocked.");
    }

    public void OnBuy_TowerDamage() 
    {
        if (UpgradeManager.Instance == null) return;
        TryBuyGlobal(UpgradeManager.Instance.towerDamageGlobal, "TowerDmg", "Global turret firmware updated.");
    }

    private void TryBuyGlobal(UpgradeManager.UpgradePath path, string key, string msg)
    {
        if (UpgradeManager.Instance.TryBuyGlobalUpgrade(path, key)) { UpdatePlayerUI(); Type(msg); }
        else Type("Insufficient funds.");
    }

    // --- WEAPON LIST LOGIC ---

    private void UpdateWeaponListUI()
    {
        // 1. Check Global Managers
        if (UpgradeManager.Instance == null || _playerStats == null) 
        {
            Debug.LogWarning("UpdateWeaponListUI: Managers missing.");
            return;
        }

        // 2. Check Rows Array
        if (weaponRows == null)
        {
            Debug.LogWarning("UpdateWeaponListUI: Weapon Rows array is null.");
            return;
        }

        foreach (var row in weaponRows)
        {
            // 3. Check Row Object
            if (row == null) continue;

            // 4. Check UI Elements inside Row
            if (row.selectButton == null || row.purchaseButton == null)
            {
                Debug.LogWarning($"UpdateWeaponListUI: Missing button ref for {row.weaponName}");
                continue;
            }

            bool isOwned = UpgradeManager.Instance.IsWeaponUnlocked(row.weaponName);
            int cost = UpgradeManager.Instance.GetWeaponUnlockCost(row.weaponName);

            var selectTxt = row.selectButton.GetComponentInChildren<TMP_Text>();
            if (selectTxt) selectTxt.text = row.weaponName;
            
            if (isOwned)
            {
                // OWNED STATE
                if(row.selectButtonImage) row.selectButtonImage.color = Color.white; 
                row.selectButton.interactable = true;
                
                var buyTxt = row.purchaseButton.GetComponentInChildren<TMP_Text>();
                if(buyTxt) buyTxt.text = "Already Purchased"; 
                row.purchaseButton.interactable = false; 

                if(row.infoText) row.infoText.text = ""; 
            }
            else
            {
                // UNOWNED STATE
                if(row.selectButtonImage) row.selectButtonImage.color = Color.gray; 
                row.selectButton.interactable = false; 

                var buyTxt = row.purchaseButton.GetComponentInChildren<TMP_Text>();
                // Match User Request: "Purchase for (cost)"
                if(buyTxt) buyTxt.text = $"Purchase for {cost}";
                
                row.purchaseButton.interactable = true;

                if(row.infoText)
                {
                    if (_playerStats.scrapMetal < cost)
                    {
                        row.infoText.text = "Insufficient Funds";
                        row.infoText.color = Color.red;
                    }
                    else
                    {
                        row.infoText.text = ""; 
                    }
                }
            }
        }
    }

    public void OnBuy_UnlockWeapon(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= weaponRows.Length) return;
        if (UpgradeManager.Instance == null) return;

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
        if (UpgradeManager.Instance == null) return;

        string weaponName = weaponRows[rowIndex].weaponName;

        if (!UpgradeManager.Instance.IsWeaponUnlocked(weaponName))
        {
            Type("Access Denied: Weapon not owned.");
            return;
        }

        _selectedWeaponName = weaponName;
        
        ShowScreen(WeaponSpecific_Container);
        UpdateWeaponSpecificUI();
        
        if (header_WeaponName) header_WeaponName.text = $"Upgrading: {_selectedWeaponName}";
        Type($"Modifying {_selectedWeaponName}...");
    }

    // --- WEAPON SPECIFIC LOGIC ---

    private void UpdateWeaponSpecificUI()
    {
        if (UpgradeManager.Instance == null) return;
        var wData = UpgradeManager.Instance.GetWeaponData(_selectedWeaponName);
        if (wData == null) return;

        // Standard Stats
        UpdateBtnText(wData.damagePath, btnText_SpecificDmg, "Damage");
        UpdateBtnText(wData.fireRatePath, btnText_SpecificRate, "Fire Rate");
        UpdateBtnText(wData.maxAmmoPath, btnText_SpecificMaxAmmo, "Max Ammo");
        UpdateBtnText(wData.clipSizePath, btnText_SpecificClip, "Clip Size");

        // --- NEW: Handle Rocket Launcher Guidance ---
        if (_selectedWeaponName == "RocketLauncher")
        {
            // 1. Show the button
            if(btn_SpecialUpgrade) btn_SpecialUpgrade.gameObject.SetActive(true);

            // 2. Update Text/Interactability
            if (btnText_SpecialUpgrade)
            {
                if (wData.guidanceUnlocked)
                {
                    btnText_SpecialUpgrade.text = "Guidance System\nOWNED";
                    if(btn_SpecialUpgrade) btn_SpecialUpgrade.interactable = false;
                }
                else
                {
                    btnText_SpecialUpgrade.text = $"Unlock Guidance\nCost: {wData.guidanceCost}";
                    if(btn_SpecialUpgrade) btn_SpecialUpgrade.interactable = true;
                }
            }
        }
        else
        {
            // Hide the button for other weapons (Shotgun, Rifle, etc.)
            if(btn_SpecialUpgrade) btn_SpecialUpgrade.gameObject.SetActive(false);
        }
        // --------------------------------------------
    }
    
    public void OnBuy_SpecialUpgrade()
    {
        if (UpgradeManager.Instance == null) return;

        // Try to buy "Guidance"
        if (UpgradeManager.Instance.TryBuyWeaponStat(_selectedWeaponName, "Guidance")) 
        { 
            UpdateWeaponSpecificUI(); 
            Type("Guidance Chipset Installed."); 
        }
        else 
        { 
            Type("Insufficient funds or already owned."); 
        }
    }

    public void OnBuy_SpecificDmg()
    {
        if (UpgradeManager.Instance == null) return;
        if(UpgradeManager.Instance.TryBuyWeaponStat(_selectedWeaponName, "Damage")) { UpdateWeaponSpecificUI(); Type("Ballistics improved."); }
        else Type("Insufficient funds.");
    }

    public void OnBuy_SpecificRate()
    {
        if (UpgradeManager.Instance == null) return;
        if(UpgradeManager.Instance.TryBuyWeaponStat(_selectedWeaponName, "Rate")) { UpdateWeaponSpecificUI(); Type("Mechanism cycled."); }
        else Type("Insufficient funds.");
    }

    public void OnBuy_SpecificMaxAmmo()
    {
        if (UpgradeManager.Instance == null) return;
        if(UpgradeManager.Instance.TryBuyWeaponStat(_selectedWeaponName, "MaxAmmo")) { UpdateWeaponSpecificUI(); Type("Capacity increased."); }
        else Type("Insufficient funds.");
    }

    public void OnBuy_SpecificClip()
    {
        if (UpgradeManager.Instance == null) return;
        if(UpgradeManager.Instance.TryBuyWeaponStat(_selectedWeaponName, "ClipSize")) { UpdateWeaponSpecificUI(); Type("Magazine expanded."); }
        else Type("Insufficient funds.");
    }

    // --- BOT LOGIC ---
    private void RefreshBotUI()
    {
        if (_botScript == null) return;
        if(btn_UpgradeCapacity) { btn_UpgradeCapacity.gameObject.SetActive(_botScript.carryCapacity < 5); btn_UpgradeCapacity.GetComponentInChildren<TMP_Text>().text = $"Expand Cargo\nCost: {COST_CAPACITY}"; }
        if(btn_UpgradeFlight) { btn_UpgradeFlight.gameObject.SetActive(_botScript.carryCapacity > 1 && !_botScript.canFly); btn_UpgradeFlight.GetComponentInChildren<TMP_Text>().text = $"Flight Systems\nCost: {COST_FLIGHT}"; }
        if(btn_UpgradeHeal) { btn_UpgradeHeal.gameObject.SetActive(_botScript.canFly && !_botScript.canHeal); btn_UpgradeHeal.GetComponentInChildren<TMP_Text>().text = $"Medical Module\nCost: {COST_HEAL}"; }
        if(botStatusText) botStatusText.text = $"Load: {_botScript.carryCapacity} | Flight: {(_botScript.canFly?"ON":"OFF")}";
    }
    public void OnBuy_BotCapacity() { if(_playerStats.SpendScrap(COST_CAPACITY)) { _botScript.UpgradeCapacity(_botScript.carryCapacity+2); RefreshBotUI(); Type("Bot Upgraded."); } else Type("No Cash."); }
    public void OnBuy_BotFlight() { if(_playerStats.SpendScrap(COST_FLIGHT)) { _botScript.UpgradeFlight(); RefreshBotUI(); Type("Bot Upgraded."); } else Type("No Cash."); }
    public void OnBuy_BotHeal() { if(_playerStats.SpendScrap(COST_HEAL)) { _botScript.UpgradeHealing(); RefreshBotUI(); Type("Bot Upgraded."); } else Type("No Cash."); }

    // --- HELPERS ---

    private void UpdateBtnText(UpgradeManager.UpgradePath path, TextMeshProUGUI txt, string desc)
    {
        if (txt == null) return;
        if (path.currentLevel >= path.maxLevel) 
        {
            txt.text = $"{desc}\nMAX LEVEL";
        }
        else 
        {
            txt.text = $"{desc} (Lvl {path.currentLevel})\nCost: {path.GetCost()}";
        }
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