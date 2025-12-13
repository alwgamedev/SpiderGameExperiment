using System;
using UnityEngine;

public class PICFluid
{
    readonly int width;//width as # of cells
    readonly int height;
    readonly int numCells;
    readonly float cellSize;
    readonly float worldWidth;
    readonly float worldHeight;
    readonly float worldPositionX;//position of the lower left corner of the grid in world
    readonly float worldPositionY;

    readonly int numParticles;
    readonly float particleAccel;//dt * gravity

    //particles
    readonly float collisionBounciness;
    readonly float particleRadius;
    readonly float[] particlePositionX;//positions are local, i.e. (0,0) is the lower left corner of the grid (makes transferring to grid and back easier)
    readonly float[] particlePositionY;
    readonly float[] particleVelocityX;
    readonly float[] particleVelocityY;

    //grid -- NOTE: row 0 is at the bottom of the grid, row 1 above it, etc.
    readonly float[] velocityX;
    readonly float[] velocityY;
    readonly float[] density;

    readonly int collisionMask;
    readonly Collider2D[] obstacle;//per grid cell

    readonly float[] gridBufferA;
    readonly float[] gridBufferB;

    int Index(int i, int j) => i * width + j;
    float NormalizedDensity(int i, int j)//could be useful e.g. for setting color
    {
        //divide density by integral of weight functions over the (valid area within the) four boxes influencing the cell
        if (OnPerimeterI(i) && OnPerimeterJ(j))
        {
            return 0.5f * density[Index(i, j)];//divide by 2
        }
        else if (OnPerimeterI(i) || OnPerimeterJ(j))
        {
            return 0.31f * density[Index(i, j)];//divide by 3.25
        }
        else
        {
            return 0.25f * density[Index(i, j)];//divide by 4
        }

        bool OnPerimeterI(int p)
        {
            return p == 0 || p == height - 1;
        }
        bool OnPerimeterJ(int q)
        {
            return q == 0 || q == width - 1;
        }

    }

    //SIMULATION

    public void Update(float dt)
    {
        IntegrateParticles(dt);
        SetOccupiedCells();
        ResolveCollisions(dt);
        TransferParticleVelocitiesToGrid();
        Project();
        TransferGridVelocitiesToParticles();
    }

