using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Selects the controlled character and creates slower enemy-player characters.
/// </summary>
public class PlayerSwitcher : MonoBehaviour
{
    [Header("Assign Characters from Hierarchy")]
    public GameObject character1;
    public GameObject character2;

    [Header("Character State")]
    [Tooltip("True while the dragon is selected as the playable character.")]
    public bool dragon;

    [Header("Enemy Players")]
    [SerializeField, Min(3)] private int minimumEnemyCount = 3;
    [SerializeField, Min(3)] private int maximumEnemyCount = 12;
    private int enemyPlayerCount;
    [SerializeField, Min(1f)] private float enemySpawnRadius = 10f;
    [SerializeField] private Vector2 arenaSpawnHalfExtents = new Vector2(48f, 43f);
    [SerializeField, Min(0.1f)] private float enemyMoveSpeed = 5.25f;
    [SerializeField, Min(0.1f)] private float enemyAttackCooldown = 1.4f;
    [SerializeField, Min(1)] private int enemyDamage = 5;
    [SerializeField, Min(1f)] private float victoryRestartDelaySeconds = 5f;

    public static bool IsDragonActive { get; private set; }
    public static PlayerSwitcher Instance { get; private set; }

    private static readonly Color PlayerTeamColor = new Color32(166, 77, 255, 255);
    private static readonly Color EnemyTeamColor = new Color32(232, 54, 64, 255);

    private const float BurstClawRadius = 15f;
    private const int BurstClawDamage = 10;
    private const int MinimumEnemyCountFloor = 3;
    private const int TutorialEnemyCount = 3;
    private const int TutorialEnemyHealth = 80;
    private const int TutorialEnemyDamage = 1;
    private const float TutorialEnemyMoveSpeed = 2.25f;
    private const float TutorialEnemyAttackCooldown = 3f;
    private const float TutorialSpawnHalfExtent = 12f;
    private const float TutorialBurstChargePerSecond = 10f;
    private const float CharacterScaleMultiplier = 1.2f;
    private const float TutorialMovementDistance = 1.5f;
    private const int EnemySpawnPositionAttempts = 48;
    private const float EnemySpawnPlayerClearance = 12f;
    private const float EnemySpawnSeparation = 8f;
    private const string WinCountPreferenceKey = "DragonHuntWins";
    private const string LossCountPreferenceKey = "DragonHuntLosses";
    private static bool hasCompletedTutorialThisSession;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetTutorialSession()
    {
        hasCompletedTutorialThisSession = false;
    }

    private readonly List<FighterController> spawnedEnemies = new List<FighterController>();
    private readonly HashSet<FighterController> defeatedEnemies = new HashSet<FighterController>();
    private readonly List<Vector3> enemySpawnPositions = new List<Vector3>();
    private bool hasEnded;
    private bool isTutorialRound;
    private bool tutorialHealthReduced;
    private Vector3 tutorialStartPosition;

