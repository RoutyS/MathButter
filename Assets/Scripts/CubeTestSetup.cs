using UnityEngine;

public class CubeTestSetup : MonoBehaviour
{
    [Header("Test Settings")]
    public Material testMaterial;
    public bool showWireframe = true;
    
    void Start()
    {
        CreateTestCube();
    }
    
    void CreateTestCube()
    {
        // Créer un GameObject pour le cube de test
        GameObject cubeObject = new GameObject("Loop Subdivision Test Cube");
        
        // Ajouter MeshFilter et MeshRenderer
        MeshFilter meshFilter = cubeObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = cubeObject.AddComponent<MeshRenderer>();
        
        // Créer un mesh de cube simple (triangulé)
        meshFilter.mesh = CreateTriangulatedCubeMesh();
        
        // Appliquer le matériau
        if (testMaterial != null)
        {
            meshRenderer.material = testMaterial;
        }
        else
        {
            // Créer un matériau par défaut
            Material defaultMat = new Material(Shader.Find("Standard"));
            defaultMat.color = Color.cyan;
            meshRenderer.material = defaultMat;
        }
        
        // Ajouter le script de subdivision Loop
        Loop loopScript = cubeObject.AddComponent<Loop>();
        loopScript.subdivisionLevels = 1;
        
        // Optionnel : ajouter wireframe
        if (showWireframe)
        {
            AddWireframeDisplay(cubeObject);
        }
        
        // Positionner la caméra pour voir le cube
        Camera.main.transform.position = new Vector3(3, 3, 3);
        Camera.main.transform.LookAt(cubeObject.transform.position);
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
    
    void AddWireframeDisplay(GameObject target)
    {
        // Créer un objet enfant pour le wireframe
        GameObject wireframeObject = new GameObject("Wireframe");
        wireframeObject.transform.SetParent(target.transform);
        wireframeObject.transform.localPosition = Vector3.zero;
        wireframeObject.transform.localScale = Vector3.one * 1.001f; // Légèrement plus grand
        
        MeshFilter wireframeMeshFilter = wireframeObject.AddComponent<MeshFilter>();
        MeshRenderer wireframeMeshRenderer = wireframeObject.AddComponent<MeshRenderer>();
        
        // Partager le mesh avec l'objet parent
        wireframeMeshFilter.mesh = target.GetComponent<MeshFilter>().mesh;
        
        // Matériau wireframe
        Material wireframeMaterial = new Material(Shader.Find("Unlit/Color"));
        wireframeMaterial.color = Color.black;
        wireframeMeshRenderer.material = wireframeMaterial;
        
        // Ajouter le script de wireframe
        WireframeRenderer wireframeScript = wireframeObject.AddComponent<WireframeRenderer>();
    }
}

// Script auxiliaire pour afficher le wireframe
public class WireframeRenderer : MonoBehaviour
{
    void OnRenderObject()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.mesh == null) return;
        
        Mesh mesh = meshFilter.mesh;
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        
        // Dessiner les lignes
        GL.Begin(GL.LINES);
        GL.Color(Color.black);
        
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 v0 = transform.TransformPoint(vertices[triangles[i]]);
            Vector3 v1 = transform.TransformPoint(vertices[triangles[i + 1]]);
            Vector3 v2 = transform.TransformPoint(vertices[triangles[i + 2]]);
            
            // Dessiner les 3 arêtes du triangle
            GL.Vertex(v0); GL.Vertex(v1);
            GL.Vertex(v1); GL.Vertex(v2);
            GL.Vertex(v2); GL.Vertex(v0);
        }
        
        GL.End();
    }
}