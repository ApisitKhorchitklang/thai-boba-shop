using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Main menu (over a slow fly-around of the street), pause menu, settings and the day-save list.
public class GameMenus : MonoBehaviour
{
    public static GameMenus Instance { get; private set; }
    // False while the main menu is up: customers don't come and the clock doesn't run
    public static bool GameRunning { get; private set; } = true;
    public static bool IsOpen => Instance != null && Instance.AnyOpen || ShopHUD.SummaryOpen || OrderPanel.IsOpen;

    [Header("Panels")]
    public GameObject mainMenu;
    public GameObject pausePanel;
    public GameObject settingsPanel;
    public GameObject loadPanel;
    [Tooltip("HUD objects hidden while the main menu shows")]
    public GameObject[] gameplayUI;

    [Header("Main menu")]
    public Button continueButton;
    public TMP_Text continueLabel;
    public TMP_Text newGameLabel;
    public TMP_Text versionText;

    [Header("Load list")]
    public Transform loadListRoot;
    public GameObject loadRowTemplate;
    public TMP_Text loadEmptyText;

    [Header("Settings")]
    public Slider masterSlider, musicSlider, sfxSlider, mouseSlider;
    public TMP_Text controlsLabel, qualityLabel, fullscreenLabel;
    [Tooltip("Language button labels (settings + main menu)")]
    public TMP_Text languageLabel, mainLanguageLabel;
    public GameObject fullscreenRow;

    [Header("World")]
    public Camera menuCamera;
    public Camera playerCamera;
    public MonoBehaviour[] playerControls;
    public int startingMoney = 100;

    bool AnyOpen => (mainMenu != null && mainMenu.activeSelf) || (pausePanel != null && pausePanel.activeSelf) ||
                    (settingsPanel != null && settingsPanel.activeSelf) || (loadPanel != null && loadPanel.activeSelf);

    GameObject settingsReturn;
    bool confirmNew;

    void Awake()
    {
        Instance = this;
        GameRunning = false;
        Loc.Changed += OnLanguageChanged;
    }

    void OnDestroy()
    {
        Loc.Changed -= OnLanguageChanged;
        if (Instance == this) { Instance = null; GameRunning = true; }
        Time.timeScale = 1f;
    }