    public int TotalEnemyCount => spawnedEnemies.Count;
    public int EnemiesDefeated => defeatedEnemies.Count;
    public int Wins => PlayerPrefs.GetInt(WinCountPreferenceKey, 0);
    public int Losses => PlayerPrefs.GetInt(LossCountPreferenceKey, 0);
    public int EnemiesRemaining => Mathf.Max(0, spawnedEnemies.Count - defeatedEnemies.Count);
    public bool HasWon { get; private set; }
    public bool HasLost { get; private set; }
    public bool IsTutorialRound => isTutorialRound;
    public bool TutorialMoved { get; private set; }
    public bool TutorialClawHit { get; private set; }
    public bool TutorialDashHit { get; private set; }
    public bool TutorialBurstUsed { get; private set; }
    public bool TutorialHealthReduced => tutorialHealthReduced;
    public bool IsTutorialChecklistComplete => TutorialMoved && TutorialClawHit && TutorialDashHit && TutorialBurstUsed;
    public float VictoryRestartSecondsRemaining { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (Instance != this) return;

        isTutorialRound = !hasCompletedTutorialThisSession;
        tutorialStartPosition = character2 != null ? character2.transform.position : Vector3.zero;
        int minimumEnemies = Mathf.Max(MinimumEnemyCountFloor, minimumEnemyCount);
        int maximumEnemies = Mathf.Max(minimumEnemies, maximumEnemyCount);
        enemyPlayerCount = isTutorialRound
            ? TutorialEnemyCount
            : Random.Range(minimumEnemies, maximumEnemies + 1);

        if (character1 != null)
        {
            character1.transform.localScale = Vector3.Scale(
                character1.transform.localScale,
                Vector3.one * CharacterScaleMultiplier);
            character1.SetActive(true);
            ApplyTeamColor(character1, EnemyTeamColor);
        }

        if (character2 != null)
        {
            character2.transform.localScale = Vector3.Scale(
                character2.transform.localScale,
                Vector3.one * CharacterScaleMultiplier);
            character2.SetActive(true);
            ApplyTeamColor(character2, PlayerTeamColor);
            ConfigurePlayerCombat(character2);
        }

        if (character1 == null)
        {
            Debug.LogError("PlayerSwitcher: Character 1 is not assigned.", this);
        }

        if (character2 == null)
        {
            Debug.LogError("PlayerSwitcher: Dragon (Character 2) is not assigned.", this);
        }

        PrepareEnemyHunt();
        SetSelectedCharacter(true);
        SpawnEnemyPlayers();

        if (GetComponent<CombatHUD>() == null)
        {
            gameObject.AddComponent<CombatHUD>();
        }

        ArenaHazardManager hazardManager = GetComponent<ArenaHazardManager>();
        if (isTutorialRound)
        {
            if (hazardManager != null) hazardManager.enabled = false;
        }
        else if (hazardManager == null)
        {
            gameObject.AddComponent<ArenaHazardManager>();
        }

        if (GetComponent<ArenaEnvironment>() == null)
        {
            gameObject.AddComponent<ArenaEnvironment>();
        }
    }

    private void ConfigurePlayerCombat(GameObject character)
    {
        FighterController fighter = character.GetComponent<FighterController>();
        if (fighter == null) return;

        fighter.attackRange = BurstClawRadius;
        fighter.attackForwardOffset = 0f;
        fighter.attackHeightOffset = 0f;
        fighter.attackCooldown = 0.55f;
        fighter.damagePerHit = BurstClawDamage;
        fighter.burstChargePerDamage = 1.5f;
        if (isTutorialRound)
        {
            fighter.SetBurstChargeRate(TutorialBurstChargePerSecond);
        }
    }

