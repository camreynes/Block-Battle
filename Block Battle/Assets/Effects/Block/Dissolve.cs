using UnityEngine;

// Thank you 'Sasquatch B Studios' for the shader map
// 'https://www.youtube.com/watch?v=HYWaU97-UC4'

public class Dissolve : MonoBehaviour
{
    private float _dissolveMaximum = 0f;
    private string _dissolveString = "";
    private bool _isDissolving = false;
    private float timer = 0;
    private Material _material;

    void Start()
    {
        Renderer spriteRenderer = GetComponent<Renderer>();
        _material = spriteRenderer.material;
        _material.SetFloat(_dissolveString, 0);
    }

    void Update()
    {
        if (_isDissolving)
        {
            timer += Time.deltaTime;
            float dissolveAmount = (timer / Global.effectDuration) * _dissolveMaximum; // Normalize 0-1.1
            _material.SetFloat(_dissolveString, dissolveAmount);
            // If dissolveAmount exceeds 1.1, reset timer and stop dissolving
            if (dissolveAmount >= _dissolveMaximum)
            {
                timer = 0;
                _isDissolving=false;
            }
        }
    }

    public void VerticalDissolve()
    {
        _dissolveString = "_VerticalDissolve";
        _dissolveMaximum = 1.1f;
        StartDissolve();
    }

    public void NormalDissolve()
    {
        _dissolveString = "_DissolveAmount";
        _dissolveMaximum = 1f;
        StartDissolve();
    }

    private void StartDissolve()
    {
        _isDissolving = true;
        timer = 0;
        _material.SetFloat(_dissolveString, 0);
    }
}