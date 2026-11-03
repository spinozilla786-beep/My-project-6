using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles the character's attack, health, and knockout state.
/// </summary>
public class FighterController : MonoBehaviour
{
    [Header("Player Identifier")]
    [Tooltip("Use 1 for the player and 2 for the dragon.")]
    public int playerID = 1;

    [Header("Health")]
    public int maxHealth = 100;
    public int currentHealth = 100;

    [Header("Attack Settings")]
    [Tooltip("Optional point from which the attack is checked. Uses character forward if empty.")]
    public Transform attackPoint;
    [Min(0.1f)] public float attackRange = 2.8f;
    [Min(0f)] public float attackForwardOffset = 1.8f;
    [Min(0f)] public float attackHeightOffset = 0.7f;
    [Min(0.1f)] public float attackCooldown = 0.55f;
    [Min(1)] public int damagePerHit = 20;
    [SerializeField] private TMP_Text damageFeedbackText;
    [SerializeField, Min(0.1f)] private float damageFeedbackDuration = 1.2f;

    [Header("Burst Attack")]
    [SerializeField, Min(1f)] private float burstAttackRadius = 30f;
    [SerializeField, Min(0f)] private float burstChargePerSecond = 1f;
    [Min(0f)] public float burstChargePerDamage = 1.5f;
    [SerializeField, Min(1f)] private float burstKnockbackVelocity = 10f;
    [SerializeField, Range(0.05f, 1f)] private float clawHitWindowDuration = 0.3f;
    [SerializeField, Min(0.1f)] private float burstAttackCooldown = 1f;
    private const float BurstChargeRequired = 100f;
    private const int BurstHealthLimit = 30;
    private const float HitKnockbackSpeed = 10f;
    private const float KnockbackControlLockSeconds = 0.25f;
    private const int ShieldRingSegmentCount = 64;
    private const float ShieldRingWidth = 0.75f;
    private const float ShieldRingPulseRadius = 0.35f;
    private const float ShieldGroundOffset = 0.12f;
    private const float ShieldRadiusMinimum = 7f;
    private const float ShieldRadiusMaximum = 14f;
    private const float ShieldRadiusMargin = 1.5f;
    private static readonly Color BlockShieldColor = new Color32(38, 132, 255, 245);

    [Header("Jump, Slam & Combo Abilities")]
    [SerializeField, Min(1f)] private float jumpVelocity = 18f;
    [SerializeField, Min(1f)] private float slamFallSpeed = 25f;
    [SerializeField, Min(1f)] private float groundSlamRadius = 30f;
    [SerializeField, Min(0f)] private float groundSlamKnockbackMultiplier = 2f;
    [SerializeField, Min(0.1f)] private float groundSlamShockwaveDuration = 0.65f;
    [SerializeField, Min(0.05f)] private float groundSlamShockwaveWidth = 0.7f;
    [SerializeField, Min(0.1f)] private float groundSlamCooldown = 2.5f;
    [SerializeField, Min(0.05f)] private float groundCheckDistance = 0.3f;
    [SerializeField, Min(0.1f)] private float fourPressComboWindow = 5f;
    [SerializeField, Min(0.1f)] private float spinAttackDuration = 0.8f;
    [SerializeField, Min(0.1f)] private float airDashSpeed = 32f;
    [SerializeField, Min(0.1f)] private float airDashDuration = 0.45f;
    [SerializeField, Min(0.1f)] private float airDashCooldown = 1.5f;
    [SerializeField, Min(1f)] private float airDashTargetRange = 30f;
    [SerializeField, Min(0.1f)] private float godModeDuration = 15f;
    private const int GodModeHealth = 1000;
    private const int GodModeActivationHealth = 1;
    private const int GroundSlamWaveSegmentCount = 64;
    private const float GroundCheckStartOffset = 0.08f;
    private const float MinimumAscendingVelocity = 0.1f;
    private static readonly Color GroundSlamWaveColor = new Color(0.2f, 1f, 0.15f, 1f);

    [Header("Death Settings")]
    [SerializeField, Min(0f)] private float deathSpinSpeedDegrees = 720f;
    [SerializeField, Min(0f)] private float deathLabelHeight = 2f;
    [SerializeField, Range(0.1f, 0.5f)] private float meteorSquashDurationSeconds = 0.2f;

    public const float MeteorDeathDurationSeconds = 5f;
    private const float MeteorSquashHeightScale = 0.18f;
    private const float MeteorSquashWidthScale = 1.3f;

    [Header("State Flags")]
    public bool isBlocking;
    public bool isAttacking;

    private Animator anim;
    private Rigidbody body;
    private Collider bodyCollider;
    private float nextAttackTime;
    private bool isDead;
    private bool isJumping;
    private bool isGroundSlamming;
    private bool isAirDashing;
    private bool isSpinAttacking;
    private bool isGodModeActive;
    private float nextGroundSlamAvailableTime;
    private float nextAirDashAvailableTime;
    private float fourPressComboExpiresAt;
    private float airDashEndsAt;
    private int fourPressComboCount;
    private Vector3 airDashDirection;
    private CollisionDetectionMode originalCollisionDetectionMode;
    private float deathSpinAngle;
    private float deathStartedAt;
    private bool isMeteorDeath;
    private Vector3 deathPosition;
    private Vector3 deathKnockbackVelocity;
    private Vector3 deathStartingScale;
    private Quaternion deathStartingRotation;
    private float burstCharge;
    private Canvas deathLabelCanvas;
    private Coroutine damageFeedbackCoroutine;
    private Coroutine knockbackControlCoroutine;
    private LineRenderer blockShieldRing;
    private Material blockShieldMaterial;
    private Transform groundTransform;

