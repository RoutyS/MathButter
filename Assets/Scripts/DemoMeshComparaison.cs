using System.Collections.Generic;
using UnityEngine;

public enum SubdivisionType
{
    Loop,
    Kobbelt,
    CatmullClark
}

public class DemoMeshComparaison : MonoBehaviour
{
    [Header("Subdivision Settings")]
    public SubdivisionType subdivisionType = SubdivisionType.Loop;

    public Material loopMaterial;
    public bool showWireframes = true;
    public int subdivisionLevels = 1;
    public Mesh inputMesh;

    private Mesh lastAppliedMesh;

    void Start()
    {
        if (inputMesh == null)
        {
            Debug.Log("Aucun mesh assigné, génération d'un mesh type terrain...");
            inputMesh = GenerateTerrainLikeMesh(10, 10, 1f); // largeur, hauteur, échelle
        }

        ApplySelectedMesh();
    }

    Mesh GenerateTerrainLikeMesh(int width, int height, float scale)
    {
        Mesh mesh = new Mesh();
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        // Paramètres pour générer des pics
        float peakHeight = 3f;    // Hauteur max des pics
        float noiseScale = 2f;    // Échelle du bruit Perlin
        float spikeChance = 0.1f; // Probabilité d'avoir un pic à un sommet

        for (int y = 0; y <= height; y++)
        {
            for (int x = 0; x <= width; x++)
            {
                float xPos = x * scale;
                float zPos = y * scale;

                // Base hauteur avec Perlin noise
                float baseHeight = Mathf.PerlinNoise(x * noiseScale / width, y * noiseScale / height);

                // Générer un pic avec une certaine probabilité
                float yPos = baseHeight;

                if (Random.value < spikeChance)
                {
                    // Pic pointu : on ajoute une valeur élevée multipliée par un facteur aléatoire
                    yPos += peakHeight * Random.Range(0.5f, 1f);
                }

                vertices.Add(new Vector3(xPos, yPos, zPos));

                float u = (float)x / width;
                float v = (float)y / height;
                uvs.Add(new Vector2(u, v));
            }
        }

        // Génération des triangles (2 par carré)
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = x + y * (width + 1);
                int iRight = i + 1;
                int iBelow = i + (width + 1);
                int iBelowRight = iBelow + 1;

                triangles.Add(i);
                triangles.Add(iBelow);
                triangles.Add(iRight);

                triangles.Add(iRight);
                triangles.Add(iBelow);
                triangles.Add(iBelowRight);
            }
        }

        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    [ContextMenu("Generate Terrain Mesh")]
    public void GenerateTerrainMesh()
    {
        inputMesh = GenerateTerrainLikeMesh(10, 10, 1f); // 10x10 "cases", chaque case fait 1x1 unité
        ApplySelectedMesh();
    }

    [ContextMenu("Appliquer le mesh manuellement")]
    public void ApplyMeshManually()
    {
        if (inputMesh != null)
        {
            ApplySelectedMesh();
            Debug.Log("Mesh appliqué manuellement !");
        }
        else
        {
            Debug.LogWarning("Aucun mesh assigné.");
        }
    }

    void Update()
    {
        if (Application.isPlaying)
        {
            if (inputMesh != null && inputMesh != lastAppliedMesh)
            {
                ApplySelectedMesh();
            }
        }
    }

    public void ApplySelectedMesh()
    {
        lastAppliedMesh = inputMesh;

        // Supprimer les anciens enfants (objets créés)
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        switch(subdivisionType)
        {
            case SubdivisionType.Loop:
                CreateSubdivisionObject<Loop>(inputMesh, "Loop Subdivision Object");
                break;

            case SubdivisionType.Kobbelt:
                CreateSubdivisionObject<KobbeltSubdivision>(inputMesh, "Kobbelt Subdivision Object");
                break;

            case SubdivisionType.CatmullClark:
                CreateSubdivisionObject<CatmullClark>(inputMesh, "Catmull-Clark Subdivision Object");
                break;
        }

        Debug.Log("Mesh appliqué avec subdivision : " + subdivisionType);
    }

    void CreateSubdivisionObject<T>(Mesh mesh, string name) where T : MonoBehaviour
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(transform);
        obj.transform.localPosition = Vector3.zero;

        MeshFilter meshFilter = obj.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = obj.AddComponent<MeshRenderer>();

        meshFilter.mesh = mesh;

        if (loopMaterial != null)
        {
            meshRenderer.material = loopMaterial;
        }
        else
        {
            Material defaultMat = new Material(Shader.Find("Standard"));
            defaultMat.color = Color.white;
            meshRenderer.material = defaultMat;
        }

        T subdivisionScript = obj.AddComponent<T>();

        // Essaie de configurer subdivisionLevels et appliquer la subdivision
        var subdivisionLevelsProperty = typeof(T).GetProperty("subdivisionLevels");
        var applySubdivisionMethod = typeof(T).GetMethod("ApplySubdivision");

        if (subdivisionLevelsProperty != null)
            subdivisionLevelsProperty.SetValue(subdivisionScript, subdivisionLevels);

        if (applySubdivisionMethod != null)
            applySubdivisionMethod.Invoke(subdivisionScript, null);

        SetupCamera();
    }

    void SetupCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            mainCamera.transform.position = new Vector3(0, 2, 6);
            mainCamera.transform.LookAt(Vector3.zero);
        }
    }

    [ContextMenu("Increase Subdivision Level")]
    public void IncreaseSubdivisionLevel()
    {
        subdivisionLevels++;
        UpdateSubdivisionLevels();
    }

    [ContextMenu("Decrease Subdivision Level")]
    public void DecreaseSubdivisionLevel()
    {
        subdivisionLevels = Mathf.Max(0, subdivisionLevels - 1);
        UpdateSubdivisionLevels();
    }

    void UpdateSubdivisionLevels()
    {
        // Trouve le script de subdivision actuellement actif
        MonoBehaviour subdivisionScript = null;

        // On cherche parmi les enfants (car on recrée un GameObject pour la subdivision)
        foreach (Transform child in transform)
        {
            subdivisionScript = child.GetComponent<Loop>() as MonoBehaviour;
            if (subdivisionScript != null) break;

            subdivisionScript = child.GetComponent<KobbeltSubdivision>() as MonoBehaviour;
            if (subdivisionScript != null) break;

            subdivisionScript = child.GetComponent<CatmullClark>() as MonoBehaviour;
            if (subdivisionScript != null) break;
        }

        if (subdivisionScript != null)
        {
            var subdivisionLevelsProperty = subdivisionScript.GetType().GetProperty("subdivisionLevels");
            var applySubdivisionMethod = subdivisionScript.GetType().GetMethod("ApplySubdivision");

            if (subdivisionLevelsProperty != null)
                subdivisionLevelsProperty.SetValue(subdivisionScript, subdivisionLevels);

            if (applySubdivisionMethod != null)
                applySubdivisionMethod.Invoke(subdivisionScript, null);
        }

        Debug.Log($"Updated subdivision levels to: {subdivisionLevels}");
    }
}