    void Start()
    {
        if (loadRowTemplate != null) loadRowTemplate.SetActive(false);
        BindSettings();
        ShowMain();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (OrderPanel.IsOpen) { OrderPanel.Instance.Close(); return; }
            if (settingsPanel.activeSelf || loadPanel.activeSelf) Back();
            else if (GameRunning && !ShopHUD.SummaryOpen) TogglePause();
        }
    }

    // ---------------------------------------------------------------- main menu
    void ShowMain()
    {
        GameRunning = false;
        Time.timeScale = 1f;
        SetWorldMode(menu: true);
        Show(mainMenu);
        confirmNew = false;
        RefreshMainLabels();
    }

    void RefreshMainLabels()
    {
        if (newGameLabel != null) newGameLabel.text = confirmNew ? Loc.T("กดอีกครั้ง: ลบเซฟเดิม", "Press again: delete old save") : Loc.T("เริ่มเกมใหม่", "New game");
        SaveSystem.SaveData latest = SaveSystem.PeekLatest();
        if (continueButton != null) continueButton.interactable = latest != null;
        if (continueLabel != null)
            continueLabel.text = latest != null ? Loc.T($"เล่นต่อ  ·  วันที่ {latest.day}", $"Continue  ·  Day {latest.day}") : Loc.T("เล่นต่อ", "Continue");
        if (mainLanguageLabel != null) mainLanguageLabel.text = Loc.EN ? "ภาษาไทย" : "English";
    }

    // Language: settings button and the quick switch on the main menu
    public void CycleLanguage()
    {
        GameAudio.Play(Sfx.Click);
        Loc.Toggle();
    }

    void OnLanguageChanged()
    {
        RefreshMainLabels();
        RefreshSettingsLabels();
        if (loadPanel != null && loadPanel.activeSelf) OpenLoad();
    }

    public void ContinueButton()
    {
        GameAudio.Play(Sfx.Click);
        if (!SaveSystem.HasSave) return;
        SaveSystem.LoadGame();
        BeginPlay();
    }

    // First click asks for confirmation when a save exists
    public void NewGameButton()
    {
        GameAudio.Play(Sfx.Click);
        if (SaveSystem.HasSave && !confirmNew)
        {
            confirmNew = true;
            if (newGameLabel != null) newGameLabel.text = Loc.T("กดอีกครั้ง: ลบเซฟเดิม", "Press again: delete old save");
            return;
        }
        StartNewGame();
    }

    // Main menu: today's challenge (same customers for everyone today, never saved)
    public void ChallengeButton()
    {
        GameAudio.Play(Sfx.Click);
        StartChallenge(ChallengeMode.TodaySeed);
    }

    public void StartChallenge(int seed)
    {
        ChallengeMode.Begin(seed);
        SaveSystem.ApplyFresh(ChallengeMode.StartMoney, ChallengeMode.StartLevel, 1 + seed % 30);
        BeginPlay();
        TimeCyc clock = FindFirstObjectByType<TimeCyc>();
        if (clock != null) clock.SetTime(ChallengeMode.StartHour, 0);
        int best = ChallengeMode.Best(seed);
        ShopHUD.ToastNow(Loc.T($"ท้าทายรายวัน {ChallengeMode.DateLabel}! ทำคะแนนให้มากที่สุดก่อน 15:00" + (best > 0 ? $" (สถิติวันนี้ {best})" : ""),
                               $"Daily challenge {ChallengeMode.DateLabel}! Score as much as you can before 15:00" + (best > 0 ? $" (today's best {best})" : "")), 4f);
    }

    public void StartNewGame()
    {
        ChallengeMode.End();
        SaveSystem.StartNewGame(startingMoney);
        BeginPlay();
    }

    public void QuitButton()
    {
        GameAudio.Play(Sfx.Click);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void BeginPlay()
    {
        HideAll();
        SetWorldMode(menu: false);
        GameRunning = true;
        Time.timeScale = 1f;
        GameAudio.Play(Sfx.Start);
    }

    // ---------------------------------------------------------------- pause
    public void TogglePause()
    {
        if (!GameRunning) return;
        bool open = !pausePanel.activeSelf;
        GameAudio.Play(open ? Sfx.Open : Sfx.Close);
        if (open) Show(pausePanel); else HideAll();
        Time.timeScale = open ? 0f : 1f;
    }

    public void ResumeButton()
    {
        if (pausePanel.activeSelf) TogglePause();
    }

    // Pause menu: play the first-day tutorial again
    public void ReplayTutorialButton()
    {
        if (TutorialSystem.Instance != null) TutorialSystem.Instance.Replay();
        ResumeButton();
    }

    public void SaveButton()
    {
        SaveSystem.SaveGame();
        GameAudio.Play(Sfx.Confirm);
        ShopHUD.Toast(Loc.T("บันทึกเกมแล้ว (โหลดแล้วจะเริ่มต้นวันนี้ใหม่)", "Game saved (loading restarts this day)"));
    }

    public void MainMenuButton()
    {
        ChallengeMode.End();
        GameAudio.Play(Sfx.Click);
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex >= 0
            ? SceneManager.GetActiveScene().name : SceneManager.GetActiveScene().path);
    }

    // ---------------------------------------------------------------- load list
    public void OpenLoad()
    {
        GameAudio.Play(Sfx.Open);
        settingsReturn = mainMenu.activeSelf ? mainMenu : pausePanel;
        Show(loadPanel);
        foreach (Transform t in loadListRoot)
            if (t.gameObject != loadRowTemplate) Destroy(t.gameObject);
        List<SaveSystem.SaveInfo> saves = SaveSystem.ListDaySaves();
        if (loadEmptyText != null) loadEmptyText.gameObject.SetActive(saves.Count == 0);
        int shown = 0;
        foreach (SaveSystem.SaveInfo s in saves)
        {
            if (shown++ >= 8) break;
            GameObject row = Instantiate(loadRowTemplate, loadListRoot);
            row.SetActive(true);
            TMP_Text label = row.GetComponentInChildren<TMP_Text>();
            label.text = Loc.T($"วันที่ {s.data.day}   ·   {s.data.money:N0} ฿   ·   ชื่อเสียง {s.data.reputation:0.0}   ·   เลเวล {s.data.level}\n" +
                         $"<size=70%>บันทึกเมื่อ {s.data.savedAt}</size>",
                         $"Day {s.data.day}   ·   {s.data.money:N0} ฿   ·   Reputation {s.data.reputation:0.0}   ·   Level {s.data.level}\n" +
                         $"<size=70%>Saved {s.data.savedAt}</size>");
            string path = s.path;
            row.GetComponent<Button>().onClick.AddListener(() =>
            {
                GameAudio.Play(Sfx.Confirm);
                SaveSystem.LoadGame(path);
                if (GameRunning) { HideAll(); Time.timeScale = 1f; }
                else BeginPlay();
            });
        }
    }

    // ---------------------------------------------------------------- settings
    public void OpenSettings()
    {
        GameAudio.Play(Sfx.Open);
        settingsReturn = mainMenu.activeSelf ? mainMenu : pausePanel;
        Show(settingsPanel);
        RefreshSettingsLabels();
    }

    public void Back()
    {
        GameAudio.Play(Sfx.Close);
        if (settingsReturn != null) Show(settingsReturn);
        else HideAll();
    }

    void BindSettings()
    {
        Bind(masterSlider, GameSettings.Master, v => GameSettings.Master = v);
        Bind(musicSlider, GameSettings.Music, v => GameSettings.Music = v);
        Bind(sfxSlider, GameSettings.Sfx, v => GameSettings.Sfx = v);
        Bind(mouseSlider, GameSettings.MouseSensitivity, v => GameSettings.MouseSensitivity = v);
        if (fullscreenRow != null) fullscreenRow.SetActive(!Application.isMobilePlatform);
    }

    static void Bind(Slider s, float value, System.Action<float> set)
    {
        if (s == null) return;
        s.SetValueWithoutNotify(value);
        s.onValueChanged.AddListener(v => set(v));
    }

    public void CycleControls()
    {
        GameAudio.Play(Sfx.Click);
        GameSettings.Controls = (GameSettings.ControlMode)(((int)GameSettings.Controls + 1) % 3);
        RefreshSettingsLabels();
    }

    public void CycleQuality()
    {
        GameAudio.Play(Sfx.Click);
        GameSettings.Quality = (GameSettings.Quality + 1) % 3;
        RefreshSettingsLabels();
    }

    public void ToggleFullscreen()
    {
        GameAudio.Play(Sfx.Click);
        GameSettings.Fullscreen = !GameSettings.Fullscreen;
        RefreshSettingsLabels();
    }

    void RefreshSettingsLabels()
    {
        if (controlsLabel != null) controlsLabel.text = Loc.T("การควบคุม: ", "Controls: ") + GameSettings.ControlsLabel;
        if (qualityLabel != null) qualityLabel.text = Loc.T("กราฟิก: ", "Graphics: ") + GameSettings.QualityLabel;
        if (fullscreenLabel != null) fullscreenLabel.text = GameSettings.Fullscreen ? Loc.T("เต็มจอ: เปิด", "Fullscreen: on") : Loc.T("เต็มจอ: ปิด", "Fullscreen: off");
        if (languageLabel != null) languageLabel.text = Loc.T("ภาษา: ไทย", "Language: English");
    }

    // ---------------------------------------------------------------- helpers
    void Show(GameObject panel)
    {
        foreach (GameObject p in new[] { mainMenu, pausePanel, settingsPanel, loadPanel })
            if (p != null) p.SetActive(p == panel);
    }

    void HideAll()
    {
        Show(null);
    }

    void SetWorldMode(bool menu)
    {
        if (gameplayUI != null)
            foreach (GameObject g in gameplayUI) if (g != null) g.SetActive(!menu);
        if (playerControls != null)
            foreach (MonoBehaviour b in playerControls) if (b != null) b.enabled = !menu;
        if (menuCamera != null)
        {
            menuCamera.gameObject.SetActive(menu);
            if (playerCamera != null)
            {
                playerCamera.enabled = !menu;
                AudioListener l = playerCamera.GetComponent<AudioListener>();
                if (l != null) l.enabled = !menu;
            }
        }
    }
}
