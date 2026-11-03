using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Creates cube-rock obstacles and warns before consecutive falling hazard waves.
/// </summary>
public class ArenaHazardManager : MonoBehaviour
{
    [Header("Arena Layout")]
    [SerializeField, Min(0)] private int rockClusterCount = 9;
    [SerializeField, Min(1)] private int cubesPerRockCluster = 4;
    [SerializeField] private float groundSurfaceY = -0.15f;
    [SerializeField] private Vector2 arenaHalfExtents = new Vector2(63f, 60f);

    [Header("Falling Hazards")]
    [SerializeField, Min(1)] private int dropsPerWave = 5;
    [SerializeField, Min(1f)] private float waveInterval = 30f;
    [SerializeField, Min(0.1f)] private float secondsBetweenDrops = 1.25f;
    [SerializeField, Min(0.1f)] private float warningSeconds = 1.5f;
    [SerializeField, Min(5f)] private float dropHeight = 60f;
    [SerializeField, Min(0.25f)] private float hazardDiameter = 1.8f;
    [SerializeField, Min(0.5f)] private float warningMarkerDiameter = 7.5f;

    private const int MaximumPositionAttempts = 160;
    private const float RockClearanceFromCharacters = 12f;
    private const float RockClearanceFromOtherRocks = 13f;
    private const float HazardClearanceFromCharacters = 5f;
    private const float HazardClearanceFromRocks = 4f;
    private const float MinimumRockCubeSize = 1.2f;
    private const float MaximumRockCubeSize = 2.4f;
    private const float MinimumRockCubeHeight = 1.2f;
    private const float MaximumRockCubeHeight = 2.8f;
    private const float MarkerSurfaceOffset = 0.025f;
    private const float MarkerCylinderThickness = 0.025f;
    private const float HazardMass = 12f;
    private const float HazardLinearDrag = 0f;
    private const float HazardAngularDrag = 0.05f;
    private const float RemainingWaveDelayMinimum = 0f;

    private static readonly Color RockColor = new Color(0.32f, 0.29f, 0.27f, 1f);
    private static readonly Color HazardColor = new Color(0.12f, 0.07f, 0.16f, 1f);
    private static readonly Color WarningColor = new Color(0.12f, 0.08f, 0.1f, 1f);

    private readonly List<Vector2> rockPositions = new List<Vector2>();
    private Material rockMaterial;
    private Material hazardMaterial;
    private Material warningMaterial;

    private void Start()
    {
        rockMaterial = CreateRuntimeMaterial("Arena Rock Material", RockColor);
        hazardMaterial = CreateRuntimeMaterial("Falling Hazard Material", HazardColor);
        warningMaterial = CreateRuntimeMaterial("Hazard Landing Marker Material", WarningColor);
        CreateRockClusters();
        StartCoroutine(RunHazardWaves());
    }

    private void CreateRockClusters()
    {
        for (int clusterIndex = 0; clusterIndex < rockClusterCount; clusterIndex++)
        {
            if (!TryChoosePosition(RockClearanceFromCharacters, RockClearanceFromOtherRocks, out Vector2 position))
            {
                continue;
            }

            rockPositions.Add(position);
            GameObject cluster = new GameObject($"Cube Rock Cluster {clusterIndex + 1}");
            cluster.transform.position = new Vector3(position.x, groundSurfaceY, position.y);

            int cubeCount = Random.Range(cubesPerRockCluster, cubesPerRockCluster + 3);
            for (int cubeIndex = 0; cubeIndex < cubeCount; cubeIndex++)
            {
                CreateRockCube(cluster.transform, cubeIndex);
            }
        }
    }

    private void CreateRockCube(Transform cluster, int cubeIndex)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = $"Rock Cube {cubeIndex + 1}";
        cube.transform.SetParent(cluster, false);

