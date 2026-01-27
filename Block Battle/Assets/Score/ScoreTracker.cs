using TMPro;
using UnityEngine;

public class ScoreTracker : MonoBehaviour
{
    private GameObject _textPrefab;

    //private float _width;
    //private float centX;
    public void InitializeSelf(GameObject textPrefab)
    {
        _textPrefab = textPrefab;

        // SpriteRenderer bounds setup
        //SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        //Vector2 size = spriteRenderer.size;
        //_width = size.x;
        //centX = _width / 2f;

        // WORLD SPACE TEXT (no canvas) - uses prefab else TMPAnimator doesn't work on init
        GameObject _textObject = Instantiate(_textPrefab);
        _textObject.transform.SetParent(transform, false);
        //_textObject.transform.localPosition = new Vector3(0, 0, 0);
        //_textObject.transform.localScale = Vector3.one * 0.08f;

        TextMeshPro _worldText = _textObject.GetComponent<TextMeshPro>();
        _worldText.text = "<sketchy freq=2 amp=0.05 delay=.75>TEST TEXT</sketchy>";
        _worldText.fontSize = 25;
        _worldText.color = Color.black;
    }

}
