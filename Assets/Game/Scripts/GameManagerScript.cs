using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class GameManagerScript : MonoBehaviour {
    ////////////////////////////////////////////////////////////////////////////////
    /// Variables
    ////////////////////////////////////////////////////////////////////////////////
    // Inputs
    [SerializeField] private Sprite[] powerupSprites;
    [SerializeField] private GameObject powerupPrefab;
    [SerializeField] private GameObject powerupFolder;

    // Level management
    [SerializeField] private GameObject[] levelPrefabs;
    [SerializeField] private Sprite[] levelBackgrounds;
    [SerializeField] private SpriteRenderer backgroundRenderer;
    private GameObject currentLevelInstance;
    private int currentLevelIndex = 0;
    private bool isSwapping = false;

    // Scoring
    private int[] scores = new int[2]; // 0 = Player1 (WASD), 1 = Player2 (Arrows)
    private PlayerScript[] players;
    private const int score_to_win = 3;

    // UI
    [SerializeField] private TMP_FontAsset pixelFont;
    private TextMeshProUGUI scoreText;

    // Spawn variables
    private float powerupInterval = 12f;
    private float powerupTimer = 0f;
    private Vector2 powerupSpawnRange = new Vector2(13f, 7f);



    ////////////////////////////////////////////////////////////////////////////////
    /// Start
    ////////////////////////////////////////////////////////////////////////////////
    private IEnumerator Start() {
        BuildScoreUI();
        currentLevelInstance = Instantiate(levelPrefabs[currentLevelIndex]);
        CachePlayersFromLevel(currentLevelInstance);
        ApplyBackground(currentLevelIndex);
        yield return null;
        UpdateScoreUI();
    }

    private void BuildScoreUI() {
        var canvasGO = new GameObject("ScoreCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        var textGO = new GameObject("ScoreText");
        textGO.transform.SetParent(canvasGO.transform, false);

        scoreText = textGO.AddComponent<TextMeshProUGUI>();

        var fallbackFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        scoreText.font = pixelFont != null ? pixelFont : fallbackFont;

        scoreText.fontSize = 72;
        scoreText.fontStyle = FontStyles.Bold;
        scoreText.alignment = TextAlignmentOptions.Top;
        scoreText.color = Color.white;
        scoreText.richText = true;

        var rect = textGO.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, 80f);
        rect.anchoredPosition = new Vector2(0f, -20f);
    }



    ////////////////////////////////////////////////////////////////////////////////
    /// Update
    ////////////////////////////////////////////////////////////////////////////////
    private void Update() {
        /////////////// Spawn powerup logic ///////////////
        powerupTimer += Time.deltaTime;
        if (powerupTimer >= powerupInterval) {
            powerupTimer = 0f;

            SpawnPowerup();
        }
    }



    ////////////////////////////////////////////////////////////////////////////////
    /// Scoring
    ////////////////////////////////////////////////////////////////////////////////
    public void OnPlayerDied(PlayerScript deadPlayer) {
        if (isSwapping) return;

        scores[GetOpponentIndex(deadPlayer)]++;
        UpdateScoreUI();

        if (scores[0] >= score_to_win || scores[1] >= score_to_win)
            SwapLevel();
    }

    private int GetOpponentIndex(PlayerScript dead) => (dead == players[0]) ? 1 : 0;

    private void UpdateScoreUI() {
        if (scoreText == null) return;
        string c0 = players != null ? GoatHex(players[0].GoatColor) : "#FFFFFF";
        string c1 = players != null ? GoatHex(players[1].GoatColor) : "#FFFFFF";
        scoreText.text = $"<color={c1}>{scores[0]}</color>  —  <color={c0}>{scores[1]}</color>";
    }

    private string GoatHex(string color) => color switch {
        "Black"  => "#A8B4C0",
        "Brown"  => "#D4A882",
        "Gold"   => "#F5E09A",
        "Red"    => "#F0A8A8",
        "White"  => "#E8E0F5",
        _        => "#FFFFFF"
    };



    ////////////////////////////////////////////////////////////////////////////////
    /// Level management
    ////////////////////////////////////////////////////////////////////////////////
    private void CachePlayersFromLevel(GameObject levelInstance) {
        players = levelInstance.GetComponentsInChildren<PlayerScript>();
        // Stable order: Player1 (WASD) = index 0, Player2 (Arrows) = index 1
        System.Array.Sort(players, (a, b) =>
            string.Compare(a.gameObject.name, b.gameObject.name, System.StringComparison.Ordinal));
    }

    private void SwapLevel() {
        if (levelPrefabs == null || levelPrefabs.Length == 0) {
            Debug.LogWarning("No level prefabs assigned to GameManager.");
            scores[0] = scores[1] = 0;
            UpdateScoreUI();
            return;
        }

        isSwapping = true;
        StartCoroutine(SwapLevelRoutine());
    }

    private IEnumerator SwapLevelRoutine() {
        // Clear powerups
        foreach (Transform child in powerupFolder.transform)
            Destroy(child.gameObject);
        powerupTimer = 0f;

        // Pick new level index (re-roll until different, unless only 1 option)
        int newIndex = levelPrefabs.Length == 1 ? 0 : currentLevelIndex;
        while (newIndex == currentLevelIndex)
            newIndex = Random.Range(0, levelPrefabs.Length);

        currentLevelInstance.SetActive(false);
        DestroyImmediate(currentLevelInstance);
        currentLevelInstance = Instantiate(levelPrefabs[newIndex]);
        currentLevelIndex = newIndex;

        scores[0] = scores[1] = 0;
        CachePlayersFromLevel(currentLevelInstance);
        ApplyBackground(currentLevelIndex);

        yield return null; // wait for new players' Start() so animator is ready

        UpdateScoreUI();
        isSwapping = false;
    }

    private void ApplyBackground(int index) {
        if (backgroundRenderer == null || levelBackgrounds == null || index >= levelBackgrounds.Length) return;
        backgroundRenderer.sprite = levelBackgrounds[index];
    }



    ////////////////////////////////////////////////////////////////////////////////
    /// Powerup spawning
    ////////////////////////////////////////////////////////////////////////////////
    public void SpawnPowerup() {
        var platformLayer = LayerMask.GetMask("Platform");
        float x, y;
        const int maxAttempts = 20;
        int attempts = 0;

        // Try to spawn powerup at random pos
        // Retry if collision with platform
        do {
            x = Random.Range(-powerupSpawnRange.x, powerupSpawnRange.x);
            y = Random.Range(-powerupSpawnRange.y, powerupSpawnRange.y);
            attempts++;
        } while (Physics2D.OverlapCircle(new Vector2(x, y), 0.5f, platformLayer) != null && attempts < maxAttempts);

        // Give up if no space found
        if (attempts >= maxAttempts)
            return;

        // Load data
        var position = new Vector3(x, y, 0f);
        var sprite = powerupSprites[Random.Range(0, powerupSprites.Length)];

        // Load powerup
        var powerup = Instantiate(powerupPrefab, position, Quaternion.identity);

        // Assign data
        powerup.transform.SetParent(powerupFolder.transform);
        powerup.GetComponent<SpriteRenderer>().sprite = sprite;
    }
}
