using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float speed = 15.0f;
    public float rotationSpeed = 22.5f;
    [Tooltip("When enabled, Character 1 automatically pursues the dragon and attacks on collider contact.")]
    [SerializeField] private bool autonomousOpponentEnabled = true;
    [SerializeField, Min(0f)] private float autonomousStoppingDistance = 2.4f;
    [SerializeField, Min(0.1f)] private float autonomousMoveSpeed = 5.25f;

    [Tooltip("Adjust this if the model faces sideways or backwards while moving!")]
    public float modelRotationOffset = 90f; // 90, -90, or 180 depending on the model's import axis

    [Header("Timer Settings")]
    public float timeRemaining = 60f;
    public bool isTimerRunning = true;

    [Header("UI Elements")]
    public TMP_Text countText;
    public TMP_Text wintext;
    public TMP_Text timerText;

    private Rigidbody rb;
    private Animator animator;
    private int count = 0;
    private float movementX;
    private float movementY;
    private bool hasIsWalkingParam = false;
    private Transform dragonTarget;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();

        PlayerSwitcher playerSwitcher = Object.FindAnyObjectByType<PlayerSwitcher>();
        if (playerSwitcher != null && playerSwitcher.character2 != null)
        {
            dragonTarget = playerSwitcher.character2.transform;
        }

        // Check if 'isWalking' exists in the Animator to avoid warning spam
        if (animator != null)
        {
            foreach (AnimatorControllerParameter param in animator.parameters)
            {
                if (param.name == "isWalking")
                {
                    hasIsWalkingParam = true;
                    break;
                }
            }
        }

        SetCountText();
        DisplayTime(timeRemaining);

        if (wintext != null)
        {
            wintext.gameObject.SetActive(false);
        }
    }

    void OnMove(InputValue movementValue)
    {
        Vector2 movementVector = movementValue.Get<Vector2>();
        movementX = movementVector.x;
        movementY = movementVector.y;
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        FighterController fighter = GetComponent<FighterController>();
        if (fighter != null && fighter.IsBeingKnockedBack)
        {
            SetWalkingAnimation(false);
            return;
        }

        if (autonomousOpponentEnabled && PlayerSwitcher.IsDragonActive)
        {
            MoveAutonomouslyTowardDragon();
            return;
        }

        if (PlayerSwitcher.IsDragonActive || CameraMan.IsFreeFlyActive)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            SetWalkingAnimation(false);
            return;
        }

        Vector3 moveDirection = new Vector3(movementX, 0.0f, movementY).normalized;
        ApplyMovement(moveDirection);
    }

    private void MoveAutonomouslyTowardDragon()
    {
        if (CameraMan.IsFreeFlyActive)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            SetWalkingAnimation(false);
            return;
        }

        if (dragonTarget == null)
        {
            PlayerSwitcher playerSwitcher = Object.FindAnyObjectByType<PlayerSwitcher>();
            if (playerSwitcher != null && playerSwitcher.character2 != null)
            {
                dragonTarget = playerSwitcher.character2.transform;
            }
        }

        if (dragonTarget == null)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            SetWalkingAnimation(false);
            return;
        }

        Vector3 directionToDragon = dragonTarget.position - transform.position;
        directionToDragon.y = 0f;

        if (directionToDragon.sqrMagnitude <= autonomousStoppingDistance * autonomousStoppingDistance)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            SetWalkingAnimation(false);
            return;
        }

        ApplyMovement(directionToDragon.normalized, autonomousMoveSpeed);
    }

    private void ApplyMovement(Vector3 moveDirection, float movementSpeed = -1f)
    {
        if (moveDirection.sqrMagnitude > 0.01f)
        {
            float currentSpeed = movementSpeed > 0f ? movementSpeed : speed;
            Vector3 targetVelocity = moveDirection * currentSpeed;
            targetVelocity.y = rb.linearVelocity.y;
            rb.linearVelocity = targetVelocity;

            Quaternion baseLookRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            Quaternion correctedRotation = baseLookRotation * Quaternion.Euler(0f, modelRotationOffset, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, correctedRotation, rotationSpeed * Time.fixedDeltaTime);
            SetWalkingAnimation(true);
        }
        else
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            SetWalkingAnimation(false);
        }
    }

    private void SetWalkingAnimation(bool isWalking)
    {
        if (hasIsWalkingParam && animator != null)
        {
            animator.SetBool("isWalking", isWalking);
        }
    }

    void Update()
    {
        if (isTimerRunning)
        {
            if (count >= 13)
            {
                isTimerRunning = false;
                WinGame();
            }
            else if (timeRemaining > 0)
            {
                timeRemaining -= Time.deltaTime;
                DisplayTime(timeRemaining);
            }
            else
            {
                timeRemaining = 0;
                isTimerRunning = false;
                GameOver();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("pickup 1"))
        {
            other.gameObject.SetActive(false);
            AddPickupScore(1);
        }
    }

    /// <summary>
    /// Adds collected pickups to the score and refreshes the count display.
    /// </summary>
    public void AddPickupScore(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        count += amount;
        SetCountText();
    }

    void SetCountText()
    {
        if (countText != null)
        {
            countText.text = "Count: " + count.ToString();
        }
    }

    void DisplayTime(float timeToDisplay)
    {
        if (timerText != null)
        {
            int seconds = Mathf.CeilToInt(timeToDisplay);
            timerText.text = "Time Left: " + seconds.ToString();
        }
    }

    void WinGame()
    {
        if (wintext != null)
        {
            wintext.gameObject.SetActive(true);
        }
        Debug.Log("You Won!");
    }

    void GameOver()
    {
        if (timerText != null)
        {
            timerText.text = "Game Over!";
        }
        Debug.Log("Game Over!");

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
