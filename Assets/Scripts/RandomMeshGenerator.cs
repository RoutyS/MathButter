using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RandomMeshGenerator : MonoBehaviour
{
    public enum MeshType { Triangular, Quad };

    [Header("Mesh Settings")]
    public MeshType meshType = MeshType.Triangular;
    public int width = 5;
    public int height = 5;
    public float cellSize = 1f;

    public Mesh GeneratedMesh { get; private set; }

    void Start()
    {
        GeneratedMesh = GenerateRandomMesh();
        GetComponent<MeshFilter>().mesh = GeneratedMesh;
    }

    Mesh GenerateRandomMesh()
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        for (int y = 0; y <= height; y++)
        {
            for (int x = 0; x <= width; x++)
            {
                float z = Random.Range(-0.5f, 0.5f);
                vertices.Add(new Vector3(x * cellSize, y * cellSize, z));
            }
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = y * (width + 1) + x;

                int a = i;
                int b = i + 1;
                int c = i + width + 1;
                int d = i + width + 2;

                if (meshType == MeshType.Triangular)
                {
                    if (Random.value > 0.5f)
                    {
                        triangles.AddRange(new int[] { a, d, b });
                        triangles.AddRange(new int[] { a, c, d });
                    }
                    else
                    {
                        triangles.AddRange(new int[] { a, c, b });
                        triangles.AddRange(new int[] { b, c, d });
                    }
                }
                else if (meshType == MeshType.Quad)
                {
                    triangles.AddRange(new int[] { a, c, b });
                    triangles.AddRange(new int[] { b, c, d });
                }
            }
        }

        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
