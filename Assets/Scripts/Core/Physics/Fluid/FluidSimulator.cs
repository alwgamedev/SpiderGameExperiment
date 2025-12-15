using System;
using UnityEditor;
using UnityEngine;

public class FluidSimulator
{
    public readonly int width;//grid width in # of cells
    public readonly int height;
    public readonly int numCells;
    public readonly float cellSize;
    public readonly float cellSizeInverse;
    public readonly float worldWidth;
    public readonly float worldHeight;

    //particles
    public readonly int numParticles;
    public readonly float gravity;
    public readonly float particleRadius;
    public readonly float collisionBounciness;
    public readonly float[] particlePositionX;//positions are local, where (0,0) is the lower left corner of the grid (makes transferring to grid and back easier)
    public readonly float[] particlePositionY;
    public readonly float[] particleVelocityX;
    public readonly float[] particleVelocityY;

    readonly int[] cellContainingParticle;
    readonly int[] particlesByCell;//array of length = numParticles, partitioned into chunks for each cell; each chunk stores the indices of the particles in that cell
    readonly int[] cellStart;//[i] => starting index of the chunk dedicated to cell i in the particlesByCellArray
    readonly int[] cellParticleCount;//then we could use this for density instead

    int spawnPointer;//particles at index >= particlePointer are inactive and not simulated

    //grid -- NOTE: row 0 is at the bottom of the grid, row 1 above it, etc.
    readonly float[] density;
    readonly float[] velocityX;
    readonly float[] velocityY;
    readonly float[] bufferA;
    readonly float[] bufferB;

    readonly int collisionMask;
    readonly Collider2D[] obstacle;//per grid cell

    public int SpawnPointer => spawnPointer;

    public FluidSimulator(int width, int height, float cellSize, int numParticles, float gravity, float particleRadius,
        float collisionBounciness, int collisionMask)
    {
        this.width = width;
        this.height = height;
        numCells = width * height;
        this.cellSize = cellSize;
        cellSizeInverse = 1 / cellSize;
        worldWidth = width * cellSize;
        worldHeight = height * cellSize;

        this.numParticles = numParticles;
        this.gravity = gravity;

        this.particleRadius = particleRadius;
        this.collisionBounciness = collisionBounciness;
        particlePositionX = new float[numParticles];
        particlePositionY = new float[numParticles];
        particleVelocityX = new float[numParticles];
        particleVelocityY = new float[numParticles];

        cellContainingParticle = new int[numParticles];
        particlesByCell = new int[numParticles];
        cellStart = new int[numCells];
        cellParticleCount = new int[numCells];

        density = new float[numCells];
        velocityX = new float[numCells];
        velocityY = new float[numCells];
        bufferA = new float[numCells];
        bufferB = new float[numCells];

        this.collisionMask = collisionMask;
        obstacle = new Collider2D[numCells];
    }

    int Index(int i, int j) => i * width + j;

    //where 0 <= i <= height and 0 <= j <= width
    //maybe it would be better to have your mesh vertices at the center of the cells
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

    public void SpawnParticles(int num, float spread, float positionX, float positionY, float initialVelocityX = 0, float initialVelocityY = 0)
    {
        int k = 0;
        var a = 0.5f * cellSize;
        positionX = Mathf.Clamp(positionX, a, worldWidth - a);
        positionY = Mathf.Clamp(positionY, a, worldHeight - a);
        while (k < num && spawnPointer < numParticles)
        {
            particlePositionX[spawnPointer] = positionX + MathTools.RandomFloat(-spread, spread);
            particlePositionY[spawnPointer] = positionY + MathTools.RandomFloat(-spread, spread);
            particleVelocityX[spawnPointer] = initialVelocityX;
            particleVelocityY[spawnPointer] = initialVelocityY;
            k++;
            spawnPointer++;
        }
    }

    public void DrawParticleGizmos(float worldPositionX, float worldPositionY)
    {
        Gizmos.color = Color.black;
        for (int i = 0; i < spawnPointer; i++)
        {
            Gizmos.DrawSphere(
                new(particlePositionX[i] + worldPositionX,
                particlePositionY[i] + worldPositionY), particleRadius);
        }
    }

