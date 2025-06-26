using UnityEngine;

public class KobbeltSubdivisionDemo : MonoBehaviour
{
    [Header("√3 Subdivision Settings")]
    public Material kobbeltMaterial;
    public bool showWireframe = true;
    public int subdivisionLevels = 1;
    public Mesh inputMesh; // Permet d'assigner n'importe quel mesh
    
    [Header("Map Generation Settings")]
    public MapType mapType = MapType.FlatTerrain;
    public int mapSize = 10; // Nombre de segments par côté
    public float mapScale = 10f; // Taille totale de la map
    public float heightVariation = 2f; // Variation de hauteur pour le terrain
    public float noiseScale = 0.1f; // Échelle du bruit Perlin
    public AnimationCurve heightCurve = AnimationCurve.EaseInOut(0, 0, 1, 1); // Courbe de hauteur
    
    public enum MapType
    {
        FlatTerrain,      // Terrain plat avec légères variations
        HillyTerrain,     // Terrain vallonné
        MountainTerrain,  // Terrain montagneux
        Island,           // Île avec plage
        Valley,           // Vallée
        Crater,           // Cratère
        Cube              // Le cube original
    }
    
    void Start()
    {
        if (inputMesh != null)
        {
            CreateKobbeltObject(inputMesh, "√3-Kobbelt Subdivision Object");
        }
        else
        {
            CreateKobbeltMap();
        }
    }
    
    void CreateKobbeltObject(Mesh mesh, string name)
    {
        GameObject obj = new GameObject(name);
        obj.transform.position = Vector3.zero;
        
        MeshFilter meshFilter = obj.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = obj.AddComponent<MeshRenderer>();
        
        meshFilter.mesh = mesh;
        
        SetupMaterial(meshRenderer);
        
        // Ajouter le script de subdivision Kobbelt
        KobbeltSubdivision kobbeltScript = obj.AddComponent<KobbeltSubdivision>();
        kobbeltScript.subdivisionLevels = subdivisionLevels;
        
        SetupCamera();
    }
    
    void CreateKobbeltMap()
    {
        GameObject mapObject = new GameObject($"√3-Kobbelt {mapType} Map");
        mapObject.transform.position = Vector3.zero;
    
        // SOLUTION SIMPLE : Retourner la map de 180° sur l'axe X
        mapObject.transform.rotation = Quaternion.Euler(180f, 0f, 0f);
    
        MeshFilter meshFilter = mapObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = mapObject.AddComponent<MeshRenderer>();
    
        meshFilter.mesh = GenerateMapMesh();
    
        SetupMaterial(meshRenderer);
    
        // Ajouter le script de subdivision Kobbelt
        KobbeltSubdivision kobbeltScript = mapObject.AddComponent<KobbeltSubdivision>();
        kobbeltScript.subdivisionLevels = subdivisionLevels;
    
        SetupCamera();
    }
    
