using UnityEngine;

public class KobbeltSubdivisionDemo : MonoBehaviour
{
    [Header("√3 Subdivision Settings")]
    public Material kobbeltMaterial;
    public bool showWireframe = true;
    public int subdivisionLevels = 1;
    public Mesh inputMesh; // Nouveau: permet d'assigner n'importe quel mesh
    
    void Start()
    {
        if (inputMesh != null)
        {
            CreateKobbeltObject(inputMesh, "√3-Kobbelt Subdivision Object");
        }
        else
        {
            Debug.LogWarning("Aucun mesh assigné, création d'un cube par défaut");
            CreateKobbeltCube();
        }
    }
    
    void CreateKobbeltObject(Mesh mesh, string name)
    {
        GameObject obj = new GameObject(name);
        obj.transform.position = Vector3.zero;
        
        MeshFilter meshFilter = obj.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = obj.AddComponent<MeshRenderer>();
        
        meshFilter.mesh = mesh;
        
        if (kobbeltMaterial != null)
        {
            meshRenderer.material = kobbeltMaterial;
        }
        else
        {
            Material defaultMat = new Material(Shader.Find("Standard"));
            defaultMat.color = Color.magenta;
            meshRenderer.material = defaultMat;
        }
        
        // Ajouter le script de subdivision Kobbelt
        KobbeltSubdivision kobbeltScript = obj.AddComponent<KobbeltSubdivision>();
        kobbeltScript.subdivisionLevels = subdivisionLevels;
        
    
        
        SetupCamera();
    }
    
    void CreateKobbeltCube()
    {
        GameObject cubeObject = new GameObject("√3-Kobbelt Subdivision Cube");
        cubeObject.transform.position = Vector3.zero;
        
        MeshFilter meshFilter = cubeObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = cubeObject.AddComponent<MeshRenderer>();
        
        meshFilter.mesh = CreateTriangulatedCubeMesh();
        
        if (kobbeltMaterial != null)
        {
            meshRenderer.material = kobbeltMaterial;
        }
        else
        {
            Material defaultMat = new Material(Shader.Find("Standard"));
            defaultMat.color = Color.magenta;
            meshRenderer.material = defaultMat;
        }
        
        // Ajouter le script de subdivision Kobbelt
        KobbeltSubdivision kobbeltScript = cubeObject.AddComponent<KobbeltSubdivision>();
        kobbeltScript.subdivisionLevels = subdivisionLevels;
        
        if (showWireframe)
        {
            AddWireframe(cubeObject, Color.red);
        }
        
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
            0, 2, 1,  0, 3, 2,
            1, 2, 6,  1, 6, 5,
            5, 6, 7,  5, 7, 4,
            4, 7, 3,  4, 3, 0,
            3, 7, 6,  3, 6, 2,
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
        
        wireframeObject.AddComponent<WireframeRenderer>();
    }
    
    void SetupCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            mainCamera.transform.position = new Vector3(0, 3, 8);
            mainCamera.transform.LookAt(Vector3.zero);
        }
    }
    
    [ContextMenu("Increase Subdivision Level")]
    public void IncreaseSubdivisionLevel()
    {
        subdivisionLevels++;
        UpdateSubdivisionLevel();
    }
    
    [ContextMenu("Decrease Subdivision Level")]
    public void DecreaseSubdivisionLevel()
    {
        subdivisionLevels = Mathf.Max(0, subdivisionLevels - 1);
        UpdateSubdivisionLevel();
    }
    
    void UpdateSubdivisionLevel()
    {
        KobbeltSubdivision kobbeltScript = FindObjectOfType<KobbeltSubdivision>();
        if (kobbeltScript != null)
        {
            kobbeltScript.subdivisionLevels = subdivisionLevels;
            kobbeltScript.ApplyKobbeltSubdivision();
            Debug.Log($"Subdivision level updated to {subdivisionLevels}");
        }
    }
}