    private void ApplyTeamColor(GameObject character, Color teamColor)
    {
        Renderer[] renderers = character.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer characterRenderer in renderers)
        {
            Material[] materials = characterRenderer.materials;
            foreach (Material material in materials)
            {
                if (material == null) continue;
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", teamColor);
                if (material.HasProperty("_Color")) material.SetColor("_Color", teamColor);
            }
        }
    }

    private void PrepareEnemyHunt()
    {
        if (character1 != null)
        {
            PlayerController oldController = character1.GetComponent<PlayerController>();
            if (oldController != null)
            {
                oldController.isTimerRunning = false;
                oldController.enabled = false;
            }

            PlayerInput oldInput = character1.GetComponent<PlayerInput>();
            if (oldInput != null)
            {
                oldInput.enabled = false;
            }
        }

        GameTimer[] legacyTimers = Object.FindObjectsByType<GameTimer>();
        foreach (GameTimer legacyTimer in legacyTimers)
        {
            legacyTimer.enabled = false;
        }

        string[] legacyHudObjects = { "Canvas/countText", "Canvas/gametimer", "Canvas/wintext", "Canvas/gameover", "Canvas/DamagePopup" };
        foreach (string objectPath in legacyHudObjects)
        {
            GameObject legacyHudObject = GameObject.Find(objectPath);
            if (legacyHudObject != null)
            {
                legacyHudObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Records a successful practice-round attack against an enemy.
    /// </summary>
    public void NotifyTutorialAttackHit(bool wasFlameDash)
    {
        if (!isTutorialRound) return;
        if (wasFlameDash)
        {
            TutorialDashHit = true;
        }
        else
        {
            TutorialClawHit = true;
        }

        if (!tutorialHealthReduced && TutorialClawHit && TutorialDashHit)
        {
            FighterController dragonFighter = character2 != null
                ? character2.GetComponent<FighterController>()
                : null;
            if (dragonFighter != null)
            {
                dragonFighter.SetHealthAtMostBurstThreshold();
                tutorialHealthReduced = true;
                Debug.Log("Tutorial attacks complete. Dragon health reduced to the burst threshold.");
            }
        }
    }

    /// <summary>
    /// Records that the player activated the tutorial round's burst attack.
    /// </summary>
    public void NotifyTutorialBurstUsed()
    {
        if (isTutorialRound) TutorialBurstUsed = true;
    }



    private void Update()
    {
        if (isTutorialRound && !TutorialMoved && character2 != null &&
            (character2.transform.position - tutorialStartPosition).sqrMagnitude >= TutorialMovementDistance * TutorialMovementDistance)
        {
            TutorialMoved = true;
        }

        if (Instance != this || hasEnded || Keyboard.current == null) return;

        GameObject controlledCharacter = GetControlledCharacter();
        FighterController controlledFighter = controlledCharacter != null
            ? controlledCharacter.GetComponent<FighterController>()
            : null;
        bool controlledCharacterIsAirborne = controlledFighter != null && controlledFighter.IsJumping;

        if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame)
        {
            SetSelectedCharacter(false);
        }
        else if (!controlledCharacterIsAirborne &&
            (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame))
        {
            SetSelectedCharacter(true);
        }
    }

    /// <summary>
    /// Returns the character currently receiving player input.
    /// </summary>
    public GameObject GetControlledCharacter()
    {
        return dragon ? character2 : character1;
    }

    /// <summary>
    /// Updates the enemy-hunt result when a fighter is defeated.
    /// </summary>
    public void NotifyFighterDefeated(FighterController defeatedFighter)
    {
        if (defeatedFighter == null || hasEnded)
        {
            return;
        }

        if (defeatedFighter.playerID <= 2)
        {
            HasLost = true;
            RecordLoss();
            FinishMatch();
            StartCoroutine(RestartAfterPlayerDefeat());
            Debug.Log(defeatedFighter.IsMeteorDeath
                ? "Meteor defeated a player. The next round starts in five seconds."
                : "Player defeated. The next round starts in five seconds.");
            return;
        }

        if (!spawnedEnemies.Contains(defeatedFighter) || !defeatedEnemies.Add(defeatedFighter))
        {
            return;
        }

        Debug.Log($"Enemy defeated. {EnemiesDefeated}/{TotalEnemyCount} defeated; {EnemiesRemaining} remaining.");
        if (spawnedEnemies.Count > 0 && EnemiesRemaining == 0)
        {
            if (isTutorialRound && !IsTutorialChecklistComplete)
            {
                StartNextTutorialPracticeWave();
                return;
            }

            if (isTutorialRound)
            {
                hasCompletedTutorialThisSession = true;
            }

            HasWon = true;
            RecordWin();
            FinishMatch();
            StartCoroutine(RestartAfterVictory());
            Debug.Log(isTutorialRound
                ? "Tutorial complete. The regular hunt starts in five seconds."
                : "All enemies defeated. Hunt won; next round starts in five seconds.");
        }
    }

    private void StartNextTutorialPracticeWave()
    {
        foreach (FighterController defeatedEnemy in defeatedEnemies)
        {
            if (defeatedEnemy != null)
            {
                Destroy(defeatedEnemy.gameObject);
            }
        }

        spawnedEnemies.Clear();
        defeatedEnemies.Clear();
        enemySpawnPositions.Clear();
        SpawnEnemyPlayers();
        Debug.Log("Tutorial practice wave refreshed. Try each attack listed in the tutorial panel.");
    }

    private void RecordWin()
    {
        PlayerPrefs.SetInt(WinCountPreferenceKey, Wins + 1);
        PlayerPrefs.Save();
    }

    private void RecordLoss()
    {
        PlayerPrefs.SetInt(LossCountPreferenceKey, Losses + 1);
        PlayerPrefs.Save();
    }

    private void FinishMatch()
    {
        hasEnded = true;

        EnemyPursuitController[] enemyControllers = Object.FindObjectsByType<EnemyPursuitController>();
        foreach (EnemyPursuitController enemyController in enemyControllers)
        {
            Rigidbody enemyBody = enemyController.GetComponent<Rigidbody>();
            if (enemyBody != null)
            {
                enemyBody.linearVelocity = Vector3.zero;
                enemyBody.angularVelocity = Vector3.zero;
            }

            enemyController.enabled = false;
        }
    }

    private IEnumerator RestartAfterVictory()
    {
        VictoryRestartSecondsRemaining = victoryRestartDelaySeconds;
        while (VictoryRestartSecondsRemaining > 0f)
        {
            yield return new WaitForSecondsRealtime(1f);
            VictoryRestartSecondsRemaining = Mathf.Max(0f, VictoryRestartSecondsRemaining - 1f);
        }

        int currentSceneBuildIndex = SceneManager.GetActiveScene().buildIndex;
        if (currentSceneBuildIndex >= 0)
        {
            SceneManager.LoadScene(currentSceneBuildIndex);
        }
        else
        {
            Debug.LogError("PlayerSwitcher: The current scene is not in Build Settings, so the next round cannot reload.", this);
        }
    }

    private IEnumerator RestartAfterPlayerDefeat()
    {
        yield return new WaitForSecondsRealtime(FighterController.MeteorDeathDurationSeconds);

        int currentSceneBuildIndex = SceneManager.GetActiveScene().buildIndex;
        if (currentSceneBuildIndex >= 0)
        {
            SceneManager.LoadScene(currentSceneBuildIndex);
        }
        else
        {
            Debug.LogError("PlayerSwitcher: The current scene is not in Build Settings, so the next round cannot reload.", this);
        }
    }

    private void SetSelectedCharacter(bool selectDragon)
    {
        if (selectDragon && character2 == null)
        {
            Debug.LogError("PlayerSwitcher: Dragon (Character 2) is not assigned.", this);
            return;
        }

        if (!selectDragon && character1 == null)
        {
            Debug.LogError("PlayerSwitcher: Character 1 is not assigned.", this);
            return;
        }

        if (!selectDragon && !character1.activeInHierarchy)
        {
            return;
        }

        dragon = selectDragon;
        IsDragonActive = selectDragon;

        Rigidbody selectedBody = (selectDragon ? character2 : character1).GetComponent<Rigidbody>();
        if (selectedBody != null) selectedBody.WakeUp();

        Debug.Log(selectDragon ? "Switched to Dragon" : "Switched to Character 1");
    }

    private void SpawnEnemyPlayers()
    {
        if (character1 == null || character2 == null) return;

        Vector3 enemyTemplatePosition = character1.transform.position;
        Quaternion enemyTemplateRotation = character1.transform.rotation;
        character1.SetActive(false);
        enemySpawnPositions.Clear();

        for (int enemyIndex = 0; enemyIndex < enemyPlayerCount; enemyIndex++)
        {
            Vector3 spawnPosition = FindRandomEnemySpawnPosition(enemyTemplatePosition.y);
            GameObject enemy = Instantiate(character1, spawnPosition, enemyTemplateRotation);
            enemy.name = $"Enemy Player {enemyIndex + 1}";
            enemy.SetActive(true);
            ApplyTeamColor(enemy, EnemyTeamColor);

            PlayerController playerController = enemy.GetComponent<PlayerController>();
            if (playerController != null) playerController.enabled = false;

            PlayerInput playerInput = enemy.GetComponent<PlayerInput>();
            if (playerInput != null) playerInput.enabled = false;

            FighterController fighter = enemy.GetComponent<FighterController>();
            if (fighter != null)
            {
                fighter.playerID = enemyIndex + 3;
                fighter.maxHealth = isTutorialRound ? TutorialEnemyHealth : fighter.maxHealth;
                fighter.currentHealth = fighter.maxHealth;
                fighter.damagePerHit = isTutorialRound ? TutorialEnemyDamage : enemyDamage;
                fighter.attackCooldown = isTutorialRound ? TutorialEnemyAttackCooldown : enemyAttackCooldown;
                fighter.attackRange = 2.8f;
                fighter.attackForwardOffset = 1.8f;
                fighter.attackHeightOffset = 0.7f;
            }

            EnemyPursuitController enemyController = enemy.AddComponent<EnemyPursuitController>();
            enemyController.moveSpeed = isTutorialRound ? TutorialEnemyMoveSpeed : enemyMoveSpeed;
            enemyController.attackCooldown = isTutorialRound ? TutorialEnemyAttackCooldown : enemyAttackCooldown;
            enemyController.damagePerHit = isTutorialRound ? TutorialEnemyDamage : enemyDamage;

            if (fighter != null)
            {
                spawnedEnemies.Add(fighter);
            }
        }
    }

    private Vector3 FindRandomEnemySpawnPosition(float spawnY)
    {
        Vector3 playerPosition = character2.transform.position;
        float minimumPlayerDistance = isTutorialRound ? 6f : Mathf.Max(EnemySpawnPlayerClearance, enemySpawnRadius);
        float minimumPlayerDistanceSquared = minimumPlayerDistance * minimumPlayerDistance;
        float enemySeparation = isTutorialRound ? 5f : EnemySpawnSeparation;
        float minimumEnemyDistanceSquared = enemySeparation * enemySeparation;

        for (int attempt = 0; attempt < EnemySpawnPositionAttempts; attempt++)
        {
            Vector3 candidate = isTutorialRound
                ? new Vector3(
                    playerPosition.x + Random.Range(-TutorialSpawnHalfExtent, TutorialSpawnHalfExtent),
                    spawnY,
                    playerPosition.z + Random.Range(-TutorialSpawnHalfExtent, TutorialSpawnHalfExtent))
                : new Vector3(
                    Random.Range(-arenaSpawnHalfExtents.x, arenaSpawnHalfExtents.x),
                    spawnY,
                    Random.Range(-arenaSpawnHalfExtents.y, arenaSpawnHalfExtents.y));

            Vector2 candidatePlanar = new Vector2(candidate.x, candidate.z);
            Vector2 playerPlanar = new Vector2(playerPosition.x, playerPosition.z);
            if ((candidatePlanar - playerPlanar).sqrMagnitude < minimumPlayerDistanceSquared)
            {
                continue;
            }

            bool tooCloseToExistingEnemy = false;
            foreach (Vector3 existingPosition in enemySpawnPositions)
            {
                Vector2 existingPlanar = new Vector2(existingPosition.x, existingPosition.z);
                if ((candidatePlanar - existingPlanar).sqrMagnitude < minimumEnemyDistanceSquared)
                {
                    tooCloseToExistingEnemy = true;
                    break;
                }
            }

            if (tooCloseToExistingEnemy)
            {
                continue;
            }

            enemySpawnPositions.Add(candidate);
            return candidate;
        }

        Vector3 fallbackPosition = isTutorialRound
            ? new Vector3(
                playerPosition.x + TutorialSpawnHalfExtent,
                spawnY,
                playerPosition.z + TutorialSpawnHalfExtent)
            : new Vector3(
                Random.Range(-arenaSpawnHalfExtents.x, arenaSpawnHalfExtents.x),
                spawnY,
                Random.Range(-arenaSpawnHalfExtents.y, arenaSpawnHalfExtents.y));
        enemySpawnPositions.Add(fallbackPosition);
        return fallbackPosition;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;
        IsDragonActive = false;
    }
}
