using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Extends the arena floor and builds randomly distributed low-poly scenery.
/// </summary>
public sealed class ArenaEnvironment : MonoBehaviour
{
    private const float GroundScaleMultiplier = 1.3f;
    private const float GroundSurfaceOffset = 0.035f;
    private const float TreeTrunkHeight = 7.5f;
    private const float TreeCanopyHeight = 9f;
    private const float TreeSpawnExtent = 93f;
    private const float TreeRingInnerExtent = 73f;
    private const float TreeMinimumSpacing = 20f;
    private const float KnollSpawnExtent = 61f;
    private const float KnollMinimumSpacing = 22f;
    private const float KnollTreeClearance = 12f;
    private const float PondDecorationClearance = 19f;
    private const int TreeCount = 20;
    private const int KnollCount = 12;
    private const int MaximumDecorationAttempts = 1200;
    private const int GrassTextureResolution = 256;
    private const int GrassBladeCount = 1100;
    private const float GrassTextureTiling = 25f;
    private const float TreeScaleMinimum = 0.82f;
    private const float TreeScaleMaximum = 1.22f;
    private const float KnollScaleMinimum = 0.78f;
    private const float KnollScaleMaximum = 1.22f;

    private static readonly Color TrunkColor = new Color32(105, 66, 40, 255);
    private static readonly Color LeafDarkColor = new Color32(32, 112, 57, 255);
    private static readonly Color LeafMidColor = new Color32(44, 145, 69, 255);
    private static readonly Color LeafLightColor = new Color32(77, 171, 83, 255);
    private static readonly Color GrassColor = new Color32(86, 150, 68, 255);
    private static readonly Color GrassLightColor = new Color32(123, 174, 79, 255);
    private static readonly Color PondBankColor = new Color32(165, 140, 77, 255);
    private static readonly Color PondWaterColor = new Color32(38, 143, 178, 255);
    private static readonly Color[] GrassTexturePalette =
    {
        new Color32(75, 127, 55, 255),
        new Color32(88, 143, 62, 255),
        new Color32(102, 153, 68, 255),
        new Color32(115, 161, 75, 255),
        new Color32(63, 116, 52, 255)
    };
    private static readonly Color[] GrassBladePalette =
    {
        new Color32(128, 171, 78, 255),
        new Color32(150, 182, 89, 255),
        new Color32(60, 112, 49, 255),
        new Color32(107, 158, 68, 255)
    };

    private readonly List<Material> runtimeMaterials = new List<Material>();
    private readonly List<Vector2> treePositions = new List<Vector2>();
    private readonly List<Vector2> knollPositions = new List<Vector2>();
    private Transform environmentRoot;
    private Material trunkMaterial;
    private Material leafDarkMaterial;
    private Material leafMidMaterial;
    private Material leafLightMaterial;
    private Material grassMaterial;
    private Material grassLightMaterial;
    private Material pondBankMaterial;
    private Material pondWaterMaterial;
    private Material groundMaterial;
    private Texture2D groundGrassTexture;
    private float groundSurfaceY;
    private Vector2 pondPosition;

    private void Start()
    {
        GameObject ground = GameObject.Find("ground");
        if (ground != null)
        {
            ground.transform.localScale *= GroundScaleMultiplier;
            groundSurfaceY = ground.transform.position.y;
            ApplyGrassTexture(ground);
        }
        else
        {
            groundSurfaceY = -0.15f;
        }

        CreateMaterials();
        GameObject rootObject = new GameObject("Low Poly Arena Terrain");
        environmentRoot = rootObject.transform;
        CreatePond();
        CreateRandomTrees();
        CreateRandomGrassKnolls();
    }

