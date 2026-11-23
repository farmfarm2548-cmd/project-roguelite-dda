using UnityEngine;

public class YSort25D : MonoBehaviour
{
    public int orderOffset = 5000;
    public bool updateEveryFrame = true;

    private SpriteRenderer _sr;
    private SpriteRenderer _swordSr;

    private void Awake()
    {
        if (orderOffset < 1000) orderOffset = 5000;
        _sr = GetComponent<SpriteRenderer>();
        var sword = transform.Find("Sword");
        if (sword != null) _swordSr = sword.GetComponent<SpriteRenderer>();
        ApplySort();
    }

    private void LateUpdate()
    {
        if (updateEveryFrame) ApplySort();
    }

    public void ApplySort()
    {
        int baseOrder = orderOffset + Mathf.RoundToInt(-transform.position.y * 100f);
        if (_sr != null) _sr.sortingOrder = baseOrder;
        if (_swordSr != null) _swordSr.sortingOrder = baseOrder + 2;
    }
}
