using UnityEngine;

public class DemoMapCatmull : MonoBehaviour
{
    [Header("Catmull-Clark Settings")]
    public Material catmullMaterial;
    public bool showWireframes = true;
    public int subdivisionLevels = 1;

    [Header("Random Terrain Settings")]
    public int gridSize = 10;
    public float heightScale = 2f;

    private GameObject terrainObject;

    void Start()
    {
        Mesh terrainMesh = GenerateRandomTerrainMesh(gridSize, heightScale);
        CreateCatmullClarkObject(terrainMesh, "Catmull-Clark Terrain");
    }

    /// <summary>
    /// Génère un mesh de terrain à base de Perlin Noise.
    /// </summary>
    Mesh GenerateRandomTerrainMesh(int size, float heightScale)
    {
        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[(size + 1) * (size + 1)];
        int[] triangles = new int[size * size * 6];

        for (int z = 0; z <= size; z++)
        {
            for (int x = 0; x <= size; x++)
            {
                float y = Mathf.PerlinNoise(x * 0.3f, z * 0.3f) * heightScale;
                vertices[z * (size + 1) + x] = new Vector3(x, y, z);
            }
        }

        int t = 0;
        for (int z = 0; z < size; z++)
        {
            for (int x = 0; x < size; x++)
            {
                int i = z * (size + 1) + x;

                // Triangle 1
                triangles[t++] = i;
                triangles[t++] = i + size + 1;
                triangles[t++] = i + 1;

                // Triangle 2
                triangles[t++] = i + 1;
                triangles[t++] = i + size + 1;
                triangles[t++] = i + size + 2;
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// Crée l'objet terrain avec subdivision Catmull-Clark.
    /// </summary>
    void CreateCatmullClarkObject(Mesh mesh, string name)
    {
        terrainObject = new GameObject(name);
        terrainObject.transform.position = Vector3.zero;

        var meshFilter = terrainObject.AddComponent<MeshFilter>();
        var meshRenderer = terrainObject.AddComponent<MeshRenderer>();

        meshFilter.mesh = mesh;

        // Appliquer matériau
        Material mat = catmullMaterial != null
            ? catmullMaterial
            : new Material(Shader.Find("Standard")) { color = new Color(0.6f, 0.4f, 1f) };

        meshRenderer.material = mat;

        // Ajouter script Catmull-Clark
        var cc = terrainObject.AddComponent<CatmullClark>();
        cc.subdivisionLevels = subdivisionLevels;

        // Afficher wireframe optionnel
     
        SetupCamera();
    }

    /// <summary>
    /// Ajoute un wireframe non interactif.
    /// </summary>
    void AddWireframe(GameObject target, Color wireframeColor)
    {
        var wireframeObject = new GameObject("Wireframe");
        wireframeObject.transform.SetParent(target.transform);
        wireframeObject.transform.localPosition = Vector3.zero;
        wireframeObject.transform.localScale = Vector3.one * 1.001f;

        var meshFilter = wireframeObject.AddComponent<MeshFilter>();
        var meshRenderer = wireframeObject.AddComponent<MeshRenderer>();

        meshFilter.mesh = target.GetComponent<MeshFilter>().mesh;

        var material = new Material(Shader.Find("Unlit/Color")) { color = wireframeColor };
        meshRenderer.material = material;

        wireframeObject.AddComponent<WireframeRenderer>();
    }

    /// <summary>
    /// Positionne automatiquement la caméra.
    /// </summary>
    void SetupCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;

        cam.transform.position = new Vector3(gridSize / 2f, heightScale * 3f, -gridSize * 1.5f);
        cam.transform.LookAt(new Vector3(gridSize / 2f, 0, gridSize / 2f));
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
        if (terrainObject == null) return;

        var cc = terrainObject.GetComponent<CatmullClark>();
        if (cc != null)
        {
            cc.subdivisionLevels = subdivisionLevels;
            cc.ApplyCatmullClarkSubdivision();
        }

        Debug.Log($"Subdivision level set to: {subdivisionLevels}");
    }
}
