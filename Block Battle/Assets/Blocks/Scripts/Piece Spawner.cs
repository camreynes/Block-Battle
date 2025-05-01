using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class PieceSpawner : MonoBehaviour
{
    [SerializeField] private GameObject _Tetromino;

    private float _timeToFall = 0.8f;
    private Piece_Script Piece;

    public Vector2[] _positions;

    void Start()
    {
        Piece = _Tetromino.GetComponent<Piece_Script>();
        _positions = Piece.GetPositions();

        Piece.SpawnBlocks(_positions);

        StartCoroutine(DelayedStart());
    }

    // Delay before the first block falls
    private IEnumerator DelayedStart()
    {
        yield return new WaitForSeconds(_timeToFall);
        StartCoroutine(BlockFall());         
    }

    // Update is called once per frame
    private IEnumerator BlockFall()
    {
        while (true)
        {
            // Create offset vectors (-1 y) and check if we can place there
            Vector2[] newPieceLocations = Piece.CreateOffsetVectors(1, 0);
            bool canPlace = Piece.CheckBlockLocations(newPieceLocations);

            if (canPlace)
            {
                Piece.moveBlocks(1,0);
            }

            yield return new WaitForSeconds(_timeToFall); // fall every second
        }
    }
}
