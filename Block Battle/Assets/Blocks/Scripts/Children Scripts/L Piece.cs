using System;
using System.Collections.Generic;
using UnityEngine;

public class OPiece : PieceScript
{
    PieceType _childPieceType = PieceType.L;

    // Piece inital positions for the L piece
    private Vector2Int[] _initialPositions = new Vector2Int[4] {
        new Vector2Int(4, 18), // Pivot/center
        new Vector2Int(5, 18), // Right of pivot
        new Vector2Int(5, 19), // Top Right of pivot
        new Vector2Int(3, 18) // Left of pivot
    };

    // 0 = 0 spawn/0 degrees, 1 = right/90 degrees, 2 = reverse/180 degrees, 3 = left/270 degrees

    //------------------CLOCKWISE ROTATIONS------------------
    public static readonly Vector2Int[] _rotate0to1 = new Vector2Int[4] {
        new Vector2Int(0, 0), new Vector2Int(-1, -1), new Vector2Int(0, -2), new Vector2Int(1, 1)
    };

    public static readonly Vector2Int[] _rotate1to2 = new Vector2Int[4] {
        new Vector2Int(0, 0), new Vector2Int(-1, 1), new Vector2Int(-2, 0), new Vector2Int(1, -1)
    };

    public static readonly Vector2Int[] _rotate2to3 = new Vector2Int[4] {
        new Vector2Int(0, 0), new Vector2Int(1, 1), new Vector2Int(0, 2), new Vector2Int(-1, -1)
    };

    public static readonly Vector2Int[] _rotate3to0 = new Vector2Int[4] {
        new Vector2Int(0, 0), new Vector2Int(1, -1), new Vector2Int(2, 0), new Vector2Int(-1, 1)
    };

    //------------------CCW ROTATIONS (I used to use Vector2 subtraction to subtract but decided to manually add tables O(N) > O(1))------------------
    public static readonly Vector2Int[] _rotate0to3 = new Vector2Int[4] {
        new Vector2Int(0, 0), new Vector2Int(-1, 1), new Vector2Int(-2, 0), new Vector2Int(1, -1)
    };

    public static readonly Vector2Int[] _rotate1to0 = new Vector2Int[4] {
        new Vector2Int(0, 0), new Vector2Int(1, 1), new Vector2Int(0, 2), new Vector2Int(-1, -1)
    };

    public static readonly Vector2Int[] _rotate2to1 = new Vector2Int[4] {
        new Vector2Int(0, 0), new Vector2Int(1, -1), new Vector2Int(2, 0), new Vector2Int(-1, 1)
    };

    public static readonly Vector2Int[] _rotate3to2 = new Vector2Int[4] {
        new Vector2Int(0, 0), new Vector2Int(-1, -1), new Vector2Int(0, -2), new Vector2Int(1, 1)
    };


    private Dictionary<(int, int), Vector2Int[]> rotations = new Dictionary<(int, int), Vector2Int[]>()
    {
        [(0, 1)] = _rotate0to1,
        [(1, 2)] = _rotate1to2,
        [(2, 3)] = _rotate2to3,
        [(3, 0)] = _rotate3to0,
        [(0, 3)] = _rotate0to3,
        [(1, 0)] = _rotate1to0,
        [(2, 1)] = _rotate2to1,
        [(3, 2)] = _rotate3to2
    };

    //------------------OVERRIDE METHODS------------------

    public override Vector2Int[] GetInitialPositions()
    {
        return _initialPositions;
    }

    public override PieceType GetPieceType()
    {
        return _childPieceType;
    }

    protected override Vector2Int[] GetRotatedPositions(int stateFrom, bool isClockwise)
    {
        int stateTo = (stateFrom == 0 && !isClockwise) ? 3 : stateFrom - 1; // Fixes the 0 case
        return rotations[(stateFrom, isClockwise ? Math.Abs((stateFrom + 1) % 4) : stateTo)];
    }
}
