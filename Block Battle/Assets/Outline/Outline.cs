using UnityEngine;

public class Outline : MonoBehaviour
{
    [SerializeField] private GameObject _outlinePrefab;

    private GameObject[] _outlines = new GameObject[4];
    [SerializeField] private SpriteRenderer[] _outlineSprites;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void InitializeSelf()
    {
        for (int i = 0; i < 4; i++) // If I were to add more pieces, I would change this value to max number of pieces and pool unused pieces
        {
            GameObject obj = Instantiate(_outlinePrefab);
            obj.transform.SetParent(transform);
            _outlines[i] = obj;
        }
    }
}
