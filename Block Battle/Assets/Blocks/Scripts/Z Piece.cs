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
        new Vector2(19, 3),
        new Vector2(19, 0),
        new Vector2(18, 4),
        new Vector2(18, 5)
    };

    private void Start()
    {
        SpawnBlocks(positions);
    }
}
