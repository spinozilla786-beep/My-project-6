using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays the dragon hunt objective, attack controls, and live cooldown meters.
/// </summary>
public sealed class CombatHUD : MonoBehaviour
{
    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;
    private const float PanelWidth = 460f;
    private const float PanelHeight = 430f;
    private const float PanelPadding = 24f;
    private const float CooldownBarWidth = PanelWidth - PanelPadding * 2f;
    private const float CooldownBarHeight = 14f;

    private static readonly Color PanelColor = new Color32(5, 10, 18, 250);
    private static readonly Color AccentColor = new Color32(77, 190, 255, 255);
    private static readonly Color ReadyColor = new Color32(96, 226, 176, 255);
    private static readonly Color CooldownColor = new Color32(255, 133, 92, 255);
    private static readonly Color BarBackgroundColor = new Color32(31, 47, 63, 255);
    private static readonly Color HealthHighColor = new Color32(83, 203, 132, 255);
    private static readonly Color HealthMidColor = new Color32(239, 186, 68, 255);
    private static readonly Color HealthLowColor = new Color32(232, 77, 77, 255);
    private static readonly Color BurstColor = new Color32(105, 176, 255, 255);

    private PlayerSwitcher playerSwitcher;
    private FighterController activeFighter;
    private DragonController dragonController;
    private TextMeshProUGUI objectiveText;
    private TextMeshProUGUI enemyCountText;
    private TextMeshProUGUI recordText;
    private TextMeshProUGUI healthText;
    private TextMeshProUGUI attackCooldownText;
    private TextMeshProUGUI dashCooldownText;
    private TextMeshProUGUI burstText;
    private RectTransform healthFill;
    private RectTransform attackCooldownFill;
    private RectTransform dashCooldownFill;
    private RectTransform burstFill;
    private GameObject victoryOverlay;
    private TextMeshProUGUI victoryTitleText;
    private TextMeshProUGUI victoryCountdownText;
    private GameObject tutorialPanel;
    private TextMeshProUGUI tutorialChecklistText;

    private void Awake()
    {
        BuildCanvas();
    }

    private void Update()
    {
        if (playerSwitcher == null)
        {
            playerSwitcher = PlayerSwitcher.Instance;
        }

        GameObject controlledCharacter = playerSwitcher != null ? playerSwitcher.GetControlledCharacter() : null;
        if (controlledCharacter != null)
        {
            activeFighter = controlledCharacter.GetComponent<FighterController>();
            dragonController = controlledCharacter.GetComponent<DragonController>();
        }

        UpdateObjective();
        UpdatePlayerMeters();
        UpdateCooldownMeters();
        UpdateTutorialPanel();
        UpdateVictoryScreen();
    }

