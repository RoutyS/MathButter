using UnityEngine;

public class DemoLoop : MonoBehaviour
{
    [Header("Loop Subdivision Settings")]
    public Material loopMaterial;
    public bool showWireframes = true;
    public int subdivisionLevels = 1;

    void Start()
    {
        CreateLoopCube();
    }

    void CreateLoopCube()
    {
        GameObject cubeObject = new GameObject("Loop Subdivision Cube");
        cubeObject.transform.position = Vector3.zero;

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

        // Centrer la caméra sur ce cube
        SetupCamera();
    }

    Mesh CreateTriangulatedCubeMesh()
    {
        Mesh mesh = new Mesh();

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

        int[] triangles = new int[]
        {
            0, 2, 1,  0, 3, 2,     // Face avant
            1, 2, 6,  1, 6, 5,     // Face droite
            5, 6, 7,  5, 7, 4,     // Face arrière
            4, 7, 3,  4, 3, 0,     // Face gauche
            3, 7, 6,  3, 6, 2,     // Face haut
            4, 0, 1,  4, 1, 5      // Face bas
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
