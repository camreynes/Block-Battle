using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class Global : MonoBehaviour
{
    public static List<Vector2Int> scenePreset = new List<Vector2Int>();

    public static void GetPreset()
    {
        scenePreset = SaveUtility.Load() ? scenePreset : new List<Vector2Int>();
    }

}


[System.Serializable]
public class VectorWrapper
{
    public List<Vector2Int> positions;

    public VectorWrapper(List<Vector2Int> list)
    {
        positions = list;
    }
}

public static class SaveUtility
{
    private static string SavePath => Path.Combine(Application.persistentDataPath, "positions.json");

    public static void Save(List<Vector2Int> list)
    {
        VectorWrapper wrapper = new VectorWrapper(list);
        string json = JsonUtility.ToJson(wrapper, true);
        File.WriteAllText(SavePath, json);
        Debug.Log($"Saved to {SavePath}");
    }

    public static bool Load()
    {
        if (File.Exists(SavePath))
        {
            string json = File.ReadAllText(SavePath);
            VectorWrapper wrapper = JsonUtility.FromJson<VectorWrapper>(json);
            Global.scenePreset = wrapper.positions;
            return true;
        }
        Debug.LogWarning("Save file not found.");
        return false;
    }
}