using UnityEngine;

public class ZPiece : PieceScript
{
    private Vector2Int[] positions = new Vector2Int[4] {
        new Vector2Int(0, 3),
        new Vector2Int(0, 4),
        new Vector2Int(1, 4),
        new Vector2Int(1, 5)
    };
    
    public override Vector2Int[] GetInitialPositions()
    {
        return positions;
    }

}
