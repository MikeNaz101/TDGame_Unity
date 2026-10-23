using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds a desktop-OS look around the existing PC upgrade screens at runtime:
/// wallpaper with desktop icons, a window frame with a title bar, a taskbar with a
/// start button / clock / scrap counter, a BIOS boot sequence, a lock screen,
/// toast notifications, a real arrow/hand mouse cursor, CRT monitor effects and UI sounds.
///
/// Lives on the same canvas as PC_UI_Manager, which adds it automatically if it's missing,
/// so nothing has to be rebuilt in the scene. The existing containers and buttons are
/// left exactly where they are; the shell only draws around them.
/// </summary>
[DisallowMultipleComponent]
public class PC_DesktopShell : MonoBehaviour
{
    public enum Mode { Off, Locked, Booting, Desktop }
    public enum NoticeType { Info, Success, Error }

    [Header("Screen Layout")]
    [Tooltip("Rotation of the PC content relative to the canvas. The existing PC buttons are rotated -90 on Z, so the desktop is too.")]
    public float contentRotationZ = -90f;
    [Tooltip("Counteracts the canvas' non-uniform scale so text and icons aren't stretched.")]
    public bool correctAspect = true;
    [Tooltip("Optional. The full-screen background panel. The wallpaper is drawn just above it. Auto-found by the name 'Panel'.")]
    public RectTransform backgroundPanel;
    public float taskbarHeight = 30f;
    public float titleBarHeight = 22f;
    public float windowMargin = 6f;

    [Header("Branding")]
    public string osName = "COMMAND//OS";
    public string osVersion = "v2.4";
    [Tooltip("Lines printed during a cold boot. The first time the PC is used each session it cold boots; after that it just wakes up.")]
    public string[] bootLines =
    {
        "COMMAND//OS BIOS v2.4  (c) Outpost Systems",
        "CPU: ScrapCore X2 @ 3.20GHz ........ OK",
        "Memory test: 65536K ................. OK",
        "Detecting turret uplink ............. OK",
        "Mounting drone bay controller ....... OK",
        "Loading armory database ............. OK",
        "Syncing scrap ledger ................ OK",
        "Starting desktop environment...",
    };

    [Header("Colors")]
    public Color wallpaperTop = new Color(0.04f, 0.07f, 0.13f);
    public Color wallpaperBottom = new Color(0.02f, 0.22f, 0.28f);
    public Color accentColor = new Color(0.22f, 0.84f, 0.96f);
    public Color windowColor = new Color(0.05f, 0.07f, 0.10f, 0.94f);
    public Color titleBarColor = new Color(0.10f, 0.14f, 0.20f, 1f);
    public Color taskbarColor = new Color(0.03f, 0.04f, 0.07f, 0.97f);
    public Color buttonColor = new Color(0.12f, 0.16f, 0.22f, 1f);
    public Color textColor = new Color(0.88f, 0.94f, 0.98f);
    public Color successColor = new Color(0.36f, 0.95f, 0.56f);
    public Color errorColor = new Color(1f, 0.40f, 0.40f);
    public Color bootTextColor = new Color(0.46f, 1f, 0.56f);

    [Header("Cursor")]
    [Tooltip("Swap the cursor's art for a classic arrow that turns into a hand over buttons.")]
    public bool replaceCursorArt = true;
    public float cursorScale = 1.3f;

    [Header("Monitor Effects")]
    public bool crtEffect = true;
    [Range(0f, 0.3f)] public float scanlineStrength = 0.07f;
    [Range(0f, 1f)] public float vignetteStrength = 0.5f;

    [Header("Audio (leave empty to use built-in synthesized sounds)")]
    public AudioClip hoverSound;
    public AudioClip clickSound;
    public AudioClip successSound;
    public AudioClip errorSound;
    public AudioClip notifySound;
    public AudioClip bootSound;
    public AudioClip shutdownSound;
    public AudioClip ambientHum;
    [Range(0f, 1f)] public float sfxVolume = 0.6f;
    [Range(0f, 1f)] public float humVolume = 0.05f;

    [Header("Timing")]
    public float coldBootDuration = 1.6f;
    public float wakeDuration = 0.35f;
    public float noticeDuration = 2.6f;
    [Tooltip("Characters per second when a notification types itself out.")]
    public float noticeTypeSpeed = 70f;

    public Mode CurrentMode { get; private set; } = Mode.Off;
    public bool IsBooting => CurrentMode == Mode.Booting;
    public bool HasBooted { get; private set; }
    public RectTransform CursorLayer => _cursorLayer;
    public Vector2 DesktopSize => _desktopSize;
    public TMP_Text SplashText => _splashText;
    public TMP_Text LockPromptText => _lockPrompt;

    public event Action StartClicked;
    public event Action MinimizeClicked;
    public event Action LogOffClicked;
    public event Action<int> AppClicked;

    // --- BUILT UI ---
    private bool _built;
    private RectTransform _canvasRect;
    private TMP_FontAsset _font;
    private Vector2 _desktopSize;

    private RectTransform _backRoot, _frontRoot, _cursorLayer;
    private GameObject _desktopIcons, _windowBody, _titleBar, _taskbar, _lockScreen, _crt;
    private TMP_Text _titleText, _trayScrap, _trayClock, _lockClock, _lockDate, _lockPrompt, _splashText, _bootText;
    private CanvasGroup _bootGroup, _toastGroup;
    private RectTransform _bootProgressFill, _rollingBand;
    private Image _toastAccent;
    private TMP_Text _toastIcon, _toastText;
    private RawImage _scanlines;

