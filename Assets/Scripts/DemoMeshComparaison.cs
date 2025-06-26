using System.Collections.Generic;
using UnityEngine;

public class DemoMeshComparaison : MonoBehaviour
{
    [Header("Loop Subdivision Settings")]
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

        // Génération des sommets et UVs
        for (int y = 0; y <= height; y++)
        {
            for (int x = 0; x <= width; x++)
            {
                float xPos = x * scale;
                float yPos = 0f; // tu peux ajouter du bruit ici si tu veux un relief
                float zPos = y * scale;

                vertices.Add(new Vector3(xPos, yPos, zPos));

                // UV normalisé de 0 à 1
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

                // Triangle 1
                triangles.Add(i);
                triangles.Add(iBelow);
                triangles.Add(iRight);

                // Triangle 2
                triangles.Add(iRight);
                triangles.Add(iBelow);
                triangles.Add(iBelowRight);
            }
        }

        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);  // ASSIGNE LES UVs ICI
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

        CreateLoopObject(inputMesh, "Loop Subdivision Object");
        Debug.Log("Mesh appliqué depuis l'inspector !");
    }

    void CreateLoopObject(Mesh mesh, string name)
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
            // fallback: un matériau blanc standard si pas de damier assigné
            Material defaultMat = new Material(Shader.Find("Standard"));
            defaultMat.color = Color.white;
            meshRenderer.material = defaultMat;
        }

        Loop loopScript = obj.AddComponent<Loop>();
        loopScript.subdivisionLevels = subdivisionLevels;

        SetupCamera();
    }

    void CreateLoopCube()
    {
        GameObject cubeObject = new GameObject("Loop Subdivision Cube");
        cubeObject.transform.SetParent(transform);
        cubeObject.transform.localPosition = Vector3.zero;

        MeshFilter meshFilter = cubeObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = cubeObject.AddComponent<MeshRenderer>();

        meshFilter.mesh = CreateTriangulatedCubeMesh();

        if (loopMaterial != null)
        {
            meshRenderer.material = loopMaterial;
        }
        else
        {
            // Tu peux mettre une couleur par défaut ici ou un autre matériau par défaut
            Material defaultMat = new Material(Shader.Find("Standard"));
            defaultMat.color = Color.white;
            meshRenderer.material = defaultMat;
        }

        Loop loopScript = cubeObject.AddComponent<Loop>();
        loopScript.subdivisionLevels = subdivisionLevels;

        SetupCamera();
    }

    Mesh CreateTriangulatedCubeMesh()
    {
        Mesh mesh = new Mesh();

        Vector3[] vertices = new Vector3[]
        {
            new Vector3(-1, -1,  1), // 0
            new Vector3( 1, -1,  1), // 1
            new Vector3( 1,  1,  1), // 2
            new Vector3(-1,  1,  1), // 3
            new Vector3(-1, -1, -1), // 4
            new Vector3( 1, -1, -1), // 5
            new Vector3( 1,  1, -1), // 6
            new Vector3(-1,  1, -1), // 7
        };

        int[] triangles = new int[]
        {
            0, 2, 1,  0, 3, 2,     // Avant
            1, 2, 6,  1, 6, 5,     // Droite
            5, 6, 7,  5, 7, 4,     // Arrière
            4, 7, 3,  4, 3, 0,     // Gauche
            3, 7, 6,  3, 6, 2,     // Haut
            4, 0, 1,  4, 1, 5      // Bas
        };

        // UVs simples pour chaque sommet (une approximation pour un cube)
        Vector2[] uvs = new Vector2[]
        {
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1),
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1),
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;  // ASSIGNATION UVS ICI
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
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
        Loop loopScript = FindObjectOfType<Loop>();
        if (loopScript != null)
        {
            loopScript.subdivisionLevels = subdivisionLevels;
            loopScript.ApplyLoopSubdivision();
        }

        Debug.Log($"Updated subdivision levels to: {subdivisionLevels}");
    }
}
