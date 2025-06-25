using UnityEngine;

public class SubdivisionComparison : MonoBehaviour
{
    [Header("Comparison Settings")]
    public Material loopMaterial;
    public Material kobbeltMaterial;
    public bool showWireframes = true;
    public int subdivisionLevels = 1;
    
    [Header("Spacing")]
    public float cubeSpacing = 4f;
    
    void Start()
    {
        CreateComparisonScene();
    }
    
    void CreateComparisonScene()
    {
        // Créer le cube original (référence)
        CreateReferenceCube();
        
        // Créer le cube avec Loop subdivision
        CreateLoopCube();
        
        // Créer le cube avec √3-Kobbelt subdivision
        CreateKobbeltCube();
        
        // Positionner la caméra pour voir les trois cubes
        SetupCamera();
        
        // Créer des labels
        CreateLabels();
        CreateCatmullClarkCube();
    }
    
    void CreateReferenceCube()
    {
        GameObject cubeObject = new GameObject("Original Cube");
        cubeObject.transform.position = new Vector3(-cubeSpacing, 0, 0);
        
        MeshFilter meshFilter = cubeObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = cubeObject.AddComponent<MeshRenderer>();
        
        meshFilter.mesh = CreateTriangulatedCubeMesh();
        
        // Matériau gris pour l'original
        Material originalMat = new Material(Shader.Find("Standard"));
        originalMat.color = Color.gray;
        meshRenderer.material = originalMat;
        
        if (showWireframes)
        {
            AddWireframe(cubeObject, Color.white);
        }
    }
    void CreateCatmullClarkCube()
    {
        GameObject cubeObject = new GameObject("Catmull-Clark Subdivision Cube");
        cubeObject.transform.position = new Vector3(2 * cubeSpacing, 0, 0);
    
        MeshFilter meshFilter = cubeObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = cubeObject.AddComponent<MeshRenderer>();

        meshFilter.mesh = CreateTriangulatedCubeMesh(); // Utiliser la même base

        // Matériau (violet clair)
        Material ccMat = new Material(Shader.Find("Standard"));
        ccMat.color = new Color(0.6f, 0.4f, 1f); // light purple
        meshRenderer.material = ccMat;

        // Ajouter le script de subdivision Catmull-Clark
        CatmullClark ccScript = cubeObject.AddComponent<CatmullClark>();
        ccScript.subdivisionLevels = subdivisionLevels;

        if (showWireframes)
        {
            AddWireframe(cubeObject, new Color(0.7f, 0.3f, 1f)); // wireframe violet
        }
    }

    void CreateLoopCube()
    {
        GameObject cubeObject = new GameObject("Loop Subdivision Cube");
        cubeObject.transform.position = new Vector3(0, 0, 0);
        
        MeshFilter meshFilter = cubeObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = cubeObject.AddComponent<MeshRenderer>();
        
        meshFilter.mesh = CreateTriangulatedCubeMesh();
        
        // Matériau pour Loop
        if (loopMaterial != null)
        {
            meshRenderer.material = loopMaterial;
        }
        else
        {
            Material loopMat = new Material(Shader.Find("Standard"));
            loopMat.color = Color.cyan;
            meshRenderer.material = loopMat;
        }
        
        // Ajouter le script Loop
        Loop loopScript = cubeObject.AddComponent<Loop>();
        loopScript.subdivisionLevels = subdivisionLevels;
        
        if (showWireframes)
        {
            AddWireframe(cubeObject, Color.blue);
        }
    }
    
    void CreateKobbeltCube()
    {
        GameObject cubeObject = new GameObject("√3-Kobbelt Subdivision Cube");
        cubeObject.transform.position = new Vector3(cubeSpacing, 0, 0);
        
        MeshFilter meshFilter = cubeObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = cubeObject.AddComponent<MeshRenderer>();
        
        meshFilter.mesh = CreateTriangulatedCubeMesh();
        
        // Matériau pour Kobbelt
        if (kobbeltMaterial != null)
        {
            meshRenderer.material = kobbeltMaterial;
        }
        else
        {
            Material kobbeltMat = new Material(Shader.Find("Standard"));
            kobbeltMat.color = Color.magenta;
            meshRenderer.material = kobbeltMat;
        }
        
        // Ajouter le script Kobbelt
        KobbeltSubdivision kobbeltScript = cubeObject.AddComponent<KobbeltSubdivision>();
        kobbeltScript.subdivisionLevels = subdivisionLevels;
        
        if (showWireframes)
        {
            AddWireframe(cubeObject, Color.red);
        }
    }
    
