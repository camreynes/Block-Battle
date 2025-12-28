using TMPro;
using UnityEngine;

// THIS CLASS HAS ZERO RELATION TO HOLD STATE
public class Hold : MonoBehaviour
{

    GameObject _heldPiece;
    [SerializeField] private GameObject[] _pieces;

    float _width;
    float _height;
    float centX;
    float centY;

    int _type = -1; // Current piece type being held
    SpriteRenderer spr;
    private Sprite[] _sprites;

    private GameObject _textPrefab;

    /// <summary>
    /// Initializes the Hold component by finding center for future use.
    /// Sets up sprites, similar to Preview.cs
    /// </summary>
    public void InitializeSelf(GameObject textPrefab)
    {
        _textPrefab = textPrefab;

        // SpriteRenderer bounds setup
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        Vector2 size = spriteRenderer.size;
        _width = size.x;
        _height = size.y;

        centX = _width / 2f;
        centY = _height / 2f;

        _heldPiece = new GameObject("heldPiece");
        _heldPiece.transform.SetParent(transform, false);
        _heldPiece.transform.localPosition = new Vector3(-centX, -centY, 0);
        _heldPiece.SetActive(true);
        spr = _heldPiece.AddComponent<SpriteRenderer>();
        spr.GetComponent<SpriteRenderer>().sortingOrder = 3;

        _sprites = new Sprite[_pieces.Length];
        for (int i = 0; i < _pieces.Length; i++)
        {
            _sprites[i] = _pieces[i].GetComponent<SpriteRenderer>().sprite;
        }

        // WORLD SPACE TEXT (no canvas) - uses prefab else TMPAnimator doesn't work on init
        GameObject _textObject = Instantiate(_textPrefab);
        _textObject.transform.SetParent(transform, false);
        _textObject.transform.localPosition = new Vector3(-centX, 0.18f, -1);
        _textObject.transform.localScale = Vector3.one * 0.08f;

        TextMeshPro _worldText = _textObject.GetComponent<TextMeshPro>();
        _worldText.text = "<sketchy freq=2 amp=0.05 delay=.75>HOLD</sketchy>";
        _worldText.fontSize = 25;
        _worldText.color = Color.black;

    }

    /// <summary>
    /// Updates the held piece. Returns the previously held type (or -1 if none).
    /// </summary>
    public int UpdateHold(int type)
    {
        // Range guard
        if (type < 0 || type >= _sprites.Length)
            return -1;

        int previous = _type; // -1 if none

        _type = type;
        spr.sprite = _sprites[type];

        return previous; // caller can spawn this piece if >= 0
    }
}
