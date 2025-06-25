using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class CubeMesh : MonoBehaviour
{
    void Start()
    {
        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh originalMesh = temp.GetComponent<MeshFilter>().sharedMesh;
        Destroy(temp);

        Mesh mesh = new Mesh
        {
            name = "SubdividableCube"
        };

        // Copie les données triangulées du cube
        mesh.vertices = originalMesh.vertices;
        mesh.triangles = originalMesh.triangles;
        mesh.normals = originalMesh.normals;

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().mesh = mesh;
        GetComponent<MeshRenderer>().material = new Material(Shader.Find("Standard"))
        {
            color = Color.yellow
        };
    }
}
