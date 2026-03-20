using UnityEngine;
using System.Collections.Generic;

public class GrappleShootPreview : MonoBehaviour
{
    [SerializeField] LineRenderer lineRenderer;
    [SerializeField] float arcLengthStep;
    [SerializeField] float velocitySmoothingRate;
    [SerializeField] LayerMask terminationMask;

    SpiderMover player;
    GrappleCannon grapple;
    Vector3[] positions;
    Vector3 lastShootPosition;
    Vector3 lastShootDirection;
    Vector3 lastTerminusPosition;
    bool playerFacingRight;

    Material material;
    float length;

    int lengthProperty;
 

    private void Start()
    {
        player = Spider.Player.Mover;
        grapple = player.Grapple;
        positions = new Vector3[lineRenderer.positionCount];
        lineRenderer.enabled = false;
        lengthProperty = Shader.PropertyToID("_Length");
        material = lineRenderer.material;
        lineRenderer.SetMaterials(new List<Material>() { material });
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
            SetLineRendererPositions();
            //playerFacingRight = player.FacingRight;//transform.lossyScale.x > 0;was there a reason we were doing this instead of just check player.FacingRight?
        }
        else if (lineRenderer.enabled)
        {
            lineRenderer.enabled = false;
        }
    }

    //first we'll just renderer the parabola 
    private void SetLineRendererPositions()
    {
        Vector3 p = grapple.SourcePosition;
        bool playerFacingRight = player.FacingRight;
        bool directionChange = playerFacingRight != this.playerFacingRight;
        lastShootDirection = directionChange ? grapple.ShootDirection : 
            MathTools.CheapRotationalLerpClamped(lastShootDirection, grapple.ShootDirection, velocitySmoothingRate * Time.deltaTime, out directionChange);
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
            float a = arcLengthStep;
            float t = 0;
            length = 0f;

            for (int i = 1; i < positions.Length; i++)
            {
                if (hitGround)
                {
                    positions[i] = positions[i - 1];
                }
                else
                {
                    float vx = t * g.x + v.x;
                    float vy = t * g.y + v.y;
                    float dt = a / Mathf.Sqrt(vx * vx + vy * vy);//time to increase arc length by fixed amount (results in better rendering than fixed time step)
                    t += dt;
                    positions[i] = new Vector3(p.x + t * v.x + t * t * l.x, p.y + t * v.y + t * t * l.y, 0);
                    var r = Physics2D.Linecast(positions[i - 1], positions[i], terminationMask);
                    if (r)
                    {
                        hitGround = true;
                        lastTerminusPosition = r.point;
                        positions[i] = lastTerminusPosition;
                        length = i * arcLengthStep;
                    }
                }
            }

            if (!hitGround)
            {
                lastTerminusPosition = positions[^1];
                length = arcLengthStep * (positions.Length - 1);
            }

            material.SetFloat(lengthProperty, length);
        }

        lastShootPosition = p;
        this.playerFacingRight = playerFacingRight;
        lineRenderer.SetPositions(positions);
    }
}