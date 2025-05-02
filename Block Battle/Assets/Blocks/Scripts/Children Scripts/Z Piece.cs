using UnityEngine;

public class ZPiece : PieceScript
{
    private Vector2[] positions = new Vector2[4] {
        new Vector2(0, 3),
        new Vector2(0, 4),
        new Vector2(1, 4),
        new Vector2(1, 5)
    };
    
    public override Vector2[] GetInitialPositions()
    {
        return positions;
    }

}
