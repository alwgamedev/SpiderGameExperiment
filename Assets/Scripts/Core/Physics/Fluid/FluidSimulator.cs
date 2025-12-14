using System;
using UnityEngine;
using static UnityEngine.InputSystem.HID.HID;

public class FluidSimulator
{
    public readonly int width;//grid width in # of cells
    public readonly int height;
    public readonly int numCells;
    public readonly float cellSize;
    public readonly float worldWidth;
    public readonly float worldHeight;
    public readonly float worldPositionX;//position of the lower left corner of the grid in world
    public readonly float worldPositionY;

    //particles
    public readonly int numParticles;
    public readonly float particleAccel;//dt * gravity
    public readonly float particleRadius;
    public readonly float collisionBounciness;
    public readonly float[] particlePositionX;//positions are local, where (0,0) is the lower left corner of the grid (makes transferring to grid and back easier)
    public readonly float[] particlePositionY;
    public readonly float[] particleVelocityX;
    public readonly float[] particleVelocityY;
    int particlePointer;//particles at index >= particlePointer are inactive and not simulated

    //grid -- NOTE: row 0 is at the bottom of the grid, row 1 above it, etc.
    readonly float[] velocityX;
    readonly float[] velocityY;
    readonly float[] density;

    readonly int collisionMask;
    readonly Collider2D[] obstacle;//per grid cell

    readonly float[] bufferA;
    readonly float[] bufferB;

    public int ParticlePointer => particlePointer;

    public FluidSimulator(int width, int height, float cellSize, float worldPositionX, float worldPositionY,
        int numParticles, float particleAccel, float particleRadius, float collisionBounciness, int collisionMask)
    {
        this.width = width;
        this.height = height;
        numCells = width * height;
        this.cellSize = cellSize;
        worldWidth = width * cellSize;
        worldHeight = height * cellSize;
        this.worldPositionX = worldPositionX;
        this.worldPositionY = worldPositionY;

        this.numParticles = numParticles;
        this.particleAccel = particleAccel;

        this.particleRadius = particleRadius;
        this.collisionBounciness = collisionBounciness;
        particlePositionX = new float[numParticles];
        particlePositionY = new float[numParticles];
        particleVelocityX = new float[numParticles];
        particleVelocityY = new float[numParticles];

        velocityX = new float[numCells];
        velocityY = new float[numCells];
        density = new float[numCells];
        bufferA = new float[numCells];
        bufferB = new float[numCells];

        this.collisionMask = collisionMask;
        obstacle = new Collider2D[numCells];
    }

    int Index(int i, int j) => i * width + j;

    //where 0 <= i <= height and 0 <= j <= width
    public float DensityAtVertex(int i, int j)
    {
        int count = 0;
        float sum = 0;

        if (i < height && j < width)
        {
            count++;
            sum += density[Index(i, j)];
        }
        if (i > 0 && j < width)
        {
            count++;
            sum += density[Index(i - 1, j)];
        }
        if (i > 0 && j > 0)
        {
            count++;
            sum += density[Index(i - 1, j - 1)];
        }
        if (i < height && j > 0)
        {
            count++;
            sum += density[Index(i, j - 1)];
        }

        return sum / count;
    }

    public void SpawnParticles(int num, float positionX, float positionY)
    {
        int k = 0;
        while (k < num && particlePointer < numParticles)
        {
            //randomize position otherwise the particles will follow the same path for the rest of time
            particlePositionX[particlePointer] = positionX + MathTools.RandomFloat(-0.5f * cellSize, 0.5f * cellSize);
            particlePositionY[particlePointer] = positionY + MathTools.RandomFloat(-0.5f * cellSize, 0.5f * cellSize);
            InterpolateParticleVelocity(width - Mathf.Epsilon, height - Mathf.Epsilon, particlePointer);
            k++;
            particlePointer++;
        }
    }