    private readonly RaycastHit[] groundCheckHits = new RaycastHit[16];
    private readonly HashSet<FighterController> targetsHitThisAirDash = new HashSet<FighterController>();
    private readonly HashSet<FighterController> targetsHitThisSpin = new HashSet<FighterController>();

    public bool IsDead => isDead;
    public bool IsMeteorDeath => isMeteorDeath;
    public bool IsBeingKnockedBack { get; private set; }
    public bool IsJumping => isJumping;
    public bool IsSpinComboReady => fourPressComboCount >= 3 && Time.time <= fourPressComboExpiresAt;
    public bool IsSpinAttacking => isSpinAttacking;
    public bool IsGodModeActive => isGodModeActive;
    /// <summary>
    /// Returns the horizontal speed used by standard hit knockback.
    /// </summary>
    public static float RegularKnockbackSpeed => HitKnockbackSpeed;
    public float AttackCooldownRemaining => Mathf.Max(0f, nextAttackTime - Time.time);
    public float AttackCooldownProgress => attackCooldown <= 0f
        ? 1f
        : Mathf.Clamp01(1f - AttackCooldownRemaining / attackCooldown);
    public float HealthProgress => maxHealth <= 0 ? 0f : Mathf.Clamp01((float)currentHealth / maxHealth);
    public float BurstChargeProgress => Mathf.Clamp01(burstCharge / BurstChargeRequired);
    public bool CanUseBurstAttack => !isDead && currentHealth <= BurstHealthLimit && burstCharge >= BurstChargeRequired;
    public float BurstChargePercent => BurstChargeProgress * 100f;

    private void Start()
    {
        currentHealth = maxHealth;
        isDead = false;
        anim = GetComponent<Animator>();
        body = GetComponent<Rigidbody>();
        if (body != null && !body.isKinematic)
        {
            body.useGravity = true;
        }
        bodyCollider = GetComponent<Collider>();
        InitializeBlockShield();

        if (damageFeedbackText != null)
        {
            damageFeedbackText.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (isDead)
        {
            UpdateDeathSpin();
            UpdateDeathLabel();
            if (playerID >= 3 && Time.unscaledTime - deathStartedAt >= MeteorDeathDurationSeconds)
            {
                Destroy(gameObject);
            }
            return;
        }

        if (IsThisCharacterSelected())
        {
            burstCharge = Mathf.Min(BurstChargeRequired, burstCharge + burstChargePerSecond * Time.deltaTime);
        }

        if (playerID == 2)
        {
            isBlocking = Keyboard.current != null &&
                PlayerSwitcher.IsDragonActive &&
                !CameraMan.IsFreeFlyActive &&
                Keyboard.current.digit5Key.isPressed;
        }

        if (Keyboard.current == null || CameraMan.IsFreeFlyActive || !IsThisCharacterSelected()) return;

        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            ExecuteBurstAttack();
        }

        if (Keyboard.current.spaceKey.wasPressedThisFrame && !isJumping && !isGroundSlamming && !isAirDashing && IsGrounded())
        {
            BeginJump();
        }

        bool slamPressed = Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame;
        if (slamPressed && isJumping && !isGroundSlamming && Time.time >= nextGroundSlamAvailableTime)
        {
            BeginGroundSlam();
        }

        if (Keyboard.current.digit4Key.wasPressedThisFrame || Keyboard.current.numpad4Key.wasPressedThisFrame)
        {
            RegisterFourPressForCombo();
            if (isJumping && !isGroundSlamming)
            {
                BeginAirDash();
            }
            else if (playerID == 2 && !isAttacking)
            {
                ExecuteAttack("Attack");
            }
        }

        if (Keyboard.current.eKey.wasPressedThisFrame && IsSpinComboReady)
        {
            StartCoroutine(PerformSpinAttack());
            return;
        }

        if (Keyboard.current.uKey.wasPressedThisFrame && currentHealth == GodModeActivationHealth && !isGodModeActive)
        {
            StartCoroutine(PerformGodMode());
            return;
        }

        if (isAttacking) return;

        if (playerID == 1 && (Keyboard.current.fKey.wasPressedThisFrame || Keyboard.current.gKey.wasPressedThisFrame))
        {
            ExecuteAttack(Keyboard.current.gKey.wasPressedThisFrame ? "HeavyAttack" : "LightAttack");
        }
    }

    private void FixedUpdate()
    {
        if (body == null || isDead) return;

        if (isAirDashing)
        {
            if (Time.time >= airDashEndsAt)
            {
                EndAirDash();
            }
            else
            {
                body.linearVelocity = airDashDirection * airDashSpeed;
            }
        }

        if (isGroundSlamming && IsGrounded())
        {
            ResolveGroundSlam();
        }
        else if (isJumping && !isGroundSlamming && body.linearVelocity.y <= 0f && IsGrounded())
        {
            isJumping = false;
        }
    }

