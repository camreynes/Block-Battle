using UnityEngine;

public class HoldState
{
    /*
     * This class tracks hold states
     * Essentially it's used to determine if we should conitnue moving a piece
     * Delays are based off DAS (delayed auto shift) and ARR (auto repeate rate) configurable per piece
     * Each HoldPiece has readonly vars of each, a bool for ifheld, and a timer
     */

    public bool IsHolding { get; private set; }
    public float HoldTimer { get; private set; }

    private readonly float dasDelay;
    private readonly float arrRate;

    public HoldState(float dasDelay, float arrRate)
    {
        this.dasDelay = dasDelay;
        this.arrRate = arrRate;
    }

    public void StartHold()
    {
        IsHolding = true;
        HoldTimer = 0f;
    }

    public void StopHold()
    {
        IsHolding = false;
        HoldTimer = 0f;
    }

    // Increments time and determines if movement should occur this frame.
    public bool ShouldRepeat()
    {
        if (!IsHolding) return false;

        HoldTimer += Time.deltaTime;

        // First press, if we're not holding, we want to move immediately
        if (HoldTimer <= Time.deltaTime)
            return true;

        // Das and ARR delay
        if (HoldTimer >= dasDelay)
        {
            float repeatTime = dasDelay + arrRate;
            if (HoldTimer >= repeatTime)
            {
                HoldTimer -= arrRate;
                return true;
            }
        }

        return false;
    }
}
