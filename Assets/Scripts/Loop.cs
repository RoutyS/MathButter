using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class Loop : MonoBehaviour
{
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
        if (meshFilter == null)
        {
            Debug.LogError("MeshFilter component required!");
            return;
        }
        
        // Sauvegarder le mesh original
        originalMesh = Instantiate(meshFilter.mesh);
        
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
    
    [ContextMenu("Apply Loop Subdivision")]
    public void ApplyLoopSubdivision()
    {
        if (originalMesh == null) return;
        
        Mesh currentMesh = Instantiate(originalMesh);
        
        for (int i = 0; i < subdivisionLevels; i++)
        {
            currentMesh = PerformLoopSubdivision(currentMesh);
        }
        
        meshFilter.mesh = currentMesh;
    }
    
    Mesh PerformLoopSubdivision(Mesh inputMesh)
    {
        Vector3[] oldVertices = inputMesh.vertices;
        int[] oldTriangles = inputMesh.triangles;
        
        // 1. Construire la structure des arêtes
        Dictionary<Edge, int> edgeToNewVertex = new Dictionary<Edge, int>();
        Dictionary<Edge, List<int>> edgeToTriangles = new Dictionary<Edge, List<int>>();
        Dictionary<int, List<int>> vertexNeighbors = new Dictionary<int, List<int>>();
        
        BuildTopology(oldTriangles, edgeToTriangles, vertexNeighbors);
        
        // 2. Calculer les nouveaux points d'arête
        List<Vector3> newVertices = new List<Vector3>(oldVertices);
        CalculateEdgePoints(oldVertices, oldTriangles, edgeToTriangles, edgeToNewVertex, newVertices);
        
        // 3. Calculer les nouveaux points de vertex
        CalculateVertexPoints(oldVertices, vertexNeighbors, newVertices);
        
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
                      Dictionary<int, List<int>> vertexNeighbors)
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
                           Dictionary<Edge, int> edgeToNewVertex, 
                           List<Vector3> newVertices)
    {
        foreach (var kvp in edgeToTriangles)
        {
            Edge edge = kvp.Key;
            List<int> adjacentTriangles = kvp.Value;
            
            Vector3 edgePoint;
            
            if (adjacentTriangles.Count == 2)
            {
                // Arête interne - utiliser la formule de Loop
                Vector3 v1 = oldVertices[edge.v1];
                Vector3 v2 = oldVertices[edge.v2];
                
                // Trouver les deux autres vertices des triangles adjacents
                Vector3 vLeft = FindThirdVertex(oldTriangles, adjacentTriangles[0], edge.v1, edge.v2, oldVertices);
                Vector3 vRight = FindThirdVertex(oldTriangles, adjacentTriangles[1], edge.v1, edge.v2, oldVertices);
                
                // Formule de Loop: e = 3/8 * (v1 + v2) + 1/8 * (vLeft + vRight)
                edgePoint = 0.375f * (v1 + v2) + 0.125f * (vLeft + vRight);
            }
            else
            {
                // Arête de bordure - simple moyenne
                edgePoint = 0.5f * (oldVertices[edge.v1] + oldVertices[edge.v2]);
            }
            
            int newVertexIndex = newVertices.Count;
            newVertices.Add(edgePoint);
            edgeToNewVertex[edge] = newVertexIndex;
        }
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
                              List<Vector3> newVertices)
    {
        for (int i = 0; i < oldVertices.Length; i++)
        {
            List<int> neighbors = vertexNeighbors[i];
            int n = neighbors.Count;
            
            float alpha = CalculateLoopAlpha(n);
            
            Vector3 neighborSum = Vector3.zero;
            foreach (int neighbor in neighbors)
            {
                neighborSum += oldVertices[neighbor];
            }
            
            // Formule de Loop: v' = (1-n*α)*v + α*∑neighbors
            Vector3 newVertexPoint = (1f - n * alpha) * oldVertices[i] + alpha * neighborSum;
            newVertices[i] = newVertexPoint;
        }
    }
    
    float CalculateLoopAlpha(int n)
    {
        if (n == 3)
        {
            return 3f / 16f;
        }
        else
        {
            float cosValue = Mathf.Cos(2f * Mathf.PI / n);
            return (1f / n) * (5f / 8f - Mathf.Pow(3f / 8f + 1f / 4f * cosValue, 2f));
        }
    }
    
    void BuildNewTriangles(int[] oldTriangles, Dictionary<Edge, int> edgeToNewVertex, 
                          List<int> newTriangles)
    {
        for (int i = 0; i < oldTriangles.Length; i += 3)
        {
            int v0 = oldTriangles[i];
            int v1 = oldTriangles[i + 1];
            int v2 = oldTriangles[i + 2];
            
            // Obtenir les nouveaux points d'arête
            int e01 = edgeToNewVertex[new Edge(v0, v1)];
            int e12 = edgeToNewVertex[new Edge(v1, v2)];
            int e20 = edgeToNewVertex[new Edge(v2, v0)];
            
            // Créer 4 nouveaux triangles selon le schéma 1-to-4 de Loop
            // Triangle central
            newTriangles.AddRange(new int[] { e01, e12, e20 });
            
            // Triangles des coins
            newTriangles.AddRange(new int[] { v0, e01, e20 });
            newTriangles.AddRange(new int[] { v1, e12, e01 });
            newTriangles.AddRange(new int[] { v2, e20, e12 });
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