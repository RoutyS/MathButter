using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class Loop : MonoBehaviour
{
    [Header("UV Mapping")]
    public bool useAutoUV = true;
    public AutoUVMapper.ProjectionMode uvMode = AutoUVMapper.ProjectionMode.SmartProjection;
    private AutoUVMapper uvMapper;


    [Header("Subdivision Settings")]
    public int subdivisionLevels = 1;
    public bool autoUpdate = false;
    
    private Mesh originalMesh;
    private MeshFilter meshFilter;
    
    // Structure pour représenter une arête
    public struct Edge
    {
        public int v1, v2;
        public int leftTriangle, rightTriangle;
        
        public Edge(int vertex1, int vertex2)
        {
            v1 = Mathf.Min(vertex1, vertex2);
            v2 = Mathf.Max(vertex1, vertex2);
            leftTriangle = -1;
            rightTriangle = -1;
        }
        
        public override bool Equals(object obj)
        {
            if (obj is Edge other)
                return v1 == other.v1 && v2 == other.v2;
            return false;
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
        
        // Sauvegarder le mesh original
        originalMesh = Weld(Instantiate(meshFilter.mesh));
        
        // Appliquer la subdivision
        ApplyLoopSubdivision();
    }
    
    void Update()
    {
        if (autoUpdate)
        {
            ApplyLoopSubdivision();
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


    [ContextMenu("Apply Loop Subdivision")]
    public void ApplyLoopSubdivision()
    {
        if (originalMesh == null) return;
        
        Mesh currentMesh = Instantiate(originalMesh);
        
        for (int i = 0; i < subdivisionLevels; i++)
        {
            currentMesh = PerformLoopSubdivision(currentMesh);
        }
        
        // Corriger les orientations des triangles
        FixTriangleOrientations(currentMesh);
        
        meshFilter.mesh = currentMesh;

        if (useAutoUV && uvMapper != null)
            uvMapper.GenerateUVs();
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

    void AddEdgeTriangle(Dictionary<Edge, List<int>> dict, Edge e, int tri)
    {
        if (!dict.ContainsKey(e))
            dict[e] = new List<int>();
        dict[e].Add(tri);
    }
    
    Mesh PerformLoopSubdivision(Mesh inputMesh)
    {
        Vector3[] oldVertices = inputMesh.vertices;
        int[] oldTriangles = inputMesh.triangles;
        
        // 1. Construire la structure des arêtes
        Dictionary<Edge, int> edgeToNewVertex = new Dictionary<Edge, int>();
        Dictionary<Edge, List<int>> edgeToTriangles = new Dictionary<Edge, List<int>>();
        Dictionary<int, List<int>> vertexNeighbors = new Dictionary<int, List<int>>();
        HashSet<int> boundaryVertices = new HashSet<int>();
        HashSet<Edge> boundaryEdges = new HashSet<Edge>();
        
        BuildTopology(oldTriangles, edgeToTriangles, vertexNeighbors, boundaryVertices, boundaryEdges);
        
        // Debug info
        Debug.Log($"Mesh info: {oldVertices.Length} vertices, {oldTriangles.Length/3} triangles, {boundaryEdges.Count} boundary edges, {boundaryVertices.Count} boundary vertices");
        
        // 2. Calculer les nouveaux points d'arête
        List<Vector3> newVertices = new List<Vector3>(oldVertices);
        CalculateEdgePoints(oldVertices, oldTriangles, edgeToTriangles, boundaryEdges, edgeToNewVertex, newVertices);
        
        // 3. Calculer les nouveaux points de vertex
        CalculateVertexPoints(oldVertices, vertexNeighbors, boundaryVertices, boundaryEdges, newVertices);
        
        // 4. Construire les nouveaux triangles
        List<int> newTriangles = new List<int>();
        BuildNewTriangles(oldTriangles, edgeToNewVertex, newTriangles);
        
        // 5. Créer le nouveau mesh
        Mesh newMesh = new Mesh();
        newMesh.vertices = newVertices.ToArray();
        newMesh.triangles = newTriangles.ToArray();
        newMesh.RecalculateNormals();
        newMesh.RecalculateBounds();
        
        return newMesh;
    }
    
    void BuildTopology(int[] triangles, Dictionary<Edge, List<int>> edgeToTriangles, 
                      Dictionary<int, List<int>> vertexNeighbors,
                      HashSet<int> boundaryVertices, HashSet<Edge> boundaryEdges)
    {
        // Initialiser le dictionnaire des voisins
        for (int i = 0; i < triangles.Length; i++)
        {
            int vertex = triangles[i];
            if (!vertexNeighbors.ContainsKey(vertex))
                vertexNeighbors[vertex] = new List<int>();
        }
        
        // Construire les relations d'arêtes et de voisinage
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int v0 = triangles[i];
            int v1 = triangles[i + 1];
            int v2 = triangles[i + 2];
            int triangleIndex = i / 3;
            
            // Ajouter les arêtes du triangle
            AddEdgeToTriangle(new Edge(v0, v1), triangleIndex, edgeToTriangles);
            AddEdgeToTriangle(new Edge(v1, v2), triangleIndex, edgeToTriangles);
            AddEdgeToTriangle(new Edge(v2, v0), triangleIndex, edgeToTriangles);
            
            // Ajouter les voisins
            AddNeighbor(vertexNeighbors, v0, v1);
            AddNeighbor(vertexNeighbors, v0, v2);
            AddNeighbor(vertexNeighbors, v1, v0);
            AddNeighbor(vertexNeighbors, v1, v2);
            AddNeighbor(vertexNeighbors, v2, v0);
            AddNeighbor(vertexNeighbors, v2, v1);
        }
        
        // Identifier les arêtes et vertices de bordure
        foreach (var kvp in edgeToTriangles)
        {
            if (kvp.Value.Count == 1)
            {
                Edge edge = kvp.Key;
                boundaryEdges.Add(edge);
                boundaryVertices.Add(edge.v1);
                boundaryVertices.Add(edge.v2);
            }
        }
    }
    
    void AddEdgeToTriangle(Edge edge, int triangleIndex, Dictionary<Edge, List<int>> edgeToTriangles)
    {
        if (!edgeToTriangles.ContainsKey(edge))
            edgeToTriangles[edge] = new List<int>();
        edgeToTriangles[edge].Add(triangleIndex);
    }
    
    void AddNeighbor(Dictionary<int, List<int>> vertexNeighbors, int vertex, int neighbor)
    {
        if (!vertexNeighbors[vertex].Contains(neighbor))
            vertexNeighbors[vertex].Add(neighbor);
    }
    
    void CalculateEdgePoints(Vector3[] oldVertices, int[] oldTriangles, 
                           Dictionary<Edge, List<int>> edgeToTriangles, 
                           HashSet<Edge> boundaryEdges,
                           Dictionary<Edge, int> edgeToNewVertex, 
                           List<Vector3> newVertices)
    {
        foreach (var kvp in edgeToTriangles)
        {
            Edge edge = kvp.Key;
            List<int> adjacentTriangles = kvp.Value;
            
            Vector3 edgePoint;
            
            if (boundaryEdges.Contains(edge))
            {
                // Arête de bordure - utiliser la règle de bordure
                edgePoint = CalculateBoundaryEdgePoint(edge, oldVertices);
            }
            else if (adjacentTriangles.Count == 2)
            {
                // Arête manifold standard - utiliser la formule de Loop
                edgePoint = CalculateInteriorEdgePoint(edge, adjacentTriangles, oldTriangles, oldVertices);
            }
            else
            {
                // Arête non-manifold (plus de 2 triangles) - utiliser une approche robuste
                Debug.LogWarning($"Non-manifold edge detected: {edge.v1} - {edge.v2} with {adjacentTriangles.Count} triangles");
                edgePoint = CalculateNonManifoldEdgePoint(edge, adjacentTriangles, oldTriangles, oldVertices);
            }
            
            int newVertexIndex = newVertices.Count;
            newVertices.Add(edgePoint);
            edgeToNewVertex[edge] = newVertexIndex;
        }
    }
    
    Vector3 CalculateBoundaryEdgePoint(Edge edge, Vector3[] vertices)
    {
        // Pour les arêtes de bordure, utiliser la moyenne simple pour préserver la forme
        Vector3 v1 = vertices[edge.v1];
        Vector3 v2 = vertices[edge.v2];
        return 0.5f * (v1 + v2);
    }
    
    Vector3 CalculateInteriorEdgePoint(Edge edge, List<int> adjacentTriangles, int[] oldTriangles, Vector3[] oldVertices)
    {
        Vector3 v1 = oldVertices[edge.v1];
        Vector3 v2 = oldVertices[edge.v2];
        
        // Trouver les deux autres vertices des triangles adjacents
        Vector3 vLeft = FindThirdVertex(oldTriangles, adjacentTriangles[0], edge.v1, edge.v2, oldVertices);
        Vector3 vRight = FindThirdVertex(oldTriangles, adjacentTriangles[1], edge.v1, edge.v2, oldVertices);
        
        // Formule de Loop: e = 3/8 * (v1 + v2) + 1/8 * (vLeft + vRight)
        return 0.375f * (v1 + v2) + 0.125f * (vLeft + vRight);
    }
    
    Vector3 CalculateNonManifoldEdgePoint(Edge edge, List<int> adjacentTriangles, int[] triangles, Vector3[] vertices)
    {
        Vector3 v1 = vertices[edge.v1];
        Vector3 v2 = vertices[edge.v2];
        
        if (adjacentTriangles.Count > 2)
        {
            Vector3 edgeCenter = 0.5f * (v1 + v2);
            Vector3 oppositeSum = Vector3.zero;
            
            // Collecter tous les vertices opposés
            foreach (int triangleIndex in adjacentTriangles)
            {
                Vector3 oppositeVertex = FindThirdVertex(triangles, triangleIndex, edge.v1, edge.v2, vertices);
                oppositeSum += oppositeVertex;
            }
            
            // Formule modifiée pour les arêtes non-manifold
            float edgeWeight = 0.6f;
            float oppositeWeight = 0.4f / adjacentTriangles.Count;
            
            return edgeWeight * edgeCenter + oppositeWeight * oppositeSum;
        }
        
        // Fallback: simple moyenne
        return 0.5f * (v1 + v2);
    }
    
    Vector3 FindThirdVertex(int[] triangles, int triangleIndex, int v1, int v2, Vector3[] vertices)
    {
        int baseIndex = triangleIndex * 3;
        int tv0 = triangles[baseIndex];
        int tv1 = triangles[baseIndex + 1];
        int tv2 = triangles[baseIndex + 2];
        
        if (tv0 != v1 && tv0 != v2) return vertices[tv0];
        if (tv1 != v1 && tv1 != v2) return vertices[tv1];
        return vertices[tv2];
    }
    
    void CalculateVertexPoints(Vector3[] oldVertices, Dictionary<int, List<int>> vertexNeighbors,
                             HashSet<int> boundaryVertices, HashSet<Edge> boundaryEdges,
                             List<Vector3> newVertices)
    {
        for (int i = 0; i < oldVertices.Length; i++)
        {
            if (!vertexNeighbors.ContainsKey(i))
            {
                // Vertex isolé - le garder tel quel
                Debug.LogWarning($"Isolated vertex found: {i}");
                continue;
            }
            
            List<int> neighbors = vertexNeighbors[i];
            if (neighbors.Count == 0)
            {
                continue;
            }
            
            Vector3 newVertexPoint;
            
            if (boundaryVertices.Contains(i))
            {
                // Vertex de bordure - utiliser la règle de bordure
                newVertexPoint = CalculateBoundaryVertexPoint(i, oldVertices, neighbors, boundaryEdges);
            }
            else
            {
                // Vertex intérieur - utiliser la règle de Loop standard
                newVertexPoint = CalculateInteriorVertexPoint(i, oldVertices, neighbors);
            }
            
            newVertices[i] = newVertexPoint;
        }
    }
    
    Vector3 CalculateBoundaryVertexPoint(int vertexIndex, Vector3[] oldVertices, List<int> neighbors, HashSet<Edge> boundaryEdges)
    {
        // Pour un vertex de bordure, ne considérer que les voisins qui sont aussi sur la bordure
        List<int> boundaryNeighbors = new List<int>();
        
        foreach (int neighbor in neighbors)
        {
            Edge edge = new Edge(vertexIndex, neighbor);
            if (boundaryEdges.Contains(edge))
            {
                boundaryNeighbors.Add(neighbor);
            }
        }
        
        if (boundaryNeighbors.Count == 2)
        {
            // Cas normal: vertex de bordure avec exactement 2 voisins de bordure
            // Règle de subdivision de bordure: 1/8 * (n1 + n2) + 3/4 * v
            Vector3 v = oldVertices[vertexIndex];
            Vector3 n1 = oldVertices[boundaryNeighbors[0]];
            Vector3 n2 = oldVertices[boundaryNeighbors[1]];
            
            return 0.75f * v + 0.125f * (n1 + n2);
        }
        else
        {
            // Cas particulier (coin, etc.) - garder le vertex original
            Debug.Log($"Boundary vertex {vertexIndex} has {boundaryNeighbors.Count} boundary neighbors");
            return oldVertices[vertexIndex];
        }
    }
    
    Vector3 CalculateInteriorVertexPoint(int vertexIndex, Vector3[] oldVertices, List<int> neighbors)
    {
        int n = neighbors.Count;
        float alpha = CalculateLoopAlpha(n);
        
        Vector3 neighborSum = Vector3.zero;
        foreach (int neighbor in neighbors)
        {
            neighborSum += oldVertices[neighbor];
        }
        
        // Formule de Loop: v' = (1-n*α)*v + α*∑neighbors
        return (1f - n * alpha) * oldVertices[vertexIndex] + alpha * neighborSum;
    }
    
    float CalculateLoopAlpha(int n)
    {
        if (n == 3)
        {
            return 3f / 16f;
        }
        else if (n > 3)
        {
            float cosValue = Mathf.Cos(2f * Mathf.PI / n);
            return (1f / n) * (5f / 8f - Mathf.Pow(3f / 8f + 1f / 4f * cosValue, 2f));
        }
        else
        {
            // Pour n < 3, utiliser une valeur conservative
            return 1f / 8f;
        }
    }
    
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

    void BuildNewTriangles(int[] oldTriangles, Dictionary<Edge, int> edgeToNewVertex, 
                          List<int> newTriangles)
    {
        for (int i = 0; i < oldTriangles.Length; i += 3)
        {
            int v0 = oldTriangles[i];
            int v1 = oldTriangles[i + 1];
            int v2 = oldTriangles[i + 2];
            
            // Vérifier que toutes les arêtes existent
            Edge e01 = new Edge(v0, v1);
            Edge e12 = new Edge(v1, v2);
            Edge e20 = new Edge(v2, v0);
            
            if (!edgeToNewVertex.ContainsKey(e01) || 
                !edgeToNewVertex.ContainsKey(e12) || 
                !edgeToNewVertex.ContainsKey(e20))
            {
                Debug.LogError($"Missing edge vertex for triangle {i/3}");
                continue;
            }
            
            // Obtenir les nouveaux points d'arête
            int ne01 = edgeToNewVertex[e01];
            int ne12 = edgeToNewVertex[e12];
            int ne20 = edgeToNewVertex[e20];
            
            // Créer 4 nouveaux triangles selon le schéma 1-to-4 de Loop
            // Triangle central
            newTriangles.AddRange(new int[] { ne01, ne12, ne20 });
            
            // Triangles des coins
            newTriangles.AddRange(new int[] { v0, ne01, ne20 });
            newTriangles.AddRange(new int[] { v1, ne12, ne01 });
            newTriangles.AddRange(new int[] { v2, ne20, ne12 });
        }
    }
    
    [ContextMenu("Reset to Original")]
    public void ResetToOriginal()
    {
        if (originalMesh != null)
        {
            meshFilter.mesh = Instantiate(originalMesh);
        }
    }
}