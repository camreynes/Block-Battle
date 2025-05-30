using UnityEngine;

// Thank you 'Sasquatch B Studios' for the shader map
// 'https://www.youtube.com/watch?v=HYWaU97-UC4'

public class Dissolve : MonoBehaviour
{
    private float effectDuration = .15f;
    private string _verticalDissolve = "_VerticalDissolve";
    private string _dissolveAmount = "_DissolveAmount";
    private bool _isDissolving = false;
    private float timer = 0;
    private Material _material;

    void Start()
    {
        Renderer spriteRenderer = GetComponent<Renderer>();
        _material = spriteRenderer.material;
        _material.SetFloat(_verticalDissolve, 0);
    }

    void Update()
    {
        if (_isDissolving)
        {
            timer += Time.deltaTime;
            float dissolveAmount = (timer / effectDuration) * 1.1f; // Normalize 0-1.1
            _material.SetFloat(_verticalDissolve, dissolveAmount);
            Debug.Log($"Dissolve Timer: {timer}");

            // If dissolveAmount exceeds 1.1, reset timer and stop dissolving
            if (dissolveAmount >= 1.1f)
            {
                timer = 0;
                _isDissolving=false;
            }
        }
    }

    public void VerticalDissolve()
    {
        _isDissolving = true;
        timer = 0;
        _material.SetFloat(_verticalDissolve, 0);
    }

    public void NormalDissolve()
    {
        _isDissolving = true;
        timer = 0;
        _material.SetFloat(_verticalDissolve, 0);
    }
}
