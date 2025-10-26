using UnityEngine;

public class JumpPreviewArrow : MonoBehaviour
{
    [SerializeField] SpriteRenderer arrowNeck;
    [SerializeField] SpriteRenderer arrowHead;
    [SerializeField] float stretchMin;
    [SerializeField] float stretchMax;

    Transform arrowHeadAnchor;

    private void Start()
    {
        arrowHeadAnchor = new GameObject().transform;
        arrowHeadAnchor.position = arrowHead.transform.position;
        arrowHeadAnchor.SetParent(arrowNeck.transform);
    }

    private void SetStretch(float stretch)
    {
        var s = arrowNeck.transform.localScale;
        s.y = stretch;

    }
}