    private void ApplyGrassTexture(GameObject ground)
    {
        Renderer groundRenderer = ground.GetComponent<Renderer>();
        if (groundRenderer == null || groundRenderer.sharedMaterial == null) return;

        Shader groundShader = groundRenderer.sharedMaterial.shader;
        groundMaterial = new Material(groundRenderer.sharedMaterial)
        {
            name = "Procedural Grass Arena Ground"
        };
        groundMaterial.shader = groundShader;
        groundGrassTexture = CreateGrassTexture();

        if (groundMaterial.HasProperty("_BaseMap"))
        {
            groundMaterial.SetTexture("_BaseMap", groundGrassTexture);
            groundMaterial.SetTextureScale("_BaseMap", Vector2.one * GrassTextureTiling);
        }
        else if (groundMaterial.HasProperty("_MainTex"))
        {
            groundMaterial.SetTexture("_MainTex", groundGrassTexture);
            groundMaterial.SetTextureScale("_MainTex", Vector2.one * GrassTextureTiling);
        }

        if (groundMaterial.HasProperty("_BaseColor"))
        {
            groundMaterial.SetColor("_BaseColor", Color.white);
        }
        if (groundMaterial.HasProperty("_Color"))
        {
            groundMaterial.SetColor("_Color", Color.white);
        }

        groundRenderer.material = groundMaterial;
    }