    private readonly List<Image> _appUnderlines = new List<Image>();
    private bool _appsConfigured;
    private bool _windowVisible;
    private Action _closeAction;

    // --- CURSOR ---
    private RectTransform _cursor;
    private RawImage _cursorArt;
    private bool _cursorHover;

    // --- AUDIO ---
    private AudioSource _sfx, _hum;

    // --- STATE ---
    private PlayerStats _playerStats;
    private int _lastScrap = int.MinValue;
    private int _lastClockMinute = -1;
    private bool _skipBoot;
    private float _toastShownAt = -100f;
    private int _toastLength;
    private Coroutine _windowAnim, _punchAnim;
    private GameObject _animContainer;
    private RectTransform _punchTarget;
    private Vector3 _punchBasePos, _punchBaseScale;
    private readonly Dictionary<int, Vector3> _containerBaseScale = new Dictionary<int, Vector3>();
    private readonly StringBuilder _sb = new StringBuilder(256);

    // --- SHARED GENERATED ASSETS ---
    private static Sprite s_rounded;
    private static Texture2D s_arrow, s_hand, s_scanlines, s_vignette, s_band, s_grid;

    void Awake()
    {
        Build();
    }

    void OnDisable()
    {
        // The canvas is switched off when the player walks away; leave everything in a clean state.
        StopAllCoroutines();
        _windowAnim = null;
        _punchAnim = null;
        RestoreAnimatedContainer();
        RestorePunchTarget();
        if (_toastGroup) _toastGroup.alpha = 0f;
        if (_hum) _hum.Stop();
        CurrentMode = Mode.Off;
    }

    // =====================================================================
    // PUBLIC API
    // =====================================================================

    /// <summary>Lock screen shown while the player stands at the PC but isn't using it.</summary>
    public void ShowLockScreen()
    {
        SetMode(Mode.Locked);
        ShowWindow(null, null, null);
        UpdateClock(true);
    }

    /// <summary>Plays the boot (or wake) sequence, then switches to the desktop.</summary>
    public IEnumerator Boot()
    {
        bool cold = !HasBooted;
        _skipBoot = false;
        SetMode(Mode.Booting);
        _bootGroup.alpha = 1f;
        SetBootProgress(0f);

        if (cold)
        {
            float delay = bootLines.Length > 0 ? coldBootDuration / bootLines.Length : 0f;
            _sb.Clear();
            _sb.Append("<mspace=0.56em>");
            for (int i = 0; i < bootLines.Length; i++)
            {
                _sb.Append(bootLines[i]).Append('\n');
                _bootText.text = _sb.ToString();
                SetBootProgress((i + 1f) / bootLines.Length);
                Play(null, PC_ProceduralAudio.Hover, 0.35f);
                yield return Wait(delay);
            }
        }
        else
        {
            _bootText.text = $"<mspace=0.56em>{osName} {osVersion}\nResuming session...";
            float start = Time.unscaledTime;
            while (Time.unscaledTime - start < wakeDuration && !_skipBoot)
            {
                SetBootProgress((Time.unscaledTime - start) / wakeDuration);
                yield return null;
            }
        }

        SetBootProgress(1f);
        Play(bootSound, PC_ProceduralAudio.Boot, cold ? 1f : 0.6f);
        HasBooted = true;
        SetMode(Mode.Desktop);

        // Fade the boot screen away to reveal the desktop underneath.
        _bootGroup.gameObject.SetActive(true);
        float fadeStart = Time.unscaledTime;
        while (Time.unscaledTime - fadeStart < 0.25f)
        {
            _bootGroup.alpha = 1f - (Time.unscaledTime - fadeStart) / 0.25f;
            yield return null;
        }
        _bootGroup.alpha = 0f;
        _bootGroup.gameObject.SetActive(false);
    }

    public void SkipBoot() => _skipBoot = true;

    public void PlayShutdown() => Play(shutdownSound, PC_ProceduralAudio.Shutdown, 0.8f);

    /// <summary>Shows a container inside the window frame. Pass null to show the bare desktop.</summary>
    public void ShowWindow(GameObject container, string title, Action onClose)
    {
        _closeAction = onClose;
        _windowVisible = container != null;
        if (_titleText) _titleText.text = title ?? "";
        ApplyVisibility();

        if (_windowAnim != null) StopCoroutine(_windowAnim);
        RestoreAnimatedContainer();
        if (container != null && isActiveAndEnabled) _windowAnim = StartCoroutine(OpenWindowRoutine(container));
    }

    /// <summary>Builds the taskbar buttons and desktop icons once. Glyphs are short labels drawn on the icons.</summary>
    public void ConfigureApps(string[] names, string[] glyphs)
    {
        Build();
        if (_appsConfigured || names == null) return;
        _appsConfigured = true;

        float x = 4f + 64f + 3f;
        for (int i = 0; i < names.Length; i++)
        {
            int index = i;
            var btn = NewButton("App_" + names[i], _taskbar.transform, names[i], 9f, () => AppClicked?.Invoke(index));
            PlaceLeft((RectTransform)btn.transform, x, 62f, taskbarHeight - 8f);
            x += 65f;

            var underline = NewImage("Active", btn.transform, accentColor);
            Anchor(underline.rectTransform, new Vector2(0.15f, 0f), new Vector2(0.85f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 2f));
            underline.enabled = false;
            _appUnderlines.Add(underline);

            string glyph = glyphs != null && i < glyphs.Length ? glyphs[i] : names[i].Substring(0, 1);
            BuildDesktopIcon(i, names[i], glyph, () => AppClicked?.Invoke(index));
        }
    }

