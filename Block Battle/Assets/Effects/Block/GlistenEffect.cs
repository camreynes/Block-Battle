using UnityEngine;

public class GlistenEffect : MonoBehaviour
{
    public float effectDuration = 2; // Total time for the glisten to sweep across
    public string glistenProperty = "_GlistenProgress"; // Match the shader property name

    private float _timer = 0f;
    private bool _isShining = false;
    private Material _material;

    void Start()
    {
        // Get a unique material instance so this block glistens independently
        Renderer renderer = GetComponent<Renderer>();
        _material = renderer.material;
        TriggerGlisten();
    }

    void Update()
    {
        if (_isShining)
        {
            _timer += Time.deltaTime;
            float progress = _timer / effectDuration;
            _material.SetFloat(glistenProperty, progress);

            if (progress >= 1.1f)
            {
                _isShining = false;
                _timer = 0f;
            }
        }
    }

    public void TriggerGlisten()
    {
        _isShining = true;
        _timer = 0f;
        _material.SetFloat(glistenProperty, 0f);
    }
}
