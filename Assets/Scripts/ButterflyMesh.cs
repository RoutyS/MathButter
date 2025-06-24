using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ButterflyMesh : MonoBehaviour
{
    void Start()
    {
        Mesh mesh = new Mesh();

        // Sommets d’un carré
        Vector3[] vertices = new Vector3[]
        {
            new Vector3(0, 0, 0),    // 0
            new Vector3(1, 0, 0),    // 1
            new Vector3(0, 0, 1),    // 2
            new Vector3(1, 0, 1)     // 3
        };

        // Triangles (2 pour former un carré)
        int[] triangles = new int[]
        {
            0, 2, 1,   // premier triangle
            2, 3, 1    // deuxième triangle
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        GetComponent<MeshFilter>().mesh = mesh;

        // Matériau simple
        GetComponent<MeshRenderer>().material = new Material(Shader.Find("Standard"))
        {
            color = Color.green
        };
    }

    void OnDrawGizmos()
    {
        if (GetComponent<MeshFilter>()?.sharedMesh == null) return;

        Gizmos.color = Color.red;
        var verts = GetComponent<MeshFilter>().sharedMesh.vertices;
        for (int i = 0; i < verts.Length; i++)
        {
            Gizmos.DrawSphere(transform.TransformPoint(verts[i]), 0.03f);
        }
    }

}


