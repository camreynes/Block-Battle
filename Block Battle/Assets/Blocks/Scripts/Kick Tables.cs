// https://tetris.fandom.com/wiki/SRS refence for all kick tables used here

using System.Collections.Generic;
using UnityEngine;

public enum PieceType
{
    I, O, T, J, L, S, Z
}

public enum RotationState
{
    Spawn = 0,
    Right = 1,
    Reverse = 2,
    Left = 3
}

public static class KickTableManager
{
    // SRS kick data (J, L, S, Z, T pieces)
    private static readonly Dictionary<(RotationState, RotationState), Vector2Int[]> JLSTZ_Kicks = new()
    {
        { (RotationState.Spawn, RotationState.Right),  new Vector2Int[] { new(0, 0), new(0, -1), new(1, -1), new(-2, 0), new(-2, -1) } },
        { (RotationState.Right, RotationState.Spawn),  new Vector2Int[] { new(0, 0), new(0, 1), new(-1, 1), new(2, 0), new(2, 1) } },

        { (RotationState.Right, RotationState.Reverse),  new Vector2Int[] { new(0, 0), new(0, 1), new(-1, 1), new(2, 0), new(2, 1) } },
        { (RotationState.Reverse, RotationState.Right),  new Vector2Int[] { new(0, 0), new(0, -1), new(1, -1), new(-2, 0), new(-2, -1) } },

        { (RotationState.Reverse, RotationState.Left),  new Vector2Int[] { new(0, 0), new(0, 1), new(1, 1), new(-2, 0), new(-2, 1) } },
        { (RotationState.Left, RotationState.Reverse),  new Vector2Int[] { new(0, 0), new(0, -1), new(-1, -1), new(2, 0), new(2, -1) } },

        { (RotationState.Left, RotationState.Spawn),  new Vector2Int[] { new(0, 0), new(0, -1), new(-1, -1), new(2, 0), new(2, -1) } },
        { (RotationState.Spawn, RotationState.Left),  new Vector2Int[] { new(0, 0), new(0, 1), new(1, 1), new(-2, 0), new(-2, 1) } }
    };

    // SRS kick data for I-piece
    private static readonly Dictionary<(RotationState, RotationState), Vector2Int[]> I_Kicks = new()
    {
        { (RotationState.Spawn, RotationState.Right),  new Vector2Int[] { new(0, 0), new(0, -2), new(0, 1), new(1, -2), new(-2, 1) } },
        { (RotationState.Right, RotationState.Spawn),  new Vector2Int[] { new(0, 0), new(0, 2), new(0, -1), new(-1, 2), new(2, -1) } },

        { (RotationState.Right, RotationState.Reverse),  new Vector2Int[] { new(0, 0), new(0, -1), new(0, 2), new(2, -1), new(-1, 2) } },
        { (RotationState.Reverse, RotationState.Right),  new Vector2Int[] { new(0, 0), new(0, 1), new(0, -2), new(-2, 1), new(1, -2) } },

        { (RotationState.Reverse, RotationState.Left),  new Vector2Int[] { new(0, 0), new(0, 2), new(0, -1), new(-1, 2), new(2, -1) } },
        { (RotationState.Left, RotationState.Reverse),  new Vector2Int[] { new(0, 0), new(0, -2), new(0, 1), new(1, -2), new(-2, 1) } },

        { (RotationState.Left, RotationState.Spawn),  new Vector2Int[] { new(0, 0), new(0, 1), new(0, -2), new(-2, 1), new(1, -2) } },
        { (RotationState.Spawn, RotationState.Left),  new Vector2Int[] { new(0, 0), new(0, -1), new(0, 2), new(2, -1), new(-1, 2) } }
    };

    public static Vector2Int[] GetSRSKicks(PieceType type, RotationState from, RotationState to)
    {
        return type == PieceType.I
            ? I_Kicks.TryGetValue((from, to), out var kicksI) ? kicksI : new[] { Vector2Int.zero }
            : JLSTZ_Kicks.TryGetValue((from, to), out var kicks) ? kicks : new[] { Vector2Int.zero };
    }

    // Stub: Add TETR.IO 180° kicks here later
    public static Vector2Int[] GetTetrio180Kicks(PieceType type)
    {
        return new[] { Vector2Int.zero }; // Placeholder — insert real logic later
    }
}