    void SetupMaterial(MeshRenderer renderer)
    {
        if (kobbeltMaterial != null)
        {
            // Créer une copie du matériau pour éviter de modifier l'original
            Material mat = new Material(kobbeltMaterial);
            mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off); // Désactiver le culling
            renderer.material = mat;
        }
        else
        {
            Material doubleSidedMat = new Material(Shader.Find("Standard"));
            doubleSidedMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            doubleSidedMat.color = GetMapColor();
            renderer.material = doubleSidedMat;
        }
    }
    
    Color GetMapColor()
    {
        switch (mapType)
        {
            case MapType.FlatTerrain: return new Color(0.4f, 0.8f, 0.2f); // Vert prairie
            case MapType.HillyTerrain: return new Color(0.3f, 0.7f, 0.3f); // Vert colline
            case MapType.MountainTerrain: return new Color(0.6f, 0.5f, 0.4f); // Brun montagne
            case MapType.Island: return new Color(0.8f, 0.7f, 0.5f); // Beige sable
            case MapType.Valley: return new Color(0.2f, 0.6f, 0.3f); // Vert foncé
            case MapType.Crater: return new Color(0.5f, 0.3f, 0.2f); // Brun rouge
            default: return Color.magenta;
        }
    }
    
   Mesh GenerateMapMesh()
{
    if (mapType == MapType.Cube)
    {
        return CreateTriangulatedCubeMesh();
    }
    
    Mesh mesh = new Mesh();
    
    // Générer les vertices
    int vertexCount = (mapSize + 1) * (mapSize + 1);
    Vector3[] vertices = new Vector3[vertexCount];
    
    float stepSize = mapScale / mapSize;
    float halfScale = mapScale * 0.5f;
    
    for (int z = 0; z <= mapSize; z++)
    {
        for (int x = 0; x <= mapSize; x++)
        {
            int index = z * (mapSize + 1) + x;
            
            float worldX = x * stepSize - halfScale;
            float worldZ = z * stepSize - halfScale;
            float height = GetHeightForPosition(worldX, worldZ);
            
            vertices[index] = new Vector3(worldX, height, worldZ);
        }
    }
    
    // Générer les triangles avec la bonne orientation
    int triangleCount = mapSize * mapSize * 2;
    int[] triangles = new int[triangleCount * 3];
    int triIndex = 0;
    
    for (int z = 0; z < mapSize; z++)
    {
        for (int x = 0; x < mapSize; x++)
        {
            int bottomLeft = z * (mapSize + 1) + x;
            int bottomRight = bottomLeft + 1;
            int topLeft = (z + 1) * (mapSize + 1) + x;
            int topRight = topLeft + 1;
            
            // Premier triangle (sens antihoraire vu du dessus pour normales vers le haut)
            triangles[triIndex * 3] = bottomLeft;
            triangles[triIndex * 3 + 1] = topLeft;
            triangles[triIndex * 3 + 2] = bottomRight;
            triIndex++;
            
            // Deuxième triangle (sens antihoraire vu du dessus pour normales vers le haut)
            triangles[triIndex * 3] = bottomRight;
            triangles[triIndex * 3 + 1] = topLeft;
            triangles[triIndex * 3 + 2] = topRight;
            triIndex++;
        }
    }
    
    mesh.vertices = vertices;
    mesh.triangles = triangles;
    mesh.RecalculateNormals();
    mesh.RecalculateBounds();
    
    return mesh;
}
    
    float GetHeightForPosition(float x, float z)
    {
        float normalizedX = (x + mapScale * 0.5f) / mapScale; // 0-1
        float normalizedZ = (z + mapScale * 0.5f) / mapScale; // 0-1
        float distanceFromCenter = Vector2.Distance(new Vector2(normalizedX, normalizedZ), Vector2.one * 0.5f) * 2f;
        
        float height = 0f;
        
        switch (mapType)
        {
            case MapType.FlatTerrain:
                height = Mathf.PerlinNoise(x * noiseScale, z * noiseScale) * heightVariation * 0.2f;
                break;
                
            case MapType.HillyTerrain:
                height = Mathf.PerlinNoise(x * noiseScale, z * noiseScale) * heightVariation;
                height += Mathf.PerlinNoise(x * noiseScale * 2f, z * noiseScale * 2f) * heightVariation * 0.3f;
                break;
                
            case MapType.MountainTerrain:
                height = Mathf.PerlinNoise(x * noiseScale, z * noiseScale) * heightVariation * 2f;
                height += Mathf.PerlinNoise(x * noiseScale * 0.5f, z * noiseScale * 0.5f) * heightVariation;
                height = heightCurve.Evaluate(height / (heightVariation * 3f)) * heightVariation * 3f;
                break;
                
            case MapType.Island:
                float islandHeight = Mathf.PerlinNoise(x * noiseScale, z * noiseScale) * heightVariation;
                float falloff = Mathf.Clamp01(1f - distanceFromCenter);
                falloff = falloff * falloff; // Transition plus douce
                height = islandHeight * falloff;
                break;
                
            case MapType.Valley:
                height = heightVariation - (distanceFromCenter * heightVariation * 0.8f);
                height += Mathf.PerlinNoise(x * noiseScale * 2f, z * noiseScale * 2f) * heightVariation * 0.3f;
                break;
                
            case MapType.Crater:
                float craterDepth = distanceFromCenter < 0.3f ? -heightVariation * (0.3f - distanceFromCenter) * 3f : 0f;
                float craterRim = distanceFromCenter > 0.25f && distanceFromCenter < 0.4f ? heightVariation * 0.5f : 0f;
                height = craterDepth + craterRim;
                height += Mathf.PerlinNoise(x * noiseScale * 3f, z * noiseScale * 3f) * heightVariation * 0.1f;
                break;
        }
        
        return height;
    }
    
    Mesh CreateTriangulatedCubeMesh()
    {
        Mesh mesh = new Mesh();
    
        Vector3[] vertices = new Vector3[]
        {
            new Vector3(-1, -1,  1), // 0
            new Vector3( 1, -1,  1), // 1
            new Vector3( 1,  1,  1), // 2
            new Vector3(-1,  1,  1), // 3
            new Vector3(-1, -1, -1), // 4
            new Vector3( 1, -1, -1), // 5
            new Vector3( 1,  1, -1), // 6
            new Vector3(-1,  1, -1), // 7
        };
    
        // Triangles orientés correctement (sens antihoraire vu de l'extérieur)
        int[] triangles = new int[]
        {
            // Face avant (regarde vers +Z)
            0, 2, 1,  0, 3, 2,

            // Face droite (regarde vers +X)  
            1, 6, 5,  1, 2, 6,

            // Face arrière (regarde vers -Z)
            5, 7, 4,  5, 6, 7,

            // Face gauche (regarde vers -X)
            4, 3, 0,  4, 7, 3,

            // Face haut (regarde vers +Y)
            3, 6, 2,  3, 7, 6,

            // Face bas (regarde vers -Y)
            4, 1, 5,  4, 0, 1
        };
    
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    
        return mesh;
    }
    void SetupCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            Vector3 cameraPos = mapType == MapType.Cube ? 
                new Vector3(0, 3, 8) : 
                new Vector3(0, mapScale * 0.8f, mapScale * 0.8f);
            
            mainCamera.transform.position = cameraPos;
            mainCamera.transform.LookAt(Vector3.zero);
        }
    }
    
    [ContextMenu("Regenerate Map")]
    public void RegenerateMap()
    {
        // Détruire l'objet existant
        KobbeltSubdivision existingScript = FindObjectOfType<KobbeltSubdivision>();
        if (existingScript != null)
        {
            DestroyImmediate(existingScript.gameObject);
        }
        
        // Recréer la map
        CreateKobbeltMap();
    }
    
    [ContextMenu("Increase Subdivision Level")]
    public void IncreaseSubdivisionLevel()
    {
        subdivisionLevels++;
        UpdateSubdivisionLevel();
    }
    
    [ContextMenu("Decrease Subdivision Level")]
    public void DecreaseSubdivisionLevel()
    {
        subdivisionLevels = Mathf.Max(0, subdivisionLevels - 1);
        UpdateSubdivisionLevel();
    }
    
    void UpdateSubdivisionLevel()
    {
        KobbeltSubdivision kobbeltScript = FindObjectOfType<KobbeltSubdivision>();
        if (kobbeltScript != null)
        {
            kobbeltScript.subdivisionLevels = subdivisionLevels;
            kobbeltScript.ApplyKobbeltSubdivision();
            Debug.Log($"Subdivision level updated to {subdivisionLevels}");
        }
    }
}