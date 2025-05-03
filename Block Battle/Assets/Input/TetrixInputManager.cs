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
    private static readonly Dictionary<int, TetrixControls> playerControlsMap = new();

    private static readonly Dictionary<GameInputAction, Func<TetrixControls, InputAction>> actionMap = new()
    {
        [GameInputAction.MOVE_LEFT] = c => c.PlayerActions.MoveLeft,
        [GameInputAction.MOVE_RIGHT] = c => c.PlayerActions.MoveRight,
        [GameInputAction.SOFT_DROP] = c => c.PlayerActions.SoftDrop,
        [GameInputAction.HARD_DROP] = c => c.PlayerActions.HardDrop,
        [GameInputAction.ROTATE_CW] = c => c.PlayerActions.RotateCW,
        [GameInputAction.ROTATE_CCW] = c => c.PlayerActions.RotateCCW,
        [GameInputAction.HOLD] = c => c.PlayerActions.Hold,
        [GameInputAction.PAUSE] = c => c.PlayerActions.Pause
    };

    public static void RegisterPlayer(int playerId, PlayerInput input)
    {
        if (playerControlsMap.ContainsKey(playerId)) return; // Already registered check

        var controls = new TetrixControls();
        controls.devices = input.devices.ToArray();
        controls.Enable();

        playerControlsMap[playerId] = controls;
    }

    public static void UnregisterPlayer(int playerId)
    {
        if (playerControlsMap.TryGetValue(playerId, out var controls))
        {
            controls.Disable();
            playerControlsMap.Remove(playerId);
        }
    }

    public static bool WasPressed(GameInputAction action, int playerId)
    {
        if (!playerControlsMap.TryGetValue(playerId, out var controls)) return false;  //  Ensure player is registered
        if (!actionMap.TryGetValue(action, out var getAction)) return false; // Ensure action is valid

        return getAction(controls).WasPressedThisFrame();
    }

    public static bool IsHeld(GameInputAction action, int playerId)
    {
        if (!playerControlsMap.TryGetValue(playerId, out var controls)) return false;
        if (!actionMap.TryGetValue(action, out var getAction)) return false;

        return getAction(controls).IsPressed();
    }

    public static InputAction GetInputAction(GameInputAction action, int playerId)
    {
        if (!playerControlsMap.TryGetValue(playerId, out var controls)) return null;
        if (!actionMap.TryGetValue(action, out var getAction)) return null;

        return getAction(controls);
    }
}
