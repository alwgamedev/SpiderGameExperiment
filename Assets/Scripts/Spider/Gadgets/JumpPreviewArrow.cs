using UnityEngine;

public class JumpPreviewArrow : MonoBehaviour
{
    [SerializeField] SpriteRenderer arrowNeck;
    [SerializeField] SpriteRenderer arrowHead;
    [SerializeField] Transform arrowNeckAnchor;//for fixing position after scaling arrowNeck
    [SerializeField] float stretchMin;
    [SerializeField] float stretchMax;
    [SerializeField] Color colorMin0;//min = crouchProgress == 0 (and then arrow fades from color0 to color1 along its body)
    [SerializeField] Color colorMin1;
    [SerializeField] Color colorMax0;//max = crouchProgress == 1
    [SerializeField] Color colorMax1;

    Transform arrowHeadAnchor;
    Material neckMaterial;

    bool chargingJump;
    bool hasReachedMax;

    const string Color0Property = "_Color0";
    const string Color1Property = "_Color1";

    //SpiderMovementController Player => SpiderMovementController.Player;

    //private void OnEnable()
    //{
    //    if (player == null)
    //    {
    //        player = SpiderMovementController.Player;
    //    }
    //    if (player != null)
    //    {
    //        player.JumpChargeBegan.AddListener(OnBeginJumpCharge);
    //        player.JumpChargeEnded.AddListener(HideArrow);
    //    }
    //}

    //private void OnDrawGizmos()
    //{
    //    if (arrowHeadAnchor)
    //    {
    //        Gizmos.color = Color.blue;
    //        Gizmos.DrawSphere(arrowHeadAnchor.position, 0.1f);
    //    }
    //    if (arrowNeckAnchor)
    //    {
    //        Gizmos.color = Color.red;
    //        Gizmos.DrawSphere(arrowNeckAnchor.position, 0.1f);
    //    }
    //}

    private void Start()
    {
        //if (player == null)//if our OnEnable was before player's, then we need to try this again
        //{
        //    player = SpiderMovementController.Player;
        //    player.JumpChargeBegan.AddListener(OnBeginJumpCharge);
        //    player.JumpChargeEnded.AddListener(HideArrow);
        //}

        arrowHeadAnchor = new GameObject("Arrow Head Anchor").transform;
        arrowHeadAnchor.position = new(arrowNeck.bounds.center.x, arrowNeck.bounds.max.y, 0);//need to make sure it's right on the edge of bounding box, otherwise scales poorly
        arrowHeadAnchor.SetParent(arrowNeck.transform, true);

        neckMaterial = new Material(arrowNeck.material);
        arrowNeck.material = neckMaterial;//copy so changes made to material during play don't persist

        HideArrow();
    }

    private void Update()
    {
        if (chargingJump)
        {
            UpdateArrow();
        }
    }

    public void OnBeginJumpCharge()
    {
        //we could just disable the sprite renderer components, but then transforms still getting updated
        arrowNeckAnchor.gameObject.SetActive(true);
        arrowNeck.gameObject.SetActive(true);
        arrowHead.gameObject.SetActive(true);
        ResetArrow();
        chargingJump = true;
    }

    public void HideArrow()
    {
        arrowNeckAnchor.gameObject.SetActive(false);//and arrowHeadAnchor is a child of arrowNeck so gets set inactive automatically
        arrowNeck.gameObject.SetActive(false);
        arrowHead.gameObject.SetActive(false);
        chargingJump = false;
    }

    private void UpdateArrow()
    {
        if (!hasReachedMax)
        {
            var t = SpiderMovementController.Player.CrouchProgress;
            SetStretch(Mathf.Lerp(stretchMin, stretchMax, t));
            SetColor(t);
            if (t >= 1)
            {
                hasReachedMax = true;
            }
        }
    }

    private void SetStretch(float stretch)
    {
        var p = arrowNeckAnchor.position;
        var s = arrowNeck.transform.localScale;
        s.y = stretch;
        arrowNeck.transform.localScale = s;
        arrowNeck.transform.position += p - arrowNeckAnchor.position;
        arrowHead.transform.position = arrowHeadAnchor.position;
    }

    private void SetColor(float lerpParameter)
    {
        var c0 = Color.LerpUnclamped(colorMin0, colorMax0, lerpParameter);
        var c1 = Color.LerpUnclamped(colorMin1, colorMax1, lerpParameter);
        neckMaterial.SetColor(Color0Property, c0);
        neckMaterial.SetColor(Color1Property, c1);
        arrowHead.color = c1;
    }

    private void ResetArrow()
    {
        SetStretch(stretchMin);
        neckMaterial.SetColor(Color0Property, colorMin0);
        neckMaterial.SetColor(Color1Property, colorMin1);
        arrowHead.color = colorMin1;
        hasReachedMax = false;
    }

    //private void OnDisable()
    //{
    //    player.JumpChargeBegan.RemoveListener(OnBeginJumpCharge);
    //    player.JumpChargeEnded.AddListener(HideArrow);
    //}
}