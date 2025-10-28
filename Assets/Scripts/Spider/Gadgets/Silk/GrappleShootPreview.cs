using System;
using UnityEngine;
using UnityEngine.UIElements;

public class GrappleShootPreview : MonoBehaviour
{
    [SerializeField] LineRenderer lineRenderer;
    [SerializeField] SpriteRenderer terminus;
    [SerializeField] double arcLengthStep;
    [SerializeField] float velocitySmoothingRate;
    [SerializeField] float extensionRate;
    [SerializeField] LayerMask terminationMask;

    GrappleCannon grapple;
    Vector3[] positions;
    Vector3 lastShootPosition;
    Vector3 lastShootDirection;
    //Vector3 lastShootVelocity;
    Vector3 lastTerminusPosition;
    bool playerFacingRight;

    SpiderMovementController Player => SpiderMovementController.Player;
 

    private void Start()
    {
        grapple = SpiderMovementController.Player.Grapple;
        positions = new Vector3[lineRenderer.positionCount];
        lineRenderer.enabled = false;
    }

    private void LateUpdate()
    {
        if (grapple.PoweringUp)
        {
            if (!lineRenderer.enabled)
            {
                lineRenderer.enabled = true;
                lastShootDirection = grapple.ShootDirection;
            }
            UpdateShootPreview();
            playerFacingRight = Player.transform.lossyScale.x > 0;
        }
        else if (lineRenderer.enabled)
        {
            lineRenderer.enabled = false;
        }
    }

    private void UpdateShootPreview()
    {
        SetLineRendererPositions();
        //+update shader properties & end pt marker
    }

    //first we'll just renderer the parabola 
    private void SetLineRendererPositions()
    {
        Vector3 p = grapple.SourcePosition;
        bool playerFacingRight = Player.FacingRight;
        bool directionChange = playerFacingRight != this.playerFacingRight;
        lastShootDirection = directionChange ? grapple.ShootDirection : MathTools.CheapRotationalLerpClamped(lastShootDirection, grapple.ShootDirection, velocitySmoothingRate * Time.deltaTime, out directionChange);
        Vector3 v = grapple.ShootSpeed * lastShootDirection;

        if (!directionChange && grapple.PowerUpFraction == 1)
        {
            var d = p - lastShootPosition;
            for (int i = 0; i < lineRenderer.positionCount; i++)
            {
                positions[i] += d;
            }
        }
        else
        {

            Vector3 g = Physics2D.gravity;
            Vector3 l = 0.5f * g;

            positions[0] = p;
            bool hitGround = false;
            double a = Mathf.Pow(grapple.PowerUpFraction, extensionRate) * arcLengthStep;
            double t = 0;
            //need double precision to prevent line renderer from quivering

            for (int i = 1; i < positions.Length; i++)
            {
                if (hitGround)
                {
                    positions[i] = positions[i - 1];
                }
                else
                {
                    double vx = t * g.x + v.x;
                    double vy = t * g.y + v.y;
                    double dt = a / Math.Sqrt(vx * vx + vy * vy);//time to increase arc length by fixed amount (results in better rendering than fixed time step)
                    t += dt;
                    positions[i] = new Vector3((float)(p.x + t * v.x + t * t * l.x), (float)(p.y + t * v.y + t * t * l.y), 0);
                    var r = Physics2D.Linecast(positions[i - 1], positions[i], terminationMask);
                    if (r)
                    {
                        hitGround = true;
                        lastTerminusPosition = r.point;
                        positions[i] = lastTerminusPosition;
                    }
                }
            }

            if (!hitGround)
            {
                lastTerminusPosition = positions[^1];
            }
        }

        lastShootPosition = p;
        this.playerFacingRight = playerFacingRight;
        lineRenderer.SetPositions(positions);
    }
}