using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Follows the selected character from a smooth third-person perspective, with optional free-fly control.
/// </summary>
public class CameraMan : MonoBehaviour
{
    [Header("Follow Camera")]
    [SerializeField, Min(0f)] private float lookAtHeight = 1.35f;
    [SerializeField, Min(0.01f)] private float followSmoothTime = 0.25f;

    [Header("Free-Fly Movement")]
    [SerializeField] private float moveSpeed = 12f;
    [SerializeField] private float fastMoveMultiplier = 3f;
    [SerializeField] private float lookSensitivity = 0.12f;
    [SerializeField] private float minimumPitch = -85f;
    [SerializeField] private float maximumPitch = 85f;

    public static bool IsFreeFlyActive { get; private set; }

    private Transform cameraTransform;
    private PlayerSwitcher playerSwitcher;
    private Vector3 followVelocity;
    private Vector3 followOffset;
    private float yaw;
    private float pitch;
    private bool hasFollowOffset;
    private bool isLooking;

    private void Start()
    {
        Camera sceneCamera = Camera.main;
        cameraTransform = sceneCamera != null ? sceneCamera.transform : transform;

        Vector3 startingAngles = cameraTransform.eulerAngles;
        yaw = startingAngles.y;
        pitch = startingAngles.x > 180f ? startingAngles.x - 360f : startingAngles.x;
        playerSwitcher = PlayerSwitcher.Instance != null
            ? PlayerSwitcher.Instance
            : Object.FindAnyObjectByType<PlayerSwitcher>();
    }

    private void Update()
    {
        if (Keyboard.current == null || Mouse.current == null || cameraTransform == null) return;

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            isLooking = true;
            IsFreeFlyActive = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else if (Mouse.current.rightButton.wasReleasedThisFrame)
        {
            StopLooking();
        }

        if (isLooking)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            yaw += mouseDelta.x * lookSensitivity;
            pitch = Mathf.Clamp(pitch - mouseDelta.y * lookSensitivity, minimumPitch, maximumPitch);
            cameraTransform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        Vector3 input = Vector3.zero;
        if (Keyboard.current.wKey.isPressed) input.z += 1f;
        if (Keyboard.current.sKey.isPressed) input.z -= 1f;
        if (Keyboard.current.dKey.isPressed) input.x += 1f;
        if (Keyboard.current.aKey.isPressed) input.x -= 1f;
        if (Keyboard.current.spaceKey.isPressed) input.y += 1f;
        if (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.cKey.isPressed) input.y -= 1f;

        if (input.sqrMagnitude > 0f)
        {
            float speedMultiplier = Keyboard.current.leftShiftKey.isPressed ? fastMoveMultiplier : 1f;
            Vector3 movement = cameraTransform.TransformDirection(input.normalized);
            cameraTransform.position += movement * moveSpeed * speedMultiplier * Time.unscaledDeltaTime;
        }
    }

    private void LateUpdate()
    {
        if (IsFreeFlyActive || cameraTransform == null) return;

        if (playerSwitcher == null)
        {
            playerSwitcher = PlayerSwitcher.Instance != null
                ? PlayerSwitcher.Instance
                : Object.FindAnyObjectByType<PlayerSwitcher>();
        }

        GameObject controlledCharacter = playerSwitcher != null ? playerSwitcher.GetControlledCharacter() : null;
        if (controlledCharacter == null) return;

        Transform target = controlledCharacter.transform;
        if (!hasFollowOffset)
        {
            followOffset = cameraTransform.position - target.position;
            hasFollowOffset = true;
        }

        Vector3 focusPoint = target.position + Vector3.up * lookAtHeight;
        Vector3 desiredPosition = target.position + followOffset;
        Quaternion desiredRotation = Quaternion.LookRotation(focusPoint - desiredPosition, Vector3.up);

        cameraTransform.position = Vector3.SmoothDamp(
            cameraTransform.position,
            desiredPosition,
            ref followVelocity,
            followSmoothTime);
        cameraTransform.rotation = Quaternion.Slerp(
            cameraTransform.rotation,
            desiredRotation,
            1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(followSmoothTime, 0.001f)));

        yaw = cameraTransform.eulerAngles.y;
        pitch = cameraTransform.eulerAngles.x > 180f
            ? cameraTransform.eulerAngles.x - 360f
            : cameraTransform.eulerAngles.x;
    }



    private void OnDisable()
    {
        StopLooking();
    }

    private void StopLooking()
    {
        if (!isLooking) return;

        isLooking = false;
        IsFreeFlyActive = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
