using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

public class InitializeGrids : MonoBehaviour
{
    [SerializeField] private GameObject _playerGridPrefab;
    [SerializeField] private GameObject _gridBackgroundPrefab;
    [SerializeField] private GameObject _previewPrefab;
    [SerializeField] private GameObject _previewBackgroundPrefab;
    [SerializeField] private GameObject _outlinePrefab;
    [SerializeField] private GameObject _holdPrefab;
    [SerializeField] private GameObject _holdBackgroundPrefab;



    [SerializeField] private GameObject _pieceController;

    private Vector3 _defaultGridPos = new Vector3(-2.247f, -4.49f); // Default position for the grid
    private Vector3 _defaultGridScale = new Vector3(2.809f, 2.809f); // Default scale for the grid

    //private Vector3 _defaultBlockPos = new Vector3(); // Default position for the block
    //private Vector3 _defaultBlockScale = new Vector3(4.494813f, 8.948033f); // Default scale for the block

    public Dictionary<String,GameObject> InitializeGrid(int playerId)
    {
        Dictionary<String, GameObject> dict = new Dictionary<String, GameObject>();

        // Create player
        GameObject player = new GameObject($"Player_{playerId}");
        player.transform.SetParent(transform, false);

        // Create a new grid for the player
        GameObject grid = Instantiate(_playerGridPrefab);
        grid.name = $"Grid_{playerId}";
        grid.transform.localScale = _defaultGridScale;
        grid.GetComponent<SpriteRenderer>().sortingOrder = 2;
        BlockGrid blockGrid = grid.GetComponent<BlockGrid>();
        blockGrid.InitializeDimens();
        float gridWidth = (float) blockGrid.GetTotalWidth();
        float gridHeight = (float) blockGrid.GetTotalHeight();
        float gridX = - (gridWidth / 2);
        float gridY = - (gridHeight / 2);
        grid.transform.localPosition = new Vector3(gridX,gridY,0f);
        grid.transform.SetParent(player.transform, true);
        blockGrid.InitializeSelf(playerId);
        dict.Add("player", player);
        dict.Add("grid", grid);

        // Create a new background for the grid
        GameObject gridBackground = Instantiate(_gridBackgroundPrefab);
        gridBackground.name = $"GridBackground_{playerId}";
        gridBackground.transform.SetParent(grid.transform, false);
        gridBackground.transform.localPosition = Vector3.zero;
        gridBackground.transform.localScale = Vector3.one;
        gridBackground.GetComponent<SpriteRenderer>().sortingOrder = 1;
        dict.Add("gridBackground", gridBackground);

        // Create a new piece controller for the player
        GameObject pieceController = Instantiate(_pieceController);
        pieceController.name = $"PieceController_{playerId}";
        pieceController.transform.SetParent(grid.transform, true);
        pieceController.GetComponent<PieceController>().SetGrid(grid.GetComponent<BlockGrid>());
        pieceController.GetComponent<PieceController>().SetPlayerID(playerId);
        pieceController.SetActive(true);
        dict.Add("pieceController", pieceController);

        // Create a new piece preview window
        GameObject preview = Instantiate(_previewPrefab);
        preview.name = $"PiecePreview_{playerId}";
        preview.transform.SetParent(player.transform, true);
        float prevX = gridX + gridWidth * 1.05f;
        float prevY = gridY + gridHeight;
        preview.transform.localPosition = new Vector3(prevX, prevY, 0f);
        preview.transform.localScale = new Vector3(_defaultGridScale.x*.65f, _defaultGridScale.y*.65f, 1f);
        preview.GetComponent<SpriteRenderer>().sortingOrder = 2;
        pieceController.GetComponent<PieceController>().SetPreview(preview.GetComponent<Preview>());
        preview.GetComponent<Preview>().InitializeSelf();
        dict.Add("preview", preview);

        // Create a new piece preview background
        GameObject previewBackground = Instantiate(_previewBackgroundPrefab);
        previewBackground.name = $"PreviewBackground_{playerId}";
        previewBackground.transform.SetParent(preview.transform, false);
        previewBackground.transform.localPosition = Vector3.zero;
        previewBackground.transform.localScale = Vector3.one;
        previewBackground.GetComponent<SpriteRenderer>().sortingOrder = 1;
        dict.Add("previewBackground", previewBackground);

        // Create a new outline object
        GameObject outlinePreview = Instantiate(_outlinePrefab);
        outlinePreview.name = $"Outline_{playerId}";
        outlinePreview.transform.SetParent(player.transform, true);
        outlinePreview.transform.localPosition = Vector3.zero;
        outlinePreview.transform.localScale = Vector3.one;
        pieceController.GetComponent<PieceController>().SetOutline(outlinePreview.GetComponent<Outline>());
        outlinePreview.GetComponent<Outline>().InitializeSelf();
        outlinePreview.GetComponent<Outline>().SetGrid(grid.GetComponent<BlockGrid>());
        dict.Add("outline", previewBackground);

        // Create a new hold window
        GameObject hold = Instantiate(_holdPrefab);
        hold.name = $"Hold_{playerId}";
        hold.transform.SetParent(player.transform, true);
        float holdX = gridX - gridWidth * 0.05f;
        float holdY = gridY + gridHeight;
        hold.transform.localPosition = new Vector3(holdX, holdY, 0f);
        hold.transform.localScale = new Vector3(_defaultGridScale.x * .65f, _defaultGridScale.y * .65f, 1f);
        hold.GetComponent<SpriteRenderer>().sortingOrder = 2;
        pieceController.GetComponent<PieceController>().SetHold(hold.GetComponent<Hold>());
        hold.GetComponent<Hold>().InitializeSelf();
        dict.Add("hold", hold);

        // Create a new hold background
        GameObject holdBackground = Instantiate(_holdBackgroundPrefab);
        holdBackground.name = $"holdBackground_{playerId}";
        holdBackground.transform.SetParent(hold.transform, false);
        holdBackground.transform.localPosition = Vector3.zero;
        holdBackground.transform.localScale = Vector3.one;
        holdBackground.GetComponent<SpriteRenderer>().sortingOrder = 1;
        dict.Add("holdBackground", holdBackground);

        return dict;
    }
}
