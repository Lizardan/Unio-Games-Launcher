using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Collections.Generic;
using Debug = UnityEngine.Debug;

[System.Serializable]
public class GameInfo
{
    public string gameName;      // "Kindoms of King"
    public string githubRepo;    // "Lizardan/Kindoms-of-King"
}

public class GameLauncher : MonoBehaviour
{
    [Header("=== ИГРЫ ===")]
    [SerializeField] private List<GameInfo> games = new List<GameInfo>();

    [Header("=== НАСТРОЙКИ ЛАУНЧЕРА ===")]
    [SerializeField] private string launcherGithubUsername = "Lizardan";
    [SerializeField] private string launcherGithubRepo = "Unio-Games-Launcher";

    // UI элементы
    private Text titleText;
    private Text versionText;
    private Text launcherVersionText;
    private Button launcherUpdateButton;
    private GameObject launcherUpdateButtonGO;

    // Элементы кнопки обновления
    private Image launcherButtonBg;               // фон кнопки (синий/серый)
    private Text launcherButtonMainText;          // "Update to v..."
    private GameObject launcherProgressContainer; // контейнер прогресса (зелёная полоса + текст)
    private Image launcherProgressFill;           // зелёная полоска (filled)
    private Text launcherProgressText;            // проценты на кнопке

    private Text statusText;
    private Button actionButton;
    private Text buttonText;
    private Slider progressSlider;
    private Text progressText;
    private GameObject progressPanel;

    // Данные по играм
    private int selectedGameIndex = 0;
    private Dictionary<int, string> localVersions = new Dictionary<int, string>();
    private Dictionary<int, string> remoteVersions = new Dictionary<int, string>();
    private Dictionary<int, bool> gameInstalled = new Dictionary<int, bool>();

    // Левая панель со списком игр
    private Transform gameListContent;
    private List<GameObject> gameButtons = new List<GameObject>();
    private GameObject comingSoonCard;

    private string RootPath => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    private string GamesFolder => Path.Combine(RootPath, "Games");

    private string LAUNCHER_VERSION_API_URL => $"https://api.github.com/repos/{launcherGithubUsername}/{launcherGithubRepo}/releases/latest";
    private string LAUNCHER_DOWNLOAD_URL => $"https://github.com/{launcherGithubUsername}/{launcherGithubRepo}/releases/latest/download/Launcher.zip";

    void Start()
    {
        CleanupTempFolders();
        CreateUI();
        LoadAllLocalData();
        StartCoroutine(CheckAllRemoteVersions());
        StartCoroutine(CheckLauncherUpdate());
    }

    void CleanupTempFolders()
    {
        try
        {
            string tempPath = Application.temporaryCachePath;
            if (Directory.Exists(tempPath))
                Directory.Delete(tempPath, true);
            string parent = Path.GetDirectoryName(tempPath);
            if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
            {
                if (Directory.GetFiles(parent).Length == 0 && Directory.GetDirectories(parent).Length == 0)
                    Directory.Delete(parent);
            }
        }
        catch { }
    }

    void CreateUI()
    {
        // EventSystem
        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
        eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        // Canvas
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960, 540);
        gameObject.AddComponent<GraphicRaycaster>();

