// ✅ DemoButterfly.cs fusionné avec fonctionnalités de ButterflyMesh.cs
//    - Gestion des formes : Quad, Cube, Terrain, Icosphere
//    - Génération dynamique + subdivision Butterfly
//    - Tout est centralisé dans un seul script propre

using System.Collections.Generic;
using UnityEngine;

public class DemoButterfly : MonoBehaviour
{
    public Material material;
    [Range(0, 4)] public int subdivisionLevels = 1;
    public bool autoSubdivideOnStart = true;
    public bool showDebugPoints = false;
    public bool showWireframe = false;
    public bool showDebugLogs = false;
    public bool smoothAfterSubdivision = true;

    public enum MeshType { Quad, Cube, Terrain, Icosphere }

    [Header("Mesh Parameters")]
    public MeshType meshType = MeshType.Cube;


    [Header("Terrain Settings")]
    public int terrainResolution = 16;
    public float terrainSize = 8f;
    public float terrainHeight = 2f;

    [Header("Icosphere Settings")]
    public int icosphereSubdivisions = 1;
    public float icosphereRadius = 2f;

    private Mesh originalMesh;
    private ButterflySubdivision butterflySubdivision;
    private MeshType previousMeshType;

    void Awake()
    {
        previousMeshType = meshType;
        butterflySubdivision = GetComponent<ButterflySubdivision>();
        if (butterflySubdivision == null)
            butterflySubdivision = gameObject.AddComponent<ButterflySubdivision>();
    }

    void Start()
    {
        SetupMesh();
        if (autoSubdivideOnStart) ApplySubdivision();
    }

    void SetupMesh()
    {
        MeshFilter mf = GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();
        MeshRenderer mr = GetComponent<MeshRenderer>() ?? gameObject.AddComponent<MeshRenderer>();

        switch (meshType)
        {
            case MeshType.Quad:
                originalMesh = CreateQuad();
                break;
            case MeshType.Cube:
                originalMesh = CreateTriangulatedCubeMesh();
                break;
            case MeshType.Terrain:
                originalMesh = CreateBumpyPlane(terrainSize, terrainSize, terrainResolution, terrainHeight);
                break;
            case MeshType.Icosphere:
                originalMesh = CreateIcosphere(icosphereRadius, icosphereSubdivisions);
                break;
        }

        mf.mesh = originalMesh;
        ApplyMaterial(mr);
        transform.position = Vector3.zero;

        if (showDebugLogs)
        {
            Debug.Log($"[Mesh Created] {meshType} → Verts: {originalMesh.vertexCount}, Tris: {originalMesh.triangles.Length / 3}");
        }
    }

    [ContextMenu("Apply Subdivision")]
    public void ApplySubdivision()
    {
        if (originalMesh == null)
        {
            Debug.LogError("[!] No original mesh!");
            return;
        }

        Mesh subdivided = new Mesh
        {
            vertices = originalMesh.vertices,
            triangles = originalMesh.triangles
        };
        subdivided.RecalculateNormals();

        for (int i = 0; i < subdivisionLevels; i++)
        {
            subdivided = butterflySubdivision.SubdivideButterfly(subdivided);
            if (showDebugLogs)
                Debug.Log($"Subdivision {i + 1}/{subdivisionLevels} - Vertices: {subdivided.vertexCount}");
        }

        if (smoothAfterSubdivision)
            LaplacianSmooth(subdivided);

        GetComponent<MeshFilter>().mesh = subdivided;
    }

    public void ResetToOriginal()
    {
        if (originalMesh != null)
        {
            GetComponent<MeshFilter>().mesh = originalMesh;
            if (showDebugLogs) Debug.Log("[Reset] Reverted to original mesh");
        }
    }

