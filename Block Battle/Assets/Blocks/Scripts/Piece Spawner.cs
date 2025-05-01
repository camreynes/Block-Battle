using Unity.VisualScripting;
using UnityEngine;

public class PieceSpawner : MonoBehaviour
{
    [SerializeField] private GameObject _Tetromino;

    void Start()
    {
        GameObject Piece = _Tetromino.GetComponent<Piece_Script>().gameObject;


    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
