using UnityEngine;

/// <summary>
/// Pursues the currently controlled character and periodically attacks within range.
/// </summary>
public class EnemyPursuitController : MonoBehaviour
{
    [Min(0.1f)] public float moveSpeed = 5.25f;
    [Min(0.1f)] public float attackRange = 1.7f;
    [Min(0.1f)] public float attackCooldown = 1.4f;
    [Min(1)] public int damagePerHit = 5;
    [Min(0f)] public float rotationSpeed = 8f;
    [Min(0f)] public float modelRotationOffset = 90f;

    private Rigidbody body;
    private Animator animator;
    private FighterController fighter;
    private PlayerSwitcher playerSwitcher;
    private float nextAttackTime;
    private bool hasWalkingParameter;

    private void Start()
    {
        body = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        fighter = GetComponent<FighterController>();
        playerSwitcher = PlayerSwitcher.Instance;

        if (animator == null) return;
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == "isWalking" && parameter.type == AnimatorControllerParameterType.Bool)
            {
                hasWalkingParameter = true;
                break;
            }
        }
    }

    private void FixedUpdate()
    {
        if (body == null || CameraMan.IsFreeFlyActive || fighter == null || fighter.currentHealth <= 0)
        {
            StopMoving();
            return;
        }

        if (fighter.IsBeingKnockedBack)
        {
            SetWalking(false);
            return;
        }

        GameObject targetObject = GetTargetObject();
        if (targetObject == null) targetObject = playerSwitcher != null ? playerSwitcher.character2 : null;
        if (targetObject == null)
        {
            StopMoving();
            return;
        }

        FighterController targetFighter = targetObject.GetComponent<FighterController>();
        if (targetFighter == null || targetFighter.currentHealth <= 0)
        {
            StopMoving();
            return;
        }

        Vector3 direction = targetObject.transform.position - transform.position;
        direction.y = 0f;
        float stoppingDistance = Mathf.Max(attackRange * 0.8f, 0.5f);
        if (direction.sqrMagnitude <= stoppingDistance * stoppingDistance)
        {
            StopMoving();
            FaceTarget(direction);
            return;
        }

        Vector3 velocity = direction.normalized * moveSpeed;
        velocity.y = body.linearVelocity.y;
        body.linearVelocity = velocity;
        FaceTarget(direction);
        SetWalking(true);
    }

    private void Update()
    {
        if (CameraMan.IsFreeFlyActive || fighter == null || fighter.currentHealth <= 0 || Time.time < nextAttackTime)
        {
            return;
        }

        GameObject targetObject = GetTargetObject();
        if (targetObject == null) targetObject = playerSwitcher != null ? playerSwitcher.character2 : null;
        if (targetObject == null) return;

        FighterController targetFighter = targetObject.GetComponent<FighterController>();
        if (targetFighter == null || targetFighter.currentHealth <= 0) return;

        Vector3 toTarget = targetObject.transform.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude > attackRange * attackRange) return;

        nextAttackTime = Time.time + attackCooldown;
        targetFighter.TakeDamage(damagePerHit, fighter);
        TriggerAttackAnimation();
    }

    private GameObject GetTargetObject()
    {
        if (playerSwitcher == null) playerSwitcher = PlayerSwitcher.Instance;
        return playerSwitcher != null ? playerSwitcher.GetControlledCharacter() : null;
    }

    private void FaceTarget(Vector3 direction)
    {
        if (direction.sqrMagnitude <= 0.001f) return;
        Quaternion baseRotation = Quaternion.LookRotation(direction, Vector3.up);
        Quaternion desiredRotation = baseRotation * Quaternion.Euler(0f, modelRotationOffset, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationSpeed * Time.fixedDeltaTime);
    }

    private void StopMoving()
    {
        if (body != null)
        {
            body.linearVelocity = new Vector3(0f, body.linearVelocity.y, 0f);
        }
        SetWalking(false);
    }

    private void SetWalking(bool isWalking)
    {
        if (hasWalkingParameter && animator != null)
        {
            animator.SetBool("isWalking", isWalking);
        }
    }

    private void TriggerAttackAnimation()
    {
        if (animator == null) return;
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Trigger &&
                (parameter.name == "LightAttack" || parameter.name == "Attack"))
            {
                animator.SetTrigger(parameter.name);
                return;
            }
        }
    }
}