    void LaplacianSmooth(Mesh mesh)
    {
        Vector3[] verts = mesh.vertices;
        int[] tris = mesh.triangles;
        var neighbors = new Dictionary<int, HashSet<int>>();

        for (int i = 0; i < tris.Length; i += 3)
        {
            int a = tris[i], b = tris[i + 1], c = tris[i + 2];
            AddNeighbor(neighbors, a, b); AddNeighbor(neighbors, a, c);
            AddNeighbor(neighbors, b, a); AddNeighbor(neighbors, b, c);
            AddNeighbor(neighbors, c, a); AddNeighbor(neighbors, c, b);
        }

        Vector3[] smoothed = new Vector3[verts.Length];
        for (int i = 0; i < verts.Length; i++)
        {
            if (!neighbors.ContainsKey(i)) { smoothed[i] = verts[i]; continue; }
            Vector3 avg = Vector3.zero;
            foreach (int n in neighbors[i]) avg += verts[n];
            avg /= neighbors[i].Count;
            smoothed[i] = Vector3.Lerp(verts[i], avg, 0.3f);
        }
        mesh.vertices = smoothed;
        mesh.RecalculateNormals();
    }

    void AddNeighbor(Dictionary<int, HashSet<int>> map, int a, int b)
    {
        if (!map.ContainsKey(a)) map[a] = new();
        map[a].Add(b);
    }

    void ApplyMaterial(MeshRenderer mr)
    {
        if (material != null) mr.material = material;
        else
        {
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = meshType == MeshType.Terrain ? new Color(0.4f, 0.8f, 0.3f) : Color.cyan;
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.1f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.4f);
            mr.material = mat;
        }
    }

    void OnValidate()
    {
        if (previousMeshType != meshType)
        {
            SetupMesh();
            previousMeshType = meshType;

            if (autoSubdivideOnStart)
            {
                ApplySubdivision();
            }
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
        else if (Input.GetKeyDown(KeyCode.W))
        {
            showWireframe = !showWireframe;
        }
    }

    Mesh CreateQuad()
    {
        Mesh mesh = new Mesh { name = "Quad" };
        mesh.vertices = new[]
        {
            new Vector3(0,0,0), new Vector3(1,0,0), new Vector3(1,0,1), new Vector3(0,0,1)
        };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateNormals();
        return mesh;
    }

    Mesh CreateTriangulatedCubeMesh()
    {
        Mesh mesh = new Mesh { name = "TriangulatedCube" };
        Vector3[] v = {
            new Vector3(-1,-1, 1), new Vector3(1,-1, 1), new Vector3(1, 1, 1), new Vector3(-1, 1, 1),
            new Vector3(-1,-1,-1), new Vector3(1,-1,-1), new Vector3(1, 1,-1), new Vector3(-1, 1,-1)
        };
        int[] t = {
            0,1,2, 0,2,3,
            1,5,6, 1,6,2,
            5,4,7, 5,7,6,
            4,0,3, 4,3,7,
            3,2,6, 3,6,7,
            4,5,1, 4,1,0
        };
        mesh.vertices = v; mesh.triangles = t; mesh.RecalculateNormals();
        return mesh;
    }

    Mesh CreateBumpyPlane(float width, float height, int resolution, float heightScale)
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
                float z = Mathf.PerlinNoise(xf * 0.1f, yf * 0.1f) * heightScale;
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
        return mesh;
    }

    Mesh CreateIcosphere(float radius, int subdivisions)
    {
        List<Vector3> vertices = new();
        List<int> triangles = new();
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        vertices.AddRange(new Vector3[] {
            new Vector3(-1,  t,  0), new Vector3( 1,  t,  0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0,-1,  t), new Vector3(0, 1,  t), new Vector3(0,-1,-t), new Vector3(0, 1,-t),
            new Vector3(t, 0,-1), new Vector3(t, 0, 1), new Vector3(-t,0,-1), new Vector3(-t,0, 1)
        });
        int[] faces = {
            0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
            1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
            3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
            4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
        };
        triangles.AddRange(faces);
        for (int i = 0; i < vertices.Count; i++)
            vertices[i] = vertices[i].normalized * radius;
        Mesh mesh = new Mesh { name = "Icosphere" };
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
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

    void OnRenderObject()
    {
        if (showWireframe)
        {
            MeshFilter mf = GetComponent<MeshFilter>();
            if (mf != null && mf.mesh != null)
            {
                GL.wireframe = true;
                Graphics.DrawMeshNow(mf.mesh, transform.localToWorldMatrix);
                GL.wireframe = false;
            }
        }
    }
}
