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
    
    [Header("Audio Settings")]
    public AudioSource voiceAudioSource; // Drag an AudioSource here (or the PC's speaker)

    [Header("Voice Lines")]
    public AudioClip welcomeVoiceLine;      // "Welcome back, Commander."
    public AudioClip weaponsMenuVoiceLine;  // "Armory database accessed."
    public AudioClip playerMenuVoiceLine;   // "Biometrics scanning..."
    public AudioClip botMenuVoiceLine;      // "Drone diagnostics loaded."
    
    [Header("Desktop Shell")]
    [Tooltip("Optional. The desktop-OS look around these screens. Added automatically if missing.")]
    public PC_DesktopShell desktopShell;
    public string title_MainMenu = "Command Center";
    public string title_Player = "Operator Upgrades";
    public string title_WeaponList = "Armory";
    public string title_WeaponSpecific = "Armory  >  ";
    public string title_Bot = "Drone Bay";
    [Tooltip("How often the open screen re-checks prices while you have the PC open (scrap can change while you shop).")]
    public float liveRefreshInterval = 0.5f;

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
    
    [Header("Ammo Purchase")]
    public Button btn_BuyAmmo;
    public int ammoCost = 50;
    
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
    private GameObject _currentScreen;
    private bool _skipStartup;
    private bool _startingUp;
    private bool _interfaceRunning;
    private int _lastRefreshScrap = int.MinValue;
    private float _nextRefreshTime;

    private const string AFFORD_COLOR = "#5CF28F";
    private const string EXPENSIVE_COLOR = "#FF6B6B";

    /// <summary>True while the boot/welcome sequence is playing.</summary>
    public bool IsStartingUp => _startingUp;
    public PC_DesktopShell Shell => desktopShell;

    void Awake()
    {
        if (desktopShell == null) desktopShell = GetComponent<PC_DesktopShell>();
        if (desktopShell == null) desktopShell = gameObject.AddComponent<PC_DesktopShell>();
        desktopShell.Build();
        desktopShell.ConfigureApps(new[] { "OPERATOR", "ARMORY", "DRONES" }, new[] { "OP", "AR", "DR" });
        desktopShell.AppClicked += OnTaskbarApp;
        desktopShell.StartClicked += ShowMainMenu;
        desktopShell.MinimizeClicked += () => ShowScreen(null);
        desktopShell.LogOffClicked += LogOff;
    }

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
        if (voiceAudioSource == null) voiceAudioSource = GetComponent<AudioSource>();
    }
    
    void OnDisable()
    {
        // Coroutines die with the canvas, so don't stay stuck in the startup state.
        _startupCoroutine = null;
        _startingUp = false;
    }

    void Update()
    {
        // Scrap can change while the player is shopping (the bot keeps collecting),
        // so keep prices and their affordable/too-expensive colours current.
        if (!_interfaceRunning || _currentScreen == null || _playerStats == null) return;
        if (Time.unscaledTime < _nextRefreshTime) return;
        _nextRefreshTime = Time.unscaledTime + liveRefreshInterval;
        if (_playerStats.scrapMetal == _lastRefreshScrap) return;
        RefreshCurrentScreen();
    }

    public void StartPCInterface()
    {
        _interfaceRunning = true;
        _startingUp = true;
        if (_startupCoroutine != null) StopCoroutine(_startupCoroutine);
        _startupCoroutine = StartCoroutine(StartupRoutine());
    }

    public void StopInterface()
    {
        _interfaceRunning = false;
        if (typewriter != null) typewriter.StopTyping();
        if (_startupCoroutine != null) { StopCoroutine(_startupCoroutine); _startupCoroutine = null; }
        _startingUp = false;
        ShowScreen(null);
        if(pcDisplay) pcDisplay.gameObject.SetActive(false); 
        if (desktopShell != null) desktopShell.PlayShutdown();
    }

    /// <summary>Called when the player clicks during the boot/welcome sequence.</summary>
    public void SkipStartup()
    {
        _skipStartup = true;
        if (desktopShell != null) desktopShell.SkipBoot();
        if (typewriter != null) typewriter.CompleteTyping();
    }

    private IEnumerator StartupRoutine()
    {
        _skipStartup = false;

        if (desktopShell != null)
        {
            bool firstBoot = !desktopShell.HasBooted;
            yield return desktopShell.Boot();
            PlayVoice(welcomeVoiceLine);

            if (firstBoot && typewriter != null && !_skipStartup)
            {
                // Full welcome the first time; after that just a quick notification.
                typewriter.DisplayText(desktopShell.SplashText, pcWelcomeMessage);
                while (typewriter.IsTyping && !_skipStartup) yield return null;
                float holdUntil = Time.unscaledTime + 0.8f;
                while (Time.unscaledTime < holdUntil && !_skipStartup) yield return null;
                typewriter.StopTyping();
            }
            else
            {
                desktopShell.Notify("Welcome back. Session restored.", PC_DesktopShell.NoticeType.Info);
            }
        }
        else
        {
            if(pcDisplay) pcDisplay.gameObject.SetActive(true);
            PlayVoice(welcomeVoiceLine);
            if (typewriter != null && pcDisplay != null) 
            { 
                yield return typewriter.DisplayText(pcDisplay, pcWelcomeMessage); 
                yield return new WaitForSeconds(1f); 
            }
        }

        _startupCoroutine = null;
        _startingUp = false;
        ShowMainMenu();
    }

    public void ShowMainMenu()
    {
        if (IsStartingUp) return;
        if(pcDisplay) pcDisplay.gameObject.SetActive(false);
        ShowScreen(MainMenu_Container);
    }

    public void ShowInteractPrompt(bool show, string message)
    {
        if (desktopShell != null)
        {
            // The monitor shows a lock screen with the prompt instead of bare text.
            ShowScreen(null);
            if (pcDisplay) pcDisplay.gameObject.SetActive(false);
            if (show)
            {
                desktopShell.ShowLockScreen();
                if (typewriter != null) typewriter.DisplayText(desktopShell.LockPromptText, message);
                else if (desktopShell.LockPromptText) desktopShell.LockPromptText.text = message;
            }
            else if (typewriter != null) typewriter.StopTyping();
            return;
        }

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

    private void OnTaskbarApp(int index)
    {
        if (IsStartingUp) return;
        switch (index)
        {
            case 0: OnMainMenu_PlayerClicked(); break;
            case 1: OnMainMenu_WeaponsClicked(); break;
            case 2: OnMainMenu_BotzClicked(); break;
        }
    }

    private void LogOff()
    {
        var interaction = FindObjectOfType<PlayerInteractionManager>();
        if (interaction != null) interaction.EndPCInteraction();
    }
    
    private void PlayVoice(AudioClip clip)
    {
        if (voiceAudioSource != null && clip != null)
        {
            voiceAudioSource.Stop(); // Stop any previous line so they don't overlap
            voiceAudioSource.PlayOneShot(clip);
        }
    }

    // --- MAIN MENU BUTTONS ---
    
    public void OnMainMenu_PlayerClicked() 
    { 
        ShowScreen(PlayerUpgrade_Container); 
        UpdatePlayerUI();
        Type("Biometrics Loaded.");
        PlayVoice(playerMenuVoiceLine);
    }

    public void OnMainMenu_WeaponsClicked() 
    { 
        ShowScreen(WeaponList_Container); 
        UpdateWeaponListUI();
        Type("Armory Database Accessed.");
        PlayVoice(weaponsMenuVoiceLine);
    }

    public void OnMainMenu_BotzClicked() 
    { 
        ShowScreen(BotUpgrade_Container); 
        RefreshBotUI();
        Type("Bot Modification Module Loaded.");
        PlayVoice(botMenuVoiceLine);
    }

    public void OnMainMenu_MysteryClicked() 
    { 
        Fail("ERROR: Encrypted Data. Access Denied.");
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
        if (path.currentLevel >= path.maxLevel) { Fail("Already at max level."); return; }
        if (UpgradeManager.Instance.TryBuyGlobalUpgrade(path, key)) { UpdatePlayerUI(); Success(msg); }
        else Fail(InsufficientFundsMessage(path.GetCost()));
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
                if(buyTxt) buyTxt.text = $"Purchase for {CostLabel(cost)}";
                
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

        if (UpgradeManager.Instance.IsWeaponUnlocked(wName)) { Fail($"{wName} is already installed."); return; }

        if (UpgradeManager.Instance.TryUnlockWeapon(wName)) 
        { 
            UpdateWeaponListUI(); 
            Success($"{wName} Unlocked."); 
        }
        else 
        { 
            Fail(InsufficientFundsMessage(UpgradeManager.Instance.GetWeaponUnlockCost(wName))); 
        }
    }

    public void OnSelect_WeaponToUpgrade(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= weaponRows.Length) return;
        if (UpgradeManager.Instance == null) return;

        string weaponName = weaponRows[rowIndex].weaponName;

        if (!UpgradeManager.Instance.IsWeaponUnlocked(weaponName))
        {
            Fail("Access Denied: Weapon not owned.");
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

        // 1. Standard Stats (Always Show)
        UpdateBtnText(wData.damagePath, btnText_SpecificDmg, "Damage");
        UpdateBtnText(wData.fireRatePath, btnText_SpecificRate, "Fire Rate");
        UpdateBtnText(wData.maxAmmoPath, btnText_SpecificMaxAmmo, "Max Ammo");
        UpdateBtnText(wData.clipSizePath, btnText_SpecificClip, "Clip Size");

        // 2. Special Upgrade Logic (Rocket Launcher ONLY)
        // Make sure your WeaponData asset is named exactly "RocketLauncher"
        if (_selectedWeaponName == "RocketLauncher") 
        {
            // SHOW the button
            if(btn_SpecialUpgrade) btn_SpecialUpgrade.gameObject.SetActive(true);

            if (btnText_SpecialUpgrade)
            {
                if (wData.guidanceUnlocked)
                {
                    btnText_SpecialUpgrade.text = "Guidance System\nOWNED";
                    if(btn_SpecialUpgrade) btn_SpecialUpgrade.interactable = false;
                }
                else
                {
                    btnText_SpecialUpgrade.text = $"Unlock Guidance\nCost: {CostLabel(wData.guidanceCost)}";
                    if(btn_SpecialUpgrade) btn_SpecialUpgrade.interactable = true;
                }
            }
        }
        else
        {
            // HIDE the button for everything else (Rifle, Shotgun, etc.)
            if(btn_SpecialUpgrade) btn_SpecialUpgrade.gameObject.SetActive(false);
        }
    }
    
    public void OnBuy_SpecialUpgrade()
    {
        if (UpgradeManager.Instance == null) return;

        // Try to buy "Guidance"
        var wData = UpgradeManager.Instance.GetWeaponData(_selectedWeaponName);
        if (wData != null && wData.guidanceUnlocked) { Fail("Guidance system already installed."); return; }

        if (UpgradeManager.Instance.TryBuyWeaponStat(_selectedWeaponName, "Guidance")) 
        { 
            UpdateWeaponSpecificUI(); 
            Success("Guidance Chipset Installed."); 
        }
        else 
        { 
            Fail(wData != null ? InsufficientFundsMessage(wData.guidanceCost) : "Insufficient funds."); 
        }
    }

    public void OnBuy_SpecificDmg() => TryBuyWeaponStat("Damage", "Ballistics improved.");
    public void OnBuy_SpecificRate() => TryBuyWeaponStat("Rate", "Mechanism cycled.");
    public void OnBuy_SpecificMaxAmmo() => TryBuyWeaponStat("MaxAmmo", "Capacity increased.");
    public void OnBuy_SpecificClip() => TryBuyWeaponStat("ClipSize", "Magazine expanded.");

    private void TryBuyWeaponStat(string stat, string successMsg)
    {
        if (UpgradeManager.Instance == null) return;
        var path = GetWeaponPath(stat);
        if (path != null && path.currentLevel >= path.maxLevel) { Fail("Already at max level."); return; }

        if (UpgradeManager.Instance.TryBuyWeaponStat(_selectedWeaponName, stat)) { UpdateWeaponSpecificUI(); Success(successMsg); }
        else Fail(path != null ? InsufficientFundsMessage(path.GetCost()) : "Insufficient funds.");
    }

    private UpgradeManager.UpgradePath GetWeaponPath(string stat)
    {
        var wData = UpgradeManager.Instance != null ? UpgradeManager.Instance.GetWeaponData(_selectedWeaponName) : null;
        if (wData == null) return null;
        switch (stat)
        {
            case "Damage": return wData.damagePath;
            case "Rate": return wData.fireRatePath;
            case "MaxAmmo": return wData.maxAmmoPath;
            case "ClipSize": return wData.clipSizePath;
        }
        return null;
    }
    
    public void OnBuy_RefillAmmo()
    {
        if (_playerStats == null) return;

        // 1. Check money
        if (_playerStats.scrapMetal < ammoCost)
        {
            Fail(InsufficientFundsMessage(ammoCost));
            return;
        }

        // 2. Access Weapon Controller
        WeaponControllerHS weaponController = FindObjectOfType<WeaponControllerHS>();
        if (weaponController != null)
        {
            // Optional: Check if we are holding the weapon we are looking at
            // For simplicity, this refills the CURRENTLY EQUIPPED weapon's reserve

            weaponController.RefillCurrentReserve(); 
            _playerStats.SpendScrap(ammoCost);
            Success("Ammunition Replicated.");
        }
        else
        {
            Fail("No weapon linked to this terminal.");
        }
    }

    // --- BOT LOGIC ---
    private void RefreshBotUI()
    {
        if (_botScript == null) _botScript = FindObjectOfType<ScrapCollectorBot>();
        if (_botScript == null)
        {
            if (botStatusText) botStatusText.text = "No drone detected.";
            return;
        }
        SetBotButton(btn_UpgradeCapacity, _botScript.carryCapacity < 5, "Expand Cargo", COST_CAPACITY);
        SetBotButton(btn_UpgradeFlight, _botScript.carryCapacity > 1 && !_botScript.canFly, "Flight Systems", COST_FLIGHT);
        SetBotButton(btn_UpgradeHeal, _botScript.canFly && !_botScript.canHeal, "Medical Module", COST_HEAL);
        if(botStatusText) botStatusText.text = $"Load: {_botScript.carryCapacity} | Flight: {(_botScript.canFly?"ON":"OFF")} | Medic: {(_botScript.canHeal?"ON":"OFF")}";
    }

    private void SetBotButton(Button button, bool available, string label, int cost)
    {
        if (button == null) return;
        button.gameObject.SetActive(available);
        var txt = button.GetComponentInChildren<TMP_Text>();
        if (txt) txt.text = $"{label}\nCost: {CostLabel(cost)}";
    }

    public void OnBuy_BotCapacity() => TryBuyBot(COST_CAPACITY, () => _botScript.UpgradeCapacity(_botScript.carryCapacity + 2), "Cargo bay expanded.");
    public void OnBuy_BotFlight() => TryBuyBot(COST_FLIGHT, () => _botScript.UpgradeFlight(), "Flight systems online.");
    public void OnBuy_BotHeal() => TryBuyBot(COST_HEAL, () => _botScript.UpgradeHealing(), "Medical module installed.");

    private void TryBuyBot(int cost, System.Action apply, string successMsg)
    {
        if (_botScript == null || _playerStats == null) { Fail("No drone detected."); return; }
        if (_playerStats.SpendScrap(cost)) { apply(); RefreshBotUI(); Success(successMsg); }
        else Fail(InsufficientFundsMessage(cost));
    }

    // --- HELPERS ---

    private void UpdateBtnText(UpgradeManager.UpgradePath path, TextMeshProUGUI txt, string desc)
    {
        if (txt == null) return;
        if (path.currentLevel >= path.maxLevel) 
        {
            txt.text = $"{desc}\n<color={AFFORD_COLOR}>MAX LEVEL</color>";
        }
        else 
        {
            txt.text = $"{desc} (Lvl {path.currentLevel}/{path.maxLevel})\nCost: {CostLabel(path.GetCost())}";
        }
    }

    /// <summary>Cost text coloured green if the player can afford it right now, red if not.</summary>
    private string CostLabel(int cost)
    {
        bool canAfford = _playerStats == null || _playerStats.scrapMetal >= cost;
        return $"<color={(canAfford ? AFFORD_COLOR : EXPENSIVE_COLOR)}>{cost:N0}</color>";
    }

    private string InsufficientFundsMessage(int cost)
    {
        if (_playerStats == null) return "Insufficient funds.";
        return $"Insufficient funds. Need {Mathf.Max(0, cost - _playerStats.scrapMetal):N0} more scrap.";
    }

    private void Success(string msg) => Feedback(msg, PC_DesktopShell.NoticeType.Success);
    private void Fail(string msg) => Feedback(msg, PC_DesktopShell.NoticeType.Error);
    private void Type(string msg) => Feedback(msg, PC_DesktopShell.NoticeType.Info);

    private void Feedback(string msg, PC_DesktopShell.NoticeType type)
    {
        if (desktopShell != null)
        {
            desktopShell.Notify(msg, type);
            if (type != PC_DesktopShell.NoticeType.Info) desktopShell.PulseLastClicked(type == PC_DesktopShell.NoticeType.Success);
            return;
        }
        if(typewriter) typewriter.DisplayText(pcDisplay, msg);
    }

    public void BackToMainMenu() { ShowMainMenu(); }
    public void BackToWeaponList() { OnMainMenu_WeaponsClicked(); }

    private void RefreshCurrentScreen()
    {
        if (_playerStats != null) _lastRefreshScrap = _playerStats.scrapMetal;
        if (_currentScreen == null) return;
        if (_currentScreen == PlayerUpgrade_Container) UpdatePlayerUI();
        else if (_currentScreen == WeaponList_Container) UpdateWeaponListUI();
        else if (_currentScreen == WeaponSpecific_Container) UpdateWeaponSpecificUI();
        else if (_currentScreen == BotUpgrade_Container) RefreshBotUI();
    }

    private void ShowScreen(GameObject screenToShow)
    {
        if(MainMenu_Container) MainMenu_Container.SetActive(false);
        if(BotUpgrade_Container) BotUpgrade_Container.SetActive(false);
        if(PlayerUpgrade_Container) PlayerUpgrade_Container.SetActive(false);
        if(WeaponList_Container) WeaponList_Container.SetActive(false);
        if(WeaponSpecific_Container) WeaponSpecific_Container.SetActive(false);
        
        if (screenToShow != null) screenToShow.SetActive(true);
        _currentScreen = screenToShow;
        if (_playerStats != null) _lastRefreshScrap = _playerStats.scrapMetal;

        if (desktopShell != null)
        {
            desktopShell.ShowWindow(screenToShow, WindowTitle(screenToShow), WindowCloseAction(screenToShow));
            desktopShell.SetActiveApp(screenToShow == PlayerUpgrade_Container ? 0
                : screenToShow == WeaponList_Container || screenToShow == WeaponSpecific_Container ? 1
                : screenToShow == BotUpgrade_Container ? 2 : -1);
        }
    }

    private string WindowTitle(GameObject screen)
    {
        if (screen == null) return "";
        if (screen == MainMenu_Container) return title_MainMenu;
        if (screen == PlayerUpgrade_Container) return title_Player;
        if (screen == WeaponList_Container) return title_WeaponList;
        if (screen == WeaponSpecific_Container) return title_WeaponSpecific + _selectedWeaponName;
        if (screen == BotUpgrade_Container) return title_Bot;
        return screen.name;
    }

    private System.Action WindowCloseAction(GameObject screen)
    {
        // Closing a window goes back one level, and closing the Command Center logs off.
        if (screen == WeaponSpecific_Container) return BackToWeaponList;
        if (screen == MainMenu_Container) return LogOff;
        return ShowMainMenu;
    }
}