    public void DrawVelocityFieldGizmos(float worldPositionX, float worldPositionY)
    {
        var r = 0.15f * cellSize;
        var scale = 0.5f * cellSize;
        for (int i = 0; i < height; i++)
        {
            for (int j = 0; j < width; j++)
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

    public void Update(float dt, float worldPositionX, float worldPositionY, float particleDrag,
        int pushApartIterations, int gaussSeidelIterations, float overRelaxation, float weight)
    {
        IntegrateParticles(dt, particleDrag);
        PushParticlesApart(pushApartIterations);
        SetOccupiedCells(worldPositionX, worldPositionY);
        ResolveCollisions(dt, worldPositionX, worldPositionY);
        TransferParticleVelocitiesToGrid();
        Project(gaussSeidelIterations, overRelaxation);
        TransferGridVelocitiesToParticles(weight);
    }

    private void IntegrateParticles(float dt, float drag)
    {
        for (int k = 0; k < spawnPointer; k++)
        {
            var d = dt * drag * Speed(k);
            particleVelocityX[k] -= d * particleVelocityX[k];
            particleVelocityY[k] += dt * gravity - d * particleVelocityY[k];
            particlePositionX[k] += dt * particleVelocityX[k];
            particlePositionY[k] += dt * particleVelocityY[k];
        }

        float Speed(int k)
        {
            return Mathf.Sqrt(particleVelocityX[k] * particleVelocityX[k] + particleVelocityY[k] * particleVelocityY[k]);
        }
    }


    private void SetOccupiedCells(float worldPositionX, float worldPositionY)
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

    private void ResolveCollisions(float dt, float worldPositionX, float worldPositionY)
    {
        for (int k = 0; k < spawnPointer; k++)
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
                        var b = particleVelocityX[k] * nX + particleVelocityY[k] * nY;
                        if (b < 0)
                        {
                            var a = 2 * (particleVelocityX[k] * nY - particleVelocityY[k] * nX);
                            particleVelocityX[k] = -collisionBounciness * (particleVelocityX[k] - a * nY);
                            particleVelocityY[k] = -collisionBounciness * (particleVelocityY[k] + a * nX);
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

            //wall collisions -- can get rid of once we have ground collision
            //(but we may still want to do something about particles escaping through the roof, e.g. just reset them to the center of the grid)
            var c = cellSize + particleRadius;
            if (particlePositionX[k] < c)
            {
                var dx = particlePositionX[k] - c;
                particlePositionX[k] -= dx;
                if (particleVelocityX[k] < 0)
                {
                    particleVelocityX[k] = -collisionBounciness * particleVelocityX[k];
                }

                particleVelocityX[k] -= dx / dt;
            }
            else if (particlePositionX[k] > worldWidth - c)
            {
                var dx = particlePositionX[k] - worldWidth + c;
                particlePositionX[k] -= dx;
                if (particleVelocityX[k] > 0)
                {
                    particleVelocityX[k] = -collisionBounciness * particleVelocityX[k];
                }

                particleVelocityX[k] -= dx / dt;
            }

            if (particlePositionY[k] < c)
            {
                var dy = particlePositionY[k] - c;
                particlePositionY[k] -= dy;
                if (particleVelocityY[k] < 0)
                {
                    particleVelocityY[k] = -collisionBounciness * particleVelocityY[k];
                }

                particleVelocityY[k] -= dy / dt;
            }
            else if (particlePositionY[k] > worldHeight - c)
            {
                var dy = particlePositionY[k] - worldHeight + c;
                particlePositionY[k] -= dy;
                if (particleVelocityY[k] > 0)
                {
                    particleVelocityY[k] = -collisionBounciness * particleVelocityY[k];
                }
                particleVelocityY[k] -= dy / dt;
            }
        }
    }

    private void PushParticlesApart(int iterations)
    {
        for (int m = 0; m < iterations; m++)
        {
            Array.Fill(cellContainingParticle, -1);
            Array.Fill(cellParticleCount, 0);
            for (int k = 0; k < spawnPointer; k++)
            {
                var w = cellSizeInverse * particlePositionX[k];
                var h = cellSizeInverse * particlePositionY[k];
                if (float.IsNaN(w) || float.IsNaN(h))
                {
                    continue;
                }
                var i = Mathf.Clamp((int)h, 0, height - 1);
                var j = Mathf.Clamp((int)w, 0, width - 1);
                cellContainingParticle[k] = Index(i, j);
                cellParticleCount[Index(i, j)]++;
            }

            cellStart[0] = 0;
            for (int k = 1; k < numCells; k++)
            {
                cellStart[k] = cellStart[k - 1] + cellParticleCount[k - 1];
            }

            for (int k = 0; k < spawnPointer; k++)
            {
                //fill each cell chunk starting at the end, using the cell particle count to know what slot we're on within the chunk
                //(i.e. decrementing the cellParticleCount after each placement)
                var c = cellContainingParticle[k];
                if (c < 0)
                {
                    continue;
                }
                var l = cellStart[c] + --cellParticleCount[c];
                particlesByCell[l] = k;

                //when we finish filling the chunk, we still know the cell particle count:
                //it's cellStart[k + 1] - cellStart[k]
                //although later we may want to restore it if we're going to use it for density?
            }


            //now loop over all particles and check for overlap with particles in adjacent cells
            //we'll ignore the fact that particles may move to different cells during this process
            float tolerance = 2.25f * particleRadius;
            float toleranceSqrd = tolerance * tolerance;
            for (int k = 0; k < spawnPointer; k++)
            {
                var c = cellContainingParticle[k];
                if (c < 0)
                {
                    continue;
                }

                var x = particlePositionX[k];
                var y = particlePositionY[k];

                if (float.IsNaN(x) || float.IsNaN(y))
                {
                    continue;
                }

                int i = c / width;
                int j = c % width;

                CheckCell(c);

                if (j < width - 1)
                {
                    CheckCell(c + 1);
                }

                if (j > 0)
                {
                    CheckCell(c - 1);
                }

                if (i < height - 1)
                {
                    CheckCell(c + width);
                    if (j < width - 1)
                    {
                        CheckCell(c + width + 1);
                    }
                    if (j > 0)
                    {
                        CheckCell(c + width - 1);
                    }
                }

                if (i > 0)
                {
                    CheckCell(c - width);
                    if (j < width - 1)
                    {
                        CheckCell(c - width + 1);
                    }
                    if (j > 0)
                    {
                        CheckCell(c - width - 1);
                    }
                }

                void CheckCell(int cell)
                {
                    var start = cellStart[cell];
                    var count = cellContainingParticle[k] < numCells - 1 ? cellStart[cell + 1] - cellStart[cell] : spawnPointer - start;
                    for (int j = start; j < start + count - 1; j++)
                    {
                        var k1 = particlesByCell[j];
                        if (k1 != k)
                        {
                            var x1 = particlePositionX[k1] - x;
                            var y1 = particlePositionY[k1] - y;
                            var d = x1 * x1 + y1 * y1;
                            if (d < toleranceSqrd)
                            {
                                d = Mathf.Sqrt(d);
                                var diff = 0.5f * (tolerance - d);
                                float a, b = 0;
                                if (d == 0)
                                {
                                    a = x1 != 0 ? Mathf.Sign(x1) : 0;
                                    b = y1 != 0 ? Mathf.Sign(y1) : 0;
                                    if (a != 0 && b != 0)
                                    {
                                        a *= MathTools.cos45;
                                        b *= MathTools.cos45;
                                    }
                                }
                                else
                                {
                                    a = diff * x1 / d;
                                    b = diff * y1 / d;
                                }

                                if (float.IsInfinity(a) || float.IsInfinity(b))
                                {
                                    a = float.IsInfinity(a) ? Mathf.Sign(a) : 0;
                                    b = float.IsInfinity(b) ? Mathf.Sign(b) : 0;
                                    a *= diff;
                                    b *= diff;
                                    if (a != 0 && b != 0)
                                    {
                                        a *= MathTools.cos45;
                                        b *= MathTools.cos45;
                                    }
                                }
                                particlePositionX[k] -= a;
                                particlePositionY[k] -= b;
                                particlePositionX[k1] += a;
                                particlePositionY[k1] += b;
                            }
                        }
                    }
                }
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

        for (int k = 0; k < spawnPointer; k++)
        {
            var w = particlePositionX[k] * cellSizeInverse;
            var h = particlePositionY[k] * cellSizeInverse;
            var shiftedGridCoordX = Mathf.Clamp(-0.5f + w, -0.5f, maxWidth);
            var shiftedGridCoordY = Mathf.Clamp(-0.5f + h, -0.5f, maxHeight);
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
            if (density[k] > 0)
            {
                velocityX[k] /= density[k];
                velocityY[k] /= density[k];
                density[k] *= 0.25f;
            }
        }
    }

    private void TransferGridVelocitiesToParticles(float weight)
    {
        //each particle's new velocity is a weighted average of the velocities at the nearest four cell centers

        var maxHeight = height - 0.5f;
        var maxWidth = width - 0.5f;

        for (int k = 0; k < spawnPointer; k++)
        {
            var w = particlePositionX[k] * cellSizeInverse;
            var h = particlePositionY[k] * cellSizeInverse;
            var shiftedGridCoordX = Mathf.Clamp(-0.5f + w, -0.5f, maxWidth);
            var shiftedGridCoordY = Mathf.Clamp(-0.5f + h, -0.5f, maxHeight);
            var i = (int)Mathf.Floor(shiftedGridCoordY);
            var j = (int)Mathf.Floor(shiftedGridCoordX);
            var boxCoordX = shiftedGridCoordX - j;
            var boxCoordY = shiftedGridCoordY - i;

            float sumX = 0;
            float sumY = 0;
            float denom = 0;

            if (!(i < 0))
            {
                if (!(j < 0) && density[Index(i, j)] > 0)
                {
                    var wt = 2 - boxCoordX - boxCoordY;
                    sumX += wt * velocityX[Index(i, j)];
                    sumY += wt * velocityY[Index(i, j)];
                    denom += wt;
                }
                if (j + 1 < width && density[Index(i, j + 1)] > 0)
                {
                    var wt = 1 + boxCoordX - boxCoordY;
                    sumX += wt * velocityX[Index(i, j + 1)];
                    sumY += wt * velocityY[Index(i, j + 1)];
                    denom += wt;
                }
            }
            if (i + 1 < width)
            {
                if (!(j < 0) && density[Index(i + 1, j)] > 0)
                {
                    var wt = 1 - boxCoordX + boxCoordY;
                    sumX += wt * velocityX[Index(i + 1, j)];
                    sumY += wt * velocityY[Index(i + 1, j)];
                    denom += wt;
                }
                if (j + 1 < width && density[Index(i + 1, j + 1)] > 0)
                {
                    var wt = boxCoordX + boxCoordY;
                    sumX += wt * velocityX[Index(i + 1, j + 1)];
                    sumY += wt * velocityY[Index(i + 1, j + 1)];
                    denom += wt;
                }
            }

            if (denom != 0)
            {
                particleVelocityX[k] = Mathf.Lerp(particleVelocityX[k], sumX / denom, weight);
                particleVelocityY[k] = Mathf.Lerp(particleVelocityY[k], sumY / denom, weight);
            }
        }
    }

    //project velocity field onto its incompressible part
    private void Project(int solveIterations, float overRelaxation)
    {
        //ideas:
        //a) should be able to solve the system directly, without gauss seidel?
        //b) find the incompressible part directly: divergence free vector field with same curl as v (just make sure to use central differences to avoid going outside bdry)

        Array.Copy(velocityX, bufferA, velocityX.Length);
        Array.Copy(velocityY, bufferB, velocityY.Length);

        for (int k = 0; k < solveIterations; k++)
        {
            for (int i = 1; i < height - 1; i++)
            {
                for (int j = 1; j < width - 1; j++)
                {
                    if (density[Index(i, j)] == 0 || obstacle[Index(i, j)])
                    {
                        continue;
                    }

                    var d = 0.25f * overRelaxation * (velocityX[Index(i, j + 1)] - velocityX[Index(i, j)] + velocityY[Index(i + 1, j)] - velocityY[Index(i, j)]);
                    if (!obstacle[Index(i, j + 1)])
                    {
                        velocityX[Index(i, j + 1)] -= d;
                    }
                    if (j > 1 && !obstacle[Index(i, j - 1)])
                    {
                        velocityX[Index(i, j)] += d;
                    }
                    if (!obstacle[Index(i + 1, j)])
                    {
                        velocityY[Index(i + 1, j)] -= d;
                    }
                    if (i > 1 && !obstacle[Index(i - 1, j)])
                    {
                        velocityY[Index(i, j)] += d;
                    }
                }
            }
        }

        for (int i = 1; i < height - 1; i++)
        {
            for (int j = 1; j < width - 1; j++)
            {
                if (obstacle[Index(i, j)])
                {
                    velocityX[Index(i, j)] = bufferA[Index(i, j)];
                    velocityY[Index(i, j)] = bufferB[Index(i, j)];
                }
            }
        }

        //var d = 2 * cellSize;

        //    for (int i = 1; i < height - 1; i++)
        //    {
        //        for (int j = 1; j < width - 1; j++)
        //        {
        //            bufferA[Index(i, j)] = d * (velocityX[Index(i, j + 1)] - velocityX[Index(i, j - 1)] + velocityY[Index(i + 1, j)] - velocityY[Index(i, j)]);
        //        }
        //    }

        //    SetBoundary(bufferA);
        //    Array.Fill(bufferB, 0);//maybe we can start with a better guess for faster convergence

        //    for (int k = 0; k < solveIterations; k++)
        //    {
        //        for (int i = 1; i < height - 1; i++)
        //        {
        //            for (int j = 1; j < width - 1; j++)
        //            {
        //                bufferB[Index(i, j)] = 0.25f * (-bufferA[Index(i, j)] +
        //                    bufferB[Index(i, j + 1)] + bufferB[Index(i, j - 1)] + bufferB[Index(i + 1, j)] + bufferB[Index(i - 1, j)]);
        //            }
        //        }

        //        SetBoundary(bufferB);
        //    }

        //    d = 0.5f * cellSizeInverse;
        //    for (int i = 1; i < height - 1; i++)
        //    {
        //        for (int j = 1; j < width - 1; j++)
        //        {
        //            velocityX[Index(i, j)] -= d * (bufferB[Index(i, j + 1)] - bufferB[Index(i, j - 1)]);
        //            velocityY[Index(i, j)] -= d * (bufferB[Index(i + 1, j)] - bufferB[Index(i - 1, j)]);
        //        }
        //    }
        //    SetBoundary(velocityX, velocityY);
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