    private void IntegrateParticles(float dt)
    {
        for (int i = 0; i < numParticles; i++)
        {
            particleVelocityY[i] += particleAccel;//dt * gravity
            particlePositionX[i] += dt * particleVelocityX[i];
            particlePositionY[i] += dt * particleVelocityY[i];
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
        for (int k = 0; k < numParticles; k++)
        {
            var i = (int)Mathf.Floor(particlePositionY[k] / cellSize);
            var j = (int)Mathf.Floor(particlePositionX[k] / cellSize);
            if (!(i < 0) && i < height && !(j < 0) && j < width)
            {
                var o = obstacle[Index(i, j)];
                if (o)
                {
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
                        particleVelocityX[k] += dx / dt;
                        particleVelocityY[k] += dy / dt;
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

            //wall collisions

            if (particlePositionY[k] < 0)
            {
                particleVelocityY[k] -= particlePositionY[k] / dt;
                particlePositionY[k] -= particlePositionY[k];
            }

            if (particlePositionY[k] < worldHeight)//don't enforce x bounds for particles above the worldHeight -- that means particles can escape!
            {
                if (particlePositionX[k] < 0)
                {
                    particleVelocityX[k] -= particlePositionX[k] / dt;
                    particlePositionX[k] -= particlePositionX[k];
                }
                else if (particlePositionX[k] > worldWidth)
                {
                    particleVelocityX[k] -= (particlePositionX[k] - width) / dt;

                }
                particlePositionX[k] -= particlePositionX[k] - width;
            }
        }
    }

    private void TransferParticleVelocitiesToGrid()
    {
        //draw a cell-sized box around the particle with vertices at the centers of 4 nearest grid cells.
        //the particle's velocity has a weighted influence on these four cells,
        //and at the end we divide each cell's velocity by the total weight contributed to it
        //(I'm using that total weight as the cell's density (because I'm thinking of velocity and density being sampled at the cell's center))

        Array.Fill(velocityX, 0);
        Array.Fill(velocityY, 0);
        Array.Fill(density, 0);

        var maxHeight = height - Mathf.Epsilon;
        var maxWidth = width - Mathf.Epsilon;

        for (int k = 0; k < numParticles; k++)
        {
            var shiftedGridCoordX = Mathf.Clamp(-0.5f + (particlePositionX[k] / cellSize), -1, maxHeight);
            var shiftedGridCoordY = Mathf.Clamp(-0.5f + (particlePositionY[k] / cellSize), -1, maxWidth);
            var i = (int)Mathf.Floor(shiftedGridCoordY);
            var j = (int)Mathf.Floor(shiftedGridCoordX);
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
            }
        }
    }

    private void TransferGridVelocitiesToParticles()
    {
        //each particle's new velocity is a weighted average of the velocities at the nearest four cell centers

        var maxHeight = height - Mathf.Epsilon;
        var maxWidth = width - Mathf.Epsilon;

        for (int k = 0; k < numParticles; k++)
        {
            var shiftedGridCoordX = Mathf.Clamp(-0.5f + (particlePositionX[k] / cellSize), -1, maxHeight);
            var shiftedGridCoordY = Mathf.Clamp(-0.5f + (particlePositionY[k] / cellSize), -1, maxWidth);
            var i = (int)Mathf.Floor(shiftedGridCoordY);
            var j = (int)Mathf.Floor(shiftedGridCoordX);
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
    }

    //project velocity field onto its incompressible part
    private void Project()
    {
        //Explanation:
        //For our velocity field u, we need to find the conservative vector field w with same divergence as u.
        //Then the incompressible part of u is u - w.
        //To find w we need to solve: curl(w) = 0 and div(w) = div(u), with w = 0 along the boundary.
        //If you differentiate both sides of div(w) = div(u) wrt y and use dw1/dy = dw2/dx (zero curl),
        //you get d2w2/dx2 + d2w2/dy2 = (d/dy)(div(u)).
        //When you write out the discrete version of this identity and solve for w2[i,j] you get a nice recursive formula:
        //w2[i,j] = 0.5 * (a[i + 1, j] - a[i, j] - w2[i + 2, j] + 2 * w2[i + 1, j] + 2 * w2[i, j + 1] - w2[i, j + 2]),
        //where a = cellSize * div(u).
        //This recursion allows us to compute w2 directly in linear time. :)

        //put cellSize * div(u) into bufferA
        for (int i = 0; i < height; i++)
        {
            for (int j = 0; j < width; j++)
            {
                gridBufferA[Index(i, j)] = UnscaledDivergence(i, j, velocityX, velocityY);
            }
        }

        //put w2 into bufferB
        for (int i = height - 1; i > -1; i--)
        {
            for (int j = width - 1; j > -1; j--)
            {
                gridBufferB[Index(i, j)] = 0.5f * (gridBufferA[Index(i + 1, j)] - SafeEntry(i, j, gridBufferA)
                    - SafeEntry(i + 2, j, gridBufferB) + 2 * SafeEntry(i + 1, j, gridBufferB) + 2 * SafeEntry(i, j + 1, gridBufferB) - SafeEntry(i, j + 2, gridBufferB));
            }
        }

        //use zero curl to compute w1, and subtract w from velocity field
        for (int j = width - 1; j > -1; j--)
        {
            float sum = 0;
            for (int i = height - 1; i > -1; i--)
            {
                //w1(x,y) = -int_{y}^{y1}dw2/dx(x,t)dt, where y1 is the upper boundary of the grid (just so there's no C(x) to deal with)
                sum += SafeEntry(i, j + 1, gridBufferB) - SafeEntry(i, j, gridBufferB);
                velocityX[Index(i, j)] += sum;
                velocityY[Index(i, j)] -= gridBufferB[Index(i, j)];
            }
        }

        float SafeEntry(int i, int j, float[] arr)
        {
            return i < width && j < height ? arr[Index(i, j)] : 0;
        }

        float UnscaledDivergence(int i, int j, float[] velocityX, float[] velocityY)
        {
            return SafeEntry(i, j + 1, velocityX) - velocityX[Index(i, j)] + SafeEntry(i + 1, j, velocityY) - velocityY[Index(i, j)];
        }
    }
}