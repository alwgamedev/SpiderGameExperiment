using System;
using UnityEngine;

public class FLIPFluidSimulator
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
    public readonly float[] particlePositionX;//positions are local, where (0,0) is the lower left corner of the grid (makes transferring to grid and back easier)
    public readonly float[] particlePositionY;
    public readonly float[] particleVelocityX;
    public readonly float[] particleVelocityY;
    public readonly float[] particleCollisionChance;//0 = full collision, 1 = no collision

    readonly int[] cellContainingParticle;
    readonly int[] particlesByCell;//array of length = numParticles, partitioned into chunks for each cell; each chunk stores the indices of the particles in that cell
    readonly int[] cellStart;//[i] => starting index of the chunk dedicated to cell i in the particlesByCellArray
    readonly int[] cellParticleCount;//then we could use this for density instead

    int spawnPointer;//particles at index >= particlePointer are inactive and not simulated

    //grid -- NOTE: row 0 is at the bottom of the grid, row 1 above it, etc.
    readonly float[] density;
    readonly float[] velocityX;
    readonly float[] velocityY;
    readonly float[] prevVelocityX;
    readonly float[] prevVelocityY;
    readonly float[] bufferA;
    readonly float[] bufferB;

    readonly int collisionMask;
    readonly Collider2D[] obstacle;//per grid cell


    public int SpawnPointer => spawnPointer;

    public FLIPFluidSimulator(int width, int height, float cellSize, int numParticles, float gravity, float particleRadius, int collisionMask)
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
        particlePositionX = new float[numParticles];
        particlePositionY = new float[numParticles];
        particleVelocityX = new float[numParticles];
        particleVelocityY = new float[numParticles];
        particleCollisionChance = new float[numParticles];

        cellContainingParticle = new int[numParticles];
        particlesByCell = new int[numParticles];
        cellStart = new int[numCells];
        cellParticleCount = new int[numCells];

        density = new float[numCells];
        velocityX = new float[numCells];
        velocityY = new float[numCells];
        prevVelocityX = new float[numCells];
        prevVelocityY = new float[numCells];
        bufferA = new float[numCells];
        bufferB = new float[numCells];

        this.collisionMask = collisionMask;
        obstacle = new Collider2D[numCells];
    }

    int Index(int i, int j) => i * width + j;

    //for use in shader
    public float DensityAtVertex(int vertex)
    {
        return DensityAtVertex(vertex / (width + 1), vertex % (width + 1));
    }

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

    //good enough for now, but doesn't work very well when obstacle is right on the surface
    public void FillObstacleDensities()
    {
        for (int i = height - 1; i > -1; i--)
        {
            for (int j = 0; j < width; j++)
            {
                if (obstacle[Index(i, j)])
                {
                    var sum = 0f;
                    var ct = 0;
                    if (i < height - 1)
                    {
                        sum += density[Index(i + 1, j)];
                        ct++;
                    }
                    if (j > 0)
                    {
                        sum += density[Index(i, j - 1)];
                        ct++;
                    }

                    if (ct != 0)
                    {
                        density[Index(i, j)] = sum / ct;
                    }
                }
            }
        }
    }

    public void SpawnParticles(int num, float spread, float positionX, float positionY,
        float initialVelocityX = 0, float initialVelocityY = 0)
    {
        int k = 0;
        var a = 0.5f * cellSize;
        positionX = Mathf.Clamp(positionX, a, worldWidth - a);
        positionY = Mathf.Clamp(positionY, a, worldHeight - a);
        while (k < num && spawnPointer < numParticles)
        {
            particlePositionX[spawnPointer] = positionX + MathTools.RandomFloat(-spread, spread);
            particlePositionY[spawnPointer] = positionY + MathTools.RandomFloat(-spread, spread);
            particleCollisionChance[spawnPointer] = MathTools.RandomFloat(0, 1);
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

    public void Update(float dt, float worldPositionX, float worldPositionY,
        int pushApartIterations, float collisionBounciness,
        int gaussSeidelIterations, float overRelaxation, float flipWeight,
        float fluidDensity, float obstacleVelocityNormalizer)
    {
        IntegrateParticles(dt);
        PushParticlesApart(pushApartIterations);
        SetOccupiedCells(worldPositionX, worldPositionY);
        ResolveCollisions(dt, worldPositionX, worldPositionY, collisionBounciness);
        TransferParticleVelocitiesToGrid();
        SolveDivergence(gaussSeidelIterations, overRelaxation, fluidDensity, obstacleVelocityNormalizer);
        TransferGridVelocitiesToParticles(dt, flipWeight);
    }

    private void IntegrateParticles(float dt)
    {
        for (int k = 0; k < spawnPointer; k++)
        {
            particleVelocityY[k] += dt * gravity;
            particlePositionX[k] += dt * particleVelocityX[k];
            particlePositionY[k] += dt * particleVelocityY[k];
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

    private void ResolveCollisions(float dt, float worldPositionX, float worldPositionY, float collisionBounciness)
    {
        var dtInverse = 1 / dt;

        for (int k = 0; k < spawnPointer; k++)
        {
            var i = (int)Mathf.Floor(particlePositionY[k] / cellSize);
            var j = (int)Mathf.Floor(particlePositionX[k] / cellSize);
            if (!(i < 0) && i < height && !(j < 0) && j < width)
            {
                ////ATM collision is only for simple colliders (circle, capsule, box)
                var o = obstacle[Index(i, j)];
                if (o)
                {
                    var localPosX = particlePositionX[k] + worldPositionX - o.bounds.center.x;
                    var localPosY = particlePositionY[k] + worldPositionY - o.bounds.center.y;
                    var ellipticalCoordX = localPosX / o.bounds.extents.x;
                    var ellipticalCoordY = localPosY / o.bounds.extents.y;
                    var ellipticalR2 = ellipticalCoordX * ellipticalCoordX + ellipticalCoordY * ellipticalCoordY;
                    var distToCenter2 = localPosX * localPosX + localPosY * localPosY;
                    var boundaryR2 = distToCenter2 / ellipticalR2;

                    if (float.IsNormal(boundaryR2))
                    {
                        var boundaryR = Mathf.Sqrt(boundaryR2);
                        var distToCenter = Mathf.Sqrt(distToCenter2);
                        var correction = boundaryR - distToCenter;

                        if (correction > 0)
                        {
                            var nX = localPosX / distToCenter;
                            var nY = localPosY / distToCenter;
                            var dX = correction * nX;
                            var dY = correction * nY;

                            var dot = particleVelocityX[k] * nX + particleVelocityY[k] * nY;
                            if (dot < 0)
                            {
                                var t = 2 * (particleVelocityX[k] * nY - particleVelocityY[k] * nX);
                                particleVelocityX[k] = -collisionBounciness * (particleVelocityX[k] - t * nY);
                                particleVelocityY[k] = -collisionBounciness * (particleVelocityY[k] + t * nX);
                            }
                            particlePositionX[k] += dX;
                            particlePositionY[k] += dY;
                            particleVelocityX[k] += dtInverse * dX;
                            particleVelocityY[k] += dtInverse * dY;
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
                    particleVelocityX[k] -= dx / dt;
                    if (particleVelocityX[k] < 0)
                    {
                        particleVelocityX[k] = -collisionBounciness * particleVelocityX[k];
                    }
                }
                else if (particlePositionX[k] > worldWidth - c)
                {
                    var dx = particlePositionX[k] - worldWidth + c;
                    particlePositionX[k] -= dx;
                    particleVelocityX[k] -= dx / dt;
                    if (particleVelocityX[k] > 0)
                    {
                        particleVelocityX[k] = -collisionBounciness * particleVelocityX[k];
                    }
                }

                if (particlePositionY[k] < c)
                {
                    var dy = particlePositionY[k] - c;
                    particlePositionY[k] -= dy;
                    particleVelocityY[k] -= dy / dt;
                    if (particleVelocityY[k] < 0)
                    {
                        particleVelocityY[k] = -collisionBounciness * particleVelocityY[k];
                    }
                }
                //else if (particlePositionY[k] > worldHeight - c)
                //{
                //    var dy = particlePositionY[k] - worldHeight + c;
                //    particlePositionY[k] -= dy;
                //    particleVelocityY[k] -= dy / dt;
                //    if (particleVelocityY[k] > 0)
                //    {
                //        particleVelocityY[k] = -collisionBounciness * particleVelocityY[k];
                //    }
                //}
            }
        }
    }


    private void PushParticlesApart(int iterations)
    {
        for (int m = 0; m < iterations; m++)
        {
            Array.Fill(cellParticleCount, 0);
            for (int k = 0; k < spawnPointer; k++)
            {
                var w = cellSizeInverse;
                var h = cellSizeInverse * particlePositionY[k];
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
            var end = cellStart[numCells - 1] + cellParticleCount[numCells - 1];

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
            float tolerance = 2 * particleRadius;
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
                    var count = c < numCells - 1 ? cellStart[cell + 1] - cellStart[cell] : end - start;
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
                                particleVelocityX[k1] += a;
                                particleVelocityY[k1] += b;
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

        Array.Copy(velocityX, prevVelocityX, velocityX.Length);
        Array.Copy(velocityY, prevVelocityY, velocityY.Length);
        Array.Fill(velocityX, 0);
        Array.Fill(velocityY, 0);
        Array.Fill(cellParticleCount, 0);
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
                if (!(j < 0))
                {
                    var wt = (1 - boxCoordX) * (1 - boxCoordY);
                    velocityX[Index(i, j)] += wt * particleVelocityX[k];
                    velocityY[Index(i, j)] += wt * particleVelocityY[k];
                    density[Index(i, j)] += wt;
                }
                if (j + 1 < width)
                {
                    var wt = boxCoordX * (1 - boxCoordY);
                    velocityX[Index(i, j + 1)] += wt * particleVelocityX[k];
                    velocityY[Index(i, j + 1)] += wt * particleVelocityY[k];
                    density[Index(i, j + 1)] += wt;
                }
            }
            if (i + 1 < height)
            {
                if (!(j < 0))
                {
                    var wt = (1 - boxCoordX) * boxCoordY;
                    velocityX[Index(i + 1, j)] += wt * particleVelocityX[k];
                    velocityY[Index(i + 1, j)] += wt * particleVelocityY[k];
                    density[Index(i + 1, j)] += wt;
                }
                if (j + 1 < width)
                {
                    var wt = boxCoordX * boxCoordY;
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
            }
        }
    }

    private void TransferGridVelocitiesToParticles(float dt, float flipWeight)
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
            float flipSumX = 0;
            float flipSumY = 0;
            float denom = 0;

            if (!(i < 0))
            {
                if (!(j < 0) && density[Index(i, j)] > 0)
                {
                    var wt = (1 - boxCoordX) * (1 - boxCoordY);
                    sumX += wt * velocityX[Index(i, j)];
                    sumY += wt * velocityY[Index(i, j)];
                    flipSumX += wt * (velocityX[Index(i, j)] - prevVelocityX[Index(i, j)]);
                    flipSumY += wt * (velocityY[Index(i, j)] - prevVelocityY[Index(i, j)]);
                    denom += wt;
                }
                if (j + 1 < width && density[Index(i, j + 1)] > 0)
                {
                    var wt = boxCoordX * (1 - boxCoordY);
                    sumX += wt * velocityX[Index(i, j + 1)];
                    sumY += wt * velocityY[Index(i, j + 1)];
                    flipSumX += wt * (velocityX[Index(i, j + 1)] - prevVelocityX[Index(i, j + 1)]);
                    flipSumY += wt * (velocityY[Index(i, j + 1)] - prevVelocityY[Index(i, j + 1)]);
                    denom += wt;
                }
            }
            if (i + 1 < height)
            {
                if (!(j < 0) && density[Index(i + 1, j)] > 0)
                {
                    var wt = (1 - boxCoordX) * boxCoordY;
                    sumX += wt * velocityX[Index(i + 1, j)];
                    sumY += wt * velocityY[Index(i + 1, j)];
                    flipSumX += wt * (velocityX[Index(i + 1, j)] - prevVelocityX[Index(i + 1, j)]);
                    flipSumY += wt * (velocityY[Index(i + 1, j)] - prevVelocityY[Index(i + 1, j)]);
                    denom += wt;
                }
                if (j + 1 < width && density[Index(i + 1, j + 1)] > 0)
                {
                    var wt = boxCoordX * boxCoordY;
                    sumX += wt * velocityX[Index(i + 1, j + 1)];
                    sumY += wt * velocityY[Index(i + 1, j + 1)];
                    flipSumX += wt * (velocityX[Index(i + 1, j + 1)] - prevVelocityX[Index(i + 1, j + 1)]);
                    flipSumY += wt * (velocityY[Index(i + 1, j + 1)] - prevVelocityY[Index(i + 1, j + 1)]);
                    denom += wt;
                }
            }

            if (denom != 0)
            {
                var picX = sumX / denom;
                var picY = sumY / denom;
                var flipX = particleVelocityX[k] + dt * (flipSumX / denom);
                var flipY = particleVelocityY[k] + dt * (flipSumY / denom);
                particleVelocityX[k] = Mathf.Lerp(picX, flipX, flipWeight);
                particleVelocityY[k] = Mathf.Lerp(picY, flipY, flipWeight);
            }
        }
    }

    private void SolveDivergence(int solveIterations, float overRelaxation, float fluidDensity, float obstacleVelocityNormalizer)
    {
        for (int k = 0; k < solveIterations; k++)
        {
            for (int i = 1; i < height - 1; i++)
            {
                for (int j = 1; j < width - 1; j++)
                {
                    if (density[Index(i, j)] == 0)
                    {
                        continue;
                    }

                    var denom = 2 + (i > 1 ? 1 : 0) + (j > 1 ? 1 : 0);

                    var d = velocityX[Index(i, j + 1)] - velocityX[Index(i, j)] + velocityY[Index(i + 1, j)] - velocityY[Index(i, j)];
                    var goalDensity = obstacle[Index(i, j)] ? 
                        obstacle[Index(i, j)].attachedRigidbody ? DynamicObstacleDensity() : 0
                        : fluidDensity;
                    d -= density[Index(i, j)] - goalDensity;
                    d *= overRelaxation / denom;


                    velocityX[Index(i, j + 1)] -= d;
                    velocityY[Index(i + 1, j)] -= d;
                    if (j > 1)
                    {
                        velocityX[Index(i, j)] += d;
                    }
                    if (i > 1)
                    {
                        velocityY[Index(i, j)] += d;
                    }

                    float DynamicObstacleDensity()
                    {
                        return fluidDensity - obstacle[Index(i, j)].attachedRigidbody.linearVelocity.magnitude * obstacleVelocityNormalizer;
                    }
                }
            }
        }
    }
}