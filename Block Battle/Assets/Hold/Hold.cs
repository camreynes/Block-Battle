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

    private TMP_Text _worldText;

    /// <summary>
    /// Initializes the Hold component by finding center for future use.
    /// Sets up sprites, similar to Preview.cs
    /// </summary>
    public void InitializeSelf()
    {
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

        GameObject txt = new GameObject("HoldText");
        txt.transform.SetParent(transform, false);
        txt.transform.localPosition = new Vector3(-centX, .12f, 3);

        // Add TextMeshPro component
        _worldText = txt.AddComponent<TextMeshPro>();

        // Configure text settings
        _worldText.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/hun2 SDF");
        _worldText.text = "Hold Piece";
        _worldText.fontSize = 16;  // use generated font size
        _worldText.transform.localScale = Vector3.one * 0.1f; // scale down to fit
        _worldText.color = Color.black;
        _worldText.alignment = TextAlignmentOptions.Center;
        _worldText.enableKerning = true; // need to address P and I spacing eventually
        _worldText.characterSpacing = -4f;
        _worldText.fontStyle = TMPro.FontStyles.Bold;
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