    private Texture2D CreateGrassTexture()
    {
        Texture2D texture = new Texture2D(GrassTextureResolution, GrassTextureResolution, TextureFormat.RGBA32, false)
        {
            name = "Procedural Grass Texture",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            anisoLevel = 4
        };

        uint randomState = 0x6D2B79F5u;
        Color[] pixels = new Color[GrassTextureResolution * GrassTextureResolution];
        for (int pixelIndex = 0; pixelIndex < pixels.Length; pixelIndex++)
        {
            int paletteIndex = Mathf.Min(
                Mathf.FloorToInt(NextRandom01(ref randomState) * GrassTexturePalette.Length),
                GrassTexturePalette.Length - 1);
            float brightness = 0.88f + NextRandom01(ref randomState) * 0.24f;
            pixels[pixelIndex] = GrassTexturePalette[paletteIndex] * brightness;
        }

        for (int bladeIndex = 0; bladeIndex < GrassBladeCount; bladeIndex++)
        {
            int x = Mathf.FloorToInt(NextRandom01(ref randomState) * GrassTextureResolution);
            int y = Mathf.FloorToInt(NextRandom01(ref randomState) * GrassTextureResolution);
            int bladeLength = Random.Range(2, 7);
            int directionX = Random.Range(-1, 2);
            Color bladeColor = GrassBladePalette[Mathf.Min(
                Mathf.FloorToInt(NextRandom01(ref randomState) * GrassBladePalette.Length),
                GrassBladePalette.Length - 1)];

            for (int bladeStep = 0; bladeStep < bladeLength; bladeStep++)
            {
                int pixelX = (x + directionX * bladeStep + GrassTextureResolution) % GrassTextureResolution;
                int pixelY = (y + bladeStep) % GrassTextureResolution;
                pixels[pixelY * GrassTextureResolution + pixelX] = bladeColor;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static float NextRandom01(ref uint state)
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return (state & 0x00FFFFFFu) / 16777216f;
    }

    private void CreateMaterials()
    {
        trunkMaterial = CreateRuntimeMaterial("Low Poly Tree Trunk", TrunkColor);
        leafDarkMaterial = CreateRuntimeMaterial("Low Poly Dark Leaves", LeafDarkColor);
        leafMidMaterial = CreateRuntimeMaterial("Low Poly Green Leaves", LeafMidColor);
        leafLightMaterial = CreateRuntimeMaterial("Low Poly Light Leaves", LeafLightColor);
        grassMaterial = CreateRuntimeMaterial("Low Poly Grass", GrassColor);
        grassLightMaterial = CreateRuntimeMaterial("Low Poly Light Grass", GrassLightColor);
        pondBankMaterial = CreateRuntimeMaterial("Low Poly Pond Bank", PondBankColor);
        pondWaterMaterial = CreateRuntimeMaterial("Low Poly Pond Water", PondWaterColor);
    }

    private void CreateRandomTrees()
    {
        for (int treeIndex = 0; treeIndex < TreeCount; treeIndex++)
        {
            if (!TryChooseDecorationPosition(
                    TreeSpawnExtent,
                    TreeMinimumSpacing,
                    treePositions,
                    true,
                    0f,
                    out Vector2 position))
            {
                continue;
            }

            treePositions.Add(position);
            float randomScale = Random.Range(TreeScaleMinimum, TreeScaleMaximum);
            CreateTree(new Vector3(position.x, groundSurfaceY, position.y), treeIndex, randomScale);
        }
    }

    private void CreateTree(Vector3 position, int variationIndex, float treeScale)
    {
        GameObject treeObject = new GameObject("Geometric Tree");
        treeObject.transform.SetParent(environmentRoot, false);
        treeObject.transform.position = position;
        treeObject.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        treeObject.transform.localScale = Vector3.one * treeScale;

        CreateShape(PrimitiveType.Cylinder, "Tree Trunk", treeObject.transform,
            new Vector3(0f, TreeTrunkHeight * 0.5f, 0f),
            new Vector3(1.2f, TreeTrunkHeight * 0.5f, 1.2f), trunkMaterial);

        CreateShape(PrimitiveType.Cube, "Lower Canopy", treeObject.transform,
            new Vector3(0f, TreeCanopyHeight, 0f), new Vector3(6f, 4.2f, 6f), leafDarkMaterial)
            .transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 45f), 0f);
        CreateShape(PrimitiveType.Sphere, "Left Canopy", treeObject.transform,
            new Vector3(-2.2f, TreeCanopyHeight + 0.9f, 0.4f), new Vector3(4.4f, 4.4f, 4.4f), leafMidMaterial);
        CreateShape(PrimitiveType.Sphere, "Right Canopy", treeObject.transform,
            new Vector3(2.1f, TreeCanopyHeight + 1f, -0.3f), new Vector3(4.7f, 4.7f, 4.7f),
            variationIndex % 2 == 0 ? leafLightMaterial : leafMidMaterial);
        CreateShape(PrimitiveType.Cube, "Top Canopy", treeObject.transform,
            new Vector3(0f, TreeCanopyHeight + 3f, 0f), new Vector3(3.5f, 3.5f, 3.5f), leafMidMaterial)
            .transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 45f), 0f);
    }

    private void CreateRandomGrassKnolls()
    {
        for (int knollIndex = 0; knollIndex < KnollCount; knollIndex++)
        {
            if (!TryChooseDecorationPosition(
                    KnollSpawnExtent,
                    KnollMinimumSpacing,
                    knollPositions,
                    false,
                    KnollTreeClearance,
                    out Vector2 position))
            {
                continue;
            }

            knollPositions.Add(position);
            CreateGrassKnoll(position, knollIndex);
        }
    }

    private void CreateGrassKnoll(Vector2 position, int variationIndex)
    {
        GameObject knoll = new GameObject($"Grass Knoll {variationIndex + 1}");
        knoll.transform.SetParent(environmentRoot, false);
        knoll.transform.position = new Vector3(position.x, groundSurfaceY, position.y);
        knoll.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        float randomScale = Random.Range(KnollScaleMinimum, KnollScaleMaximum);
        CreateShape(PrimitiveType.Sphere, "Main Turf Mound", knoll.transform,
            new Vector3(0f, 0.3f, 0f), new Vector3(12f, 1.4f, 9f) * randomScale, grassMaterial);
        CreateShape(PrimitiveType.Sphere, "Turf Shoulder", knoll.transform,
            new Vector3(4f, 0.15f, -1.5f), new Vector3(7f, 0.9f, 6f) * randomScale, grassLightMaterial);
    }

    private bool TryChooseDecorationPosition(
        float halfExtent,
        float minimumSpacing,
        List<Vector2> existingPositions,
        bool keepOutsideArena,
        float additionalTreeClearance,
        out Vector2 selectedPosition)
    {
        float minimumSpacingSquared = minimumSpacing * minimumSpacing;
        float treeClearanceSquared = additionalTreeClearance * additionalTreeClearance;
        float pondClearanceSquared = PondDecorationClearance * PondDecorationClearance;

        for (int attempt = 0; attempt < MaximumDecorationAttempts; attempt++)
        {
            Vector2 candidate = new Vector2(
                Random.Range(-halfExtent, halfExtent),
                Random.Range(-halfExtent, halfExtent));

            if (keepOutsideArena &&
                Mathf.Abs(candidate.x) < TreeRingInnerExtent &&
                Mathf.Abs(candidate.y) < TreeRingInnerExtent)
            {
                continue;
            }

            if ((candidate - pondPosition).sqrMagnitude < pondClearanceSquared)
            {
                continue;
            }

            bool isTooClose = false;
            if (!keepOutsideArena && existingPositions == knollPositions)
            {
                foreach (Vector2 treePosition in treePositions)
                {
                    if ((treePosition - candidate).sqrMagnitude < treeClearanceSquared)
                    {
                        isTooClose = true;
                        break;
                    }
                }
            }

            foreach (Vector2 existingPosition in existingPositions)
            {
                if ((existingPosition - candidate).sqrMagnitude < minimumSpacingSquared)
                {
                    isTooClose = true;
                    break;
                }
            }

            if (isTooClose)
            {
                continue;
            }

            selectedPosition = candidate;
            return true;
        }

        selectedPosition = Vector2.zero;
        return false;
    }

    private void CreatePond()
    {
        float pondSign = Random.value < 0.5f ? -1f : 1f;
        pondPosition = new Vector2(Random.Range(-42f, 42f), pondSign * Random.Range(48f, 56f));
        GameObject pond = new GameObject("Pond Terrain Feature");
        pond.transform.SetParent(environmentRoot, false);
        pond.transform.position = new Vector3(pondPosition.x, groundSurfaceY, pondPosition.y);
        pond.transform.rotation = Quaternion.Euler(0f, Random.Range(-24f, 24f), 0f);

        CreateShape(PrimitiveType.Cylinder, "Sandy Pond Edge", pond.transform,
            new Vector3(0f, GroundSurfaceOffset, 0f), new Vector3(21f, 0.06f, 14f), pondBankMaterial);
        CreateShape(PrimitiveType.Cylinder, "Blue Pond", pond.transform,
            new Vector3(0f, GroundSurfaceOffset + 0.05f, 0f), new Vector3(18f, 0.045f, 11f), pondWaterMaterial);

        CreateShape(PrimitiveType.Sphere, "Pond Grass Patch A", pond.transform,
            new Vector3(-13f, GroundSurfaceOffset, -4f), new Vector3(8f, 0.35f, 5f), grassMaterial);
        CreateShape(PrimitiveType.Sphere, "Pond Grass Patch B", pond.transform,
            new Vector3(14f, GroundSurfaceOffset, 5f), new Vector3(7f, 0.3f, 5f), grassLightMaterial);
    }

    private GameObject CreateShape(
        PrimitiveType primitiveType,
        string shapeName,
        Transform parent,
        Vector3 localPosition,
        Vector3 localScale,
        Material material)
    {
        GameObject shape = GameObject.CreatePrimitive(primitiveType);
        shape.name = shapeName;
        shape.transform.SetParent(parent, false);
        shape.transform.localPosition = localPosition;
        shape.transform.localScale = localScale;

        Collider shapeCollider = shape.GetComponent<Collider>();
        if (shapeCollider != null)
        {
            Destroy(shapeCollider);
        }

        Renderer shapeRenderer = shape.GetComponent<Renderer>();
        if (shapeRenderer != null)
        {
            shapeRenderer.sharedMaterial = material;
            shapeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            shapeRenderer.receiveShadows = true;
        }

        return shape;
    }

    private Material CreateRuntimeMaterial(string materialName, Color color)
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

        runtimeMaterials.Add(material);
        return material;
    }

    private void OnDestroy()
    {
        foreach (Material material in runtimeMaterials)
        {
            if (material != null)
            {
                Destroy(material);
            }
        }

        if (groundGrassTexture != null)
        {
            Destroy(groundGrassTexture);
        }

        if (groundMaterial != null)
        {
            Destroy(groundMaterial);
        }
    }
}
