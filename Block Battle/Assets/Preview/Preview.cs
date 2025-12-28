using System.Runtime.CompilerServices;
using TMPEffects;
using TMPEffects.Components;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Preview : MonoBehaviour
{
    private float _width;
    private float _height;
    private float centX;
    private Vector2 _next1;
    private Vector2 _next2;
    private Vector2 _next3;
    private Vector2 _next4;

    private GameObject _piece1;
    private GameObject _piece2;
    private GameObject _piece3;
    private GameObject _piece4;
    private GameObject _textPrefab;

    private SpriteRenderer s1;
    private SpriteRenderer s2;
    private SpriteRenderer s3;
    private SpriteRenderer s4;

    [SerializeField] private GameObject[] _pieces;
    private Sprite[] _sprites = new Sprite[7];

    public void InitializeSelf(GameObject textPrefab)
    {
        _textPrefab = textPrefab;

        // SpriteRenderer bounds setup
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        Vector2 size = spriteRenderer.size;
        _width = size.x;
        _height = size.y;

        centX = _width / 2f;
        float segY = _height / 30f;
        float next1Y = segY * -5f;
        float next2Y = segY * -13f;
        float next3Y = segY * -19f;
        float next4Y = segY * -25f;

        _next1 = new Vector2(centX, next1Y);
        _next2 = new Vector2(centX, next2Y);
        _next3 = new Vector2(centX, next3Y);
        _next4 = new Vector2(centX, next4Y);

        _piece1 = Instantiate(_pieces[0], _next1, Quaternion.identity);
        _piece1.transform.SetParent(transform, false);
        s1 = _piece1.GetComponent<SpriteRenderer>();

        _piece2 = Instantiate(_pieces[1], _next2, Quaternion.identity);
        _piece2.transform.SetParent(transform, false);
        s2 = _piece2.GetComponent<SpriteRenderer>();

        _piece3 = Instantiate(_pieces[2], _next3, Quaternion.identity);
        _piece3.transform.SetParent(transform, false);
        s3 = _piece3.GetComponent<SpriteRenderer>();

        _piece4 = Instantiate(_pieces[3], _next4, Quaternion.identity);
        _piece4.transform.SetParent(transform, false);
        s4 = _piece4.GetComponent<SpriteRenderer>();

        for (int i = 0; i < _pieces.Length; i++)
        {
            _sprites[i] = _pieces[i].GetComponent<SpriteRenderer>().sprite;
        }

        // WORLD SPACE TEXT (no canvas) - uses prefab else TMPAnimator doesn't work on init
        GameObject _textObject = Instantiate(_textPrefab);
        _textObject.transform.SetParent(transform, false);
        _textObject.transform.localPosition = new Vector3(centX, 0.16f, -1f);
        _textObject.transform.localScale = Vector3.one * 0.08f;

        TextMeshPro _worldText = _textObject.GetComponent<TextMeshPro>();
        _worldText.text = "<sketchy freq=2 amp=0.05 delay=.75>PREVIEW</sketchy>";
        _worldText.fontSize = 25;
        _worldText.color = Color.black;
    }

    public void UpdatePreview(int[] list)
    {
        s1.sprite = _sprites[list[0]];
        s2.sprite = _sprites[list[1]];
        s3.sprite = _sprites[list[2]];
        s4.sprite = _sprites[list[3]];
    }
}