// https://tetris.fandom.com/wiki/SRS refence for all kick tables used here

using System;
using System.Collections.Generic;
using UnityEngine;

public enum PieceType
{
    I, O, T, J, L, S, Z, Preset
}

// 0 = 0 spawn/0 degrees, 1 = right/90 degrees, 2 = reverse/180 degrees, 3 = left/270 degrees
public static class KickTableManager
{
    // SRS kick data (J, L, S, Z, T pieces)
    private static readonly Dictionary<(int, int), Vector2Int[]> JLSTZ_Kicks = new()
    {
        { (0, 1),  new Vector2Int[] { new(0, 0), new(-1, 0), new(-1, 1), new(0, -2), new(-1, -2) } },
        { (1, 0),  new Vector2Int[] { new(0, 0), new(1, 0), new(1, -1), new(0, 2), new(1, 2) } },

        { (1, 2),  new Vector2Int[] { new(0, 0), new(1, 0), new(1, -1), new(0, 2), new(1, 2) } },
        { (2, 1),  new Vector2Int[] { new(0, 0), new(-1, 0), new(-1, 1), new(0, -2), new(-1, -2) } },

        { (2, 3),  new Vector2Int[] { new(0, 0), new(1, 0), new(1, 1), new(0, -2), new(1, -2) } },
        { (3, 2),  new Vector2Int[] { new(0, 0), new(-1, 0), new(-1, -1), new(0, 2), new(-1, 2) } },

        { (3, 0),  new Vector2Int[] { new(0, 0), new(-1, 0), new(-1, -1), new(0, 2), new(-1, 2) } },
        { (0, 3),  new Vector2Int[] { new(0, 0), new(1, 0), new(1, 1), new(0, -2), new(1, -2) } }
    };

    // SRS kick data for I-piece
    private static readonly Dictionary<(int, int), Vector2Int[]> I_Kicks = new()
    {
        { (0, 1),  new Vector2Int[] { new(0, 0), new(-2, 0), new(1, 0), new(-2, -1), new(1, 2) } },
        { (1, 0),  new Vector2Int[] { new(0, 0), new(2, 0), new(-1, 0), new(2, 1), new(-1, -2) } },

        { (1, 2),  new Vector2Int[] { new(0, 0), new(-1, 0), new(2, 0), new(-1, 2), new(2, -1) } },
        { (2, 1),  new Vector2Int[] { new(0, 0), new(1, 0), new(-2, 0), new(1, -2), new(-2, 1) } },

        { (2, 3),  new Vector2Int[] { new(0, 0), new(2, 0), new(-1, 0), new(2, 1), new(-1, -2) } },
        { (3, 2),  new Vector2Int[] { new(0, 0), new(-2, 0), new(1, 0), new(-2, -1), new(1, 2) } },

        { (3, 0),  new Vector2Int[] { new(0, 0), new(1, 0), new(-2, 0), new(1, -2), new(-2, 1) } },
        { (0, 3),  new Vector2Int[] { new(0, 0), new(-1, 0), new(2, 0), new(-1, 2), new(2, -1) } }

    };

    public static Vector2Int[] GetSRSKicks(PieceType type, int stateFrom, bool isClockwise)
    {
        int stateTo = (stateFrom == 0 && !isClockwise) ? 3 : stateFrom - 1; // Fixes the 0 case
        (int, int) stateFromTo = (stateFrom, isClockwise ? Math.Abs((stateFrom + 1) % 4) : stateTo);

        //Debug.Log($"KickTableManager: Getting SRS kicks for {type} from {stateFrom} to {stateTo}");
        Vector2Int[] kicks = (type == PieceType.I) ? I_Kicks[stateFromTo] : JLSTZ_Kicks[stateFromTo];

        return (type == PieceType.I) ? I_Kicks[stateFromTo] : JLSTZ_Kicks[stateFromTo];
    }

    // Might program 180 TETR.IO Kicks here
    public static Vector2Int[] GetTetrio180Kicks(PieceType type)
    {
        return new[] { Vector2Int.zero }; // Placeholder — insert real logic later
    }
}
