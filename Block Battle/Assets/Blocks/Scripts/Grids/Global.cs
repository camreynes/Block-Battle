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
    //public List<Vector2Int> positions;


    // 0 = 0 spawn/0 degrees, 1 = right/90 degrees, 2 = reverse/180 degrees, 3 = left/270 degrees

    //------------------CLOCKWISE ROTATIONS------------------
    public Vector2Int[] _rotate0to1 = new Vector2Int[4] {
        new Vector2Int(0, 0), new Vector2Int(-1, 1), new Vector2Int(1, 1), new Vector2Int(1, -1)
    };

    public Vector2Int[] _rotate1to2 = new Vector2Int[4] {
        new Vector2Int(0, 0), new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, -1)
    };

    public Vector2Int[] _rotate2to3 = new Vector2Int[4] {
        new Vector2Int(0, 0), new Vector2Int(1, -1), new Vector2Int(-1, -1), new Vector2Int(-1, 1)
    };

    public Vector2Int[] _rotate3to0 = new Vector2Int[4] {
        new Vector2Int(0, 0), new Vector2Int(-1, -1), new Vector2Int(-1, 1), new Vector2Int(1, 1)
    };

    //------------------CCW ROTATIONS (I used to use Vector2 subtraction to subtract but decided to manually add tables O(N) > O(1))------------------
    public Vector2Int[] _rotate0to3 = new Vector2Int[4] {
        new Vector2Int(0, 0), new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, -1)
    };

    public Vector2Int[] _rotate1to0 = new Vector2Int[4] {
        new Vector2Int(0, 0), new Vector2Int(1, -1), new Vector2Int(-1, -1), new Vector2Int(-1, 1)
    };

    public Vector2Int[] _rotate2to1 = new Vector2Int[4] {
        new Vector2Int(0, 0), new Vector2Int(-1, -1), new Vector2Int(-1, 1), new Vector2Int(1, 1)
    };

    public Vector2Int[] _rotate3to2 = new Vector2Int[4] {
        new Vector2Int(0, 0), new Vector2Int(-1, 1), new Vector2Int(1, 1), new Vector2Int(1, -1)
    };

    public VectorWrapper(List<Vector2Int> list)
    {
        _rotate0to1 = ConvertToUnity(_rotate0to1);
        _rotate1to2 = ConvertToUnity(_rotate1to2);
        _rotate2to3 = ConvertToUnity(_rotate2to3);
        _rotate3to0 = ConvertToUnity(_rotate3to0);
        _rotate0to3 = ConvertToUnity(_rotate0to3);
        _rotate1to0 = ConvertToUnity(_rotate1to0);
        _rotate2to1 = ConvertToUnity(_rotate2to1);
        _rotate3to2 = ConvertToUnity(_rotate3to2);
    }

    public Vector2Int[] ConvertToUnity(Vector2Int[] original)
    {
        Vector2Int[] retArr = new Vector2Int[original.Length];
        for (int i = 0; i < original.Length; i++)
        {
            retArr[i] = new Vector2Int(original[i].y, -original[i].x);
        }
        return retArr;
    }
}

public static class SaveUtility
{
    private static string SavePath => Path.Combine(Application.persistentDataPath, "test.json");

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
            //Global.scenePreset = wrapper.positions;
            return true;
        }
        Debug.LogWarning("Save file not found.");
        return false;
    }
}