    /// <summary>Highlights the running app on the taskbar. -1 clears it.</summary>
    public void SetActiveApp(int index)
    {
        for (int i = 0; i < _appUnderlines.Count; i++) _appUnderlines[i].enabled = i == index;
    }

    public void Notify(string message, NoticeType type)
    {
        Build();
        if (string.IsNullOrEmpty(message)) return;

        Color c = type == NoticeType.Success ? successColor : type == NoticeType.Error ? errorColor : accentColor;
        _toastAccent.color = c;
        _toastIcon.color = c;
        _toastIcon.text = type == NoticeType.Success ? "+" : type == NoticeType.Error ? "!" : "i";
        _toastText.text = message;
        _toastText.maxVisibleCharacters = 0;
        _toastLength = message.Length;
        _toastShownAt = Time.unscaledTime;
        _toastGroup.alpha = 1f;

        if (type == NoticeType.Success) Play(successSound, PC_ProceduralAudio.Success, 0.8f);
        else if (type == NoticeType.Error) Play(errorSound, PC_ProceduralAudio.Error, 0.8f);
        else Play(notifySound, PC_ProceduralAudio.Notify, 0.6f);
    }

    /// <summary>Gives the button that was just clicked a little pop (success) or shake (failure).</summary>
    public void PulseLastClicked(bool success)
    {
        if (_punchTarget == null || !isActiveAndEnabled) return;
        var target = _punchTarget;
        RestorePunchTarget();
        _punchTarget = target;
        _punchBasePos = target.localPosition;
        _punchBaseScale = target.localScale;
        _punchAnim = StartCoroutine(success ? PopRoutine(target) : ShakeRoutine(target));
    }

    // --- Cursor / pointer hooks, driven by PlayerInteractionManager ---

