using UnityEngine;

public class TPiece : PieceScript
{
    /// Piece inital positions for the T piece
    private Vector2Int[] _initalPositions = new Vector2Int[4] {
        new Vector2Int(1, 4), // Pivot/center
        new Vector2Int(1, 3), // Left of pivot
        new Vector2Int(0, 4), // Top of pivot
        new Vector2Int(1, 5) // Right of pivot
    };

    // 0 = 0 spawn/0 degrees, 1 = right/90 degrees, 2 = reverse/180 degrees, 3 = left/270 degrees
    private Vector2Int[] _rotate0to1 = new Vector2Int[4] {
        new Vector2Int(0, 0), // Pivot/center
        new Vector2Int(-1, 1), // Relative Left of pivot
        new Vector2Int(1, 1), // Relative Top of pivot
        new Vector2Int(1, -1) // Relative Right of pivot
    };

    private Vector2Int[] _rotate1to2 = new Vector2Int[4] {
        new Vector2Int(0, 0), // Pivot/center
        new Vector2Int(1, 1), // Relative Left of pivot
        new Vector2Int(1, -1), // Relative Top of pivot
        new Vector2Int(-1, -1) // Relative Right of pivot
    };

    private Vector2Int[] _rotate2to3 = new Vector2Int[4] {
        new Vector2Int(0, 0), // Pivot/center
        new Vector2Int(1, -1), // Relative Left of pivot
        new Vector2Int(-1, -1), // Relative Top of pivot
        new Vector2Int(-1, 1) // Relative Right of pivot
    };

    private Vector2Int[] _rotate3to0 = new Vector2Int[4] {
        new Vector2Int(0, 0), // Pivot/center
        new Vector2Int(-1, -1), // Relative Left of pivot
        new Vector2Int(-1, 1), // Relative Top of pivot
        new Vector2Int(1, 1) // Relative Right of pivot
    };

    public override PieceType PieceType => PieceType.T;

    /// <summary>Piece inital positions override</summary>
    public override Vector2Int[] GetInitialPositions()
    {
        return _initalPositions;
    }

    protected override Vector2Int[] GetRotatedPositions(int stateFrom, bool isClockwise)
    {
        if (isClockwise)
        {
            if (stateFrom == 0)
                return _rotate0to1;
            else if (stateFrom == 1)
                return _rotate1to2;
            else if (stateFrom == 2)
                return _rotate2to3;
            else if (stateFrom == 3)
                return _rotate3to0;
        }
        else
        {
            if (stateFrom == 0)
                return _rotate3to0;
            else if (stateFrom == 1)
                return _rotate0to1;
            else if (stateFrom == 2)
                return _rotate1to2;
            else if (stateFrom == 3)
                return _rotate2to3;
        }
        return _rotate0to1; // default to empty
    }

    // ROTAION METHODS

}
