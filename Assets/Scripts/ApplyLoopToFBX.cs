using UnityEngine;

namespace DefaultNamespace
{
    public class ApplyLoopToFBX : MonoBehaviour
    {
        public GameObject fbxPrefab;
        public int subdivisionLevels = 1;

        void Start()
        {
            GameObject instance = Instantiate(fbxPrefab);
            instance.transform.position = Vector3.zero;

            MeshFilter mf = instance.GetComponentInChildren<MeshFilter>();
            if (mf == null)
            {
                Debug.LogError("No MeshFilter found in the FBX prefab!");
                return;
            }

            // Ajoute le script Loop
            Loop loop = instance.AddComponent<Loop>();
            loop.subdivisionLevels = subdivisionLevels;

            // Applique la subdivision
            loop.ApplyLoopSubdivision();
        }
    }

}