    public void AdoptCursor(RectTransform cursor)
    {
        Build();
        if (cursor == null) return;
        _cursor = cursor;

        if (cursor.parent != _cursorLayer)
        {
            cursor.SetParent(_cursorLayer, false);
            cursor.localRotation = Quaternion.identity;
            cursor.localScale = Vector3.one;
            cursor.anchorMin = cursor.anchorMax = new Vector2(0.5f, 0.5f);
            cursor.pivot = new Vector2(0f, 1f); // Tip of the arrow is the click point.
        }
        cursor.SetAsLastSibling();

        // The cursor must never block the raycast that finds what it's pointing at.
        foreach (var g in cursor.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;

        if (replaceCursorArt && _cursorArt == null)
        {
            foreach (var g in cursor.GetComponents<Graphic>()) g.enabled = false;
            for (int i = 0; i < cursor.childCount; i++) cursor.GetChild(i).gameObject.SetActive(false);

            var art = NewRect("CursorArt", cursor);
            Stretch(art);
            _cursorArt = art.gameObject.AddComponent<RawImage>();
            _cursorArt.raycastTarget = false;
        }
        SetCursorHover(false, true);
    }

    public Vector2 ClampToDesktop(Vector2 p)
    {
        Vector2 half = _desktopSize * 0.5f;
        return new Vector2(Mathf.Clamp(p.x, -half.x, half.x), Mathf.Clamp(p.y, -half.y, half.y));
    }

    public void OnHoverChanged(Selectable selectable)
    {
        bool interactive = selectable != null && selectable.IsInteractable();
        SetCursorHover(interactive, false);
        if (interactive) Play(hoverSound, PC_ProceduralAudio.Hover, 0.5f);
    }

    public void OnClicked(GameObject clicked)
    {
        var selectable = clicked ? clicked.GetComponentInParent<Selectable>() : null;
        _punchTarget = selectable ? selectable.transform as RectTransform : null;
        // Buttons that already have their own click sound keep it instead of doubling up.
        if (clicked && clicked.GetComponentInParent<UIButtonSound>() == null) Play(clickSound, PC_ProceduralAudio.Click, 0.7f);
    }

    // =====================================================================
    // UPDATE
    // =====================================================================

    void Update()
    {
        if (!_built || CurrentMode == Mode.Off) return;
        float now = Time.unscaledTime;

        UpdateClock(false);

        if (CurrentMode == Mode.Desktop)
        {
            if (_playerStats == null) _playerStats = FindObjectOfType<PlayerStats>();
            if (_playerStats != null && _playerStats.scrapMetal != _lastScrap)
            {
                _lastScrap = _playerStats.scrapMetal;
                _trayScrap.text = $"SCRAP <color=#{ColorUtility.ToHtmlStringRGB(accentColor)}>{_lastScrap:N0}</color>";
            }
        }

        // Toast: type in, hold, fade out.
        if (_toastGroup.alpha > 0f)
        {
            float age = now - _toastShownAt;
            _toastText.maxVisibleCharacters = Mathf.Min(_toastLength, Mathf.FloorToInt(age * noticeTypeSpeed));
            if (age > noticeDuration) _toastGroup.alpha = Mathf.Max(0f, 1f - (age - noticeDuration) / 0.35f);
        }

        if (CurrentMode == Mode.Locked && _lockPrompt)
        {
            var c = _lockPrompt.color;
            c.a = 0.75f + 0.25f * Mathf.Sin(now * 3f);
            _lockPrompt.color = c;
        }

        if (crtEffect && _crt.activeSelf)
        {
            // Faint flicker plus a slow rolling refresh band, like an old monitor.
            var c = _scanlines.color;
            c.a = 1f - Mathf.PerlinNoise(now * 7f, 0f) * 0.25f;
            _scanlines.color = c;

            float h = _desktopSize.y;
            float t = (now % 6f) / 6f;
            _rollingBand.anchoredPosition = new Vector2(0f, Mathf.Lerp(h * 0.5f + 30f, -h * 0.5f - 30f, t));
        }
    }

    private void UpdateClock(bool force)
    {
        DateTime nowTime = DateTime.Now;
        if (!force && nowTime.Minute == _lastClockMinute) return;
        _lastClockMinute = nowTime.Minute;
        string hhmm = nowTime.ToString("HH:mm");
        if (_trayClock) _trayClock.text = hhmm;
        if (_lockClock) _lockClock.text = hhmm;
        if (_lockDate) _lockDate.text = nowTime.ToString("dddd, MMMM d");
    }

    // =====================================================================
    // MODES & VISIBILITY
    // =====================================================================

    private void SetMode(Mode mode)
    {
        Build();
        CurrentMode = mode;
        ApplyVisibility();

        bool running = mode == Mode.Booting || mode == Mode.Desktop;
        if (running && !_hum.isPlaying && humVolume > 0f)
        {
            _hum.clip = ambientHum != null ? ambientHum : PC_ProceduralAudio.Hum;
            _hum.volume = humVolume;
            _hum.Play();
        }
        else if (!running && _hum.isPlaying) _hum.Stop();

        if (mode != Mode.Desktop) _toastGroup.alpha = 0f;
        if (mode == Mode.Desktop) _lastScrap = int.MinValue; // Force the tray to refresh.
    }

    private void ApplyVisibility()
    {
        if (!_built) return;
        bool desktop = CurrentMode == Mode.Desktop;
        _backRoot.gameObject.SetActive(CurrentMode != Mode.Off);
        _windowBody.SetActive(desktop && _windowVisible);
        _titleBar.SetActive(desktop && _windowVisible);
        _desktopIcons.SetActive(desktop && !_windowVisible);
        _taskbar.SetActive(desktop);
        _lockScreen.SetActive(CurrentMode == Mode.Locked);
        _bootGroup.gameObject.SetActive(CurrentMode == Mode.Booting);
        _crt.SetActive(crtEffect && CurrentMode != Mode.Off);
    }

    // =====================================================================
    // ANIMATION
    // =====================================================================

    private IEnumerator OpenWindowRoutine(GameObject container)
    {
        _animContainer = container;
        var rt = container.transform;
        int id = container.GetInstanceID();
        if (!_containerBaseScale.TryGetValue(id, out var baseScale))
        {
            baseScale = rt.localScale;
            _containerBaseScale[id] = baseScale;
        }
        var group = container.GetComponent<CanvasGroup>();
        if (group == null) group = container.AddComponent<CanvasGroup>();

        const float duration = 0.14f;
        float start = Time.unscaledTime;
        while (Time.unscaledTime - start < duration)
        {
            float t = (Time.unscaledTime - start) / duration;
            float eased = 1f - (1f - t) * (1f - t);
            group.alpha = eased;
            rt.localScale = baseScale * Mathf.Lerp(0.96f, 1f, eased);
            yield return null;
        }
        group.alpha = 1f;
        rt.localScale = baseScale;
        _animContainer = null;
        _windowAnim = null;
    }

    private void RestoreAnimatedContainer()
    {
        if (_animContainer == null) return;
        if (_containerBaseScale.TryGetValue(_animContainer.GetInstanceID(), out var baseScale)) _animContainer.transform.localScale = baseScale;
        var group = _animContainer.GetComponent<CanvasGroup>();
        if (group) group.alpha = 1f;
        _animContainer = null;
    }

    private IEnumerator PopRoutine(RectTransform target)
    {
        float start = Time.unscaledTime;
        while (Time.unscaledTime - start < 0.18f)
        {
            float t = (Time.unscaledTime - start) / 0.18f;
            target.localScale = _punchBaseScale * (1f + Mathf.Sin(t * Mathf.PI) * 0.08f);
            yield return null;
        }
        RestorePunchTarget();
    }

    private IEnumerator ShakeRoutine(RectTransform target)
    {
        // Shake along the button's own horizontal axis so it reads as "no" on the rotated screen.
        Vector3 axis = target.localRotation * Vector3.right;
        float amplitude = target.rect.width * 0.06f;
        float start = Time.unscaledTime;
        while (Time.unscaledTime - start < 0.3f)
        {
            float t = (Time.unscaledTime - start) / 0.3f;
            target.localPosition = _punchBasePos + axis * (Mathf.Sin(t * Mathf.PI * 8f) * amplitude * (1f - t));
            yield return null;
        }
        RestorePunchTarget();
    }

    private void RestorePunchTarget()
    {
        if (_punchAnim != null) { StopCoroutine(_punchAnim); _punchAnim = null; }
        if (_punchTarget != null && _punchBaseScale != Vector3.zero)
        {
            _punchTarget.localPosition = _punchBasePos;
            _punchTarget.localScale = _punchBaseScale;
        }
        _punchBaseScale = Vector3.zero;
    }

    private IEnumerator Wait(float seconds)
    {
        float end = Time.unscaledTime + seconds;
        while (Time.unscaledTime < end && !_skipBoot) yield return null;
    }

    private void SetBootProgress(float t)
    {
        _bootProgressFill.anchorMax = new Vector2(Mathf.Clamp01(t), 1f);
    }

    private void SetCursorHover(bool hover, bool force)
    {
        if (_cursorArt == null || (!force && hover == _cursorHover)) return;
        _cursorHover = hover;
        var tex = hover ? s_hand : s_arrow;
        _cursorArt.texture = tex;
        // The hand's fingertip is a few pixels in from the left edge; shift so the click point stays put.
        _cursorArt.rectTransform.anchoredPosition = hover ? new Vector2(-5f * cursorScale, 0f) : Vector2.zero;
        _cursor.sizeDelta = new Vector2(tex.width, tex.height) * cursorScale;
    }

    // =====================================================================
    // AUDIO
    // =====================================================================

    private void Play(AudioClip custom, AudioClip fallback, float volume)
    {
        if (_sfx == null || !isActiveAndEnabled) return;
        var clip = custom != null ? custom : fallback;
        if (clip != null) _sfx.PlayOneShot(clip, sfxVolume * volume);
    }

    // =====================================================================
    // BUILDING
    // =====================================================================

    public void Build()
    {
        if (_built) return;
        _built = true;

        _canvasRect = (RectTransform)transform;
        var existingText = GetComponentInChildren<TMP_Text>(true);
        _font = existingText != null && existingText.font != null ? existingText.font : TMP_Settings.defaultFontAsset;
        GenerateAssets();

        if (backgroundPanel == null)
        {
            var panel = transform.Find("Panel");
            if (panel != null) backgroundPanel = panel as RectTransform;
        }

        _backRoot = CreateRoot("PC_Desktop_Back");
        _backRoot.SetSiblingIndex(backgroundPanel != null && backgroundPanel.parent == transform ? backgroundPanel.GetSiblingIndex() + 1 : 0);
        _frontRoot = CreateRoot("PC_Desktop_Front");
        _frontRoot.SetAsLastSibling();

        BuildWallpaper();
        BuildWindow();
        BuildTaskbar();
        BuildToast();
        BuildSplash();
        BuildLockScreen();
        BuildBootScreen();
        BuildCrt();

        _cursorLayer = NewRect("CursorLayer", _frontRoot);
        Stretch(_cursorLayer);

        var sfxGo = new GameObject("PC_Sfx");
        sfxGo.transform.SetParent(transform, false);
        _sfx = sfxGo.AddComponent<AudioSource>();
        _sfx.playOnAwake = false;
        _sfx.spatialBlend = 0f;
        _hum = sfxGo.AddComponent<AudioSource>();
        _hum.playOnAwake = false;
        _hum.loop = true;
        _hum.spatialBlend = 0f;

        CurrentMode = Mode.Off;
        ApplyVisibility();
    }

    private RectTransform CreateRoot(string name)
    {
        var rt = NewRect(name, _canvasRect);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition3D = Vector3.zero;
        rt.localRotation = Quaternion.Euler(0f, 0f, contentRotationZ);
        rt.localScale = Vector3.one;

        if (correctAspect)
        {
            float worldX = rt.TransformVector(Vector3.right).magnitude;
            float worldY = rt.TransformVector(Vector3.up).magnitude;
            if (worldX > 0f && worldY > 0f) rt.localScale = new Vector3(1f, worldX / worldY, 1f);
        }

        // Size the root to exactly cover the canvas, measured in the root's own (rotated, scaled) space.
        Rect r = _canvasRect.rect;
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
        Vector2[] corners = { new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax) };
        foreach (var c in corners)
        {
            Vector3 local = rt.InverseTransformPoint(_canvasRect.TransformPoint(c));
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }
        rt.sizeDelta = max - min;
        _desktopSize = rt.sizeDelta;
        return rt;
    }

