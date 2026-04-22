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
    private static readonly Dictionary<int, Dictionary<String,GameObject>> _playerGrids = new();
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
        [GameInputAction.SAVE_SCENE] = c => c.PlayerActions.SaveScene,

        // UI / arcade-entry directional actions.
        [GameInputAction.UP]    = c => c.PlayerActions.UP,
        [GameInputAction.DOWN]  = c => c.PlayerActions.DOWN,
        [GameInputAction.LEFT]  = c => c.PlayerActions.LEFT,
        [GameInputAction.RIGHT] = c => c.PlayerActions.RIGHT,
        [GameInputAction.ENTER] = c => c.PlayerActions.Enter
    };

    public static void RegisterPlayer(int playerID, PlayerInput input)
    {
        // IMPORTANT: this class is static, so _playerControlsMap / _playerGrids
        // survive a scene reload even though every GameObject they reference
        // does not. Before this change, "already registered" silently no-op'd,
        // leaving _playerGrids[playerID] pointing at the destroyed old scene's
        // Grid / PieceController / ScoreTracker. The reloaded scene then had
        // no piece spawning because InitializeGrid never ran - which is
        // exactly the "restart doesn't do anything" bug. Force a full
        // unregister + rebuild instead.
        if (_playerControlsMap.ContainsKey(playerID))
            UnregisterPlayer(playerID);

        var controls = new TetrixControls();
        controls.devices = input.devices.ToArray();
        controls.Enable();

        // Create Grid for player. Must happen AFTER the stale entries were
        // dropped above - otherwise a second InitializeGrid call would race
        // with whatever the previous scene left behind.
        InitializeGrids grid_manager = GameObject.FindFirstObjectByType<InitializeGrids>();
        Dictionary<String,GameObject> newDict = grid_manager.InitializeGrid(playerID);

        _playerGrids[playerID] = newDict;
        _playerControlsMap[playerID] = controls;
    }

    public static void UnregisterPlayer(int playerID)
    {
        if (_playerControlsMap.TryGetValue(playerID, out var controls))
        {
            controls.Disable();
            _playerControlsMap.Remove(playerID);
        }
        // Drop every other per-player cache too. These used to leak across
        // scene reloads because they weren't touched here - the restart bug
        // traced back to stale GameObject refs sitting in _playerGrids.
        _playerGrids.Remove(playerID);
        _playerParents.Remove(playerID);
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
