using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/**
 * TetrixInputManager is a static class that manages player input for the Tetrix game.
 * It registers and unregisters players, maps game actions to input actions, and checks input states.
 * 
 * I rewrote my control system for future player and control scaling. Thank you Wyatt and Dan from UD gamedev club for the reference to their system.
 */
public static class TetrixInputManager
{
    private static readonly Dictionary<int, TetrixControls> _playerControlsMap = new();
    private static readonly Dictionary<int, GameObject> _playerGrids = new();
    private static readonly Dictionary<int, GameObject> _playerParents = new();

    private static readonly Dictionary<GameInputAction, Func<TetrixControls, InputAction>> actionMap = new()
    {
        [GameInputAction.MOVE_LEFT] = c => c.PlayerActions.MoveLeft,
        [GameInputAction.MOVE_RIGHT] = c => c.PlayerActions.MoveRight,
        [GameInputAction.SOFT_DROP] = c => c.PlayerActions.SoftDrop,
        [GameInputAction.HARD_DROP] = c => c.PlayerActions.HardDrop,
        [GameInputAction.ROTATE_CW] = c => c.PlayerActions.RotateCW,
        [GameInputAction.ROTATE_CCW] = c => c.PlayerActions.RotateCCW,
        [GameInputAction.HOLD] = c => c.PlayerActions.Hold,
        [GameInputAction.PAUSE] = c => c.PlayerActions.Pause,
        [GameInputAction.SAVE_SCENE] = c => c.PlayerActions.SaveScene
    };

    public static void RegisterPlayer(int playerID, PlayerInput input)
    {
        if (_playerControlsMap.ContainsKey(playerID)) return; // Already registered check

        // Create a new TetrixControls instance for the player and enable it
        var controls = new TetrixControls();
        controls.devices = input.devices.ToArray();
        controls.Enable();

        // Create Grid for player
        InitializeGrids grid_manager = GameObject.FindFirstObjectByType<InitializeGrids>();
        GameObject newGrid = grid_manager.InitializeGrid(playerID);

        _playerGrids[playerID] = newGrid; // Store the grid object for the player
        _playerControlsMap[playerID] = controls;
        _playerParents[playerID] = 
    }

    public static void UnregisterPlayer(int playerID)
    {
        if (_playerControlsMap.TryGetValue(playerID, out var controls))
        {
            controls.Disable();
            _playerControlsMap.Remove(playerID);
        }
    }

    public static bool WasPressed(GameInputAction action, int playerID)
    {
        if (!_playerControlsMap.TryGetValue(playerID, out var controls)) return false;  //  Ensure player is registered
        if (!actionMap.TryGetValue(action, out var getAction)) return false; // Ensure action is valid

        return getAction(controls).WasPressedThisFrame();
    }

    public static bool IsHeld(GameInputAction action, int playerID)
    {
        if (!_playerControlsMap.TryGetValue(playerID, out var controls)) return false;
        if (!actionMap.TryGetValue(action, out var getAction)) return false;

        return getAction(controls).IsPressed();
    }

    public static InputAction GetInputAction(GameInputAction action, int playerID)
    {
        if (!_playerControlsMap.TryGetValue(playerID, out var controls)) return null;
        if (!actionMap.TryGetValue(action, out var getAction)) return null;

        return getAction(controls);
    }
}