    private void BuildCanvas()
    {
        GameObject canvasObject = new GameObject("Dragon Hunt HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        canvasScaler.matchWidthOrHeight = 0.5f;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        RectTransform panel = CreateImage("Hunt Panel", canvasRect, new Vector2(26f, 26f), PanelWidth, PanelHeight, PanelColor);
        panel.anchorMin = new Vector2(0f, 1f);
        panel.anchorMax = new Vector2(0f, 1f);
        panel.pivot = new Vector2(0f, 1f);

        CreateImage("Top Accent", panel, Vector2.zero, PanelWidth, 6f, AccentColor);
        CreateText("Title", panel, "DRAGON HUNT", 24f, 20f, 412f, 38f, 30f, AccentColor, FontStyles.Bold);
        objectiveText = CreateText("Objective", panel, "DEFEAT ALL ENEMIES", 24f, 64f, 412f, 28f, 18f, Color.white, FontStyles.Bold);
        enemyCountText = CreateText("Enemy Count", panel, "0 / 3 DEFEATED     3 LEFT", 24f, 101f, 412f, 32f, 21f, Color.white, FontStyles.Bold);
        recordText = CreateText("Win Loss Record", panel, "WINS 0     LOSSES 0", 24f, 140f, 412f, 26f, 18f, Color.white, FontStyles.Bold);

        healthText = CreateText("Player Health", panel, "DRAGON HP  100 / 100", 24f, 176f, 412f, 26f, 18f, Color.white, FontStyles.Bold);
        healthFill = CreateCooldownBar("Player Health Bar", panel, 24f, 208f);

        CreateText("Attack Label", panel, "4   CLAW ATTACK", 24f, 238f, 260f, 28f, 19f, Color.white, FontStyles.Bold);
        attackCooldownText = CreateText("Attack Cooldown", panel, "READY", 298f, 238f, 138f, 28f, 17f, ReadyColor, FontStyles.Bold, TextAlignmentOptions.Right);

        CreateText("Dash Label", panel, "E   FLAME DASH", 24f, 279f, 260f, 28f, 19f, Color.white, FontStyles.Bold);
        dashCooldownText = CreateText("Dash Cooldown", panel, "READY", 298f, 279f, 138f, 28f, 17f, ReadyColor, FontStyles.Bold, TextAlignmentOptions.Right);

        CreateText("Burst Label", panel, "Q   BURST", 24f, 320f, 260f, 28f, 19f, Color.white, FontStyles.Bold);
        burstText = CreateText("Burst Charge", panel, "0%", 298f, 320f, 138f, 28f, 17f, BurstColor, FontStyles.Bold, TextAlignmentOptions.Right);
        burstFill = CreateCooldownBar("Burst Charge Bar", panel, 24f, 354f);

        CreateText("Controls", panel, "MOVE  WASD / ARROWS       BLOCK  5\nATTACK  4       DASH  E       BURST  Q", 24f, 376f, 412f, 42f, 13f, new Color32(230, 240, 250, 255), FontStyles.Bold);
        BuildVictoryOverlay(canvasRect);
        BuildTutorialPanel(canvasRect);
    }

    private void BuildTutorialPanel(RectTransform canvasRect)
    {
        tutorialPanel = new GameObject("Tutorial Round Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform panelRect = tutorialPanel.GetComponent<RectTransform>();
        panelRect.SetParent(canvasRect, false);
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-26f, -26f);
        panelRect.sizeDelta = new Vector2(PanelWidth, 430f);
        tutorialPanel.GetComponent<Image>().color = PanelColor;
        tutorialPanel.GetComponent<Image>().raycastTarget = false;

        CreateImage("Tutorial Accent", panelRect, Vector2.zero, PanelWidth, 4f, AccentColor);
        CreateText("Tutorial Title", panelRect, "PRACTICE ROUND", 24f, 20f, 412f, 34f, 24f, AccentColor, FontStyles.Bold);
        tutorialChecklistText = CreateText(
            "Tutorial Instructions", panelRect, string.Empty, 24f, 65f, 412f, 345f, 18f,
            Color.white, FontStyles.Normal);
        tutorialChecklistText.textWrappingMode = TextWrappingModes.Normal;
    }

    private void BuildVictoryOverlay(RectTransform canvasRect)
    {
        victoryOverlay = new GameObject("Victory Screen", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform overlayRect = victoryOverlay.GetComponent<RectTransform>();
        overlayRect.SetParent(canvasRect, false);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        victoryOverlay.GetComponent<Image>().color = new Color32(8, 12, 20, 225);
        victoryOverlay.GetComponent<Image>().raycastTarget = false;

        victoryTitleText = CreateText(
            "Victory Title", overlayRect, "VICTORY!", 0f, 0f, 900f, 100f, 72f,
            ReadyColor, FontStyles.Bold, TextAlignmentOptions.Center);
        RectTransform titleRect = victoryTitleText.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = new Vector2(0f, 45f);

        victoryCountdownText = CreateText(
            "Victory Countdown", overlayRect, "NEXT ROUND IN 5", 0f, 0f, 700f, 60f, 32f,
            Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        RectTransform countdownRect = victoryCountdownText.rectTransform;
        countdownRect.anchorMin = new Vector2(0.5f, 0.5f);
        countdownRect.anchorMax = new Vector2(0.5f, 0.5f);
        countdownRect.pivot = new Vector2(0.5f, 0.5f);
        countdownRect.anchoredPosition = new Vector2(0f, -35f);

        victoryOverlay.SetActive(false);
    }

    private void UpdateTutorialPanel()
    {
        if (tutorialPanel == null || tutorialChecklistText == null) return;

        bool showTutorial = playerSwitcher != null && playerSwitcher.IsTutorialRound &&
            !playerSwitcher.HasWon && !playerSwitcher.HasLost;
        tutorialPanel.SetActive(showTutorial);
        if (!showTutorial) return;

        string moveStatus = playerSwitcher.TutorialMoved
            ? "MOVE  WASD / ARROWS — DONE"
            : "MOVE  WASD / ARROWS — TRY IT";
        string clawStatus = playerSwitcher.TutorialClawHit
            ? "CLAW  4 — DONE"
            : "CLAW  4 — hit an enemy";
        string dashStatus = playerSwitcher.TutorialDashHit
            ? "FLAME DASH  E — DONE"
            : "FLAME DASH  E — hit an enemy";
        string burstStatus = playerSwitcher.TutorialBurstUsed
            ? "BURST Q — DONE"
            : playerSwitcher.TutorialHealthReduced
                ? "BURST Q — USE AT 30 HP OR LESS"
                : "BURST Q — HEALTH DROPS AFTER CLAW + DASH";
        string nextStep = playerSwitcher.IsTutorialChecklistComplete
            ? "Checklist done. Clear the practice wave."
            : "Hit each attack on an enemy to learn it.";

        tutorialChecklistText.text =
            $"{moveStatus}\n{clawStatus}\n{dashStatus}\n{burstStatus}\n" +
            "BLOCK 5 — HOLD TO REDUCE DAMAGE\n\n" + nextStep;
    }

    private void UpdateVictoryScreen()
    {
        bool isVictory = playerSwitcher != null && playerSwitcher.HasWon;
        if (victoryOverlay == null) return;

        if (victoryOverlay.activeSelf != isVictory)
        {
            victoryOverlay.SetActive(isVictory);
        }

        if (isVictory)
        {
            bool completedTutorial = playerSwitcher.IsTutorialRound;
            victoryTitleText.text = completedTutorial ? "TUTORIAL COMPLETE!" : "VICTORY!";
            int secondsRemaining = Mathf.CeilToInt(playerSwitcher.VictoryRestartSecondsRemaining);
            victoryCountdownText.text = completedTutorial
                ? $"REGULAR ROUND IN {secondsRemaining}"
                : $"NEXT ROUND IN {secondsRemaining}";
        }
    }

    private RectTransform CreateCooldownBar(string objectName, RectTransform parent, float x, float y)
    {
        RectTransform background = CreateImage(objectName, parent, new Vector2(x, y), CooldownBarWidth, CooldownBarHeight, BarBackgroundColor);
        RectTransform fill = CreateImage("Fill", background, Vector2.zero, CooldownBarWidth, CooldownBarHeight, ReadyColor);
        fill.anchorMin = new Vector2(0f, 0f);
        fill.anchorMax = new Vector2(0f, 1f);
        fill.pivot = new Vector2(0f, 0.5f);
        return fill;
    }

    private RectTransform CreateImage(string objectName, RectTransform parent, Vector2 topLeftPosition, float width, float height, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.SetParent(parent, false);
        imageRect.anchorMin = new Vector2(0f, 1f);
        imageRect.anchorMax = new Vector2(0f, 1f);
        imageRect.pivot = new Vector2(0f, 1f);
        imageRect.anchoredPosition = new Vector2(topLeftPosition.x, -topLeftPosition.y);
        imageRect.sizeDelta = new Vector2(width, height);

        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return imageRect;
    }

    private TextMeshProUGUI CreateText(
        string objectName,
        RectTransform parent,
        string value,
        float x,
        float y,
        float width,
        float height,
        float fontSize,
        Color color,
        FontStyles fontStyle,
        TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(parent, false);
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(0f, 1f);
        textRect.pivot = new Vector2(0f, 1f);
        textRect.anchoredPosition = new Vector2(x, -y);
        textRect.sizeDelta = new Vector2(width, height);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;

        Outline outline = textObject.AddComponent<Outline>();
        outline.effectColor = new Color32(0, 0, 0, 235);
        outline.effectDistance = new Vector2(1.2f, -1.2f);
        outline.useGraphicAlpha = true;
        return text;
    }

    private void UpdateObjective()
    {
        if (playerSwitcher == null)
        {
            objectiveText.text = "CLEAR THE ARENA";
            enemyCountText.text = "0 DEFEATED     0 LEFT";
            if (recordText != null) recordText.text = "WINS 0     LOSSES 0";
            return;
        }

        if (playerSwitcher.HasWon)
        {
            objectiveText.text = "ARENA CLEARED";
            objectiveText.color = ReadyColor;
        }
        else if (playerSwitcher.HasLost)
        {
            objectiveText.text = "DRAGON DOWN";
            objectiveText.color = CooldownColor;
        }
        else
        {
            objectiveText.text = "CLEAR THE ARENA";
            objectiveText.color = Color.white;
        }

        enemyCountText.text = $"{playerSwitcher.EnemiesDefeated} DEFEATED     {playerSwitcher.EnemiesRemaining} LEFT";
        if (recordText != null)
        {
            recordText.text = $"WINS {playerSwitcher.Wins}     LOSSES {playerSwitcher.Losses}";
        }
    }

    private void UpdatePlayerMeters()
    {
        if (activeFighter == null)
        {
            healthText.text = "PLAYER HP  -- / --";
            burstText.text = "0%";
            SetCooldownBar(healthFill, 0f);
            SetCooldownBar(burstFill, 0f);
            return;
        }

        float healthProgress = activeFighter.HealthProgress;
        string characterLabel = activeFighter.playerID == 2 ? "DRAGON HP" : "PLAYER HP";
        healthText.text = $"{characterLabel}  {activeFighter.currentHealth} / {activeFighter.maxHealth}";
        SetCooldownBar(healthFill, healthProgress);
        Image healthImage = healthFill.GetComponent<Image>();
        healthImage.color = healthProgress > 0.5f
            ? HealthHighColor
            : healthProgress > 0.25f ? HealthMidColor : HealthLowColor;

        float burstProgress = activeFighter.BurstChargeProgress;
        SetCooldownBar(burstFill, burstProgress);
        burstFill.GetComponent<Image>().color = BurstColor;
        if (activeFighter.CanUseBurstAttack)
        {
            burstText.text = "READY";
            burstText.color = ReadyColor;
        }
        else if (healthProgress > 0.5f)
        {
            burstText.text = $"{burstProgress * 100f:0}%  HP>30";
            burstText.color = CooldownColor;
        }
        else
        {
            burstText.text = $"{burstProgress * 100f:0}%";
            burstText.color = BurstColor;
        }
    }

    private void UpdateCooldownMeters()
    {
        float attackProgress = activeFighter != null ? activeFighter.AttackCooldownProgress : 1f;
        float attackRemaining = activeFighter != null ? activeFighter.AttackCooldownRemaining : 0f;
        SetCooldownBar(attackCooldownFill, attackProgress);
        SetCooldownLabel(attackCooldownText, attackRemaining, false);

        float dashProgress = dragonController != null ? dragonController.BoostCooldownProgress : 1f;
        float dashRemaining = dragonController != null ? dragonController.BoostCooldownRemaining : 0f;
        bool isDashing = dragonController != null && dragonController.IsBoosting;
        SetCooldownBar(dashCooldownFill, dashProgress);
        SetCooldownLabel(dashCooldownText, dashRemaining, isDashing);
    }

    private void SetCooldownBar(RectTransform fill, float progress)
    {
        if (fill == null)
        {
            return;
        }

        float normalizedProgress = Mathf.Clamp01(progress);
        fill.sizeDelta = new Vector2(CooldownBarWidth * normalizedProgress, CooldownBarHeight);
        Image fillImage = fill.GetComponent<Image>();
        if (fillImage != null)
        {
            fillImage.color = normalizedProgress >= 1f ? ReadyColor : CooldownColor;
        }
    }

    private void SetCooldownLabel(TextMeshProUGUI label, float remaining, bool isActive)
    {
        if (label == null)
        {
            return;
        }

        if (isActive)
        {
            label.text = "ACTIVE";
            label.color = AccentColor;
        }
        else if (remaining > 0f)
        {
            label.text = $"{remaining:0.0}s";
            label.color = CooldownColor;
        }
        else
        {
            label.text = "READY";
            label.color = ReadyColor;
        }
    }
}
