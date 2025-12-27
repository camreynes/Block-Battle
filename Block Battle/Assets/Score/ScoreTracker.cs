using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScoreTracker : MonoBehaviour
{
    public void InitializeSelf()
    {
        //// SpriteRenderer bounds setup
        //SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        //Vector2 size = spriteRenderer.size;
        //_width = size.x;
        //_height = size.y;

        //float centX = _width / 2f;
        //float segY = _height / 30f;

        //for (int i = 0; i < _pieces.Length; i++)
        //{
        //    _sprites[i] = _pieces[i].GetComponent<SpriteRenderer>().sprite;
        //}

        //// Create World Space Canvas
        //Global.canvas.transform.SetParent(transform, false); // Attach to this object
        //Canvas canvas = Global.canvas.AddComponent<Canvas>();
        //canvas.renderMode = RenderMode.WorldSpace;

        //CanvasScaler scaler = Global.canvas.AddComponent<CanvasScaler>();
        //scaler.dynamicPixelsPerUnit = 10; // Optional: controls how sharp text appears

        //Global.canvas.AddComponent<GraphicRaycaster>();

        //// Configure canvas RectTransform
        //RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        //canvasRect.sizeDelta = new Vector2(2f, 1f); // World units
        //canvasRect.localPosition = Vector3.zero;   // Centered on this object
        //canvasRect.localScale = Vector3.one * 0.01f; // Scale down if needed

        //// TEXT
        //GameObject txt = new GameObject("PreviewText");
        //txt.transform.SetParent(transform, false);
        //txt.transform.localPosition = new Vector3(centX, .12f, 3);

        //// Add TextMeshPro component
        //_worldText = txt.AddComponent<TextMeshPro>();

        //// Configure text settings
        //_worldText.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/hun2 SDF");
        //_worldText.text = "Preview";
        //_worldText.fontSize = 16;  // use generated font size
        //_worldText.transform.localScale = Vector3.one * 0.1f; // scale down to fit
        //_worldText.color = Color.black;
        //_worldText.alignment = TextAlignmentOptions.Center;
        //_worldText.enableKerning = true; // need to address P and I spacing eventually
        //_worldText.characterSpacing = -4f;
        //_worldText.fontStyle = TMPro.FontStyles.Bold;
    }

}