        float width = Random.Range(MinimumRockCubeSize, MaximumRockCubeSize);
        float height = Random.Range(MinimumRockCubeHeight, MaximumRockCubeHeight);
        cube.transform.localScale = new Vector3(width, height, width * Random.Range(0.75f, 1.15f));
        cube.transform.localPosition = new Vector3(
            Random.Range(-1.2f, 1.2f),
            height * 0.5f,
            Random.Range(-1.2f, 1.2f));
        cube.transform.localRotation = Quaternion.Euler(
            Random.Range(-8f, 8f),
            Random.Range(0f, 360f),
            Random.Range(-8f, 8f));
        cube.GetComponent<Renderer>().sharedMaterial = rockMaterial;
    }

    private IEnumerator RunHazardWaves()
    {
        while (true)
        {
            float waveStartedAt = Time.time;
            for (int dropIndex = 0; dropIndex < dropsPerWave; dropIndex++)
            {
                if (TryChoosePosition(HazardClearanceFromCharacters, HazardClearanceFromRocks, out Vector2 position))
                {
                    StartCoroutine(ShowWarningThenDrop(position));
                }

                if (dropIndex < dropsPerWave - 1)
                {
                    yield return new WaitForSeconds(secondsBetweenDrops);
                }
            }

            float remainingWaveDelay = waveInterval - (Time.time - waveStartedAt);
            yield return new WaitForSeconds(Mathf.Max(RemainingWaveDelayMinimum, remainingWaveDelay));
        }
    }

    private IEnumerator ShowWarningThenDrop(Vector2 position)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = "Hazard Landing Shadow";
        marker.transform.position = new Vector3(position.x, groundSurfaceY + MarkerSurfaceOffset, position.y);
        marker.transform.localScale = new Vector3(warningMarkerDiameter, MarkerCylinderThickness, warningMarkerDiameter);
        Collider markerCollider = marker.GetComponent<Collider>();
        if (markerCollider != null)
        {
            Destroy(markerCollider);
        }

        marker.GetComponent<Renderer>().sharedMaterial = warningMaterial;
        yield return new WaitForSeconds(warningSeconds);

        GameObject hazard = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        hazard.name = "Falling Hazard Ball";
        hazard.transform.position = new Vector3(position.x, groundSurfaceY + dropHeight, position.y);
        hazard.transform.localScale = Vector3.one * hazardDiameter;
        hazard.GetComponent<Renderer>().sharedMaterial = hazardMaterial;

        Rigidbody hazardBody = hazard.AddComponent<Rigidbody>();
        hazardBody.mass = HazardMass;
        hazardBody.linearDamping = HazardLinearDrag;
        hazardBody.angularDamping = HazardAngularDrag;
        hazardBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        hazard.AddComponent<HazardBall>();

        Destroy(marker, warningSeconds + 3f);
    }

    private bool TryChoosePosition(float characterClearance, float rockClearance, out Vector2 position)
    {
        for (int attempt = 0; attempt < MaximumPositionAttempts; attempt++)
        {
            Vector2 candidate = new Vector2(
                Random.Range(-arenaHalfExtents.x, arenaHalfExtents.x),
                Random.Range(-arenaHalfExtents.y, arenaHalfExtents.y));

            if (!IsClearOfCharacters(candidate, characterClearance) || !IsClearOfRocks(candidate, rockClearance))
            {
                continue;
            }

            position = candidate;
            return true;
        }

        position = Vector2.zero;
        return false;
    }

    private bool IsClearOfCharacters(Vector2 position, float clearance)
    {
        FighterController[] fighters = FindObjectsByType<FighterController>();
        float minimumDistanceSquared = clearance * clearance;
        foreach (FighterController fighter in fighters)
        {
            if (fighter == null || fighter.IsDead) continue;

            Vector2 fighterPosition = new Vector2(fighter.transform.position.x, fighter.transform.position.z);
            if ((fighterPosition - position).sqrMagnitude < minimumDistanceSquared)
            {
                return false;
            }
        }

        return true;
    }

    private bool IsClearOfRocks(Vector2 position, float clearance)
    {
        float minimumDistanceSquared = clearance * clearance;
        foreach (Vector2 rockPosition in rockPositions)
        {
            if ((rockPosition - position).sqrMagnitude < minimumDistanceSquared)
            {
                return false;
            }
        }

        return true;
    }

    private static Material CreateRuntimeMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader)
        {
            name = materialName
        };

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        return material;
    }
}