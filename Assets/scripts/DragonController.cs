using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class DragonController : MonoBehaviour
{
    [Header("Movement & Rotation Settings")]
    public float speed = 15f;
    public float rotationSpeed = 22.5f;

    [Tooltip("Adjust this if the model faces sideways or backwards while moving!")]
    public float modelRotationOffset = 90f;

    [Header("Speed Boost & Horn Settings")]
    [Tooltip("Speed applied while the dragon's boost is active.")]
    [SerializeField] private float boostSpeed = 55f;
    [Tooltip("How long the speed boost lasts, in seconds.")]
    [SerializeField] private float boostDuration = 2f;
    [Tooltip("Cooldown between boost activations, in seconds.")]
    [SerializeField] private float boostCooldown = 5f;
    [Tooltip("Damage dealt when the dragon's dash collides with the player.")]
    [SerializeField, Min(1)] private int dashDamage = 20;
    [Tooltip("Drag the collider attached specifically to the dragon's horn here!")]
    public Collider hornCollider;

    [Header("Legacy Jump & Slam Settings")]
    [SerializeField, Min(1f)] private float jumpVelocity = 12f;
    [SerializeField, Min(1f)] private float slamFallSpeed = 25f;
    [SerializeField, Min(1f)] private float groundSlamRadius = 30f;
    [SerializeField, Min(0f)] private float groundSlamKnockbackMultiplier = 2f;
    [SerializeField, Min(0.1f)] private float groundSlamShockwaveDuration = 0.65f;
    [SerializeField, Min(0.05f)] private float groundSlamShockwaveWidth = 0.7f;
    [SerializeField, Min(0.1f)] private float groundSlamCooldown = 2.5f;
    [SerializeField, Min(0.05f)] private float groundCheckDistance = 0.3f;
    private const float GroundCheckStartOffset = 0.08f;
    private const float GroundCheckFallbackHeight = 0.5f;
    private const float SlamImpactGroundOffset = 0.05f;
    private const float MinimumAscendingVelocity = 0.1f;
    private const int GroundSlamWaveSegmentCount = 64;
    private static readonly Color GroundSlamWaveColor = new Color(0.2f, 1f, 0.15f, 1f);

    private const int DashHitBufferSize = 64;
    private const float DashSweepPadding = 0.25f;
    private readonly Collider[] dashHitBuffer = new Collider[DashHitBufferSize];
    private readonly HashSet<FighterController> targetsHitThisDash = new HashSet<FighterController>();
    private readonly HashSet<FighterController> targetsHitDirectlyThisSlam = new HashSet<FighterController>();
    private readonly RaycastHit[] groundCheckHits = new RaycastHit[16];

    private Rigidbody rb;
    private Animator animator;
    private FighterController fighter;
    private Collider bodyCollider;
    private bool hasIsWalkingParam;
    private PlayerController mainGameController;
    private bool isCurrentlyWalking;
    private bool isSpeedBoosting;
    private bool isGroundSlamming;
    private float nextBoostAvailableTime;
    private float nextGroundSlamAvailableTime;

    public float BoostCooldownRemaining => Mathf.Max(0f, nextBoostAvailableTime - Time.time);
    public float BoostCooldownProgress => boostCooldown <= 0f
        ? 1f
        : Mathf.Clamp01(1f - BoostCooldownRemaining / boostCooldown);
    public bool IsBoosting => isSpeedBoosting;
    public bool IsGroundSlamming => isGroundSlamming;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        fighter = GetComponent<FighterController>();
        bodyCollider = GetComponent<Collider>();
        mainGameController = Object.FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);

        if (hornCollider != null && hornCollider != GetComponent<Collider>())
        {
            hornCollider.enabled = false;
        }

        if (animator != null)
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.name == "isWalking")
                {
                    hasIsWalkingParam = true;
                    break;
                }
            }
        }
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        if (PlayerSwitcher.IsDragonActive &&
            !CameraMan.IsFreeFlyActive &&
            Keyboard.current.eKey.wasPressedThisFrame &&
            !isSpeedBoosting &&
            !(fighter != null && (fighter.IsSpinComboReady || fighter.IsSpinAttacking)) &&
            Time.time >= nextBoostAvailableTime)
        {
            StartCoroutine(PerformSpeedBoost());
        }

        if (!PlayerSwitcher.IsDragonActive || CameraMan.IsFreeFlyActive ||
            (fighter != null && (fighter.IsDead || fighter.IsBeingKnockedBack)))
        {
            return;
        }


    }

    private IEnumerator PerformSpeedBoost()
    {
        isSpeedBoosting = true;
        targetsHitThisDash.Clear();
        nextBoostAvailableTime = Time.time + boostCooldown;
        float originalSpeed = speed;
        speed = boostSpeed;

        if (hornCollider != null && hornCollider != GetComponent<Collider>())
        {
            hornCollider.enabled = true;
        }

        yield return new WaitForSeconds(boostDuration);

        if (hornCollider != null && hornCollider != GetComponent<Collider>())
        {
            hornCollider.enabled = false;
        }

        speed = originalSpeed;
        isSpeedBoosting = false;
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        if (Keyboard.current == null) return;

        if (!PlayerSwitcher.IsDragonActive || CameraMan.IsFreeFlyActive)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            return;
        }

        if (fighter != null && fighter.IsBeingKnockedBack)
        {
            if (hasIsWalkingParam && animator != null)
            {
                animator.SetBool("isWalking", false);
            }
            isCurrentlyWalking = false;
            return;
        }

        if (isSpeedBoosting)
        {
            ApplyDashOverlapDamage();
        }

        float moveX = 0f;
        float moveZ = 0f;

        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) moveX -= 1f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) moveX += 1f;
        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) moveZ += 1f;
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) moveZ -= 1f;

        Vector3 moveDirection = new Vector3(moveX, 0f, moveZ).normalized;
        if (moveDirection.magnitude > 0.1f)
        {
            Vector3 linearVelocity = moveDirection * speed;
            linearVelocity.y = rb.linearVelocity.y;
            rb.linearVelocity = linearVelocity;

            Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up) * Quaternion.Euler(0f, modelRotationOffset, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);

            if (hasIsWalkingParam && !isCurrentlyWalking)
            {
                isCurrentlyWalking = true;
                animator.SetBool("isWalking", true);
            }
        }
        else
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);

            if (hasIsWalkingParam && isCurrentlyWalking)
            {
                isCurrentlyWalking = false;
                animator.SetBool("isWalking", false);
            }
        }
    }

    private void BeginJump()
    {
        if (rb == null) return;

        Vector3 jumpVelocityVector = rb.linearVelocity;
        jumpVelocityVector.y = jumpVelocity;
        rb.linearVelocity = jumpVelocityVector;
        TriggerAnimationIfPresent("Jump");
    }

    private void BeginGroundSlam()
    {
        if (rb == null) return;

        isGroundSlamming = true;
        targetsHitDirectlyThisSlam.Clear();
        nextGroundSlamAvailableTime = Time.time + groundSlamCooldown;
        Vector3 slamVelocity = rb.linearVelocity;
        slamVelocity.y = -slamFallSpeed;
        rb.linearVelocity = slamVelocity;
        TriggerSlamAnimation();
    }

    private bool IsGrounded(out RaycastHit groundHit)
    {
        groundHit = default;
        if (rb == null || rb.linearVelocity.y > MinimumAscendingVelocity) return false;

        Vector3 rayOrigin = bodyCollider != null
            ? bodyCollider.bounds.min + Vector3.up * GroundCheckStartOffset
            : transform.position + Vector3.up * GroundCheckFallbackHeight;
        float rayDistance = groundCheckDistance + GroundCheckStartOffset;
        int hitCount = Physics.RaycastNonAlloc(
            rayOrigin,
            Vector3.down,
            groundCheckHits,
            rayDistance,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore);

        float nearestGroundDistance = float.MaxValue;
        bool foundGround = false;
        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            RaycastHit candidateHit = groundCheckHits[hitIndex];
            if (candidateHit.collider == null ||
                candidateHit.collider.transform == transform ||
                candidateHit.collider.transform.IsChildOf(transform) ||
                candidateHit.distance >= nearestGroundDistance)
            {
                continue;
            }

            nearestGroundDistance = candidateHit.distance;
            groundHit = candidateHit;
            foundGround = true;
        }

        return foundGround;
    }

    private void ResolveGroundSlam(Vector3 groundPoint)
    {
        EliminateDirectSlamTargetsOverlappingBody();
        if (rb != null)
        {
            Vector3 landedVelocity = rb.linearVelocity;
            landedVelocity.y = 0f;
            rb.linearVelocity = landedVelocity;
        }

        Vector3 blastOrigin = groundPoint + Vector3.up * SlamImpactGroundOffset;
        StartCoroutine(AnimateGroundSlamShockwave(blastOrigin));

        Collider[] overlappingColliders = Physics.OverlapSphere(
            blastOrigin,
            groundSlamRadius,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore);
        HashSet<Rigidbody> affectedBodies = new HashSet<Rigidbody>();
        HashSet<FighterController> affectedFighters = new HashSet<FighterController>();

        foreach (Collider overlappingCollider in overlappingColliders)
        {
            if (overlappingCollider == null || overlappingCollider.transform.IsChildOf(transform)) continue;

            FighterController target = overlappingCollider.GetComponentInParent<FighterController>();
            if (target != null)
            {
                if (target == fighter || target.IsDead || targetsHitDirectlyThisSlam.Contains(target)) continue;
                if (!affectedFighters.Add(target)) continue;

                Vector3 knockbackVelocity = GetSlamKnockbackVelocity(target.transform.position, blastOrigin);
                target.ApplyKnockback(knockbackVelocity);
                Rigidbody fighterBody = target.GetComponent<Rigidbody>();
                if (fighterBody != null) affectedBodies.Add(fighterBody);
                continue;
            }

            Rigidbody targetBody = overlappingCollider.attachedRigidbody;
            if (targetBody == null || targetBody.isKinematic || !affectedBodies.Add(targetBody)) continue;

            Vector3 propKnockback = GetSlamKnockbackVelocity(targetBody.worldCenterOfMass, blastOrigin);
            targetBody.linearVelocity = new Vector3(propKnockback.x, targetBody.linearVelocity.y, propKnockback.z);
        }

        Debug.Log($"Ground slam hit {affectedFighters.Count} fighters with the shockwave within {groundSlamRadius:0} units.", this);
    }

    private void EliminateDirectSlamTargetsOverlappingBody()
    {
        if (bodyCollider == null) return;

        Bounds bodyBounds = bodyCollider.bounds;
        Collider[] directHitColliders = Physics.OverlapBox(
            bodyBounds.center,
            bodyBounds.extents,
            Quaternion.identity,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore);

        foreach (Collider directHitCollider in directHitColliders)
        {
            TryApplyGroundSlamDirectHit(directHitCollider);
        }
    }

    private void TryApplyGroundSlamDirectHit(Collider otherCollider)
    {
        if (!isGroundSlamming || otherCollider == null || otherCollider.transform.IsChildOf(transform)) return;

        FighterController target = otherCollider.GetComponentInParent<FighterController>();
        if (target == null || target == fighter || target.IsDead || !targetsHitDirectlyThisSlam.Add(target)) return;

        target.EliminateByMeteor(GetSlamKnockbackVelocity(target.transform.position, transform.position));
    }

    private IEnumerator AnimateGroundSlamShockwave(Vector3 shockwaveOrigin)
    {
        GameObject shockwaveObject = new GameObject("Ground Slam Green Shockwave");
        LineRenderer shockwave = shockwaveObject.AddComponent<LineRenderer>();
        shockwave.useWorldSpace = true;
        shockwave.loop = true;
        shockwave.positionCount = GroundSlamWaveSegmentCount;
        shockwave.numCapVertices = 4;
        shockwave.numCornerVertices = 4;
        shockwave.alignment = LineAlignment.View;
        shockwave.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        shockwave.receiveShadows = false;

        Shader shockwaveShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shockwaveShader == null) shockwaveShader = Shader.Find("Sprites/Default");
        if (shockwaveShader == null) shockwaveShader = Shader.Find("Unlit/Color");
        if (shockwaveShader == null)
        {
            Destroy(shockwaveObject);
            yield break;
        }

        Material shockwaveMaterial = new Material(shockwaveShader)
        {
            name = "Ground Slam Green Shockwave"
        };
        if (shockwaveMaterial.HasProperty("_Surface"))
        {
            shockwaveMaterial.SetFloat("_Surface", 1f);
            shockwaveMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
        if (shockwaveMaterial.HasProperty("_SrcBlend"))
        {
            shockwaveMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        }
        if (shockwaveMaterial.HasProperty("_DstBlend"))
        {
            shockwaveMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }
        if (shockwaveMaterial.HasProperty("_ZWrite")) shockwaveMaterial.SetInt("_ZWrite", 0);
        shockwaveMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        shockwave.sharedMaterial = shockwaveMaterial;
        shockwave.startWidth = groundSlamShockwaveWidth;
        shockwave.endWidth = groundSlamShockwaveWidth;
        shockwave.startColor = GroundSlamWaveColor;
        shockwave.endColor = GroundSlamWaveColor;

        float duration = Mathf.Max(0.1f, groundSlamShockwaveDuration);
        float startTime = Time.time;
        while (Time.time - startTime < duration)
        {
            float progress = Mathf.Clamp01((Time.time - startTime) / duration);
            float radius = Mathf.Lerp(0.1f, groundSlamRadius, progress);
            Color fadingColor = GroundSlamWaveColor;
            fadingColor.a = 1f - progress;
            shockwave.startColor = fadingColor;
            shockwave.endColor = fadingColor;
            shockwave.widthMultiplier = Mathf.Lerp(1f, 0.25f, progress);
            if (shockwaveMaterial.HasProperty("_BaseColor")) shockwaveMaterial.SetColor("_BaseColor", fadingColor);
            if (shockwaveMaterial.HasProperty("_Color")) shockwaveMaterial.SetColor("_Color", fadingColor);

            for (int segmentIndex = 0; segmentIndex < GroundSlamWaveSegmentCount; segmentIndex++)
            {
                float angle = segmentIndex * Mathf.PI * 2f / GroundSlamWaveSegmentCount;
                Vector3 ringPoint = shockwaveOrigin + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                shockwave.SetPosition(segmentIndex, ringPoint);
            }

            yield return null;
        }

        Destroy(shockwaveObject);
        Destroy(shockwaveMaterial);
    }

    private Vector3 GetSlamKnockbackVelocity(Vector3 targetPosition, Vector3 blastOrigin)
    {
        Vector3 outwardDirection = targetPosition - blastOrigin;
        outwardDirection.y = 0f;
        if (outwardDirection.sqrMagnitude <= 0.001f)
        {
            outwardDirection = transform.forward;
        }

        float knockbackSpeed = FighterController.RegularKnockbackSpeed * groundSlamKnockbackMultiplier;
        return outwardDirection.normalized * knockbackSpeed;
    }

    private void TriggerSlamAnimation()
    {
        if (TriggerAnimationIfPresent("GroundSlam")) return;
        if (TriggerAnimationIfPresent("Slam")) return;
        TriggerAnimationIfPresent("Attack");
    }

    private bool TriggerAnimationIfPresent(string triggerName)
    {
        if (animator == null) return false;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == triggerName && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                animator.SetTrigger(triggerName);
                return true;
            }
        }

        return false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.gameObject.CompareTag("pickup 1")) return;

        other.gameObject.SetActive(false);

        if (mainGameController == null)
        {
            mainGameController = Object.FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
        }

        if (mainGameController != null)
        {
            mainGameController.AddPickupScore(1);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryApplyGroundSlamDirectHit(collision.collider);
        TryApplyDashDamage(collision.collider);
    }

    private void OnCollisionStay(Collision collision)
    {
        TryApplyGroundSlamDirectHit(collision.collider);
        TryApplyDashDamage(collision.collider);
    }

    private void TryApplyDashDamage(Collider otherCollider)
    {
        if (!isSpeedBoosting || otherCollider == null) return;

        FighterController target = otherCollider.GetComponentInParent<FighterController>();
        TryApplyDashDamage(target);
    }

    private void ApplyDashOverlapDamage()
    {
        if (bodyCollider == null || fighter == null || fighter.IsDead) return;

        Bounds bodyBounds = bodyCollider.bounds;
        Vector3 dashSweep = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z) * Time.fixedDeltaTime;
        Vector3 overlapCenter = bodyBounds.center + dashSweep * 0.5f;
        Vector3 overlapExtents = bodyBounds.extents + new Vector3(
            Mathf.Abs(dashSweep.x) * 0.5f + DashSweepPadding,
            DashSweepPadding,
            Mathf.Abs(dashSweep.z) * 0.5f + DashSweepPadding);

        int hitCount = Physics.OverlapBoxNonAlloc(
            overlapCenter,
            overlapExtents,
            dashHitBuffer,
            Quaternion.identity,
            Physics.AllLayers,
            QueryTriggerInteraction.Collide);

        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            Collider hitCollider = dashHitBuffer[hitIndex];
            if (hitCollider == null || hitCollider.transform == transform || hitCollider.transform.IsChildOf(transform)) continue;

            FighterController target = hitCollider.GetComponentInParent<FighterController>();
            TryApplyDashDamage(target);
        }
    }

    private void TryApplyDashDamage(FighterController target)
    {
        if (!isSpeedBoosting || !PlayerSwitcher.IsDragonActive || target == null ||
            target == fighter || target.IsDead || target.playerID < 3 || !targetsHitThisDash.Add(target))
        {
            return;
        }

        int targetHealthBeforeHit = target.currentHealth;
        target.TakeDamage(dashDamage, fighter);
        int damageDealt = targetHealthBeforeHit - target.currentHealth;
        fighter?.AddBurstChargeFromDamage(damageDealt);
        if (damageDealt > 0)
        {
            PlayerSwitcher.Instance?.NotifyTutorialAttackHit(true);
            Debug.Log($"Dragon E dash hit {target.name} for {damageDealt} damage.", this);
        }
        else
        {
            Debug.Log($"Dragon E dash contacted {target.name}, but no damage was applied.", this);
        }
    }
}
