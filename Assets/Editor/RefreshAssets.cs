using UnityEditor;
using UnityEngine;

public class RefreshAssets : MonoBehaviour
{
    [MenuItem("Tools/Refresh Assets")]
    public static void RefreshAssetDatabase()
    {
        AssetDatabase.Refresh();
        Debug.Log("Asset database refreshed.");
    }
}
