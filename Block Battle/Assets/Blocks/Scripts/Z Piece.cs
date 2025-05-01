using UnityEngine;

public class Z_Piece : Piece_Script
{
    /**
     * Default Locations for Z Piece
     * 
     * 19, 3
     * 19, 4
     * 18, 4
     * 18, 5
     */

    private Vector2[] positions = new Vector2[4] {
        new Vector2(0, 3),
        new Vector2(0, 4),
        new Vector2(1, 4),
        new Vector2(1, 5)
    };

    protected override void Start()
    {
        base.Start();  // Call parent Start
        SetPositions(positions);
        SpawnBlocks(_positions);
    }
}
