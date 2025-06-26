using System.Collections.Generic;
using UnityEngine;

public class DemoButterfly : MonoBehaviour
{
    [Header("Réglages")]
    public Material material;

    [Header("Subdivision")]
    [Range(0, 4)]
    public int subdivisionLevels = 1;

    [Header("Options")]
    public bool autoSubdivideOnStart = true;
    public bool showDebugPoints = false;
    public bool showDebugLogs = false;

    private Mesh originalMesh;

    public enum MeshType { Cube, Terrain }
    [Header("Type de mesh")]
    public MeshType meshType = MeshType.Cube;

    MeshType previousMeshType;

    void Awake()
    {
        previousMeshType = meshType;
    }

    void Start()
    {
        SetupMesh();

        if (autoSubdivideOnStart)
            ApplySubdivision();
    }

    void SetupMesh()
    {
        gameObject.name = "ButterflyMesh";

        MeshFilter mf = GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();
        MeshRenderer mr = GetComponent<MeshRenderer>() ?? gameObject.AddComponent<MeshRenderer>();

        if (meshType == MeshType.Cube)
            originalMesh = CreateTriangulatedCubeMesh();
        else
            originalMesh = CreateBumpyPlane(width: 10, height: 10, resolution: 20, heightScale: 1.5f);

        mf.mesh = originalMesh;
        ApplyMaterial(mr);
        transform.position = Vector3.zero;
    }

    [ContextMenu("Apply Subdivision")]
    public void ApplySubdivision()
    {
        Mesh subdivided = originalMesh;
        for (int i = 0; i < subdivisionLevels; i++)
            subdivided = new ButterflySubdivision().SubdivideButterfly(subdivided);

        subdivided.RecalculateNormals();
        GetComponent<MeshFilter>().mesh = subdivided;

        Debug.Log($"✅ Subdivision appliquée avec {subdivisionLevels} niveau(x). Vertices: {originalMesh.vertexCount} → {subdivided.vertexCount}");
    }

    [ContextMenu("Reset to Original")]
    public void ResetToOriginal()
    {
        GetComponent<MeshFilter>().mesh = originalMesh;
        Debug.Log("🔄 Mesh reset to original");
    }

    void ApplyMaterial(MeshRenderer mr)
    {
        if (material != null)
        {
            mr.material = material;
        }
        else
        {
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = Color.cyan;
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.3f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.6f);
            mr.material = mat;
        }
    }

    void OnValidate()
    {
        if (previousMeshType != meshType)
        {
            SetupMesh();
            previousMeshType = meshType;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Plus) || Input.GetKeyDown(KeyCode.KeypadPlus))
        {
            if (subdivisionLevels < 4)
            {
                subdivisionLevels++;
                ApplySubdivision();
            }
        }
        else if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
        {
            if (subdivisionLevels > 0)
            {
                subdivisionLevels--;
                if (subdivisionLevels == 0)
                    ResetToOriginal();
                else
                    ApplySubdivision();
            }
        }
        else if (Input.GetKeyDown(KeyCode.R))
        {
            ResetToOriginal();
        }
        else if (Input.GetKeyDown(KeyCode.Space))
        {
            ApplySubdivision();
        }
    }

    Mesh CreateTriangulatedCubeMesh()
    {
        Mesh mesh = new Mesh { name = "TriangulatedCube" };

        Vector3[] vertices = {
            new Vector3(-1, -1,  1), new Vector3( 1, -1,  1),
            new Vector3( 1,  1,  1), new Vector3(-1,  1,  1),
            new Vector3(-1, -1, -1), new Vector3( 1, -1, -1),
            new Vector3( 1,  1, -1), new Vector3(-1,  1, -1)
        };

        int[] triangles = {
            0, 1, 2, 0, 2, 3,
            1, 5, 6, 1, 6, 2,
            5, 4, 7, 5, 7, 6,
            4, 0, 3, 4, 3, 7,
            3, 2, 6, 3, 6, 7,
            0, 4, 5, 0, 5, 1
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    Mesh CreateBumpyPlane(int width, int height, int resolution, float heightScale)
    {
        Mesh mesh = new Mesh { name = "BumpyPlane" };

        List<Vector3> vertices = new();
        List<int> triangles = new();

        for (int y = 0; y <= resolution; y++)
        {
            for (int x = 0; x <= resolution; x++)
            {
                float xf = (float)x / resolution * width - width / 2f;
                float yf = (float)y / resolution * height - height / 2f;
                float z = Mathf.PerlinNoise(xf * 0.2f, yf * 0.2f) * heightScale;
                vertices.Add(new Vector3(xf, z, yf));
            }
        }

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int i = y * (resolution + 1) + x;
                int iRight = i + 1;
                int iDown = i + resolution + 1;
                int iDownRight = iDown + 1;
                triangles.AddRange(new[] { i, iDown, iRight });
                triangles.AddRange(new[] { iRight, iDown, iDownRight });
            }
        }

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    void OnDrawGizmos()
    {
        if (!showDebugPoints) return;

        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;

        Gizmos.color = Color.red;
        foreach (Vector3 v in mf.sharedMesh.vertices)
            Gizmos.DrawSphere(transform.TransformPoint(v), 0.02f);
    }

}
