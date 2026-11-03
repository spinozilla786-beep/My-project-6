using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameTimer : MonoBehaviour
{
    [Header("Timer Settings")]
    public float timeRemaining = 30f;
    public bool isTimerRunning = true;
    public float gameOverDelay = 2f;

    [Header("UI Elements")]
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI gameOverText; // Game Over UI text element

    [Header("Game State")]
    public int playerCount = 0;

    void Start()
    {
        // Ensure Game Over text is hidden at start
        if (gameOverText != null)
        {
            gameOverText.gameObject.SetActive(false);
        }

        DisplayTime(timeRemaining);
    }

    void Update()
    {
        if (isTimerRunning)
        {
            // If count is 13 or greater: Stop timer, ensure Game Over text stays FALSE/hidden, and win
            if (playerCount >= 13)
            {
                isTimerRunning = false;
                WinGame();
            }
            // Count down while time remains
            else if (timeRemaining > 0)
            {
                timeRemaining -= Time.deltaTime;
                DisplayTime(timeRemaining);
            }
            // Only trigger Game Over if time runs out before reaching 13 items
            else
            {
                timeRemaining = 0;
                isTimerRunning = false;
                DisplayTime(0);
                StartCoroutine(GameOverSequence());
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Item"))
        {
            playerCount++;
            Destroy(other.gameObject);
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
        // Explicitly disable/hide Game Over UI element
        if (gameOverText != null)
        {
            gameOverText.gameObject.SetActive(false);
        }

        // Display victory text on the timer text UI
        if (timerText != null)
        {
            timerText.text = "You Won!";
        }

        Debug.Log("You Won! Game Over disabled.");
    }

    IEnumerator GameOverSequence()
    {
        // Only executes if time ran out before reaching 13 items
        if (gameOverText != null)
        {
            gameOverText.gameObject.SetActive(true);
            gameOverText.text = "Game Over!";
        }
        else if (timerText != null)
        {
            timerText.text = "Game Over!";
        }

        Debug.Log("Game Over!");

        yield return new WaitForSeconds(gameOverDelay);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}