    public void DrawParticleGizmos()
    {
        Gizmos.color = Color.blue;
        for (int i = 0; i < particlePointer; i++)
        {
            Gizmos.DrawSphere(
                new(particlePositionX[i] + worldPositionX,
                particlePositionY[i] + worldPositionY), particleRadius);
        }
    }

    public void DrawVelocityFieldGizmos()
    {
        var r = 0.15f * cellSize;
        var scale = 0.5f * cellSize;
        for (int i = 0; i < height; i++)
        {
            for (int j =  0; j < width; j++)
            {
                var cellCenter = new Vector3(worldPositionX + (j + 0.5f) * cellSize, worldPositionY + (i + 0.5f) * cellSize);
                var v = new Vector3(velocityX[Index(i, j)], velocityY[Index(i, j)]).normalized;
                if (v == Vector3.zero)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawSphere(cellCenter, r);
                }
                else
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawLine(cellCenter, cellCenter + scale * v);
                }
            }
        }
    }

    //SIMULATION

    public void Update(float dt)
    {
        IntegrateParticles(dt);
        SetOccupiedCells();
        ResolveCollisions(dt);
        TransferParticleVelocitiesToGrid();
        //SetBoundary(velocityX, velocityY);
        Project();
        SetBoundary(velocityX, velocityY);
        TransferGridVelocitiesToParticles();
    }

    private void IntegrateParticles(float dt)
    {
        for (int k = 0; k < particlePointer; k++)
        {
            particleVelocityY[k] += particleAccel;
            particlePositionX[k] += dt * particleVelocityX[k];
            particlePositionY[k] += dt * particleVelocityY[k];
        }
    }
    private void SetOccupiedCells()
    {
        for (int i = 0; i < height; i++)
        {
            for (int j = 0; j < width; j++)
            {
                var cellCenter = new Vector2(worldPositionX + (j + 0.5f) * cellSize, worldPositionY + (i + 0.5f) * cellSize);
                obstacle[Index(i, j)] = Physics2D.OverlapCircle(cellCenter, 0.5f * cellSize, collisionMask);
            }
        }
    }

    private void ResolveCollisions(float dt)
    {
        var dtInverse = 1 / dt;

        for (int k = 0; k < particlePointer; k++)
        {
            var i = (int)Mathf.Floor(particlePositionY[k] / cellSize);
            var j = (int)Mathf.Floor(particlePositionX[k] / cellSize);
            if (!(i < 0) && i < height && !(j < 0) && j < width)
            {
                var o = obstacle[Index(i, j)];
                if (o)//if o is on ground layer we will handle differently
                {
                    //this won't work well for collision with ground (large polygon colliders)
                    var nX = particlePositionX[k] + worldPositionX - o.bounds.center.x;
                    var nY = particlePositionY[k] + worldPositionY - o.bounds.center.y;
                    var m = nX * nX + nY * nY;
                    if (m > MathTools.o41)
                    {
                        m = 1 / Mathf.Sqrt(m);
                        nX *= m;
                        nY *= m;
                        var dx = particleRadius * nX;//particleRadius is small so don't bother computing the actual overlap radius (assume particle fully tunneled)
                        var dy = particleRadius * nY;

                        //reflect the velocity over the collision normal
                        //this probably isn't necessary -- we don't need a very accurate collision system
                        var b = particleVelocityX[k] * nX + particleVelocityY[k] * nY;
                        if (b < 0)
                        {
                            b = Mathf.Sign(b) * collisionBounciness;
                            var a = 2 * (particleVelocityX[k] * nY - particleVelocityY[k] * nX);
                            particleVelocityX[k] = b * (particleVelocityX[k] - a * nY);
                            particleVelocityY[k] = b * (particleVelocityY[k] + a * nX);
                        }

                        //move the particle away from the collision
                        particleVelocityX[k] += dx * dtInverse;
                        particleVelocityY[k] += dy * dtInverse;
                        particlePositionX[k] += particleRadius * nX;
                        particlePositionY[k] += particleRadius * nY;
                        if (o.attachedRigidbody)
                        {
                            particleVelocityX[k] += o.attachedRigidbody.linearVelocity.x;
                            particleVelocityY[k] += o.attachedRigidbody.linearVelocity.y;
                        }
                    }
                }
            }

            //wall collisions -- can get rid of once we have ground collision
            //(but we may still want to do something about particles escaping through the roof, e.g. just reset them to the center of the grid)

            if (particlePositionY[k] < cellSize)
            {
                var dy = particlePositionY[k] - cellSize;
                particlePositionY[k] -= dy;
                particleVelocityY[k] -= dy * dtInverse;
            }
            else if (particlePositionY[k] > worldHeight - cellSize)
            {
                var dy = particlePositionY[k] - worldHeight + cellSize;
                particlePositionY[k] -= dy;
                particleVelocityY[k] -= collisionBounciness * dy * dtInverse;
            }

            if (particlePositionX[k] < cellSize)
            {
                var dx = particlePositionX[k] - cellSize;
                particlePositionX[k] -= dx;
                particleVelocityX[k] -= collisionBounciness * dx * dtInverse;
            }
            else if (particlePositionX[k] > worldWidth - cellSize)
            {
                var dx = particlePositionX[k] - worldWidth + cellSize;
                particlePositionX[k] -= dx;
                particleVelocityX[k] -= collisionBounciness * dx * dtInverse;
            }
        }
    }

    private void TransferParticleVelocitiesToGrid()
    {
        //draw a cell-sized box around the particle with vertices at the centers of 4 nearest grid cells.
        //the particle's velocity has a weighted influence on these four cells,
        //and at the end we divide each cell's velocity by the total weight contributed to it
        //(I'm using that total weight as the cell's density because I'm thinking of velocity and density as being sampled at the cell's center)

        Array.Fill(velocityX, 0);
        Array.Fill(velocityY, 0);
        Array.Fill(density, 0);

        var maxHeight = height - 0.5f;
        var maxWidth = width - 0.5f;

        for (int k = 0; k < particlePointer; k++)
        {
            var shiftedGridCoordX = Mathf.Clamp(-0.5f + (particlePositionX[k] / cellSize), -0.5f, maxWidth);
            var shiftedGridCoordY = Mathf.Clamp(-0.5f + (particlePositionY[k] / cellSize), -0.5f, maxHeight);
            var i = (int)Mathf.Floor(shiftedGridCoordY);
            var j = (int)Mathf.Floor(shiftedGridCoordX);
            i = Mathf.Clamp(i, -1, height - 1);
            j = Mathf.Clamp(j, -1, width - 1);
            var boxCoordX = shiftedGridCoordX - j;
            var boxCoordY = shiftedGridCoordY - i;

            if (!(i < 0))
            {
                if (!(j < 0) && !obstacle[Index(i, j)])
                {
                    var wt = 2 - boxCoordX - boxCoordY;
                    velocityX[Index(i, j)] += wt * particleVelocityX[k];
                    velocityY[Index(i, j)] += wt * particleVelocityY[k];
                    density[Index(i, j)] += wt;
                }
                if (j + 1 < width && !obstacle[Index(i, j + 1)])
                {
                    var wt = 1 + boxCoordX - boxCoordY;
                    velocityX[Index(i, j + 1)] += wt * particleVelocityX[k];
                    velocityY[Index(i, j + 1)] += wt * particleVelocityY[k];
                    density[Index(i, j + 1)] += wt;
                }
            }
            if (i + 1 < width)
            {
                if (!(j < 0) && !obstacle[Index(i + 1, j)])
                {
                    var wt = 1 - boxCoordX + boxCoordY;
                    velocityX[Index(i + 1, j)] += wt * particleVelocityX[k];
                    velocityY[Index(i + 1, j)] += wt * particleVelocityY[k];
                    density[Index(i + 1, j)] += wt;
                }
                if (j + 1 < width && !obstacle[Index(i + 1, j + 1)])
                {
                    var wt = boxCoordX + boxCoordY;
                    velocityX[Index(i + 1, j + 1)] += wt * particleVelocityX[k];
                    velocityY[Index(i + 1, j + 1)] += wt * particleVelocityY[k];
                    density[Index(i + 1, j + 1)] += wt;
                }
            }
        }

        for (int k = 0; k < numCells; k++)
        {
            if (density[k] != 0)
            {
                velocityX[k] /= density[k];
                velocityY[k] /= density[k];
                density[k] *= 0.25f;
            }
        }
    }

    private void TransferGridVelocitiesToParticles()
    {
        //each particle's new velocity is a weighted average of the velocities at the nearest four cell centers

        var maxHeight = height - 0.5f;
        var maxWidth = width - 0.5f;

        for (int k = 0; k < particlePointer; k++)
        {
            InterpolateParticleVelocity(maxWidth, maxHeight, k);
        }
    }

    private void InterpolateParticleVelocity(float maxWidth, float maxHeight,  int k)
    {
        var shiftedGridCoordX = Mathf.Clamp(-0.5f + (particlePositionX[k] / cellSize), -0.5f, maxWidth);
        var shiftedGridCoordY = Mathf.Clamp(-0.5f + (particlePositionY[k] / cellSize), -0.5f, maxHeight);
        var i = (int)Mathf.Floor(shiftedGridCoordY);
        var j = (int)Mathf.Floor(shiftedGridCoordX);
        i = Mathf.Clamp(i, -1, height - 1);
        j = Mathf.Clamp(j, -1, width - 1);
        var boxCoordX = shiftedGridCoordX - j;
        var boxCoordY = shiftedGridCoordY - i;

        particleVelocityX[k] = 0;
        particleVelocityY[k] = 0;
        float denom = 0;

        if (!(i < 0))
        {
            if (!(j < 0))
            {
                var wt = 2 - boxCoordX - boxCoordY;
                particleVelocityX[k] += wt * velocityX[Index(i, j)];
                particleVelocityY[k] += wt * velocityY[Index(i, j)];
                denom += wt;
            }
            if (j + 1 < width)
            {
                var wt = 1 + boxCoordX - boxCoordY;
                particleVelocityX[k] += wt * velocityX[Index(i, j + 1)];
                particleVelocityY[k] += wt * velocityY[Index(i, j + 1)];
                denom += wt;
            }
        }
        if (i + 1 < width)
        {
            if (!(j < 0))
            {
                var wt = 1 - boxCoordX + boxCoordY;
                particleVelocityX[k] += wt * velocityX[Index(i + 1, j)];
                particleVelocityY[k] += wt * velocityY[Index(i + 1, j)];
                denom += wt;
            }
            if (j + 1 < width)
            {
                var wt = boxCoordX + boxCoordY;
                particleVelocityX[k] += wt * velocityX[Index(i + 1, j + 1)];
                particleVelocityY[k] += wt * velocityY[Index(i + 1, j + 1)];
                denom += wt;
            }
        }

        if (denom != 0)
        {
            particleVelocityX[k] /= denom;
            particleVelocityY[k] /= denom;
        }
    }

    //project velocity field onto its incompressible part
    private void Project()
    {
        for (int i = 1; i < height - 1; i++)
        {
            for (int j = 1; j < width - 1; j++)
            {
                bufferA[Index(i, j)] = -0.5f * cellSize * (velocityX[Index(i, j + 1)] - velocityX[Index(i, j - 1)] + velocityY[Index(i + 1, j)] - velocityY[Index(i - 1, j)]);
            }
        }

        SetBoundary(bufferA);
        Array.Fill(bufferB, 0);

        for (int k = 0; k < 10; k++)
        {
            for (int i = 1; i < height - 1; i++)
            {
                for (int j = 1; j < width - 1; j++)
                {
                    bufferB[Index(i, j)] = 0.25f * (bufferA[Index(i, j)] + bufferB[Index(i - 1, j)] + bufferB[Index(i + 1, j)] +
                     bufferB[Index(i, j - 1)] + bufferB[Index(i, j + 1)]);
                }
            }
            SetBoundary(bufferB);
        }

        float d = 0.5f / cellSize;
        for (int i = 1; i < height - 1; i++)
        {
            for (int j = 1; j < width - 1; j++)
            {
                velocityX[Index(i, j)] -= d * (bufferB[Index(i, j + 1)] - bufferB[Index(i, j - 1)]);
                velocityY[Index(i, j)] -= d * (bufferB[Index(i + 1, j)] - bufferB[Index(i - 1, j)]);
            }
        }
        SetBoundary(velocityX, velocityY);
    }

    private void SetBoundary(float[] v1, float[] v2)
    {
        for (int i = 1; i < height - 1; i++)
        {
            v1[Index(i, 0)] = -v1[Index(i, 1)];
            v2[Index(i, 0)] = v2[Index(i, 1)];
            v1[Index(i, height - 1)] = -v1[Index(i, height - 2)];
            v2[Index(i, height - 1)] = v1[Index(i, height - 2)];
        }
        for (int j = 1; j < width - 1; j++)
        {
            v1[Index(0, j)] = v1[Index(1, j)];
            v2[Index(0, j)] = -v2[Index(1, j)];
            v1[Index(height - 1, j)] = v1[Index(height - 2, j)];
            v2[Index(height - 1, j)] = -v2[Index(height - 2, j)];
        }

        v1[Index(0, 0)] = 0.5f * (v1[Index(1, 0)] + v1[Index(0, 1)]);
        v2[Index(0, 0)] = 0.5f * (v2[Index(1, 0)] + v2[Index(0, 1)]);
        v1[Index(0, width - 1)] = 0.5f * (v1[Index(0, width - 2)] + v1[Index(1, width - 1)]);
        v2[Index(0, width - 1)] = 0.5f * (v2[Index(0, width - 2)] + v2[Index(1, width - 1)]);
        v1[Index(height - 1, width - 1)] = 0.5f * (v1[Index(height - 2, width - 1)] + v1[Index(height - 1, width - 2)]);
        v2[Index(height - 1, width - 1)] = 0.5f * (v2[Index(height - 2, width - 1)] + v2[Index(height - 1, width - 2)]);
        v1[Index(height - 1, 0)] = 0.5f * (v1[Index(height - 1, 1)] + v1[Index(height - 2, 0)]);
        v2[Index(height - 1, 0)] = 0.5f * (v2[Index(height - 1, 1)] + v2[Index(height - 2, 0)]);
    }

    private void SetBoundary(float[] f)
    {
        for (int i = 1; i < height - 1; i++)
        {
            f[Index(i, 0)] = f[Index(i, 1)];
            f[Index(i, height - 1)] = f[Index(i, height - 2)];
        }
        for (int j = 1; j < width - 1; j++)
        {
            f[Index(0, j)] = f[Index(1, j)];
            f[Index(height - 1, j)] = f[Index(height - 2, j)];
        }

        f[Index(0, 0)] = 0.5f * (f[Index(1, 0)] + f[Index(0, 1)]);
        f[Index(0, width - 1)] = 0.5f * (f[Index(0, width - 2)] + f[Index(1, width - 1)]);
        f[Index(height - 1, width - 1)] = 0.5f * (f[Index(height - 2, width - 1)] + f[Index(height - 1, width - 2)]);
        f[Index(height - 1, 0)] = 0.5f * (f[Index(height - 1, 1)] + f[Index(height - 2, 0)]);
    }
}