    private void RegisterFourPressForCombo()
    {
        if (Time.time > fourPressComboExpiresAt)
        {
            fourPressComboCount = 0;
        }

        fourPressComboCount = Mathf.Min(3, fourPressComboCount + 1);
        fourPressComboExpiresAt = Time.time + fourPressComboWindow;
    }

    private void BeginJump()
    {
        if (body == null) return;

        isJumping = true;
        Vector3 velocity = body.linearVelocity;
        velocity.y = jumpVelocity;
        body.linearVelocity = velocity;
        SetAnimationTriggerIfPresent("Jump");
    }

    private void BeginGroundSlam()
    {
        if (body == null) return;

        isGroundSlamming = true;
        nextGroundSlamAvailableTime = Time.time + groundSlamCooldown;
        Vector3 velocity = body.linearVelocity;
        velocity.y = -slamFallSpeed;
        body.linearVelocity = velocity;
        if (!SetAnimationTriggerIfPresent("GroundSlam") && !SetAnimationTriggerIfPresent("Slam"))
        {
            SetAnimationTriggerIfPresent("Attack");
        }
    }

    private bool IsGrounded()
    {
        if (body == null || body.linearVelocity.y > MinimumAscendingVelocity) return false;

        Vector3 rayOrigin = bodyCollider != null
            ? bodyCollider.bounds.min + Vector3.up * GroundCheckStartOffset
            : transform.position + Vector3.up * 0.5f;
        int hitCount = Physics.RaycastNonAlloc(
            rayOrigin,
            Vector3.down,
            groundCheckHits,
            groundCheckDistance + GroundCheckStartOffset,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore);

        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            Collider hitCollider = groundCheckHits[hitIndex].collider;
            if (hitCollider != null && hitCollider.transform != transform && !hitCollider.transform.IsChildOf(transform))
            {
                return true;
            }
        }

