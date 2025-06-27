using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class KobbeltSubdivision : MonoBehaviour
{
    [Header("UV Mapping")]
    public bool useAutoUV = true;
    public AutoUVMapper.ProjectionMode uvMode = AutoUVMapper.ProjectionMode.SmartProjection;
    private AutoUVMapper uvMapper;


    [Header("√3 Subdivision Settings")]
    public int subdivisionLevels = 1;
    public bool autoUpdate = false;

    [Header("Debug")]
    public bool showDebugInfo = true;

    private Mesh originalMesh;
    private MeshFilter meshFilter;

    public struct Edge
    {
        public int v1, v2;

        public Edge(int v1, int v2)
        {
            this.v1 = Mathf.Min(v1, v2);
            this.v2 = Mathf.Max(v1, v2);
        }

        public override bool Equals(object obj)
        {
            return obj is Edge other && v1 == other.v1 && v2 == other.v2;
        }

        public override int GetHashCode()
        {
            return v1.GetHashCode() ^ (v2.GetHashCode() << 2);
        }
    }

    void Start()
    {
        meshFilter = GetComponent<MeshFilter>();
        SetupUV();

        if (meshFilter == null)
        {
            Debug.LogError("MeshFilter component required!");
            return;
        }

        // Souder et sauvegarder le mesh original pour éviter les problèmes de vertices dupliqués
        originalMesh = Weld(Instantiate(meshFilter.mesh));
        ApplyKobbeltSubdivision();
    }

    void Update()
    {
        if (autoUpdate)
        {
            ApplyKobbeltSubdivision();
        }
    }

    void SetupUV()
    {
        if (useAutoUV)
        {
            uvMapper = GetComponent<AutoUVMapper>();
            if (uvMapper == null)
                uvMapper = gameObject.AddComponent<AutoUVMapper>();
            uvMapper.projectionMode = uvMode;
        }
    }


    [ContextMenu("Apply √3-Kobbelt Subdivision")]
    public void ApplyKobbeltSubdivision()
    {
        if (originalMesh == null) return;

        Mesh currentMesh = Instantiate(originalMesh);

        for (int i = 0; i < subdivisionLevels; i++)
        {
            currentMesh = PerformKobbeltSubdivision(currentMesh);
        }
        FixTriangleOrientations(currentMesh);
        GetComponent<MeshFilter>().mesh = currentMesh;

        if (useAutoUV && uvMapper != null)
            uvMapper.GenerateUVs();

    }

    Mesh PerformKobbeltSubdivision(Mesh inputMesh)
    {
        Vector3[] oldVertices = inputMesh.vertices;
        int[] oldTriangles = inputMesh.triangles;

        if (showDebugInfo)
            Debug.Log($"√3-Kobbelt Input: {oldVertices.Length} vertices, {oldTriangles.Length / 3} triangles");

        // Étape 1: Analyser la topologie
        var topology = AnalyzeTopology(oldVertices, oldTriangles);

        // Étape 2: Créer les centres de triangles et subdiviser
        var subdivisionResult = CreateSubdividedMesh(oldVertices, oldTriangles);

        // Étape 3: Perturber les vertices originaux
        Vector3[] newVertices = PerturbVertices(subdivisionResult.vertices.ToArray(),
                                               topology.vertexNeighbors,
                                               oldVertices.Length);

        // Étape 4: Appliquer le flipping des arêtes
        var finalResult = ApplyEdgeFlipping(newVertices, subdivisionResult.triangles.ToArray(),
                                          oldTriangles, oldVertices.Length);

        if (showDebugInfo)
            Debug.Log($"√3-Kobbelt Output: {finalResult.vertices.Count} vertices, {finalResult.triangles.Count / 3} triangles");

        Mesh newMesh = new Mesh();
        newMesh.vertices = finalResult.vertices.ToArray();
        newMesh.triangles = finalResult.triangles.ToArray();
        newMesh.RecalculateNormals();
        newMesh.RecalculateBounds();

        return newMesh;
    }
  void FixTriangleOrientations(Mesh mesh)
{
    Vector3[] vertices = mesh.vertices;
    int[] triangles = mesh.triangles;
    int triangleCount = triangles.Length / 3;
    
    if (triangleCount == 0) return;
    
    // 1. Construire la liste des triangles adjacents
    Dictionary<Edge, List<int>> edgeToTriangles = new Dictionary<Edge, List<int>>();
    
    for (int i = 0; i < triangleCount; i++)
    {
        int i0 = triangles[i * 3];
        int i1 = triangles[i * 3 + 1];
        int i2 = triangles[i * 3 + 2];
        
        AddEdgeTriangle(edgeToTriangles, new Edge(i0, i1), i);
        AddEdgeTriangle(edgeToTriangles, new Edge(i1, i2), i);
        AddEdgeTriangle(edgeToTriangles, new Edge(i2, i0), i);
    }
    
    // 2. Propagation d'orientation
    bool[] processed = new bool[triangleCount];
    bool[] shouldFlip = new bool[triangleCount];
    
    // Commencer avec le premier triangle (supposé bien orienté)
    Queue<int> toProcess = new Queue<int>();
    toProcess.Enqueue(0);
    processed[0] = true;
    
    while (toProcess.Count > 0)
    {
        int currentTriangle = toProcess.Dequeue();
        
        // Examiner tous les triangles voisins
        for (int edge = 0; edge < 3; edge++)
        {
            int v1 = triangles[currentTriangle * 3 + edge];
            int v2 = triangles[currentTriangle * 3 + (edge + 1) % 3];
            
            Edge sharedEdge = new Edge(v1, v2);
            
            if (edgeToTriangles.ContainsKey(sharedEdge))
            {
                foreach (int neighborTriangle in edgeToTriangles[sharedEdge])
                {
                    if (neighborTriangle == currentTriangle || processed[neighborTriangle])
                        continue;
                    
                    // Vérifier si l'orientation est cohérente
                    if (!AreTrianglesConsistentlyOriented(triangles, currentTriangle, neighborTriangle, shouldFlip[currentTriangle]))
                    {
                        shouldFlip[neighborTriangle] = true;
                    }
                    
                    processed[neighborTriangle] = true;
                    toProcess.Enqueue(neighborTriangle);
                }
            }
        }
    }
    
    // 3. Appliquer les corrections
    for (int i = 0; i < triangleCount; i++)
    {
        if (shouldFlip[i])
        {
            int baseIndex = i * 3;
            int temp = triangles[baseIndex + 1];
            triangles[baseIndex + 1] = triangles[baseIndex + 2];
            triangles[baseIndex + 2] = temp;
        }
    }
    
    mesh.triangles = triangles;
    mesh.RecalculateNormals();
}

bool AreTrianglesConsistentlyOriented(int[] triangles, int tri1, int tri2, bool tri1IsFlipped)
{
    // Trouver l'arête partagée
    int[] t1 = { triangles[tri1 * 3], triangles[tri1 * 3 + 1], triangles[tri1 * 3 + 2] };
    int[] t2 = { triangles[tri2 * 3], triangles[tri2 * 3 + 1], triangles[tri2 * 3 + 2] };
    
    // Trouver les deux vertices partagés
    List<int> shared = new List<int>();
    List<int> t1Indices = new List<int>();
    List<int> t2Indices = new List<int>();
    
    for (int i = 0; i < 3; i++)
    {
        for (int j = 0; j < 3; j++)
        {
            if (t1[i] == t2[j])
            {
                shared.Add(t1[i]);
                t1Indices.Add(i);
                t2Indices.Add(j);
            }
        }
    }
    
    if (shared.Count != 2) return true; // Pas d'arête partagée
    
    // Vérifier l'ordre des vertices partagés
    int t1EdgeStart = t1Indices[0];
    int t1EdgeEnd = t1Indices[1];
    int t2EdgeStart = t2Indices[0];
    int t2EdgeEnd = t2Indices[1];
    
    // Dans un mesh bien orienté, l'arête partagée doit être dans des directions opposées
    bool t1Forward = (t1EdgeEnd == (t1EdgeStart + 1) % 3);
    bool t2Forward = (t2EdgeEnd == (t2EdgeStart + 1) % 3);
    
    // Si tri1 est flippé, inverser sa direction
    if (tri1IsFlipped) t1Forward = !t1Forward;
    
    // Les triangles sont cohérents si leurs arêtes partagées vont dans des directions opposées
    return t1Forward != t2Forward;
}
    struct MeshTopology
    {
        public Dictionary<int, List<int>> vertexNeighbors;
        public Dictionary<Edge, List<int>> edgeToTriangles;
        public List<Edge> boundaryEdges;
    }

    MeshTopology AnalyzeTopology(Vector3[] vertices, int[] triangles)
    {
        var topology = new MeshTopology
        {
            vertexNeighbors = new Dictionary<int, List<int>>(),
            edgeToTriangles = new Dictionary<Edge, List<int>>(),
            boundaryEdges = new List<Edge>()
        };

        for (int i = 0; i < vertices.Length; i++)
            topology.vertexNeighbors[i] = new List<int>();

        for (int i = 0; i < triangles.Length; i += 3)
        {
            int v0 = triangles[i];
            int v1 = triangles[i + 1];
            int v2 = triangles[i + 2];
            int triIndex = i / 3;

            AddNeighbor(topology.vertexNeighbors, v0, v1);
            AddNeighbor(topology.vertexNeighbors, v1, v2);
            AddNeighbor(topology.vertexNeighbors, v2, v0);

            AddEdgeTriangle(topology.edgeToTriangles, new Edge(v0, v1), triIndex);
            AddEdgeTriangle(topology.edgeToTriangles, new Edge(v1, v2), triIndex);
            AddEdgeTriangle(topology.edgeToTriangles, new Edge(v2, v0), triIndex);
        }

        foreach (var kvp in topology.edgeToTriangles)
            if (kvp.Value.Count == 1)
                topology.boundaryEdges.Add(kvp.Key);

        return topology;
    }

    void AddNeighbor(Dictionary<int, List<int>> neighbors, int v1, int v2)
    {
        if (!neighbors[v1].Contains(v2)) neighbors[v1].Add(v2);
        if (!neighbors[v2].Contains(v1)) neighbors[v2].Add(v1);
    }

    void AddEdgeTriangle(Dictionary<Edge, List<int>> dict, Edge e, int tri)
    {
        if (!dict.ContainsKey(e))
            dict[e] = new List<int>();
        dict[e].Add(tri);
    }

    struct SubdivisionResult
    {
        public List<Vector3> vertices;
        public List<int> triangles;
    }

    SubdivisionResult CreateSubdividedMesh(Vector3[] oldVertices, int[] oldTriangles)
    {
        var result = new SubdivisionResult
        {
            vertices = new List<Vector3>(oldVertices),
            triangles = new List<int>()
        };

        for (int i = 0; i < oldTriangles.Length; i += 3)
        {
            int v0 = oldTriangles[i];
            int v1 = oldTriangles[i + 1];
            int v2 = oldTriangles[i + 2];

            Vector3 center = (oldVertices[v0] + oldVertices[v1] + oldVertices[v2]) / 3f;
            int centerIndex = result.vertices.Count;
            result.vertices.Add(center);

            result.triangles.AddRange(new int[] { v0, v1, centerIndex });
            result.triangles.AddRange(new int[] { v1, v2, centerIndex });
            result.triangles.AddRange(new int[] { v2, v0, centerIndex });
        }

        return result;
    }

    Vector3[] PerturbVertices(Vector3[] allVertices, Dictionary<int, List<int>> neighbors, int originalVertexCount)
    {
        Vector3[] result = new Vector3[allVertices.Length];

        for (int i = 0; i < allVertices.Length; i++)
            result[i] = allVertices[i];

        for (int i = 0; i < originalVertexCount; i++)
        {
            if (!neighbors.ContainsKey(i)) continue;

            var neighborList = neighbors[i];
            int n = neighborList.Count;

            if (n == 0) continue;

            float alpha = (4f - 2f * Mathf.Cos(2f * Mathf.PI / n)) / (9f * n);
            alpha = Mathf.Max(0f, alpha);

            Vector3 neighborSum = Vector3.zero;
            foreach (int j in neighborList)
                neighborSum += allVertices[j];

            result[i] = (1f - n * alpha) * allVertices[i] + alpha * neighborSum;
        }

        return result;
    }
    void AddTriangleWithCorrectOrientation(List<int> triangles, Vector3[] vertices, int v0, int v1, int v2)
    {
        Vector3 p0 = vertices[v0];
        Vector3 p1 = vertices[v1];
        Vector3 p2 = vertices[v2];
    
        // Calculer la normale
        Vector3 normal = Vector3.Cross(p1 - p0, p2 - p0);
    
        // Calculer le centre du triangle
        Vector3 triangleCenter = (p0 + p1 + p2) / 3f;
    
        // Si vous connaissez le centre approximatif du mesh, utilisez-le
        // Sinon, utilisez l'origine
        Vector3 meshCenter = Vector3.zero; // ou calculez le vrai centre
        Vector3 directionFromCenter = (triangleCenter - meshCenter).normalized;
    
        // Vérifier si la normale pointe vers l'extérieur
        if (Vector3.Dot(normal, directionFromCenter) > 0)
        {
            // Orientation correcte
            triangles.AddRange(new int[] { v0, v1, v2 });
        }
        else
        {
            // Inverser l'orientation
            triangles.AddRange(new int[] { v0, v2, v1 });
        }
    }
    SubdivisionResult ApplyEdgeFlipping(Vector3[] vertices, int[] triangles, int[] originalTriangles, int originalVertexCount)
    {
        var result = new SubdivisionResult
        {
            vertices = new List<Vector3>(vertices),
            triangles = new List<int>()
        };

        Dictionary<Edge, EdgeInfo> originalEdgeInfo = new Dictionary<Edge, EdgeInfo>();

        for (int i = 0; i < originalTriangles.Length; i += 3)
        {
            int v0 = originalTriangles[i];
            int v1 = originalTriangles[i + 1];
            int v2 = originalTriangles[i + 2];
            int centerIndex = originalVertexCount + (i / 3);

            AddEdgeInfo(originalEdgeInfo, v0, v1, centerIndex);
            AddEdgeInfo(originalEdgeInfo, v1, v2, centerIndex);
            AddEdgeInfo(originalEdgeInfo, v2, v0, centerIndex);
        }

        foreach (var kvp in originalEdgeInfo)
        {
            EdgeInfo info = kvp.Value;

            if (info.centers.Count == 2)
            {
                int c1 = info.centers[0];
                int c2 = info.centers[1];

                // PROBLÈME ICI : Vérifier l'orientation avant d'ajouter
                AddTriangleWithCorrectOrientation(result.triangles, vertices, info.v1, c1, c2);
                AddTriangleWithCorrectOrientation(result.triangles, vertices, c2, c1, info.v2);
            }
            else if (info.centers.Count == 1)
            {
                int c = info.centers[0];
                AddTriangleWithCorrectOrientation(result.triangles, vertices, info.v1, info.v2, c);
            }
        }

        return result;
    }

    struct EdgeInfo
    {
        public int v1, v2;
        public List<int> centers;
    }

    void AddEdgeInfo(Dictionary<Edge, EdgeInfo> dict, int v1, int v2, int center)
    {
        Edge edge = new Edge(v1, v2);

        if (!dict.ContainsKey(edge))
        {
            dict[edge] = new EdgeInfo
            {
                v1 = v1,
                v2 = v2,
                centers = new List<int>()
            };
        }
        dict[edge].centers.Add(center);
    }
   
    // Fonction de soudage pour fusionner les vertices identiques
    Mesh Weld(Mesh m)
    {
        var oldV = m.vertices;
        var oldT = m.triangles;
        var map = new Dictionary<Vector3, int>();
        var newV = new List<Vector3>();
        var remap = new int[oldV.Length];

        // 1) Créer la table de hachage
        for (int i = 0; i < oldV.Length; i++)
        {
            if (!map.TryGetValue(oldV[i], out int idx))
            {
                idx = newV.Count;
                newV.Add(oldV[i]);
                map[oldV[i]] = idx;
            }
            remap[i] = idx;
        }

        // 2) Réindexer les triangles
        var newT = new int[oldT.Length];
        for (int i = 0; i < oldT.Length; i++)
            newT[i] = remap[oldT[i]];

        // 3) Construire et retourner le mesh
        var nm = new Mesh();
        nm.vertices = newV.ToArray();
        nm.triangles = newT;
        nm.RecalculateNormals();
        nm.RecalculateBounds();
        return nm;
    }

    [ContextMenu("Reset to Original")]
    public void ResetToOriginal()
    {
        if (originalMesh != null)
            meshFilter.mesh = Instantiate(originalMesh);
    }
   /* void OnDrawGizmos()
    {
        Mesh currentMesh = GetComponent<MeshFilter>()?.mesh; // Changé ici
        if (currentMesh != null)
        {
            Vector3[] vertices = currentMesh.vertices;
            Vector3[] normals = currentMesh.normals;
    
            Gizmos.color = Color.yellow;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 worldPos = transform.TransformPoint(vertices[i]);
                Vector3 worldNormal = transform.TransformDirection(normals[i]);
                Gizmos.DrawRay(worldPos, worldNormal * 0.1f);
            }
        }
    }*/
}