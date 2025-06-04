using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

public class InitializeGrids : MonoBehaviour
{
    [SerializeField] private GameObject _playerGridPrefab;
    [SerializeField] private GameObject _gridBackgroundPrefab;
    [SerializeField] private GameObject _piecePreviewPrefab;

    [SerializeField] private GameObject _pieceController;

    private Vector3 _defaultGridPos = new Vector3(-2.247f, -4.49f); // Default position for the grid
    private Vector3 _defaultGridScale = new Vector3(2.809f, 2.809f); // Default scale for the grid

    //private Vector3 _defaultBlockPos = new Vector3(); // Default position for the block
    //private Vector3 _defaultBlockScale = new Vector3(4.494813f, 8.948033f); // Default scale for the block

    public Dictionary<String,GameObject> InitializeGrid(int playerId)
    {
        Dictionary<String, GameObject> dict = new Dictionary<String, GameObject>();

        // Create a new grid for the player
        GameObject newGrid = Instantiate(_playerGridPrefab);
        newGrid.name = $"Grid_{playerId}";
        GameObject newPlayer = new GameObject($"Player_{playerId}");
        newPlayer.transform.SetParent(transform, false);
        newGrid.transform.localScale = _defaultGridScale;
        newGrid.GetComponent<SpriteRenderer>().sortingOrder = 2;
        BlockGrid blockGrid = newGrid.GetComponent<BlockGrid>();
        blockGrid.InitializeDimens();
        float gridX = - (float)(blockGrid.GetTotalWidth()/2);
        float gridY = -(float)(blockGrid.GetTotalHeight() / 2);
        newGrid.transform.localPosition = new Vector3(gridX,gridY,0f);
        newGrid.transform.SetParent(newPlayer.transform, false);
        blockGrid.InitializeSelf(playerId);
        dict.Add("newPlayer", newPlayer);
        dict.Add("newGrid", newGrid);

        // Create a new background for the grid
        GameObject gridBackground = Instantiate(_gridBackgroundPrefab);
        gridBackground.name = $"GridBackground_{playerId}";
        gridBackground.transform.SetParent(newGrid.transform, false);
        gridBackground.transform.localPosition = Vector3.zero;
        gridBackground.transform.localScale = Vector3.one;
        gridBackground.GetComponent<SpriteRenderer>().sortingOrder = 1;
        dict.Add("gridBackground", gridBackground);

        // Create a new piece preview window


        // Create a new piece spawner for the player
        GameObject pieceController = Instantiate(_pieceController);
        pieceController.name = $"PieceController_{playerId}";
        pieceController.transform.SetParent(newGrid.transform, false);
        pieceController.GetComponent<PieceController>().SetGrid(newGrid.GetComponent<BlockGrid>());
        pieceController.GetComponent<PieceController>().SetPlayerID(playerId);
        pieceController.SetActive(true);
        dict.Add("pieceController", pieceController);

        return dict;
    }
}
