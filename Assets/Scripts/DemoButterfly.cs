using UnityEngine;

public class DemoButterfly : MonoBehaviour
{
    [Header("Réglages")]
    public Material material;
    public int levels = 1;

    void Start()
    {
        GameObject cube = this.gameObject;
        cube.name = "ButterflyCube";

        MeshFilter mf = cube.GetComponent<MeshFilter>();
        if (mf == null)
            mf = cube.AddComponent<MeshFilter>();

        MeshRenderer mr = cube.GetComponent<MeshRenderer>();
        if (mr == null)
            mr = cube.AddComponent<MeshRenderer>();

        // Création du cube triangulé
        Mesh mesh = CreateTriangulatedCubeMesh();

        // Subdivision Butterfly
        var butterfly = cube.GetComponent<ButterflySubdivision>();
        if (butterfly == null)
            butterfly = cube.AddComponent<ButterflySubdivision>();

        butterfly.inputMeshFilter = mf;

        for (int i = 0; i < levels; i++)
            mesh = butterfly.SubdivideButterfly(mesh);

        mf.mesh = mesh;

        // Matériau
        if (material != null)
            mr.material = material;
        else
        {
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = Color.cyan;
            mr.material = mat;
        }

        cube.transform.position = Vector3.zero;
    }



    Mesh CreateTriangulatedCubeMesh()
    {
        Mesh mesh = new Mesh { name = "TriangulatedCube" };

        Vector3[] v = {
            // avant
            new Vector3(-1,-1, 1), new Vector3(1,-1, 1),
            new Vector3( 1, 1, 1), new Vector3(-1, 1, 1),
            // arrière
            new Vector3(-1,-1,-1), new Vector3(1,-1,-1),
            new Vector3( 1, 1,-1), new Vector3(-1, 1,-1)
        };

        int[] t = {
            0,2,1, 0,3,2, // avant
            1,2,6, 1,6,5, // droite
            5,6,7, 5,7,4, // arrière
            4,7,3, 4,3,0, // gauche
            3,7,6, 3,6,2, // haut
            4,0,1, 4,1,5  // bas
        };

        mesh.vertices = v;
        mesh.triangles = t;
        mesh.RecalculateNormals();
        return mesh;
    }
}
