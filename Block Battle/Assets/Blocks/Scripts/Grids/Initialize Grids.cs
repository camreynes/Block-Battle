using UnityEngine;

public class InitializeGrids : MonoBehaviour
{
    [SerializeField] private GameObject _playerGridPrefab;
    [SerializeField] private GameObject _gridBackgroundPrefab;
    [SerializeField] private GameObject _pieceController;

    private Vector3 _defaultGridPos = new Vector3(-2.247f, -4.49f); // Default position for the grid
    private Vector3 _defaultGridScale = new Vector3(2.809f, 2.809f); // Default scale for the grid

    //private Vector3 _defaultBlockPos = new Vector3(); // Default position for the block
    //private Vector3 _defaultBlockScale = new Vector3(4.494813f, 8.948033f); // Default scale for the block

    public GameObject InitializeGrid(int playerId)
    {
        //GameOvbj

        // Create a new grid for the player
        GameObject newGrid = Instantiate(_playerGridPrefab);
        newGrid.name = $"Grid_{playerId}";
        GameObject newPlayer = new GameObject($"Player_{playerId}");
        newGrid.transform.SetParent(newPlayer.transform, false);
        newPlayer.transform.SetParent(transform, false);
        newGrid.transform.localPosition = _defaultGridPos;
        newGrid.transform.localScale = _defaultGridScale;
        newGrid.GetComponent<SpriteRenderer>().sortingOrder = 2;
        newGrid.GetComponent<BlockGrid>().InitializeSelf(playerId);

        // Create a new background for the grid
        GameObject gridBackground = Instantiate(_gridBackgroundPrefab);
        gridBackground.name = $"GridBackground_{playerId}";
        gridBackground.transform.SetParent(newGrid.transform, false);
        gridBackground.transform.localPosition = Vector3.zero;
        gridBackground.transform.localScale = Vector3.one;
        gridBackground.GetComponent<SpriteRenderer>().sortingOrder = 1;

        // Create a new piece spawner for the player
        GameObject pieceController = Instantiate(_pieceController);
        pieceController.name = $"PieceController_{playerId}";
        pieceController.transform.SetParent(newGrid.transform, false);
        pieceController.GetComponent<PieceController>().SetGrid(newGrid.GetComponent<BlockGrid>());
        pieceController.GetComponent<PieceController>().SetPlayerID(playerId);
        pieceController.SetActive(true); 

        return newGrid;
    }
}
