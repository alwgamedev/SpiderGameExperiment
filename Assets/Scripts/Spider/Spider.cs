using UnityEngine;

public class Spider : MonoBehaviour
{
#if UNITY_EDITOR
    [SerializeField] float timeScale;
#endif
    [SerializeField] SpiderInput spiderInput;
    [SerializeField] Health health;
    [SerializeField] SpiderMover mover;
    [SerializeField] GrappleShootPreview grappleShootPreview;
    [SerializeField] JumpPreviewArrow jumpPreviewArrow;

    public Health Health => health;
    public SpiderMover Mover => mover;
    public Collider2D TriggerCollider { get; private set; }//was for fluid interaction; need to update fluid to new physics system

    public static Spider Player { get; private set; }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            Time.timeScale = timeScale;
        }

        mover.OnValidate();
    }

    private void OnDrawGizmos()
    {
        mover.OnDrawGizmos();
    }
#endif

    private void Awake()
    {
        Player = this;
    }

    private void Start()
    {
#if UNITY_EDITOR
        Time.timeScale = timeScale;
#endif
        spiderInput.Initialize();
        health.Start();
        mover.Initialize(transform, spiderInput);
        jumpPreviewArrow.Start();
        grappleShootPreview.Start(mover.FacingRight);
    }

    private void Update()
    {
        mover.Update();
    }

    private void LateUpdate()
    {
        mover.LateUpdate();
        jumpPreviewArrow.LateUpdate(mover);
        grappleShootPreview.LateUpdate(mover.Grapple, mover.FacingRight);
    }

    private void FixedUpdate()
    {
        mover.FixedUpdate();
    }

    private void OnDestroy()
    {
        mover.OnDestroy();
        jumpPreviewArrow.OnDestroy();
        grappleShootPreview.OnDestroy();
        Player = null;
    }
}