        return false;
    }

    private void ResolveGroundSlam()
    {
        ApplyDirectSlamHitsInBodyBounds();
        isGroundSlamming = false;
        isJumping = false;
        Vector3 landedVelocity = body.linearVelocity;
        landedVelocity.y = 0f;
        body.linearVelocity = landedVelocity;

        float groundY = bodyCollider != null ? bodyCollider.bounds.min.y : transform.position.y;
        Vector3 shockwaveOrigin = new Vector3(transform.position.x, groundY + 0.05f, transform.position.z);
        StartCoroutine(AnimateGroundSlamShockwave(shockwaveOrigin));

        Collider[] hitColliders = Physics.OverlapSphere(
            shockwaveOrigin,
            groundSlamRadius,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore);
        HashSet<FighterController> hitFighters = new HashSet<FighterController>();
        HashSet<Rigidbody> hitBodies = new HashSet<Rigidbody>();
        foreach (Collider hitCollider in hitColliders)
        {
            if (hitCollider == null || hitCollider.transform.IsChildOf(transform)) continue;

            FighterController target = hitCollider.GetComponentInParent<FighterController>();
            if (target != null)
            {
                if (target == this || target.IsDead || !hitFighters.Add(target)) continue;
                target.ApplyKnockback(GetSlamKnockbackVelocity(target.transform.position, shockwaveOrigin));
                Rigidbody fighterBody = target.GetComponent<Rigidbody>();
                if (fighterBody != null) hitBodies.Add(fighterBody);
                continue;
            }

            Rigidbody propBody = hitCollider.attachedRigidbody;
            if (propBody == null || propBody.isKinematic || !hitBodies.Add(propBody)) continue;
            Vector3 propVelocity = GetSlamKnockbackVelocity(propBody.worldCenterOfMass, shockwaveOrigin);
            propBody.linearVelocity = new Vector3(propVelocity.x, propBody.linearVelocity.y, propVelocity.z);
        }
    }

    private void ApplyDirectSlamHitsInBodyBounds()
    {
        if (bodyCollider == null) return;

        Bounds bodyBounds = bodyCollider.bounds;
        Collider[] touchingColliders = Physics.OverlapBox(
            bodyBounds.center,
            bodyBounds.extents,
            Quaternion.identity,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore);
        foreach (Collider touchingCollider in touchingColliders)
        {
            TryApplyDirectSlamHit(touchingCollider);
        }
    }

    private void TryApplyDirectSlamHit(Collider otherCollider)
    {
        if (!isGroundSlamming || otherCollider == null || otherCollider.transform.IsChildOf(transform)) return;

        FighterController target = otherCollider.GetComponentInParent<FighterController>();
        if (target == null || target == this || target.IsDead) return;
        target.EliminateByMeteor(GetSlamKnockbackVelocity(target.transform.position, transform.position));
    }

    private Vector3 GetSlamKnockbackVelocity(Vector3 targetPosition, Vector3 blastOrigin)
    {
        Vector3 direction = targetPosition - blastOrigin;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.001f) direction = transform.forward;
        return direction.normalized * (RegularKnockbackSpeed * groundSlamKnockbackMultiplier);
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

        Shader waveShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (waveShader == null) waveShader = Shader.Find("Sprites/Default");
        if (waveShader == null) waveShader = Shader.Find("Unlit/Color");
        if (waveShader == null)
        {
            Destroy(shockwaveObject);
            yield break;
        }

        Material waveMaterial = new Material(waveShader) { name = "Ground Slam Green Shockwave" };
        waveMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        if (waveMaterial.HasProperty("_Surface"))
        {
            waveMaterial.SetFloat("_Surface", 1f);
            waveMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
        if (waveMaterial.HasProperty("_SrcBlend"))
        {
            waveMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        }
        if (waveMaterial.HasProperty("_DstBlend"))
        {
            waveMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }
        if (waveMaterial.HasProperty("_ZWrite")) waveMaterial.SetInt("_ZWrite", 0);
        shockwave.sharedMaterial = waveMaterial;
        shockwave.startWidth = groundSlamShockwaveWidth;
        shockwave.endWidth = groundSlamShockwaveWidth;

        float duration = Mathf.Max(0.1f, groundSlamShockwaveDuration);
        float startTime = Time.time;
        while (Time.time - startTime < duration)
        {
            float progress = Mathf.Clamp01((Time.time - startTime) / duration);
            float radius = Mathf.Lerp(0.1f, groundSlamRadius, progress);
            Color waveColor = GroundSlamWaveColor;
            waveColor.a = 1f - progress;
            shockwave.startColor = waveColor;
            shockwave.endColor = waveColor;
            shockwave.widthMultiplier = Mathf.Lerp(1f, 0.25f, progress);
            if (waveMaterial.HasProperty("_BaseColor")) waveMaterial.SetColor("_BaseColor", waveColor);
            if (waveMaterial.HasProperty("_Color")) waveMaterial.SetColor("_Color", waveColor);

            for (int segmentIndex = 0; segmentIndex < GroundSlamWaveSegmentCount; segmentIndex++)
            {
                float angle = segmentIndex * Mathf.PI * 2f / GroundSlamWaveSegmentCount;
                shockwave.SetPosition(segmentIndex, shockwaveOrigin + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }

            yield return null;
        }

        Destroy(shockwaveObject);
        Destroy(waveMaterial);
    }

    private void BeginAirDash()
    {
        if (body == null || Time.time < nextAirDashAvailableTime) return;

        airDashDirection = FindAirDashDirection();
        airDashEndsAt = Time.time + airDashDuration;
        nextAirDashAvailableTime = Time.time + airDashCooldown;
        targetsHitThisAirDash.Clear();
        originalCollisionDetectionMode = body.collisionDetectionMode;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.linearVelocity = airDashDirection * airDashSpeed;
        isAirDashing = true;
    }

    private Vector3 FindAirDashDirection()
    {
        Vector3 forward = transform.forward;
        DragonController dragonController = GetComponent<DragonController>();
        if (dragonController != null)
        {
            forward = Quaternion.Euler(0f, -dragonController.modelRotationOffset, 0f) * forward;
        }
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;

        FighterController[] fighters = Object.FindObjectsByType<FighterController>();
        FighterController nearestTarget = null;
        float nearestDistance = airDashTargetRange;
        foreach (FighterController candidate in fighters)
        {
            if (candidate == null || candidate == this || candidate.IsDead) continue;
            Vector3 toCandidate = candidate.transform.position - transform.position;
            float distance = toCandidate.magnitude;
            if (distance >= nearestDistance) continue;
            Vector3 horizontalDirection = toCandidate;
            horizontalDirection.y = 0f;
            if (horizontalDirection.sqrMagnitude <= 0.001f || Vector3.Dot(forward, horizontalDirection.normalized) < 0.15f) continue;
            nearestDistance = distance;
            nearestTarget = candidate;
        }

        if (nearestTarget == null) return forward;
        Vector3 targetDirection = nearestTarget.transform.position - transform.position;
        return targetDirection.sqrMagnitude > 0.001f ? targetDirection.normalized : forward;
    }

    private void EndAirDash()
    {
        if (!isAirDashing) return;
        isAirDashing = false;
        if (body != null)
        {
            body.collisionDetectionMode = originalCollisionDetectionMode;
            body.linearVelocity = new Vector3(0f, body.linearVelocity.y, 0f);
        }
    }

    private IEnumerator PerformSpinAttack()
    {
        fourPressComboCount = 0;
        fourPressComboExpiresAt = 0f;
        isAttacking = true;
        isSpinAttacking = true;
        targetsHitThisSpin.Clear();
        CancelInvoke(nameof(ResetAttack));
        nextAttackTime = Time.time + spinAttackDuration;
        if (body != null)
        {
            body.linearVelocity = new Vector3(0f, body.linearVelocity.y, 0f);
        }

        Quaternion startingRotation = transform.rotation;
        float startTime = Time.time;
        while (!isDead && Time.time - startTime < spinAttackDuration)
        {
            float progress = Mathf.Clamp01((Time.time - startTime) / spinAttackDuration);
            transform.rotation = startingRotation * Quaternion.Euler(0f, progress * 360f, 0f);
            ApplySpinAttackHits();
            yield return null;
        }

        if (!isDead) transform.rotation = startingRotation;
        isSpinAttacking = false;
        isAttacking = false;
    }

    private void ApplySpinAttackHits()
    {
        if (bodyCollider == null) return;

        Bounds bodyBounds = bodyCollider.bounds;
        Collider[] touchingColliders = Physics.OverlapBox(
            bodyBounds.center,
            bodyBounds.extents,
            Quaternion.identity,
            Physics.AllLayers,
            QueryTriggerInteraction.Collide);
        foreach (Collider touchingCollider in touchingColliders)
        {
            if (touchingCollider == null || touchingCollider.transform.IsChildOf(transform)) continue;
            FighterController target = touchingCollider.GetComponentInParent<FighterController>();
            if (target == null || target == this || target.IsDead || !targetsHitThisSpin.Add(target)) continue;
            target.EliminateInstantly(GetSlamKnockbackVelocity(target.transform.position, transform.position));
        }
    }

    private IEnumerator PerformGodMode()
    {
        maxHealth = GodModeHealth;
        currentHealth = GodModeHealth;
        isGodModeActive = true;
        yield return new WaitForSecondsRealtime(godModeDuration);
        if (isDead) yield break;

        isGodModeActive = false;
        EliminateInstantly();
    }

    private void TryApplyAbilityContact(Collider otherCollider)
    {
        if (otherCollider == null || otherCollider.transform.IsChildOf(transform)) return;
        FighterController target = otherCollider.GetComponentInParent<FighterController>();
        if (target == null || target == this || target.IsDead) return;

        if (isGodModeActive)
        {
            target.EliminateInstantly(GetSlamKnockbackVelocity(target.transform.position, transform.position));
            return;
        }

        if (isGroundSlamming)
        {
            TryApplyDirectSlamHit(otherCollider);
            return;
        }

        if (isAirDashing && targetsHitThisAirDash.Add(target))
        {
            int damage = Mathf.Max(1, Mathf.CeilToInt(target.maxHealth * 0.5f));
            target.TakeDamage(damage, this);
            EndAirDash();
        }
    }

    private void LateUpdate()
    {
        UpdateDeathLabel();
        UpdateBlockShield();
    }

    private void InitializeBlockShield()
    {
        groundTransform = GameObject.Find("ground")?.transform;
        if (playerID != 2) return;

        GameObject shieldObject = new GameObject("Dragon Block Shield Ring");
        blockShieldRing = shieldObject.AddComponent<LineRenderer>();
        blockShieldRing.useWorldSpace = true;
        blockShieldRing.loop = true;
        blockShieldRing.positionCount = ShieldRingSegmentCount;
        blockShieldRing.startWidth = ShieldRingWidth;
        blockShieldRing.endWidth = ShieldRingWidth;
        blockShieldRing.numCapVertices = 6;
        blockShieldRing.numCornerVertices = 6;
        blockShieldRing.alignment = LineAlignment.View;
        blockShieldRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        blockShieldRing.receiveShadows = false;
        blockShieldRing.startColor = BlockShieldColor;
        blockShieldRing.endColor = BlockShieldColor;
        blockShieldRing.enabled = false;

        Shader shieldShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shieldShader == null)
        {
            shieldShader = Shader.Find("Unlit/Color");
        }

        blockShieldMaterial = new Material(shieldShader)
        {
            name = "Dragon Blue Block Shield"
        };
        if (blockShieldMaterial.HasProperty("_BaseColor"))
        {
            blockShieldMaterial.SetColor("_BaseColor", BlockShieldColor);
        }
        if (blockShieldMaterial.HasProperty("_Color"))
        {
            blockShieldMaterial.SetColor("_Color", BlockShieldColor);
        }
        if (blockShieldMaterial.HasProperty("_EmissionColor"))
        {
            blockShieldMaterial.EnableKeyword("_EMISSION");
            blockShieldMaterial.SetColor("_EmissionColor", BlockShieldColor * 1.6f);
        }

        blockShieldRing.sharedMaterial = blockShieldMaterial;
    }

    private void UpdateBlockShield()
    {
        if (blockShieldRing == null) return;

        bool showShield = playerID == 2 && isBlocking && !isDead && IsThisCharacterSelected();
        blockShieldRing.enabled = showShield;
        if (!showShield) return;

        float ringY = groundTransform != null
            ? groundTransform.position.y + ShieldGroundOffset
            : transform.position.y;
        float pulse = Mathf.Sin(Time.time * 4f) * ShieldRingPulseRadius;
        float ringRadius = CalculateShieldRadius() + pulse;

        for (int segmentIndex = 0; segmentIndex < ShieldRingSegmentCount; segmentIndex++)
        {
            float angle = segmentIndex * Mathf.PI * 2f / ShieldRingSegmentCount;
            Vector3 ringPoint = new Vector3(
                transform.position.x + Mathf.Cos(angle) * ringRadius,
                ringY,
                transform.position.z + Mathf.Sin(angle) * ringRadius);
            blockShieldRing.SetPosition(segmentIndex, ringPoint);
        }
    }

    private float CalculateShieldRadius()
    {
        Renderer[] characterRenderers = GetComponentsInChildren<Renderer>();
        if (characterRenderers.Length == 0) return ShieldRadiusMinimum;

        Bounds characterBounds = characterRenderers[0].bounds;
        for (int rendererIndex = 1; rendererIndex < characterRenderers.Length; rendererIndex++)
        {
            characterBounds.Encapsulate(characterRenderers[rendererIndex].bounds);
        }

        float largestHorizontalExtent = Mathf.Max(characterBounds.extents.x, characterBounds.extents.z);
        return Mathf.Clamp(largestHorizontalExtent + ShieldRadiusMargin, ShieldRadiusMinimum, ShieldRadiusMaximum);
    }

    private bool IsThisCharacterSelected()
    {
        PlayerSwitcher playerSwitcher = PlayerSwitcher.Instance;
        if (playerSwitcher != null)
        {
            return playerSwitcher.GetControlledCharacter() == gameObject;
        }

        return playerID == 2
            ? PlayerSwitcher.IsDragonActive
            : playerID == 1 && !PlayerSwitcher.IsDragonActive;
    }

    private void ExecuteAttack(string animationTrigger)
    {
        if (Time.time < nextAttackTime) return;

        isAttacking = true;
        nextAttackTime = Time.time + attackCooldown;
        SetAnimationTriggerIfPresent(playerID == 2 ? "Attack" : animationTrigger);
        StartCoroutine(PerformClawHitWindow());
        Invoke(nameof(ResetAttack), Mathf.Max(attackCooldown, 0.1f));
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryApplyAbilityContact(collision.collider);
    }

    private void OnCollisionStay(Collision collision)
    {
        TryApplyAbilityContact(collision.collider);
        if (playerID != 1 || !IsThisCharacterSelected() || isDead) return;

        FighterController target = collision.collider.GetComponentInParent<FighterController>();
        if (target == null || target.playerID != 2 || target.isDead || Time.time < nextAttackTime) return;

        isAttacking = true;
        nextAttackTime = Time.time + attackCooldown;
        SetAnimationTriggerIfPresent("LightAttack");

        ApplyDamageToTarget(target, damagePerHit);
        Invoke(nameof(ResetAttack), attackCooldown);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryApplyAbilityContact(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryApplyAbilityContact(other);
    }

    private IEnumerator PerformClawHitWindow()
    {
        HashSet<FighterController> targetsHit = new HashSet<FighterController>();
        float windowEndsAt = Time.time + clawHitWindowDuration;

        while (Time.time < windowEndsAt && !isDead)
        {
            Vector3 hitboxCenter = playerID == 2
                ? transform.position
                : attackPoint != null
                    ? attackPoint.position
                    : transform.position + GetAttackForwardDirection() * attackForwardOffset + Vector3.up * attackHeightOffset;
            Collider[] overlappingColliders = Physics.OverlapSphere(
                hitboxCenter,
                attackRange,
                Physics.AllLayers,
                QueryTriggerInteraction.Collide);

            foreach (Collider overlappingCollider in overlappingColliders)
            {
                FighterController target = overlappingCollider.GetComponentInParent<FighterController>();
                if (target == null || target == this || target.isDead || target.playerID == playerID || !targetsHit.Add(target))
                {
                    continue;
                }

                ApplyDamageToTarget(target, damagePerHit);
            }

            yield return null;
        }
    }


    private Vector3 GetAttackForwardDirection()
    {
        DragonController dragonController = GetComponent<DragonController>();
        if (playerID == 2 && dragonController != null)
        {
            return Quaternion.Euler(0f, -dragonController.modelRotationOffset, 0f) * transform.forward;
        }

        return transform.forward;
    }

    private void ApplyDamageToTarget(FighterController target, int damage)
    {
        if (target == null || target.isDead) return;

        int healthBeforeHit = target.currentHealth;
        target.TakeDamage(damage, this);
        int damageDealt = healthBeforeHit - target.currentHealth;
        if (damageDealt <= 0) return;

        if (playerID == 2 && target.playerID >= 3)
        {
            PlayerSwitcher.Instance?.NotifyTutorialAttackHit(false);
        }

        AddBurstChargeFromDamage(damageDealt);
        if (playerID <= 2)
        {
            ShowDamageFeedback(damageDealt, target);
        }
    }

    /// <summary>
    /// Sets a temporary charge rate for guided tutorial practice.
    /// </summary>
    public void SetBurstChargeRate(float chargePerSecond)
    {
        burstChargePerSecond = Mathf.Max(0f, chargePerSecond);
    }

    /// <summary>
    /// Lowers tutorial health to the burst threshold without damaging or defeating the fighter.
    /// </summary>
    public void SetHealthAtMostBurstThreshold()
    {
        if (isDead || maxHealth <= 0) return;

        int burstThresholdHealth = Mathf.Max(1, BurstHealthLimit);
        currentHealth = Mathf.Min(currentHealth, burstThresholdHealth);
    }


    /// <summary>
    /// Adds burst energy when this fighter deals damage.
    /// </summary>
    public void AddBurstChargeFromDamage(int damageDealt)
    {
        if (playerID > 2 || damageDealt <= 0) return;
        burstCharge = Mathf.Min(BurstChargeRequired, burstCharge + damageDealt * burstChargePerDamage);
    }

    private void ExecuteBurstAttack()
    {
        if (!CanUseBurstAttack || Time.time < nextAttackTime) return;

        if (playerID == 2)
        {
            PlayerSwitcher.Instance?.NotifyTutorialBurstUsed();
        }

        burstCharge = 0f;
        isAttacking = true;
        nextAttackTime = Time.time + Mathf.Max(attackCooldown, burstAttackCooldown);
        if (!SetAnimationTriggerIfPresent("BurstAttack") &&
            !SetAnimationTriggerIfPresent("Burst"))
        {
            SetAnimationTriggerIfPresent("Attack");
        }

        FighterController[] fighters = Object.FindObjectsByType<FighterController>();
        Vector3 burstOrigin = transform.position;
        foreach (FighterController target in fighters)
        {
            if (target == null || target == this || target.isDead || target.playerID < 3)
            {
                continue;
            }

            Vector3 direction = target.transform.position - burstOrigin;
            direction.y = 0f;
            if (direction.sqrMagnitude > burstAttackRadius * burstAttackRadius)
            {
                continue;
            }

            float knockbackSpeed = Mathf.Min(burstKnockbackVelocity, HitKnockbackSpeed);
            Vector3 knockbackVelocity = direction.sqrMagnitude > 0.001f
                ? direction.normalized * knockbackSpeed
                : transform.forward * knockbackSpeed;
            target.EliminateInstantly(knockbackVelocity);
        }

        Invoke(nameof(ResetAttack), Mathf.Max(attackCooldown, burstAttackCooldown));
    }

    private bool SetAnimationTriggerIfPresent(string triggerName)
    {
        if (anim == null) return false;

        foreach (AnimatorControllerParameter parameter in anim.parameters)
        {
            if (parameter.name == triggerName && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                anim.SetTrigger(triggerName);
                return true;
            }
        }

        return false;
    }

    private void ShowDamageFeedback(int damageDealt, FighterController target)
    {
        if (damageFeedbackText == null) return;

        damageFeedbackText.text = $"Damage dealt: {damageDealt}  |  {target.name}: {target.currentHealth}/{target.maxHealth} HP";
        damageFeedbackText.gameObject.SetActive(true);

        if (damageFeedbackCoroutine != null)
        {
            StopCoroutine(damageFeedbackCoroutine);
        }

        damageFeedbackCoroutine = StartCoroutine(HideDamageFeedbackAfterDelay());
    }

    private IEnumerator HideDamageFeedbackAfterDelay()
    {
        yield return new WaitForSecondsRealtime(damageFeedbackDuration);
        if (damageFeedbackText != null)
        {
            damageFeedbackText.gameObject.SetActive(false);
        }
        damageFeedbackCoroutine = null;
    }

    private void ResetAttack()
    {
        isAttacking = false;
    }

    /// <summary>
    /// Removes health and starts the death behavior when health reaches zero.
    /// </summary>
    public void TakeDamage(int damage, FighterController attacker = null)
    {
        if (isDead || damage <= 0) return;
        if (attacker != null && attacker != this && attacker.IsGodModeActive)
        {
            EliminateInstantly(attacker.GetSlamKnockbackVelocity(transform.position, attacker.transform.position));
            return;
        }

        int appliedDamage = playerID == 2 && isBlocking ? 1 : damage;
        currentHealth = Mathf.Max(0, currentHealth - appliedDamage);
        Debug.Log($"{(playerID == 2 ? "Dragon" : "Player")} HP: {currentHealth}/{maxHealth} (damage taken: {appliedDamage})");

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            deathKnockbackVelocity = GetHitKnockbackVelocity(attacker);
            Die();
            return;
        }

        if (appliedDamage > 0 && attacker != null)
        {
            ApplyHitKnockback(attacker);
        }
    }

    private void ApplyHitKnockback(FighterController attacker)
    {
        if (attacker == null || isDead) return;
        ApplyKnockback(GetHitKnockbackVelocity(attacker));
    }

    /// <summary>
    /// Applies a horizontal knockback velocity and briefly prevents movement from overriding it.
    /// </summary>
    public void ApplyKnockback(Vector3 knockbackVelocity)
    {
        if (isDead) return;
        if (body == null) body = GetComponent<Rigidbody>();
        if (body == null || body.isKinematic) return;

        body.linearVelocity = new Vector3(knockbackVelocity.x, body.linearVelocity.y, knockbackVelocity.z);
        IsBeingKnockedBack = true;

        if (knockbackControlCoroutine != null)
        {
            StopCoroutine(knockbackControlCoroutine);
        }
        knockbackControlCoroutine = StartCoroutine(ReleaseMovementControlAfterKnockback());
    }

    private Vector3 GetHitKnockbackVelocity(FighterController attacker)
    {
        if (attacker == null) return Vector3.zero;

        Vector3 direction = transform.position - attacker.transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = -attacker.transform.forward;
            direction.y = 0f;
        }

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = Vector3.back;
        }

        return direction.normalized * HitKnockbackSpeed;
    }

    private IEnumerator ReleaseMovementControlAfterKnockback()
    {
        yield return new WaitForSeconds(KnockbackControlLockSeconds);
        IsBeingKnockedBack = false;
        knockbackControlCoroutine = null;
    }

    /// <summary>
    /// Immediately defeats this fighter, bypassing health and blocking rules.
    /// </summary>
    public void EliminateInstantly()
    {
        EliminateInstantly(Vector3.zero);
    }

    /// <summary>
    /// Immediately defeats this fighter and moves the defeated model outward as a burst knockback.
    /// </summary>
    public void EliminateInstantly(Vector3 knockbackVelocity)
    {
        if (isDead) return;

        currentHealth = 0;
        deathKnockbackVelocity = knockbackVelocity;
        Die();
    }

    /// <summary>
    /// Defeats this fighter with the meteor-specific squash and timed cleanup effect.
    /// </summary>
    public void EliminateByMeteor()
    {
        EliminateByMeteor(Vector3.zero);
    }

    /// <summary>
    /// Defeats this fighter with the meteor squash effect and outward launch velocity.
    /// </summary>
    public void EliminateByMeteor(Vector3 knockbackVelocity)
    {
        if (isDead) return;

        isMeteorDeath = true;
        currentHealth = 0;
        deathKnockbackVelocity = knockbackVelocity;
        Die();
    }

    private void Die()
    {
        if (isDead) return;

        isDead = true;
        isAttacking = false;
        CancelInvoke();
        deathPosition = transform.position;
        deathStartingRotation = transform.rotation;
        deathStartingScale = transform.localScale;
        deathStartedAt = Time.unscaledTime;
        deathSpinAngle = 0f;
        CalculateDeathLabelHeight();

        if (anim != null)
        {
            anim.enabled = false;
        }

        PlayerController playerMovement = GetComponent<PlayerController>();
        if (playerMovement != null)
        {
            playerMovement.enabled = false;
        }

        DragonController dragonMovement = GetComponent<DragonController>();
        if (dragonMovement != null)
        {
            dragonMovement.enabled = false;
        }

        EnemyPursuitController enemyMovement = GetComponent<EnemyPursuitController>();
        if (enemyMovement != null)
        {
            enemyMovement.enabled = false;
        }

        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.useGravity = false;
            body.isKinematic = true;
        }

        CreateDeathLabel();
        PlayerSwitcher.Instance?.NotifyFighterDefeated(this);
        Debug.Log($"{(playerID == 2 ? "Dragon" : "Player")} defeated.");
    }

    private void UpdateDeathSpin()
    {
        float unscaledDeltaTime = Time.unscaledDeltaTime;
        deathPosition += deathKnockbackVelocity * unscaledDeltaTime;
        deathKnockbackVelocity = Vector3.Lerp(deathKnockbackVelocity, Vector3.zero, unscaledDeltaTime * 2f);
        deathSpinAngle = Mathf.Repeat(deathSpinAngle + deathSpinSpeedDegrees * unscaledDeltaTime, 360f);
        transform.SetPositionAndRotation(
            deathPosition,
            Quaternion.AngleAxis(deathSpinAngle, Vector3.up) * deathStartingRotation);

        if (isMeteorDeath)
        {
            float squashProgress = Mathf.Clamp01(
                (Time.unscaledTime - deathStartedAt) / Mathf.Max(0.01f, meteorSquashDurationSeconds));
            Vector3 flattenedScale = new Vector3(
                deathStartingScale.x * MeteorSquashWidthScale,
                deathStartingScale.y * MeteorSquashHeightScale,
                deathStartingScale.z * MeteorSquashWidthScale);
            transform.localScale = Vector3.Lerp(deathStartingScale, flattenedScale, squashProgress);
        }
    }

    private void CalculateDeathLabelHeight()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds characterBounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
        {
            characterBounds.Encapsulate(renderers[index].bounds);
        }

        deathLabelHeight = Mathf.Max(deathLabelHeight, characterBounds.max.y - deathPosition.y + 0.5f);
    }

    private void CreateDeathLabel()
    {
        GameObject canvasObject = new GameObject($"{name}_DeathLabel", typeof(RectTransform), typeof(Canvas));
        deathLabelCanvas = canvasObject.GetComponent<Canvas>();
        deathLabelCanvas.renderMode = RenderMode.WorldSpace;
        deathLabelCanvas.overrideSorting = true;
        deathLabelCanvas.sortingOrder = 100;
        deathLabelCanvas.worldCamera = Camera.main;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(600f, 120f);
        canvasRect.localScale = Vector3.one * 0.01f;

        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(canvasRect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI deathText = textObject.GetComponent<TextMeshProUGUI>();
        deathText.font = TMP_Settings.defaultFontAsset;
        deathText.text = isMeteorDeath ? "METEOR KO" : "DEAD";
        deathText.fontSize = 80f;
        deathText.fontStyle = FontStyles.Bold;
        deathText.alignment = TextAlignmentOptions.Center;
        deathText.color = Color.red;
        deathText.outlineColor = new Color32(0, 0, 0, 255);
        deathText.outlineWidth = 0.2f;
        deathText.raycastTarget = false;

        deathLabelCanvas.transform.position = deathPosition + Vector3.up * deathLabelHeight;
        UpdateDeathLabel();
    }

    private void UpdateDeathLabel()
    {
        if (deathLabelCanvas == null) return;

        deathLabelCanvas.transform.position = deathPosition + Vector3.up * deathLabelHeight;
        Camera activeCamera = Camera.main;
        if (activeCamera != null)
        {
            deathLabelCanvas.transform.rotation = activeCamera.transform.rotation;
        }
    }

    private void OnDestroy()
    {
        if (deathLabelCanvas != null)
        {
            Destroy(deathLabelCanvas.gameObject);
        }

        if (blockShieldRing != null)
        {
            Destroy(blockShieldRing.gameObject);
        }

        if (blockShieldMaterial != null)
        {
            Destroy(blockShieldMaterial);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 attackCenter = attackPoint != null
            ? attackPoint.position
            : transform.position + transform.forward * attackForwardOffset + Vector3.up * attackHeightOffset;
        Gizmos.DrawWireSphere(attackCenter, attackRange);
    }
}
