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

    [SerializeField] private GameObject[] _pieces;



    public void InitializeSelf()
    {
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

        _piece2 = Instantiate(_pieces[1], _next2, Quaternion.identity);
        _piece2.transform.SetParent(transform, false);

        _piece3 = Instantiate(_pieces[2], _next3, Quaternion.identity);
        _piece3.transform.SetParent(transform, false);

        _piece4 = Instantiate(_pieces[3], _next4, Quaternion.identity);
        _piece4.transform.SetParent(transform, false);

        // Canvas and text
        GameObject canvasGO = new GameObject("Canvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // Create a Text object
        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(canvasGO.transform);
        //textGO.transform.position = transform.position; //+ new Vector3(0, -_height / 2f + 100, 0);

        // Edit text object
        TextMeshProUGUI msg = textGO.AddComponent<TextMeshProUGUI>();
        msg.text = "Next";
        msg.fontSize = 36;
        msg.alignment = TextAlignmentOptions.Center;
        msg.color = Color.white;
        // msg.rectTransform.anchoredPosition = transform.;

        // Set RectTransform position and size
        RectTransform rectTransform = msg.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(600, 200);
    }

    public void UpdatePreview(int[] list)
    {
        _piece1.GetComponent<SpriteRenderer>().sprite = _pieces[list[0]].GetComponent<SpriteRenderer>().sprite;
        _piece2.GetComponent<SpriteRenderer>().sprite = _pieces[list[1]].GetComponent<SpriteRenderer>().sprite;
        _piece3.GetComponent<SpriteRenderer>().sprite = _pieces[list[2]].GetComponent<SpriteRenderer>().sprite;
        _piece4.GetComponent<SpriteRenderer>().sprite = _pieces[list[3]].GetComponent<SpriteRenderer>().sprite;
    }
    

}