    private void BuildWallpaper()
    {
        var gradient = NewRect("Wallpaper", _backRoot).gameObject.AddComponent<RawImage>();
        gradient.texture = MakeGradient(wallpaperBottom, wallpaperTop);
        gradient.raycastTarget = false;
        Stretch(gradient.rectTransform);

        var grid = NewRect("Grid", _backRoot).gameObject.AddComponent<RawImage>();
        grid.texture = s_grid;
        grid.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.07f);
        grid.uvRect = new Rect(0f, 0f, _desktopSize.x / 32f, _desktopSize.y / 32f);
        grid.raycastTarget = false;
        Stretch(grid.rectTransform);

        var logo = NewText("Logo", _backRoot, $"<b>{osName}</b>", 46f, new Color(1f, 1f, 1f, 0.06f), TextAlignmentOptions.Center);
        Anchor(logo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, taskbarHeight * 0.5f), new Vector2(_desktopSize.x, 80f));

        _desktopIcons = NewRect("DesktopIcons", _backRoot).gameObject;
        Stretch((RectTransform)_desktopIcons.transform, 0f, 0f, taskbarHeight, 0f);
    }

    private void BuildDesktopIcon(int index, string label, string glyph, Action onClick)
    {
        const float size = 38f, pitch = 64f;
        var btn = NewButton("Icon_" + label, _desktopIcons.transform, "", 10f, onClick);
        var rt = (RectTransform)btn.transform;
        Anchor(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -14f - index * pitch), new Vector2(54f, 58f));
        // Icon buttons are see-through until hovered, like a real desktop.
        var bg = btn.targetGraphic as Image;
        var colors = btn.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0f);
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.12f);
        colors.pressedColor = new Color(1f, 1f, 1f, 0.2f);
        colors.selectedColor = colors.normalColor;
        btn.colors = colors;
        bg.color = Color.white;

        var tile = NewImage("Tile", rt, buttonColor, s_rounded);
        Anchor(tile.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -3f), new Vector2(size, size));
        var tileEdge = NewImage("Edge", tile.rectTransform, new Color(accentColor.r, accentColor.g, accentColor.b, 0.8f));
        Anchor(tileEdge.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 3f), new Vector2(-10f, 2f));
        NewText("Glyph", tile.rectTransform, $"<b>{glyph}</b>", 15f, accentColor, TextAlignmentOptions.Center).rectTransform.Fill();

        var text = NewText("Label", rt, label, 8.5f, textColor, TextAlignmentOptions.Bottom);
        Anchor(text.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 1f), new Vector2(10f, 14f));
    }

    private void BuildWindow()
    {
        float m = windowMargin;
        // The body sits behind the existing screen content so their buttons draw on top of it.
        // It's a raycast target so clicks can't fall through to the desktop icons beneath.
        _windowBody = NewRect("WindowBody", _backRoot).gameObject;
        Stretch((RectTransform)_windowBody.transform, m, m, taskbarHeight + m, m);
        var outline = NewImage("Outline", _windowBody.transform, new Color(accentColor.r, accentColor.g, accentColor.b, 0.45f), s_rounded);
        outline.rectTransform.Fill();
        var body = NewImage("Body", _windowBody.transform, windowColor, s_rounded);
        Stretch(body.rectTransform, 1f, 1f, 1f, 1f);
        body.raycastTarget = true;

        // The title bar is drawn in front so its buttons are always clickable.
        _titleBar = NewRect("TitleBar", _frontRoot).gameObject;
        var tb = (RectTransform)_titleBar.transform;
        Anchor(tb, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -m - 1f), new Vector2(-(m + 1f) * 2f, titleBarHeight));
        var bar = NewImage("Bar", tb, titleBarColor, s_rounded);
        bar.rectTransform.Fill();

        var dot = NewImage("Icon", tb, accentColor, s_rounded);
        Anchor(dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(7f, 0f), new Vector2(9f, 9f));

        _titleText = NewText("Title", tb, "", 10.5f, textColor, TextAlignmentOptions.MidlineLeft);
        Stretch(_titleText.rectTransform, 22f, 60f, 0f, 0f);

        var close = NewButton("Close", tb, "X", 10f, () => _closeAction?.Invoke());
        Anchor((RectTransform)close.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-3f, 0f), new Vector2(24f, titleBarHeight - 6f));
        var closeColors = close.colors;
        closeColors.highlightedColor = errorColor;
        closeColors.pressedColor = errorColor * 0.8f;
        close.colors = closeColors;

        var minimize = NewButton("Minimize", tb, "_", 10f, () => MinimizeClicked?.Invoke());
        Anchor((RectTransform)minimize.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-30f, 0f), new Vector2(24f, titleBarHeight - 6f));
    }

    private void BuildTaskbar()
    {
        _taskbar = NewRect("Taskbar", _frontRoot).gameObject;
        var rt = (RectTransform)_taskbar.transform;
        Anchor(rt, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, taskbarHeight));
        var bg = NewImage("Background", rt, taskbarColor);
        bg.rectTransform.Fill();
        bg.raycastTarget = true; // Clicks on the bar shouldn't reach the screens behind it.
        var line = NewImage("TopLine", rt, new Color(accentColor.r, accentColor.g, accentColor.b, 0.5f));
        Anchor(line.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 1f));

        var start = NewButton("Start", rt, $"<b>START</b>", 9.5f, () => StartClicked?.Invoke());
        PlaceLeft((RectTransform)start.transform, 4f, 64f, taskbarHeight - 8f);
        var startColors = start.colors;
        startColors.normalColor = new Color(accentColor.r * 0.45f, accentColor.g * 0.45f, accentColor.b * 0.45f, 1f);
        startColors.highlightedColor = accentColor * 0.7f;
        start.colors = startColors;

        var logOff = NewButton("LogOff", rt, "EXIT", 9f, () => LogOffClicked?.Invoke());
        PlaceRight((RectTransform)logOff.transform, 4f, 40f, taskbarHeight - 8f);
        var logColors = logOff.colors;
        logColors.highlightedColor = errorColor;
        logOff.colors = logColors;

        _trayClock = NewText("Clock", rt, "00:00", 10f, textColor, TextAlignmentOptions.Center);
        PlaceRight(_trayClock.rectTransform, 48f, 38f, taskbarHeight);

        _trayScrap = NewText("Scrap", rt, "SCRAP 0", 9.5f, textColor, TextAlignmentOptions.MidlineRight);
        PlaceRight(_trayScrap.rectTransform, 90f, 96f, taskbarHeight);
    }

    private void BuildToast()
    {
        var rt = NewRect("Toast", _frontRoot);
        Anchor(rt, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-windowMargin - 6f, taskbarHeight + windowMargin + 6f), new Vector2(230f, 40f));
        _toastGroup = rt.gameObject.AddComponent<CanvasGroup>();
        _toastGroup.blocksRaycasts = false;
        _toastGroup.interactable = false;
        _toastGroup.alpha = 0f;

        NewImage("Background", rt, new Color(0.03f, 0.04f, 0.06f, 0.96f), s_rounded).rectTransform.Fill();
        _toastAccent = NewImage("Accent", rt, accentColor);
        Anchor(_toastAccent.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(3f, 0f), new Vector2(3f, -10f));
        _toastIcon = NewText("Icon", rt, "i", 16f, accentColor, TextAlignmentOptions.Center);
        Anchor(_toastIcon.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(18f, 0f));
        _toastIcon.fontStyle = FontStyles.Bold;
        _toastText = NewText("Message", rt, "", 10f, textColor, TextAlignmentOptions.MidlineLeft);
        _toastText.textWrappingMode = TextWrappingModes.Normal;
        Stretch(_toastText.rectTransform, 30f, 8f, 3f, 3f);
    }

    private void BuildSplash()
    {
        _splashText = NewText("Splash", _frontRoot, "", 17f, textColor, TextAlignmentOptions.Center);
        _splashText.textWrappingMode = TextWrappingModes.Normal;
        Anchor(_splashText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, taskbarHeight * 0.5f), new Vector2(_desktopSize.x * 0.8f, 120f));
    }

    private void BuildLockScreen()
    {
        var rt = NewRect("LockScreen", _frontRoot);
        Stretch(rt);
        _lockScreen = rt.gameObject;
        NewImage("Dim", rt, new Color(0f, 0f, 0f, 0.45f)).rectTransform.Fill();

        var brand = NewText("Brand", rt, $"<b>{osName}</b>  <size=70%>{osVersion}</size>", 11f, new Color(textColor.r, textColor.g, textColor.b, 0.7f), TextAlignmentOptions.Center);
        Anchor(brand.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(0f, 20f));

        _lockClock = NewText("Clock", rt, "00:00", 64f, textColor, TextAlignmentOptions.Center);
        Anchor(_lockClock.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 50f), new Vector2(0f, 80f));
        _lockDate = NewText("Date", rt, "", 13f, new Color(textColor.r, textColor.g, textColor.b, 0.8f), TextAlignmentOptions.Center);
        Anchor(_lockDate.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(0f, 20f));

        _lockPrompt = NewText("Prompt", rt, "", 15f, accentColor, TextAlignmentOptions.Center);
        _lockPrompt.textWrappingMode = TextWrappingModes.Normal;
        Anchor(_lockPrompt.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(-40f, 50f));
    }

    private void BuildBootScreen()
    {
        var rt = NewRect("BootScreen", _frontRoot);
        Stretch(rt);
        _bootGroup = rt.gameObject.AddComponent<CanvasGroup>();
        _bootGroup.blocksRaycasts = false;
        NewImage("Black", rt, Color.black).rectTransform.Fill();

        _bootText = NewText("Log", rt, "", 10f, bootTextColor, TextAlignmentOptions.TopLeft);
        Stretch(_bootText.rectTransform, 18f, 18f, 60f, 18f);

        var track = NewImage("ProgressTrack", rt, new Color(bootTextColor.r, bootTextColor.g, bootTextColor.b, 0.15f));
        Anchor(track.rectTransform, new Vector2(0.15f, 0f), new Vector2(0.85f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(0f, 6f));
        var fill = NewImage("Fill", track.rectTransform, bootTextColor);
        _bootProgressFill = fill.rectTransform;
        _bootProgressFill.anchorMin = Vector2.zero;
        _bootProgressFill.anchorMax = new Vector2(0f, 1f);
        _bootProgressFill.offsetMin = _bootProgressFill.offsetMax = Vector2.zero;

        rt.gameObject.SetActive(false);
    }

    private void BuildCrt()
    {
        var rt = NewRect("CRT", _frontRoot);
        Stretch(rt);
        _crt = rt.gameObject;

        var vignette = rt.gameObject.AddComponent<RawImage>();
        vignette.texture = s_vignette;
        vignette.color = new Color(0f, 0f, 0f, vignetteStrength);
        vignette.raycastTarget = false;

        var scan = NewRect("Scanlines", rt);
        Stretch(scan);
        _scanlines = scan.gameObject.AddComponent<RawImage>();
        _scanlines.texture = s_scanlines;
        _scanlines.uvRect = new Rect(0f, 0f, 1f, _desktopSize.y / 3f);
        _scanlines.raycastTarget = false;
        _scanlines.material = null;
        // Scanline darkness comes from the texture alpha; tint carries the strength.
        _scanlines.color = Color.white;
        var scanTint = scan.gameObject.AddComponent<CanvasGroup>();
        scanTint.alpha = Mathf.Clamp01(scanlineStrength / 0.3f);
        scanTint.blocksRaycasts = false;

        _rollingBand = NewRect("RollingBand", rt);
        Anchor(_rollingBand, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 46f));
        var band = _rollingBand.gameObject.AddComponent<RawImage>();
        band.texture = s_band;
        band.color = new Color(1f, 1f, 1f, 0.035f);
        band.raycastTarget = false;
    }

    // =====================================================================
    // UI HELPERS
    // =====================================================================

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    private Image NewImage(string name, Transform parent, Color color, Sprite sprite = null)
    {
        var img = NewRect(name, parent).gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        if (sprite != null)
        {
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 2.5f;
        }
        return img;
    }

    private TMP_Text NewText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align)
    {
        var t = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        if (_font != null) t.font = _font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.richText = true;
        return t;
    }

    private Button NewButton(string name, Transform parent, string label, float fontSize, Action onClick)
    {
        var img = NewImage(name, parent, Color.white, s_rounded);
        img.raycastTarget = true;
        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = ColorBlock.defaultColorBlock;
        colors.normalColor = buttonColor;
        colors.highlightedColor = Color.Lerp(buttonColor, accentColor, 0.45f);
        colors.pressedColor = accentColor;
        colors.selectedColor = buttonColor;
        colors.disabledColor = new Color(buttonColor.r, buttonColor.g, buttonColor.b, 0.4f);
        colors.fadeDuration = 0.06f;
        btn.colors = colors;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        btn.onClick.AddListener(() => onClick?.Invoke());

        if (!string.IsNullOrEmpty(label)) NewText("Label", img.rectTransform, label, fontSize, textColor, TextAlignmentOptions.Center).rectTransform.Fill();
        return btn;
    }

    private static void Stretch(RectTransform rt, float left = 0f, float right = 0f, float bottom = 0f, float top = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    private static void Anchor(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    private static void PlaceLeft(RectTransform rt, float x, float width, float height) =>
        Anchor(rt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(width, height));

    private static void PlaceRight(RectTransform rt, float x, float width, float height) =>
        Anchor(rt, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-x, 0f), new Vector2(width, height));

    // =====================================================================
    // GENERATED TEXTURES
    // =====================================================================

    private static readonly string[] ArrowMask =
    {
        "#",
        "##",
        "#.#",
        "#..#",
        "#...#",
        "#....#",
        "#.....#",
        "#......#",
        "#.......#",
        "#........#",
        "#.........#",
        "#......#####",
        "#...#..#",
        "#..##..#",
        "#.#  #..#",
        "##   #..#",
        "#     #..#",
        "      #..#",
        "       ##",
    };

    private static readonly string[] HandMask =
    {
        "     ##",
        "    #..#",
        "    #..#",
        "    #..#",
        "    #..###",
        "    #..#..###",
        " ## #..#..#..#",
        "#..##........#",
        "#...#........#",
        " #...........#",
        "  #..........#",
        "  #.........#",
        "   #........#",
        "    #......#",
        "    #......#",
        "    ########",
    };

    private static void GenerateAssets()
    {
        if (s_rounded != null) return;

        // Rounded rectangle for 9-slicing.
        const int size = 32, radius = 10;
        var rounded = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
            float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
            byte a = (byte)(Mathf.Clamp01(radius - d + 0.5f) * 255f);
            px[y * size + x] = new Color32(255, 255, 255, a);
        }
        rounded.SetPixels32(px);
        rounded.Apply();
        s_rounded = Sprite.Create(rounded, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(12, 12, 12, 12));

        s_arrow = FromMask(ArrowMask);
        s_hand = FromMask(HandMask);

        // Scanlines: one dark row out of every three.
        s_scanlines = new Texture2D(1, 3, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
        s_scanlines.SetPixels32(new[] { new Color32(0, 0, 0, 255), new Color32(0, 0, 0, 0), new Color32(0, 0, 0, 0) });
        s_scanlines.Apply();

        // Vignette: transparent centre, dark corners.
        const int v = 64;
        s_vignette = new Texture2D(v, v, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var vp = new Color32[v * v];
        for (int y = 0; y < v; y++)
        for (int x = 0; x < v; x++)
        {
            float dx = (x + 0.5f) / v * 2f - 1f, dy = (y + 0.5f) / v * 2f - 1f;
            float d = Mathf.Clamp01((Mathf.Sqrt(dx * dx + dy * dy) - 0.55f) / 0.9f);
            vp[y * v + x] = new Color32(255, 255, 255, (byte)(d * d * 255f));
        }
        s_vignette.SetPixels32(vp);
        s_vignette.Apply();

        // Soft vertical band for the rolling refresh line.
        s_band = new Texture2D(1, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var bp = new Color32[32];
        for (int y = 0; y < 32; y++) bp[y] = new Color32(255, 255, 255, (byte)(Mathf.Sin((y + 0.5f) / 32f * Mathf.PI) * 255f));
        s_band.SetPixels32(bp);
        s_band.Apply();

        // Grid cell with a 1px line on two edges; tiled across the wallpaper.
        s_grid = new Texture2D(32, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
        var gp = new Color32[32 * 32];
        for (int y = 0; y < 32; y++)
        for (int x = 0; x < 32; x++)
            gp[y * 32 + x] = new Color32(255, 255, 255, (byte)(x == 0 || y == 0 ? 255 : 0));
        s_grid.SetPixels32(gp);
        s_grid.Apply();
    }

    private static Texture2D FromMask(string[] mask)
    {
        int w = 0;
        foreach (var row in mask) w = Mathf.Max(w, row.Length);
        int h = mask.Length;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point };
        var px = new Color32[w * h];
        for (int row = 0; row < h; row++)
        for (int x = 0; x < w; x++)
        {
            char c = x < mask[row].Length ? mask[row][x] : ' ';
            var col = c == '#' ? new Color32(10, 10, 14, 255) : c == '.' ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            px[(h - 1 - row) * w + x] = col; // Texture rows go bottom-up.
        }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    private static Texture2D MakeGradient(Color bottom, Color top)
    {
        var tex = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[64];
        for (int y = 0; y < 64; y++) px[y] = Color.Lerp(bottom, top, y / 63f);
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }
}

internal static class PC_RectExtensions
{
    public static void Fill(this RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
