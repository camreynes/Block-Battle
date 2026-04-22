// Operations for game input actions
public enum GameInputAction
{
    MOVE_LEFT,
    MOVE_RIGHT,
    SOFT_DROP,
    HARD_DROP,
    ROTATE_CW,
    ROTATE_CCW,
    HOLD,
    PAUSE,
    SAVE_SCENE,

    // UI / arcade-entry directional actions. Bound to the existing
    // UP/DOWN/LEFT/RIGHT/Enter actions in TetrixControls.inputactions,
    // so joystick works out of the box. Keyboard arrow keys are picked up
    // directly via Keyboard.current in UI code (no .inputactions edit required).
    UP,
    DOWN,
    LEFT,
    RIGHT,
    ENTER
}