    Mesh CreateTriangulatedCubeMesh()
    {
        Mesh mesh = new Mesh();
        
        // Vertices du cube
        Vector3[] vertices = new Vector3[]
        {
            // Face avant
            new Vector3(-1, -1,  1), // 0
            new Vector3( 1, -1,  1), // 1
            new Vector3( 1,  1,  1), // 2
            new Vector3(-1,  1,  1), // 3
            
            // Face arrière
            new Vector3(-1, -1, -1), // 4
            new Vector3( 1, -1, -1), // 5
            new Vector3( 1,  1, -1), // 6
            new Vector3(-1,  1, -1), // 7
        };
        
        // Triangles (2 triangles par face)
        int[] triangles = new int[]
        {
            // Face avant
            0, 2, 1,  0, 3, 2,
            // Face droite
            1, 2, 6,  1, 6, 5,
            // Face arrière
            5, 6, 7,  5, 7, 4,
            // Face gauche
            4, 7, 3,  4, 3, 0,
            // Face haut
            3, 7, 6,  3, 6, 2,
            // Face bas
            4, 0, 1,  4, 1, 5
        };
        
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        
        return mesh;
    }
    
    void AddWireframe(GameObject target, Color wireframeColor)
    {
        GameObject wireframeObject = new GameObject("Wireframe");
        wireframeObject.transform.SetParent(target.transform);
        wireframeObject.transform.localPosition = Vector3.zero;
        wireframeObject.transform.localScale = Vector3.one * 1.001f;
        
        MeshFilter wireframeMeshFilter = wireframeObject.AddComponent<MeshFilter>();
        MeshRenderer wireframeMeshRenderer = wireframeObject.AddComponent<MeshRenderer>();
        
        wireframeMeshFilter.mesh = target.GetComponent<MeshFilter>().mesh;
        
        Material wireframeMaterial = new Material(Shader.Find("Unlit/Color"));
        wireframeMaterial.color = wireframeColor;
        wireframeMeshRenderer.material = wireframeMaterial;
        
        WireframeRenderer wireframeScript = wireframeObject.AddComponent<WireframeRenderer>();
    }
    
    void SetupCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            mainCamera.transform.position = new Vector3(0, 3, 8); // un peu plus loin
            mainCamera.transform.LookAt(new Vector3(cubeSpacing / 2f, 0, 0)); // centré
        }
    }

    
    void CreateLabels()
    {
        // Créer des GameObjects avec des noms descriptifs pour l'inspector
        GameObject labelParent = new GameObject("=== SUBDIVISION COMPARISON ===");
        
        GameObject originalLabel = new GameObject("📦 Original Cube (Left)");
        originalLabel.transform.SetParent(labelParent.transform);
        
        GameObject loopLabel = new GameObject("🔵 Loop Subdivision (Center)");
        loopLabel.transform.SetParent(labelParent.transform);
        
        GameObject kobbeltLabel = new GameObject("🔴 √3-Kobbelt Subdivision (Right)");
        kobbeltLabel.transform.SetParent(labelParent.transform);
        
        GameObject infoLabel = new GameObject($"ℹ️ Subdivision Levels: {subdivisionLevels}");
        infoLabel.transform.SetParent(labelParent.transform);
        
        GameObject catmullClarkLabel = new GameObject("🟣 Catmull-Clark Subdivision (Far Right)");
        catmullClarkLabel.transform.SetParent(labelParent.transform);

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
        // Trouver et mettre à jour les scripts de subdivision
        Loop loopScript = FindObjectOfType<Loop>();
        if (loopScript != null)
        {
            loopScript.subdivisionLevels = subdivisionLevels;
            loopScript.ApplyLoopSubdivision();
        }
        
        KobbeltSubdivision kobbeltScript = FindObjectOfType<KobbeltSubdivision>();
        if (kobbeltScript != null)
        {
            kobbeltScript.subdivisionLevels = subdivisionLevels;
            kobbeltScript.ApplyKobbeltSubdivision();
        }
        CatmullClark ccScript = FindObjectOfType<CatmullClark>();
        if (ccScript != null)
        {
            ccScript.subdivisionLevels = subdivisionLevels;
            ccScript.ApplyCatmullClarkSubdivision();
        }

        
        
        Debug.Log($"Updated subdivision levels to: {subdivisionLevels}");
    }
}