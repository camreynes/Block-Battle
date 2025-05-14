using System;
using System.Collections.Generic;
using UnityEngine;

public class OnePiece : PieceScript
{
    PieceType _childPieceType = PieceType.Preset;

    /// Piece inital positions for the T piece
    private Vector2Int[] _initalPositions = new Vector2Int[1] {
        new Vector2Int(4, 19), // Pivot/center
    };


    /// <summary>Piece inital positions override</summary>
    public override Vector2Int[] GetInitialPositions()
    {
        return _initalPositions;
    }

    public override PieceType GetPieceType()
    {
        return _childPieceType;
    }

    protected override Vector2Int[] GetRotatedPositions(int stateFrom, bool isClockwise)
    {
        return new Vector2Int[] { new Vector2Int(0, 0) };
    }

    // ROTAION METHODS

}
