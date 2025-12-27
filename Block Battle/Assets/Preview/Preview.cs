using System.Runtime.CompilerServices;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Preview : MonoBehaviour
{
    private float _width;
    private float _height;
    private Vector2 _next1;
    private Vector2 _next2;
    private Vector2 _next3;
    private Vector2 _next4;

    private GameObject _piece1;
    private GameObject _piece2;
    private GameObject _piece3;
    private GameObject _piece4;

    private SpriteRenderer s1;
    private SpriteRenderer s2;
    private SpriteRenderer s3;
    private SpriteRenderer s4;

    [SerializeField] private GameObject[] _pieces;
    private Sprite[] _sprites = new Sprite[7];

    private TMP_Text _worldText;
    private Canvas _canvas;

    public void InitializeSelf()
    {
        // SpriteRenderer bounds setup
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        Vector2 size = spriteRenderer.size;
        _width = size.x;
        _height = size.y;

        float centX = _width / 2f;
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

        // Create World Space Canvas

        GameObject canvasGO = new GameObject("Canvas");
        canvasGO.transform.SetParent(transform, false); // Attach to this object
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10; // Optional: controls how sharp text appears

        canvasGO.AddComponent<GraphicRaycaster>();

        // Configure canvas RectTransform
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(2f, 1f); // World units
        canvasRect.localPosition = Vector3.zero;   // Centered on this object
        canvasRect.localScale = Vector3.one * 0.01f; // Scale down if needed

        // TEXT
        GameObject txt = new GameObject("PreviewText");
        txt.transform.SetParent(transform, false);
        txt.transform.localPosition = new Vector3(centX, .12f, 3);

        // Add TextMeshPro component
        _worldText = txt.AddComponent<TextMeshPro>();

        // Configure text settings
        _worldText.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/hun2 SDF");
        _worldText.text = "Preview";
        _worldText.fontSize = 16;  // use generated font size
        _worldText.transform.localScale = Vector3.one * 0.1f; // scale down to fit
        _worldText.color = Color.black;
        _worldText.alignment = TextAlignmentOptions.Center;
        _worldText.enableKerning = true; // need to address P and I spacing eventually
        _worldText.characterSpacing = -4f;
        _worldText.fontStyle = TMPro.FontStyles.Bold;
    }

    public void UpdatePreview(int[] list)
    {
        s1.sprite = _sprites[list[0]];
        s2.sprite = _sprites[list[1]];
        s3.sprite = _sprites[list[2]];
        s4.sprite = _sprites[list[3]];
    }
    
    public void SetCanvas(Canvas canvas)
    {
        _canvas = canvas;
    }
}
