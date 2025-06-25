using UnityEngine;

public class DemoCatmull : MonoBehaviour
{
    [Header("Catmull-Clark Settings")]
    public bool showWireframes = true;
    public int subdivisionLevels = 1;

    void Start()
    {
        CreateCatmullClarkCube();
    }

    void CreateCatmullClarkCube()
    {
        GameObject cubeObject = new GameObject("Catmull-Clark Subdivision Cube");
        cubeObject.transform.position = Vector3.zero;

        MeshFilter meshFilter = cubeObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = cubeObject.AddComponent<MeshRenderer>();

        meshFilter.mesh = CreateTriangulatedCubeMesh();

        // Matériau violet clair
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
        CatmullClark ccScript = FindObjectOfType<CatmullClark>();
        if (ccScript != null)
        {
            ccScript.subdivisionLevels = subdivisionLevels;
            ccScript.ApplyCatmullClarkSubdivision();
        }

        Debug.Log($"Updated subdivision levels to: {subdivisionLevels}");
    }
}