        // Фоновая панель
        GameObject panel = new GameObject("Panel");
        panel.transform.SetParent(transform, false);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.08f, 0.08f, 0.12f);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;

        // ====== ЛЕВАЯ ПАНЕЛЬ (1/8 ширины) ======
        GameObject leftArea = new GameObject("LeftArea");
        leftArea.transform.SetParent(panel.transform, false);
        RectTransform leftRect = leftArea.AddComponent<RectTransform>();
        leftRect.anchorMin = new Vector2(0, 0);
        leftRect.anchorMax = new Vector2(0.125f, 1);
        leftRect.offsetMin = Vector2.zero;
        leftRect.offsetMax = Vector2.zero;
        Image leftBg = leftArea.AddComponent<Image>();
        leftBg.color = new Color(0.12f, 0.12f, 0.18f);

        // Заголовок "ИГРЫ"
        GameObject gamesTitle = new GameObject("GamesTitle");
        gamesTitle.transform.SetParent(leftArea.transform, false);
        Text gamesTitleText = gamesTitle.AddComponent<Text>();
        gamesTitleText.text = "ИГРЫ";
        gamesTitleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        gamesTitleText.fontSize = 12;
        gamesTitleText.fontStyle = FontStyle.Bold;
        gamesTitleText.color = new Color(0.9f, 0.9f, 1f);
        gamesTitleText.alignment = TextAnchor.MiddleCenter;
        RectTransform gtRect = gamesTitle.GetComponent<RectTransform>();
        gtRect.anchorMin = new Vector2(0, 1);
        gtRect.anchorMax = new Vector2(1, 1);
        gtRect.pivot = new Vector2(0.5f, 1);
        gtRect.sizeDelta = new Vector2(0, 24);
        gtRect.anchoredPosition = new Vector2(0, -8);

        // Контейнер списка игр
        GameObject listContainer = new GameObject("GameListContainer");
        listContainer.transform.SetParent(leftArea.transform, false);
        RectTransform lcRect = listContainer.AddComponent<RectTransform>();
        lcRect.anchorMin = new Vector2(0, 0);
        lcRect.anchorMax = new Vector2(1, 1);
        lcRect.offsetMin = new Vector2(3, 3);
        lcRect.offsetMax = new Vector2(-3, -35);
        VerticalLayoutGroup listLayout = listContainer.AddComponent<VerticalLayoutGroup>();
        listLayout.spacing = 4;
        listLayout.childAlignment = TextAnchor.UpperCenter;
        listLayout.childForceExpandWidth = true;
        listLayout.childForceExpandHeight = false;
        gameListContent = listContainer.transform;

        // ====== ПРАВАЯ ПАНЕЛЬ (7/8 ширины) ======
        GameObject rightArea = new GameObject("RightArea");
        rightArea.transform.SetParent(panel.transform, false);
        RectTransform rightRect = rightArea.AddComponent<RectTransform>();
        rightRect.anchorMin = new Vector2(0.125f, 0);
        rightRect.anchorMax = new Vector2(1, 1);
        rightRect.offsetMin = Vector2.zero;
        rightRect.offsetMax = Vector2.zero;

        // --- Заголовок (название игры) ---
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(rightArea.transform, false);
        titleText = titleObj.AddComponent<Text>();
        titleText.text = "Kindoms of King";
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 42;
        titleText.color = new Color(0.9f, 0.9f, 1f);
        titleText.alignment = TextAnchor.MiddleCenter;
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0, -40);
        titleRect.sizeDelta = new Vector2(400, 60);

        // --- Версия игры ---
        GameObject versionObj = new GameObject("GameVersion");
        versionObj.transform.SetParent(rightArea.transform, false);
        versionText = versionObj.AddComponent<Text>();
        versionText.text = "";
        versionText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        versionText.fontSize = 18;
        versionText.color = new Color(0.6f, 0.6f, 0.7f);
        versionText.alignment = TextAnchor.MiddleCenter;
        RectTransform versionRect = versionObj.GetComponent<RectTransform>();
        versionRect.anchorMin = new Vector2(0.5f, 1f);
        versionRect.anchorMax = new Vector2(0.5f, 1f);
        versionRect.pivot = new Vector2(0.5f, 1f);
        versionRect.anchoredPosition = new Vector2(0, -95);
        versionRect.sizeDelta = new Vector2(300, 30);

        // --- Нижняя строка: лаунчер + кнопка обновления ---
        GameObject bottomRow = new GameObject("BottomRow");
        bottomRow.transform.SetParent(rightArea.transform, false);
        RectTransform bottomRowRect = bottomRow.AddComponent<RectTransform>();
        bottomRowRect.anchorMin = new Vector2(0, 0);
        bottomRowRect.anchorMax = new Vector2(1, 0);
        bottomRowRect.pivot = new Vector2(0, 0);
        bottomRowRect.anchoredPosition = new Vector2(10, 10);
        bottomRowRect.sizeDelta = new Vector2(-20, 24);
        HorizontalLayoutGroup bottomLayout = bottomRow.AddComponent<HorizontalLayoutGroup>();
        bottomLayout.childControlWidth = false;
        bottomLayout.childControlHeight = false;
        bottomLayout.spacing = 8;
        bottomLayout.childAlignment = TextAnchor.MiddleLeft;
        bottomLayout.childForceExpandWidth = false;
        bottomLayout.childForceExpandHeight = false;

        // Текст "Unio Games Launcher vX.Y.Z"
        GameObject launcherTextGO = new GameObject("LauncherVersionText");
        launcherTextGO.transform.SetParent(bottomRow.transform, false);
        launcherVersionText = launcherTextGO.AddComponent<Text>();
        launcherVersionText.text = $"Unio Games Launcher v{Application.version}";
        launcherVersionText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        launcherVersionText.fontSize = 14;
        launcherVersionText.color = new Color(0.5f, 0.5f, 0.6f);
        launcherVersionText.alignment = TextAnchor.MiddleLeft;
        RectTransform lvtRect = launcherTextGO.GetComponent<RectTransform>();
        lvtRect.sizeDelta = new Vector2(190, 24);

        // Кнопка обновления лаунчера (100x20, изначально скрыта)
        launcherUpdateButtonGO = new GameObject("LauncherUpdateButton");
        launcherUpdateButtonGO.transform.SetParent(bottomRow.transform, false);
        RectTransform lubRect = launcherUpdateButtonGO.AddComponent<RectTransform>();
        lubRect.sizeDelta = new Vector2(100, 20);
        LayoutElement luLayout = launcherUpdateButtonGO.AddComponent<LayoutElement>();
        luLayout.minWidth = 100;
        luLayout.preferredWidth = 100;
        luLayout.minHeight = 20;
        luLayout.preferredHeight = 20;

        // Фон кнопки (меняется с синего на серый при загрузке)
        launcherButtonBg = launcherUpdateButtonGO.AddComponent<Image>();
        launcherButtonBg.color = new Color(0.2f, 0.5f, 0.9f);
        launcherUpdateButton = launcherUpdateButtonGO.AddComponent<Button>();

        // Текст "Update to v..."
        GameObject mainTextGO = new GameObject("MainText");
        mainTextGO.transform.SetParent(launcherUpdateButtonGO.transform, false);
        launcherButtonMainText = mainTextGO.AddComponent<Text>();
        launcherButtonMainText.text = "Update";
        launcherButtonMainText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        launcherButtonMainText.fontSize = 12;
        launcherButtonMainText.color = Color.white;
        launcherButtonMainText.alignment = TextAnchor.MiddleCenter;
        RectTransform mainTextRect = mainTextGO.GetComponent<RectTransform>();
        mainTextRect.anchorMin = Vector2.zero;
        mainTextRect.anchorMax = Vector2.one;
        mainTextRect.sizeDelta = Vector2.zero;

        // Контейнер прогресса (зелёная полоса + проценты) – без серого фона
        launcherProgressContainer = new GameObject("ProgressContainer");
        launcherProgressContainer.transform.SetParent(launcherUpdateButtonGO.transform, false);
        RectTransform pcRect = launcherProgressContainer.AddComponent<RectTransform>();
        pcRect.anchorMin = Vector2.zero;
        pcRect.anchorMax = Vector2.one;
        pcRect.sizeDelta = Vector2.zero;

        // Зелёная полоска (ширина управляется через anchorMax)
        GameObject greenFill = new GameObject("GreenFill");
        greenFill.transform.SetParent(launcherProgressContainer.transform, false);
        RectTransform gfRect = greenFill.AddComponent<RectTransform>();
        gfRect.anchorMin = new Vector2(0, 0);
        gfRect.anchorMax = new Vector2(0, 1);       // начнём с нулевой ширины
        gfRect.pivot = new Vector2(0, 0.5f);
        gfRect.sizeDelta = Vector2.zero;
        launcherProgressFill = greenFill.AddComponent<Image>();
        launcherProgressFill.color = new Color(0.3f, 0.8f, 0.3f);
        // Filled больше не используется

        // Текст процентов
        GameObject percentTextGO = new GameObject("PercentText");
        percentTextGO.transform.SetParent(launcherProgressContainer.transform, false);
        launcherProgressText = percentTextGO.AddComponent<Text>();
        launcherProgressText.text = "0%";
        launcherProgressText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        launcherProgressText.fontSize = 12;
        launcherProgressText.color = Color.white;
        launcherProgressText.alignment = TextAnchor.MiddleCenter;
        RectTransform ptRect = percentTextGO.GetComponent<RectTransform>();
        ptRect.anchorMin = Vector2.zero;
        ptRect.anchorMax = Vector2.one;
        ptRect.sizeDelta = Vector2.zero;

        // По умолчанию всё скрыто
        launcherProgressContainer.SetActive(false);
        launcherUpdateButtonGO.SetActive(false);

        launcherUpdateButton.onClick.AddListener(() => StartCoroutine(DownloadLauncherUpdate()));

        // --- Статусное сообщение ---
        GameObject statusObj = new GameObject("Status");
        statusObj.transform.SetParent(rightArea.transform, false);
        statusText = statusObj.AddComponent<Text>();
        statusText.text = "Проверка...";
        statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        statusText.fontSize = 22;
        statusText.color = Color.white;
        statusText.alignment = TextAnchor.MiddleCenter;
        RectTransform statusRect = statusObj.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0.5f, 0.5f);
        statusRect.anchorMax = new Vector2(0.5f, 0.5f);
        statusRect.pivot = new Vector2(0.5f, 0.5f);
        statusRect.anchoredPosition = new Vector2(0, 60);
        statusRect.sizeDelta = new Vector2(400, 60);

        // --- Кнопка действия ---
        GameObject buttonObj = new GameObject("ActionButton");
        buttonObj.transform.SetParent(rightArea.transform, false);
        Image buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.color = new Color(0.2f, 0.6f, 1f);
        actionButton = buttonObj.AddComponent<Button>();
        RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(0, -10);
        buttonRect.sizeDelta = new Vector2(220, 50);

        GameObject buttonTextObj = new GameObject("ButtonText");
        buttonTextObj.transform.SetParent(buttonObj.transform, false);
        buttonText = buttonTextObj.AddComponent<Text>();
        buttonText.text = "Загрузка...";
        buttonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        buttonText.fontSize = 18;
        buttonText.color = Color.white;
        buttonText.alignment = TextAnchor.MiddleCenter;
        RectTransform buttonTextRect = buttonTextObj.GetComponent<RectTransform>();
        buttonTextRect.anchorMin = Vector2.zero;
        buttonTextRect.anchorMax = Vector2.one;
        buttonTextRect.sizeDelta = Vector2.zero;

        // --- Панель прогресса ---
        progressPanel = new GameObject("ProgressPanel");
        progressPanel.transform.SetParent(rightArea.transform, false);
        RectTransform ppRect = progressPanel.AddComponent<RectTransform>();
        ppRect.anchorMin = new Vector2(0.5f, 0.5f);
        ppRect.anchorMax = new Vector2(0.5f, 0.5f);
        ppRect.pivot = new Vector2(0.5f, 0.5f);
        ppRect.anchoredPosition = new Vector2(0, -70);
        ppRect.sizeDelta = new Vector2(400, 50);

        GameObject progressBarObj = new GameObject("ProgressBar");
        progressBarObj.transform.SetParent(progressPanel.transform, false);
        RectTransform barRect = progressBarObj.AddComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0, 1);
        barRect.anchorMax = new Vector2(1, 1);
        barRect.pivot = new Vector2(0.5f, 1);
        barRect.anchoredPosition = Vector2.zero;
        barRect.sizeDelta = new Vector2(0, 30);

        Image barBg = progressBarObj.AddComponent<Image>();
        barBg.color = new Color(0.2f, 0.2f, 0.25f);

        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(progressBarObj.transform, false);
        Image fillImg = fillObj.AddComponent<Image>();
        fillImg.color = new Color(0.3f, 0.8f, 0.3f);
        RectTransform fillRect2 = fillObj.GetComponent<RectTransform>();
        fillRect2.anchorMin = new Vector2(0, 0);
        fillRect2.anchorMax = new Vector2(0, 1);
        fillRect2.pivot = new Vector2(0, 0.5f);
        fillRect2.sizeDelta = new Vector2(0, 0);

        progressSlider = progressBarObj.AddComponent<Slider>();
        progressSlider.fillRect = fillRect2;
        progressSlider.targetGraphic = fillImg;
        progressSlider.minValue = 0;
        progressSlider.maxValue = 1;
        progressSlider.value = 0;
        progressSlider.interactable = false;

        GameObject progressTextObj = new GameObject("ProgressText");
        progressTextObj.transform.SetParent(progressPanel.transform, false);
        progressText = progressTextObj.AddComponent<Text>();
        progressText.text = "0%";
        progressText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        progressText.fontSize = 14;
        progressText.color = Color.white;
        progressText.alignment = TextAnchor.MiddleCenter;
        RectTransform ptRect2 = progressTextObj.GetComponent<RectTransform>();
        ptRect2.anchorMin = new Vector2(0, 0);
        ptRect2.anchorMax = new Vector2(1, 0);
        ptRect2.pivot = new Vector2(0.5f, 0);
        ptRect2.anchoredPosition = new Vector2(0, 2);
        ptRect2.sizeDelta = new Vector2(0, 18);

        progressPanel.SetActive(false);
    }

    void LoadAllLocalData()
    {
        for (int i = 0; i < games.Count; i++)
        {
            string gameFolder = Path.Combine(GamesFolder, games[i].gameName);
            string vPath = Path.Combine(gameFolder, $"{games[i].gameName}_version.txt");
            if (File.Exists(vPath))
                localVersions[i] = File.ReadAllText(vPath).Trim();

            gameInstalled[i] = Directory.Exists(gameFolder);
        }
        RefreshGameList();
        if (games.Count > 0)
            SelectGame(0);
    }

    IEnumerator CheckAllRemoteVersions()
    {
        for (int i = 0; i < games.Count; i++)
        {
            yield return StartCoroutine(CheckRemoteVersion(i));
        }
        RefreshGameList();
        if (games.Count > 0)
            UpdateUIState();
    }

    IEnumerator CheckRemoteVersion(int index)
    {
        string url = $"https://api.github.com/repos/{games[index].githubRepo}/releases/latest";
        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            www.SetRequestHeader("Accept", "application/vnd.github.v3+json");
            yield return www.SendWebRequest();
            if (www.result == UnityWebRequest.Result.Success)
            {
                remoteVersions[index] = ExtractTagName(www.downloadHandler.text);
            }
        }
    }

    string ExtractTagName(string json)
    {
        string key = "\"tag_name\"";
        int keyIndex = json.IndexOf(key);
        if (keyIndex == -1) return null;
        int colonIndex = json.IndexOf(':', keyIndex + key.Length);
        int startQuote = json.IndexOf('"', colonIndex + 1);
        int endQuote = json.IndexOf('"', startQuote + 1);
        return json.Substring(startQuote + 1, endQuote - startQuote - 1);
    }

    // Возвращает массив [major, minor, patch] или null
    int[] ParseVersion(string version)
    {
        if (string.IsNullOrEmpty(version)) return null;
        version = version.TrimStart('v'); // убираем 'v' если есть
        string[] parts = version.Split('.');
        if (parts.Length != 3) return null;
        int[] nums = new int[3];
        if (int.TryParse(parts[0], out nums[0]) &&
            int.TryParse(parts[1], out nums[1]) &&
            int.TryParse(parts[2], out nums[2]))
            return nums;
        return null;
    }

    void RefreshGameList()
    {
        foreach (var btn in gameButtons) Destroy(btn);
        gameButtons.Clear();
        if (comingSoonCard) Destroy(comingSoonCard);
        if (gameListContent == null) return;

        for (int i = 0; i < games.Count; i++)
        {
            int index = i;
            GameObject card = new GameObject($"GameCard_{i}");
            card.transform.SetParent(gameListContent, false);
            RectTransform cardRect = card.AddComponent<RectTransform>();
            cardRect.sizeDelta = new Vector2(0, 36);
            LayoutElement layoutElement = card.AddComponent<LayoutElement>();
            layoutElement.minHeight = 36;
            layoutElement.preferredHeight = 36;

            Image cardBg = card.AddComponent<Image>();
            cardBg.color = (index == selectedGameIndex) ? new Color(0.2f, 0.4f, 0.7f) : new Color(0.15f, 0.15f, 0.2f);
            Button cardBtn = card.AddComponent<Button>();
            cardBtn.onClick.AddListener(() => SelectGame(index));

            GameObject cardText = new GameObject("Text");
            cardText.transform.SetParent(card.transform, false);
            Text txt = cardText.AddComponent<Text>();
            txt.text = games[i].gameName;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 12;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            RectTransform txtRect = cardText.GetComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.sizeDelta = Vector2.zero;

            gameButtons.Add(card);
        }

        // Заглушка "Coming Soon..."
        comingSoonCard = new GameObject("ComingSoon");
        comingSoonCard.transform.SetParent(gameListContent, false);
        RectTransform csRect = comingSoonCard.AddComponent<RectTransform>();
        csRect.sizeDelta = new Vector2(0, 36);
        LayoutElement csLayout = comingSoonCard.AddComponent<LayoutElement>();
        csLayout.minHeight = 36;
        csLayout.preferredHeight = 36;
        Image csBg = comingSoonCard.AddComponent<Image>();
        csBg.color = new Color(0.1f, 0.1f, 0.15f);

        GameObject csText = new GameObject("Text");
        csText.transform.SetParent(comingSoonCard.transform, false);
        Text csTxt = csText.AddComponent<Text>();
        csTxt.text = "Coming\nSoon";
        csTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        csTxt.fontSize = 11;
        csTxt.fontStyle = FontStyle.Italic;
        csTxt.color = new Color(0.4f, 0.4f, 0.5f);
        csTxt.alignment = TextAnchor.MiddleCenter;
        RectTransform csTxtRect = csText.GetComponent<RectTransform>();
        csTxtRect.anchorMin = Vector2.zero;
        csTxtRect.anchorMax = Vector2.one;
        csTxtRect.sizeDelta = Vector2.zero;
    }

    void SelectGame(int index)
    {
        if (index < 0 || index >= games.Count) return;
        selectedGameIndex = index;
        RefreshGameList();
        UpdateUIState();
    }

    void UpdateUIState()
    {
        if (games.Count == 0)
        {
            titleText.text = "Нет игр";
            versionText.text = "";
            statusText.text = "Добавьте игры в инспекторе";
            buttonText.text = "...";
            return;
        }

        GameInfo game = games[selectedGameIndex];
        titleText.text = game.gameName;

        bool installed = gameInstalled.ContainsKey(selectedGameIndex) && gameInstalled[selectedGameIndex];
        string locVer = localVersions.ContainsKey(selectedGameIndex) ? localVersions[selectedGameIndex] : null;
        string remVer = remoteVersions.ContainsKey(selectedGameIndex) ? remoteVersions[selectedGameIndex] : null;

        if (installed && !string.IsNullOrEmpty(locVer))
            versionText.text = $"v{locVer.TrimStart('v')}";
        else
            versionText.text = "";

        if (!installed)
        {
            statusText.text = "Игра не установлена";
            buttonText.text = "Скачать игру";
            actionButton.onClick.RemoveAllListeners();
            actionButton.onClick.AddListener(() => StartCoroutine(DownloadGame(selectedGameIndex)));
        }
        else if (string.IsNullOrEmpty(remVer))
        {
            statusText.text = "Не удалось проверить обновления";
            buttonText.text = "Запустить игру";
            actionButton.onClick.RemoveAllListeners();
            actionButton.onClick.AddListener(() => LaunchGame(selectedGameIndex));
        }
        else if (locVer == remVer)
        {
            statusText.text = "Игра актуальна";
            buttonText.text = "Запустить игру";
            actionButton.onClick.RemoveAllListeners();
            actionButton.onClick.AddListener(() => LaunchGame(selectedGameIndex));
        }
        else
        {
            statusText.text = $"Доступна новая версия: {remVer.TrimStart('v')}";
            buttonText.text = "Обновить игру";
            actionButton.onClick.RemoveAllListeners();
            actionButton.onClick.AddListener(() => StartCoroutine(DownloadGame(selectedGameIndex)));
        }
    }

    IEnumerator DownloadGame(int index)
    {
        progressPanel.SetActive(true);
        actionButton.interactable = false;
        GameInfo game = games[index];
        string url = $"https://github.com/{game.githubRepo}/releases/latest/download/{game.gameName.Replace(" ", "")}.zip";
        string savePath = Path.Combine(RootPath, $"{game.gameName.Replace(" ", "")}.zip");

        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            DownloadHandlerFile fileHandler = new DownloadHandlerFile(savePath);
            fileHandler.removeFileOnAbort = true;
            www.downloadHandler = fileHandler;

            var operation = www.SendWebRequest();
            while (!operation.isDone)
            {
                progressSlider.value = www.downloadProgress;
                progressText.text = $"{Mathf.Round(www.downloadProgress * 100)}%";
                buttonText.text = "Загрузка...";
                yield return null;
            }

            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("[Launcher] Download completed.");
                ProcessDownloadedFile(index, savePath);
            }
            else
            {
                Debug.LogError($"[Launcher] Download error: {www.error}");
                statusText.text = "Ошибка при загрузке";
                buttonText.text = "Повторить";
                actionButton.interactable = true;
                progressPanel.SetActive(false);
            }
        }
    }

    bool ProcessDownloadedFile(int index, string filePath)
    {
        buttonText.text = "Установка...";
        FileInfo fi = new FileInfo(filePath);
        if (fi.Length == 0)
        {
            statusText.text = "Скачанный файл пуст";
            buttonText.text = "Повторить";
            actionButton.interactable = true;
            progressPanel.SetActive(false);
            return false;
        }

        using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
        {
            byte[] header = new byte[2];
            fs.Read(header, 0, 2);
            if (header[0] != 0x50 || header[1] != 0x4B)
            {
                statusText.text = "Файл повреждён";
                buttonText.text = "Повторить";
                actionButton.interactable = true;
                progressPanel.SetActive(false);
                return false;
            }
        }

        if (ExtractGame(index, filePath))
        {
            string gameFolder = Path.Combine(GamesFolder, games[index].gameName);
            File.WriteAllText(Path.Combine(gameFolder, $"{games[index].gameName}_version.txt"), remoteVersions[index]);
            File.Delete(filePath);
            gameInstalled[index] = true;
            localVersions[index] = remoteVersions[index];

            progressPanel.SetActive(false);
            actionButton.interactable = true;
            UpdateUIState();
            return true;
        }
        else
        {
            statusText.text = "Ошибка при распаковке";
            buttonText.text = "Повторить";
            actionButton.interactable = true;
            progressPanel.SetActive(false);
            return false;
        }
    }

    bool ExtractGame(int index, string zipPath)
    {
        try
        {
            string extractPath = Path.Combine(GamesFolder, games[index].gameName);
            if (Directory.Exists(extractPath))
                Directory.Delete(extractPath, true);
            Directory.CreateDirectory(extractPath);
            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extractPath);
            Debug.Log("[Launcher] Extraction successful.");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError("Extract error: " + e.Message);
            return false;
        }
    }

    void LaunchGame(int index)
    {
        string gamePath = Path.Combine(GamesFolder, games[index].gameName, games[index].gameName + ".exe");
        if (File.Exists(gamePath))
        {
            try
            {
                Process.Start(gamePath);
                Application.Quit();
            }
            catch (System.Exception e)
            {
                statusText.text = "Ошибка запуска: " + e.Message;
            }
        }
        else
        {
            statusText.text = "Исполняемый файл не найден";
            buttonText.text = "Скачать заново";
            actionButton.onClick.RemoveAllListeners();
            actionButton.onClick.AddListener(() => StartCoroutine(DownloadGame(index)));
        }
    }

    // ======== ОБНОВЛЕНИЕ ЛАУНЧЕРА ========
    IEnumerator CheckLauncherUpdate()
    {
        using (UnityWebRequest www = UnityWebRequest.Get(LAUNCHER_VERSION_API_URL))
        {
            www.SetRequestHeader("Accept", "application/vnd.github.v3+json");
            yield return www.SendWebRequest();
            if (www.result == UnityWebRequest.Result.Success)
            {
                string json = www.downloadHandler.text;
                string remoteLauncherVersion = ExtractTagName(json);
                if (remoteLauncherVersion != null)
                {
                    // Сравниваем версии как числа
                    int[] localVer = ParseVersion(Application.version);
                    int[] remoteVer = ParseVersion(remoteLauncherVersion);

                    bool updateAvailable = false;
                    if (localVer != null && remoteVer != null)
                    {
                        if (remoteVer[0] > localVer[0])
                            updateAvailable = true;
                        else if (remoteVer[0] == localVer[0] && remoteVer[1] > localVer[1])
                            updateAvailable = true;
                        else if (remoteVer[0] == localVer[0] && remoteVer[1] == localVer[1] && remoteVer[2] > localVer[2])
                            updateAvailable = true;
                    }

                    if (updateAvailable)
                    {
                        launcherButtonBg.enabled = true;
                        launcherButtonBg.color = new Color(0.2f, 0.5f, 0.9f); // синий
                        launcherButtonMainText.gameObject.SetActive(true);
                        launcherButtonMainText.text = $"Update to {remoteLauncherVersion}";
                        launcherProgressContainer.SetActive(false);
                        launcherUpdateButton.interactable = true;
                        launcherUpdateButtonGO.SetActive(true);
                    }
                }
            }
        }
    }

    IEnumerator DownloadLauncherUpdate()
    {
        launcherButtonBg.enabled = true;
        launcherButtonBg.color = new Color(0.2f, 0.2f, 0.25f); // серый, как у игры
        launcherButtonMainText.gameObject.SetActive(false);
        launcherProgressContainer.SetActive(true);
        launcherProgressFill.rectTransform.anchorMax = new Vector2(0, 1);
        launcherProgressFill.transform.SetAsLastSibling();
        launcherProgressText.gameObject.SetActive(true);
        launcherProgressText.transform.SetAsLastSibling();
        launcherProgressText.text = "0%";
        launcherUpdateButton.interactable = false;

        string tempDir = Path.Combine(Application.temporaryCachePath, "LauncherUpdate");
        string savePath = Path.Combine(tempDir, "Launcher.zip");
        string extractDir = Path.Combine(tempDir, "NewLauncher");

        Directory.CreateDirectory(tempDir);

        Debug.Log($"[LauncherUpdate] Downloading from: {LAUNCHER_DOWNLOAD_URL}");

        using (UnityWebRequest www = UnityWebRequest.Get(LAUNCHER_DOWNLOAD_URL))
        {
            DownloadHandlerFile dh = new DownloadHandlerFile(savePath);
            dh.removeFileOnAbort = true;
            www.downloadHandler = dh;
            www.SendWebRequest();

            while (!www.isDone)
            {
                float progress = www.downloadProgress;
                launcherProgressFill.rectTransform.anchorMax = new Vector2(progress, 1);
                launcherProgressText.text = $"{Mathf.Round(progress * 100)}%";
                yield return null;
            }

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[LauncherUpdate] Download failed: {www.error}");
                launcherProgressContainer.SetActive(false);
                launcherButtonBg.color = new Color(0.2f, 0.5f, 0.9f); // синий
                launcherButtonMainText.gameObject.SetActive(true);
                launcherButtonMainText.text = "Error";
                launcherUpdateButton.interactable = true;
                yield break;
            }
        }

        Debug.Log("[LauncherUpdate] Download complete, extracting...");

        try
        {
            if (Directory.Exists(extractDir))
                Directory.Delete(extractDir, true);
            System.IO.Compression.ZipFile.ExtractToDirectory(savePath, extractDir);
            File.Delete(savePath);
            Debug.Log("[LauncherUpdate] Extraction successful.");
        }
        catch (System.Exception e)
        {
            Debug.LogError("[LauncherUpdate] Extraction failed: " + e.Message);
            launcherProgressContainer.SetActive(false);
            launcherButtonBg.color = new Color(0.2f, 0.5f, 0.9f);
            launcherButtonMainText.gameObject.SetActive(true);
            launcherButtonMainText.text = "Error";
            launcherUpdateButton.interactable = true;
            yield break;
        }

        string currentDir = RootPath;
        string newExe = Path.Combine(extractDir, "Unio Games Launcher.exe");
        if (!File.Exists(newExe))
        {
            Debug.LogError("[LauncherUpdate] New exe not found in archive!");
            launcherProgressContainer.SetActive(false);
            launcherButtonBg.color = new Color(0.2f, 0.5f, 0.9f);
            launcherButtonMainText.gameObject.SetActive(true);
            launcherButtonMainText.text = "Error";
            launcherUpdateButton.interactable = true;
            yield break;
        }

        string tempParent = Path.GetDirectoryName(tempDir);
        string tempGrandParent = Path.GetDirectoryName(tempParent);

        string launcherFullPath = Path.Combine(currentDir, "Unio Games Launcher.exe");
        string cmd = $"/c timeout /t 1 /nobreak >nul && " +
                     $"xcopy \"{extractDir}\\*\" \"{currentDir}\" /E /Y /C /Q && " +
                     $"rmdir /s /q \"{tempDir}\" && " +
                     $"rmdir /s /q \"{tempParent}\" && " +
                     $"rmdir /s /q \"{tempGrandParent}\" && " +
                     $"start \"\" \"{launcherFullPath}\"";

        Debug.Log("[LauncherUpdate] Starting cmd update process...");
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = cmd,
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true
            });
            Application.Quit();
        }
        catch (System.Exception e)
        {
            Debug.LogError("[LauncherUpdate] Failed to start cmd: " + e.Message);
            launcherProgressContainer.SetActive(false);
            launcherButtonBg.color = new Color(0.2f, 0.5f, 0.9f);
            launcherButtonMainText.gameObject.SetActive(true);
            launcherButtonMainText.text = "Error";
            launcherUpdateButton.interactable = true;
        }
    }
}