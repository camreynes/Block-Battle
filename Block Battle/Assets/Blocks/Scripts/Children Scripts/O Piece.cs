using System;
using System.Collections.Generic;
using UnityEngine;

public class OBlockPiece : PieceScript // Apparently OPiece is a reserved namespace in global
{
    PieceType _childPieceType = PieceType.O;

    // Piece inital positions for the O piece
    private Vector2Int[] _initialPositions = new Vector2Int[4] {
        new Vector2Int(4, 18), 
        new Vector2Int(4, 19), 
        new Vector2Int(5, 18), 
        new Vector2Int(5, 19) 
    };
    public override Vector2Int[] GetInitialPositions()
    {
        return _initialPositions;
    }

    public override PieceType GetPieceType()
    {
        return _childPieceType;
    }
}
