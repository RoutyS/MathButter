using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ButterflySubdivision : MonoBehaviour
{
    // ‑‑‑ Paramètres de présentation --------------------------------------------------
    public Material material;
    [Range(0, 4)] public int subdivisionLevels = 1;
    public bool autoSubdivideOnStart = true;
    public bool showDebugPoints = false;
    public bool showWireframe = false;
    public bool showDebugLogs = false;
    public bool smoothAfterSubdivision = false;

    public enum MeshType { Quad, Cube, Terrain, Icosphere, Icosahedron }
    [Header("Mesh Parameters")]
    public MeshType meshType = MeshType.Cube;

    // Terrain
    [Header("Terrain Settings")]
    public int terrainResolution = 16;
    public float terrainSize = 8f;
    public float terrainHeight = 2f;

    // Icosphere
    [Header("Icosphere Settings")]
    public int icosphereSubdivisions = 1;
    public float icosphereRadius = 2f;

    // Icosahedron
    [Header("Icosahedron Settings")]
    public float icosahedronRadius = 2f;

    // ‑‑‑ Etat interne ---------------------------------------------------------------
    private Mesh originalMesh;
    private MeshType previousMeshType;

    // ================================================================================
    // ► Cycle de vie
    // ================================================================================
    void Awake()
    {
        previousMeshType = meshType;
    }

    void Start()
    {
        SetupMesh();
        if (autoSubdivideOnStart) ApplySubdivision();
    }

    void OnValidate()
    {
        if (previousMeshType != meshType)
        {
            SetupMesh();
            previousMeshType = meshType;
            if (autoSubdivideOnStart) ApplySubdivision();
        }
    }

    // ================================================================================
    // ► Méthodes publiques
    // ================================================================================

    [ContextMenu("Apply Subdivision")]
    public void ApplySubdivision()
    {
        if (originalMesh == null)
        {
            Debug.LogError("[Butterfly] Aucun mesh d'origine trouvé !");
            return;
        }

        Mesh current = Instantiate(originalMesh);
 
        current.RecalculateNormals();

        for (int i = 0; i < subdivisionLevels; i++)
        {
            current = SubdivideButterfly(current);
            if (showDebugLogs)
                Debug.Log($"Subdivision {i + 1}/{subdivisionLevels} – Verts: {current.vertexCount}");
        }

        if (smoothAfterSubdivision)
            LaplacianSmooth(current);

        GetComponent<MeshFilter>().mesh = current;
    }

    public void ResetToOriginal()
    {
        if (originalMesh != null)
            GetComponent<MeshFilter>().mesh = originalMesh;
    }

    // ================================================================================
    // ButterFly Subdivision
    // ================================================================================
    public Mesh SubdivideButterfly(Mesh input)
    {
        Vector3[] vertices = input.vertices.ToArray();
        int[] triangles = input.triangles.ToArray();


        // Construire une table de connectivité simple 
        var edgeToTriangles = BuildEdgeTriangleMap(triangles);
        var vertexToVertices = BuildVertexNeighborMap(triangles, vertices.Length);

        // Map pour éviter de créer plusieurs fois le même midpoint
        var edgeMidpointMap = new Dictionary<(int, int), int>();
        var newVertices = new List<Vector3>(vertices);

        List<int> newTriangles = new List<int>();

        // Pour chaque triangle original, créer 4 sous-triangles
        for (int t = 0; t < triangles.Length; t += 3)
        {
            int v0 = triangles[t];
            int v1 = triangles[t + 1];
            int v2 = triangles[t + 2];

            // Créer les midpoints avec Butterfly
            int m01 = GetOrCreateButterflyMidpoint(v0, v1, vertices, triangles, edgeToTriangles, vertexToVertices, edgeMidpointMap, newVertices);
            int m12 = GetOrCreateButterflyMidpoint(v1, v2, vertices, triangles, edgeToTriangles, vertexToVertices, edgeMidpointMap, newVertices);
            int m20 = GetOrCreateButterflyMidpoint(v2, v0, vertices, triangles, edgeToTriangles, vertexToVertices, edgeMidpointMap, newVertices);

            // Créer les 4 nouveaux triangles
            newTriangles.AddRange(new int[] {
                v0, m01, m20,    // Triangle coin v0
                v1, m12, m01,    // Triangle coin v1
                v2, m20, m12,    // Triangle coin v2
                m01, m12, m20    // Triangle central
            });
        }

        if (showDebugLogs)
            Debug.Log($"[Subdivision] Triangles: {triangles.Length / 3}, Verts: {vertices.Length}");


        Mesh result = new Mesh
        {
            vertices = newVertices.ToArray(),
            triangles = newTriangles.ToArray()
        };
        result.RecalculateNormals();
        return result;


    }

    // ================================================================================
    // CONSTRUCTION DE LA CONNECTIVITÉ SIMPLE
    // ================================================================================
    private Dictionary<(int, int), List<int>> BuildEdgeTriangleMap(int[] triangles)
    {
        var edgeToTriangles = new Dictionary<(int, int), List<int>>();

        for (int t = 0; t < triangles.Length; t += 3)
        {
            int triIndex = t / 3;
            int v0 = triangles[t];
            int v1 = triangles[t + 1];
            int v2 = triangles[t + 2];

            // Ajouter les 3 arêtes du triangle
            AddEdgeTriangle(edgeToTriangles, v0, v1, triIndex);
            AddEdgeTriangle(edgeToTriangles, v1, v2, triIndex);
            AddEdgeTriangle(edgeToTriangles, v2, v0, triIndex);
        }

        return edgeToTriangles;
    }

    private void AddEdgeTriangle(Dictionary<(int, int), List<int>> edgeToTriangles, int v1, int v2, int triIndex)
    {
        var edge = (Mathf.Min(v1, v2), Mathf.Max(v1, v2));
        if (!edgeToTriangles.ContainsKey(edge))
            edgeToTriangles[edge] = new List<int>();
        edgeToTriangles[edge].Add(triIndex);
    }

    private Dictionary<int, HashSet<int>> BuildVertexNeighborMap(int[] triangles, int vertexCount)
    {
        var vertexToVertices = new Dictionary<int, HashSet<int>>();

        for (int i = 0; i < vertexCount; i++)
            vertexToVertices[i] = new HashSet<int>();

        for (int t = 0; t < triangles.Length; t += 3)
        {
            int v0 = triangles[t];
            int v1 = triangles[t + 1];
            int v2 = triangles[t + 2];

            vertexToVertices[v0].Add(v1);
            vertexToVertices[v0].Add(v2);
            vertexToVertices[v1].Add(v0);
            vertexToVertices[v1].Add(v2);
            vertexToVertices[v2].Add(v0);
            vertexToVertices[v2].Add(v1);
        }

        return vertexToVertices;
    }

    // ================================================================================
    // ► CALCUL DU MIDPOINT BUTTERFLY
    // ================================================================================
    private int GetOrCreateButterflyMidpoint(int v1, int v2, Vector3[] vertices, int[] triangles,
        Dictionary<(int, int), List<int>> edgeToTriangles,
        Dictionary<int, HashSet<int>> vertexToVertices,
        Dictionary<(int, int), int> edgeMap, List<Vector3> newVertices)
    {
        var edge = (Mathf.Min(v1, v2), Mathf.Max(v1, v2));

        if (edgeMap.TryGetValue(edge, out int existingIndex))
            return existingIndex;

        Vector3 midpoint = CalculateButterflyMidpoint(v1, v2, vertices, triangles, edgeToTriangles, vertexToVertices);

        newVertices.Add(midpoint);
        int newIndex = newVertices.Count - 1;
        edgeMap[edge] = newIndex;

        return newIndex;
    }

    // ✅ Script avec gestion des 6 cas d'exception du Butterfly
    private Vector3 CalculateButterflyMidpoint(int v1, int v2,
    Vector3[] vertices, int[] triangles,
    Dictionary<(int, int), List<int>> edgeToTriangles,
    Dictionary<int, HashSet<int>> vertexToVertices)
    {
        // ---------- Helpers ----------
        bool IsBoundaryVertex(int vid)
        {
            foreach (int n in vertexToVertices[vid])
            {
                var e = (Mathf.Min(vid, n), Mathf.Max(vid, n));
                if (!edgeToTriangles.ContainsKey(e) || edgeToTriangles[e].Count == 1)
                    return true;       // au moins une arête de bord → sommet de bord
            }
            return false;
        }

        var edge = (Mathf.Min(v1, v2), Mathf.Max(v1, v2));
        Vector3 p1 = vertices[v1];
        Vector3 p2 = vertices[v2];

        // ─────────────────────────────────────────────────────────────────
        // 1) DÉTECTION DE BORD
        //    → Une arête est “bord” si UN SEUL triangle      (cas classique)
        //      ou si AU MOINS UN de ses sommets est bord (nouveaux sommets)
        // ─────────────────────────────────────────────────────────────────
        bool edgeHasOneTri = !edgeToTriangles.ContainsKey(edge) || edgeToTriangles[edge].Count == 1;
        bool isBoundary = edgeHasOneTri || IsBoundaryVertex(v1) || IsBoundaryVertex(v2);

        if (isBoundary)
        {
            // ↳ Cas 1.a / 1.b  → simple milieu, garantit la conservation du contour
            return 0.5f * (p1 + p2);
        }

        // ─────────────────────────────────────────────────────────────────
        // 2) CAS INTERNES (Butterfly complet ou partiel)
        // ─────────────────────────────────────────────────────────────────
        List<int> adjTris = edgeToTriangles[edge];
        List<int> opposite = new List<int>();

        foreach (int triIndex in adjTris)
        {
            int baseIdx = triIndex * 3;
            int[] tv = { triangles[baseIdx], triangles[baseIdx + 1], triangles[baseIdx + 2] };
            foreach (int v in tv)
                if (v != v1 && v != v2) { opposite.Add(v); break; }
        }

        // ---- CAS 2.b : aucune aile ----
        if (opposite.Count == 0)
            return 0.5f * (p1 + p2);

        // ---- CAS 2.a : une seule aile ----
        if (opposite.Count == 1)
            return 0.5f * (p1 + p2) + 0.125f * vertices[opposite[0]];

        // ---- CAS 2.c / 2.d : deux ailes (peut‑être adjacentes) ----
        Vector3 a = vertices[opposite[0]];
        Vector3 b = vertices[opposite[1]];

        Vector3 res = 0.5f * (p1 + p2) + 0.125f * (a + b);

        //  Supports -------------------------------------------------------
        List<int> supports = new List<int>();
        foreach (int n in vertexToVertices[v1])
            if (n != v2 && !opposite.Contains(n) && supports.Count < 4) supports.Add(n);
        foreach (int n in vertexToVertices[v2])
            if (n != v1 && !opposite.Contains(n) && !supports.Contains(n) && supports.Count < 4) supports.Add(n);

        bool oppAdjacent = vertexToVertices[opposite[0]].Contains(opposite[1]);
        float w = oppAdjacent ? -0.03125f : -0.0625f;
        if (supports.Count < 4) w *= 0.5f; // CAS 2.d : moitié d’ailes

        foreach (int s in supports) res += w * vertices[s];

        return res;
    }



    // ================================================================================
    // ► MÉTHODES D'INTERFACE ET UTILITAIRES
    // ================================================================================
    void SetupMesh()
    {
        MeshFilter mf = GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();
        MeshRenderer mr = GetComponent<MeshRenderer>() ?? gameObject.AddComponent<MeshRenderer>();

        switch (meshType)
        {
            case MeshType.Quad: originalMesh = CreateQuad(); break;
            case MeshType.Cube: originalMesh = CreateTriangulatedCubeMesh(); break;
            case MeshType.Terrain: originalMesh = CreateBumpyPlane(terrainSize, terrainSize, terrainResolution, terrainHeight); break;
            case MeshType.Icosphere: originalMesh = CreateIcosphere(icosphereRadius, icosphereSubdivisions); break;
            case MeshType.Icosahedron: originalMesh = CreateIcosahedron(icosahedronRadius); break;
        }

        mf.mesh = originalMesh;
        ApplyMaterial(mr);
        transform.position = Vector3.zero;

        if (showDebugLogs)
            Debug.Log($"[Mesh Created] {meshType} – Verts: {originalMesh.vertexCount}, Tris: {originalMesh.triangles.Length / 3}");
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Plus) || Input.GetKeyDown(KeyCode.KeypadPlus))
        {
            if (subdivisionLevels < 4) { subdivisionLevels++; ApplySubdivision(); }
        }
        else if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
        {
            if (subdivisionLevels > 0)
            {
                subdivisionLevels--;
                if (subdivisionLevels == 0) ResetToOriginal();
                else ApplySubdivision();
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
        if (!map.ContainsKey(a)) map[a] = new HashSet<int>();
        map[a].Add(b);
    }

    // ================================================================================
    // CRÉATION DES MESHES DE BASE
    // ================================================================================
    Mesh CreateQuad()
    {
        Mesh mesh = new Mesh { name = "Quad" };
        mesh.vertices = new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 1), new Vector3(0, 0, 1) };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateNormals();
        return mesh;
    }

    Mesh CreateTriangulatedCubeMesh()
    {
        Mesh mesh = new Mesh { name = "TriangulatedCube" };
        Vector3[] v = {
            new Vector3(-1,-1, 1), new Vector3( 1,-1, 1), new Vector3( 1, 1, 1), new Vector3(-1, 1, 1),
            new Vector3(-1,-1,-1), new Vector3( 1,-1,-1), new Vector3( 1, 1,-1), new Vector3(-1, 1,-1)
        };
        int[] t = {
            0,1,2, 0,2,3,
            1,5,6, 1,6,2,
            5,4,7, 5,7,6,
            4,0,3, 4,3,7,
            3,2,6, 3,6,7,
            4,5,1, 4,1,0
        };
        mesh.vertices = v;
        mesh.triangles = t;
        mesh.RecalculateNormals();
        return mesh;
    }

    Mesh CreateBumpyPlane(float width, float height, int resolution, float heightScale)
    {
        Mesh mesh = new Mesh { name = "BumpyPlane" };
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

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
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        vertices.AddRange(new Vector3[] {
            new Vector3(-1,  t,  0), new Vector3( 1,  t,  0), new Vector3(-1, -t,  0), new Vector3( 1, -t,  0),
            new Vector3( 0, -1,  t), new Vector3( 0,  1,  t), new Vector3( 0, -1, -t), new Vector3( 0,  1, -t),
            new Vector3( t,  0, -1), new Vector3( t,  0,  1), new Vector3(-t,  0, -1), new Vector3(-t,  0,  1)
        });
        int[] faces = {
            0,11,5,  0,5,1,   0,1,7,   0,7,10,  0,10,11,
            1,5,9,   5,11,4,  11,10,2, 10,7,6,  7,1,8,
            3,9,4,   3,4,2,   3,2,6,   3,6,8,   3,8,9,
            4,9,5,   2,4,11,  6,2,10,  8,6,7,   9,8,1
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



    // Création d'un Icosaèdre
    Mesh CreateIcosahedron(float radius)
    {
        Mesh mesh = new Mesh { name = "Icosahedron" };

        
        float phi = (1f + Mathf.Sqrt(5f)) / 2f;

        
        Vector3[] vertices = new Vector3[]
        {
            // Rectangle dans le plan XY
            new Vector3(-1, phi, 0).normalized * radius,  // 0
            new Vector3( 1, phi, 0).normalized * radius,  // 1
            new Vector3(-1,-phi, 0).normalized * radius,  // 2
            new Vector3( 1,-phi, 0).normalized * radius,  // 3
            
            // Rectangle dans le plan YZ
            new Vector3(0, -1, phi).normalized * radius,  // 4
            new Vector3(0,  1, phi).normalized * radius,  // 5
            new Vector3(0, -1,-phi).normalized * radius,  // 6
            new Vector3(0,  1,-phi).normalized * radius,  // 7
            
            // Rectangle dans le plan XZ
            new Vector3( phi, 0, -1).normalized * radius, // 8
            new Vector3( phi, 0,  1).normalized * radius, // 9
            new Vector3(-phi, 0, -1).normalized * radius, // 10
            new Vector3(-phi, 0,  1).normalized * radius  // 11
        };

        // Les 20 faces triangulaires de l'icosaèdre
        // Chaque triplet définit un triangle avec l'orientation correcte (sens anti-horaire)
        int[] triangles = new int[]
        {
            // 5 faces autour du point 0 (vertex du haut)
            0, 11, 5,
            0, 5, 1,
            0, 1, 7,
            0, 7, 10,
            0, 10, 11,
            
            // 5 faces adjacentes (ceinture du haut)
            1, 5, 9,
            5, 11, 4,
            11, 10, 2,
            10, 7, 6,
            7, 1, 8,
            
            // 5 faces autour du point 3 (vertex du bas)
            3, 9, 4,
            3, 4, 2,
            3, 2, 6,
            3, 6, 8,
            3, 8, 9,
            
            // 5 faces adjacentes (ceinture du bas)
            4, 9, 5,
            2, 4, 11,
            6, 2, 10,
            8, 6, 7,
            9, 8, 1
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        return mesh;
    }

    void ApplyMaterial(MeshRenderer mr)
    {
        if (material != null) { mr.material = material; return; }
        Material mat = new Material(Shader.Find("Standard"));

        // Couleurs spécifiques selon le type de mesh
        switch (meshType)
        {
            case MeshType.Terrain:
                mat.color = new Color(0.4f, 0.8f, 0.3f); // Vert
                break;
            case MeshType.Icosahedron:
                mat.color = new Color(1f, 0.8f, 0.2f);   // Doré/Orange
                break;
            default:
                mat.color = Color.cyan;
                break;
        }

        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.1f);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.4f);
        mr.material = mat;
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
        if (!showWireframe) return;
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf == null || mf.mesh == null) return;
        GL.wireframe = true;
        Graphics.DrawMeshNow(mf.mesh, transform.localToWorldMatrix);
        GL.wireframe = false;
    }
}