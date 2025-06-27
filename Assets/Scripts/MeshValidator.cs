using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class MeshValidator : MonoBehaviour
{
    [Header("Mesh Validation")]
    public bool validateOnStart = true;
    public bool autoFix = true;
    public float weldThreshold = 0.001f;
    
    void Start()
    {
        if (validateOnStart)
        {
            ValidateAndFixMesh();
        }
    }
    
    [ContextMenu("Validate and Fix Mesh")]
    public void ValidateAndFixMesh()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.mesh == null)
        {
            Debug.LogError("No mesh found!");
            return;
        }
        
        Mesh originalMesh = meshFilter.mesh;
        Mesh fixedMesh = ValidateAndFixMeshInternal(originalMesh);
        
        if (fixedMesh != null)
        {
            meshFilter.mesh = fixedMesh;
            Debug.Log("Mesh validated and fixed successfully!");
        }
    }
    
    public Mesh ValidateAndFixMeshInternal(Mesh inputMesh)
    {
        Vector3[] vertices = inputMesh.vertices;
        int[] triangles = inputMesh.triangles;
        
        Debug.Log($"Original mesh: {vertices.Length} vertices, {triangles.Length/3} triangles");
        
        // 1. Welding des vertices dupliqués
        if (autoFix)
        {
            WeldVertices(ref vertices, ref triangles, weldThreshold);
        }
        
        // 2. Validation de la topologie
        var edgeInfo = ValidateTopology(triangles);
        
        // 3. Correction de l'orientation des faces
        if (autoFix)
        {
            FixFaceOrientation(ref triangles, vertices);
        }
        
        // 4. Suppression des triangles dégénérés
        if (autoFix)
        {
            RemoveDegenerateTriangles(ref triangles, vertices);
        }
        
        // Créer le mesh corrigé
        Mesh fixedMesh = new Mesh();
        fixedMesh.vertices = vertices;
        fixedMesh.triangles = triangles;
        fixedMesh.RecalculateNormals();
        fixedMesh.RecalculateBounds();
        
        Debug.Log($"Fixed mesh: {vertices.Length} vertices, {triangles.Length/3} triangles");
        
        return fixedMesh;
    }
    
    void WeldVertices(ref Vector3[] vertices, ref int[] triangles, float threshold)
    {
        Dictionary<Vector3, int> vertexMap = new Dictionary<Vector3, int>();
        List<Vector3> newVertices = new List<Vector3>();
        int[] vertexRemap = new int[vertices.Length];
        
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 vertex = vertices[i];
            
            // Rechercher un vertex proche existant
            int existingIndex = -1;
            foreach (var kvp in vertexMap)
            {
                if (Vector3.Distance(kvp.Key, vertex) < threshold)
                {
                    existingIndex = kvp.Value;
                    break;
                }
            }
            
            if (existingIndex == -1)
            {
                // Nouveau vertex unique
                existingIndex = newVertices.Count;
                newVertices.Add(vertex);
                vertexMap[vertex] = existingIndex;
            }
            
            vertexRemap[i] = existingIndex;
        }
        
        // Remapper les triangles
        for (int i = 0; i < triangles.Length; i++)
        {
            triangles[i] = vertexRemap[triangles[i]];
        }
        
        vertices = newVertices.ToArray();
        Debug.Log($"Welded vertices: {vertices.Length} (was {vertexRemap.Length})");
    }
    
    Dictionary<Loop.Edge, int> ValidateTopology(int[] triangles)
    {
        Dictionary<Loop.Edge, int> edgeCount = new Dictionary<Loop.Edge, int>();
        
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Loop.Edge e1 = new Loop.Edge(triangles[i], triangles[i + 1]);
            Loop.Edge e2 = new Loop.Edge(triangles[i + 1], triangles[i + 2]);
            Loop.Edge e3 = new Loop.Edge(triangles[i + 2], triangles[i]);
            
            IncrementEdgeCount(edgeCount, e1);
            IncrementEdgeCount(edgeCount, e2);
            IncrementEdgeCount(edgeCount, e3);
        }
        
        // Analyser les résultats
        int boundaryEdges = 0;
        int manifoldEdges = 0;
        int nonManifoldEdges = 0;
        
        foreach (var kvp in edgeCount)
        {
            if (kvp.Value == 1)
                boundaryEdges++;
            else if (kvp.Value == 2)
                manifoldEdges++;
            else
                nonManifoldEdges++;
        }
        
        Debug.Log($"Mesh topology: {manifoldEdges} manifold edges, {boundaryEdges} boundary edges, {nonManifoldEdges} non-manifold edges");
        
        if (nonManifoldEdges > 0)
        {
            Debug.LogWarning($"Mesh has {nonManifoldEdges} non-manifold edges! This will cause issues with Loop subdivision.");
        }
        
        return edgeCount;
    }
    
    void IncrementEdgeCount(Dictionary<Loop.Edge, int> edgeCount, Loop.Edge edge)
    {
        if (edgeCount.ContainsKey(edge))
            edgeCount[edge]++;
        else
            edgeCount[edge] = 1;
    }
    
    void FixFaceOrientation(ref int[] triangles, Vector3[] vertices)
    {
        // Vérifier et corriger l'orientation des faces
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 v0 = vertices[triangles[i]];
            Vector3 v1 = vertices[triangles[i + 1]];
            Vector3 v2 = vertices[triangles[i + 2]];
            
            Vector3 normal = Vector3.Cross(v1 - v0, v2 - v0);
            Vector3 center = (v0 + v1 + v2) / 3f;
            
            // Si la normale pointe vers l'intérieur (heuristique simple)
            if (Vector3.Dot(normal, center) < 0)
            {
                // Inverser l'ordre des vertices
                int temp = triangles[i + 1];
                triangles[i + 1] = triangles[i + 2];
                triangles[i + 2] = temp;
            }
        }
    }
    
    void RemoveDegenerateTriangles(ref int[] triangles, Vector3[] vertices)
    {
        List<int> validTriangles = new List<int>();
        
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int v0 = triangles[i];
            int v1 = triangles[i + 1];
            int v2 = triangles[i + 2];
            
            // Vérifier si le triangle est dégénéré
            if (v0 != v1 && v1 != v2 && v2 != v0)
            {
                Vector3 edge1 = vertices[v1] - vertices[v0];
                Vector3 edge2 = vertices[v2] - vertices[v0];
                Vector3 cross = Vector3.Cross(edge1, edge2);
                
                // Si l'aire du triangle est suffisamment grande
                if (cross.magnitude > 1e-6f)
                {
                    validTriangles.Add(v0);
                    validTriangles.Add(v1);
                    validTriangles.Add(v2);
                }
            }
        }
        
        if (validTriangles.Count != triangles.Length)
        {
            Debug.Log($"Removed {(triangles.Length - validTriangles.Count) / 3} degenerate triangles");
            triangles = validTriangles.ToArray();
        }
    }
}