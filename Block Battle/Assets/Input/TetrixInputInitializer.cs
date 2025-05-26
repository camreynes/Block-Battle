using UnityEngine;
using UnityEngine.InputSystem;

public class TetrixInputInitializer : MonoBehaviour
{
    [SerializeField] private PlayerInput[] playerInputs;

    private void Awake()
    {
        foreach (var input in playerInputs)
        {
            if (input != null)
            {
                TetrixInputManager.RegisterPlayer(input.playerIndex, input);
                //Debug.Log($"Registered player {input.playerIndex}");
            }
        }
    }
}
