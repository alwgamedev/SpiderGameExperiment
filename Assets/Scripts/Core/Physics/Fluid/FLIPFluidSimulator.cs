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
    public readonly Vector2[] particlePosition;//local to grid (with (0,0) being the lower left corner)
    public readonly Vector2[] particleVelocity;

    readonly int[] cellContainingParticle;
    readonly int[] particlesByCell;//array of length = numParticles, partitioned into chunks for each cell; each chunk stores the indices of the particles in that cell
    readonly int[] cellStart;//[i] => starting index of the chunk dedicated to cell i in the particlesByCellArray
    readonly int[] cellParticleCount;//then we could use this for density instead

    int spawnPointer;//particles at index >= particlePointer are inactive and not simulated

    //grid -- NOTE: row 0 is at the bottom of the grid, row 1 above it, etc.
    readonly float[] density;
    readonly Vector2[] velocity;
    readonly Vector2[] prevVelocity;

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
        particlePosition = new Vector2[numParticles];
        particleVelocity = new Vector2[numParticles];

        cellContainingParticle = new int[numParticles];
        particlesByCell = new int[numParticles];
        cellStart = new int[numCells];
        cellParticleCount = new int[numCells];

        density = new float[numCells];
        velocity = new Vector2[numCells];
        prevVelocity = new Vector2[numCells];

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

    public void SpawnParticles(int num, float spread, Vector2 position)
    {
        int k = 0;
        var a = 0.5f * cellSize;
        position.x = Mathf.Clamp(position.x, a, worldWidth - a);
        position.y = Mathf.Clamp(position.y, a, worldHeight - a);
        while (k < num && spawnPointer < numParticles)
        {
            particlePosition[spawnPointer] = new(position.x + MathTools.RandomFloat(-spread, spread), position.y + MathTools.RandomFloat(-spread, spread));
            k++;
            spawnPointer++;
        }
    }

    public void DrawParticleGizmos(Vector2 worldPosition)
    {
        Gizmos.color = Color.black;
        for (int i = 0; i < spawnPointer; i++)
        {
            Gizmos.DrawSphere(particlePosition[i] + worldPosition, particleRadius);
        }
    }

    public void DrawVelocityFieldGizmos(Vector2 worldPosition)
    {
        var r = 0.15f * cellSize;
        var scale = 0.5f * cellSize;
        for (int i = 0; i < height; i++)
        {
            for (int j = 0; j < width; j++)
            {
                var cellCenter = new Vector3(worldPosition.x + (j + 0.5f) * cellSize, worldPosition.y + (i + 0.5f) * cellSize);
                var v = new Vector3(velocity[Index(i, j)].x, velocity[Index(i, j)].y).normalized;
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

    public void Update(float dt, Vector2 worldPosition,
        int pushApartIterations, float collisionBounciness,
        int gaussSeidelIterations, float overRelaxation, float flipWeight,
        float fluidDensity, float obstacleVelocityNormalizer)
    {
        IntegrateParticles(dt);
        PushParticlesApart(pushApartIterations);
        SetOccupiedCells(worldPosition);
        ResolveCollisions(dt, worldPosition, collisionBounciness);
        TransferParticleVelocitiesToGrid();
        SolveDivergence(gaussSeidelIterations, overRelaxation, fluidDensity, obstacleVelocityNormalizer);
        TransferGridVelocitiesToParticles(dt, flipWeight);
    }

    private void IntegrateParticles(float dt)
    {
        for (int k = 0; k < spawnPointer; k++)
        {
            particleVelocity[k].y += dt * gravity;
            particlePosition[k] += dt * particleVelocity[k];
        }
    }


    private void SetOccupiedCells(Vector2 worldPosition)
    {
        for (int i = 0; i < height; i++)
        {
            for (int j = 0; j < width; j++)
            {
                var cellCenter = new Vector2(worldPosition.x + (j + 0.5f) * cellSize, worldPosition.y + (i + 0.5f) * cellSize);
                obstacle[Index(i, j)] = Physics2D.OverlapCircle(cellCenter, 0.5f * cellSize, collisionMask);
            }
        }
    }

    private void ResolveCollisions(float dt, Vector2 worldPosition, float collisionBounciness)
    {
        var dtInverse = 1 / dt;

        for (int k = 0; k < spawnPointer; k++)
        {
            var i = (int)(particlePosition[k].y / cellSize);
            var j = (int)(particlePosition[k].x / cellSize);
            if (!(i < 0) && i < height && !(j < 0) && j < width)
            {
                //treats all colliders as ellipses based on their bounding box
                var o = obstacle[Index(i, j)];
                if (o)
                {
                    var localPosX = particlePosition[k].x + worldPosition.x - o.bounds.center.x;
                    var localPosY = particlePosition[k].y + worldPosition.y - o.bounds.center.y;
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
                            Vector2 n = new(localPosX / distToCenter, localPosY / distToCenter);
                            var dP = correction * n;

                            var dot = Vector2.Dot(particleVelocity[k], n);
                            if (dot < 0)
                            {
                                var t = n.CCWPerp();
                                particleVelocity[k] = -collisionBounciness * (particleVelocity[k] - 2 * Vector2.Dot(particleVelocity[k], t) * t);
                            }
                            particlePosition[k] += dP;
                            particleVelocity[k] += dtInverse * dP;
                        }
                    }
                }

                //wall collisions -- can get rid of once we have ground collision
                //(but we may still want to do something about particles escaping through the roof, e.g. just reset them to the center of the grid)
                var c = cellSize + particleRadius;
                if (particlePosition[k].x < c)
                {
                    var dx = particlePosition[k].x - c;
                    particlePosition[k].x -= dx;
                    particleVelocity[k].x -= dx / dt;
                    if (particlePosition[k].x < 0)
                    {
                        particleVelocity[k].x = -collisionBounciness * particleVelocity[k].x;
                    }
                }
                else if (particlePosition[k].x > worldWidth - c)
                {
                    var dx = particlePosition[k].x - worldWidth + c;
                    particlePosition[k].x -= dx;
                    particleVelocity[k].x -= dx / dt;
                    if (particleVelocity[k].x > 0)
                    {
                        particleVelocity[k].x = -collisionBounciness * particleVelocity[k].x;
                    }
                }

                if (particlePosition[k].y < c)
                {
                    var dy = particlePosition[k].y - c;
                    particlePosition[k].y -= dy;
                    particleVelocity[k].y -= dy / dt;
                    if (particleVelocity[k].y < 0)
                    {
                        particleVelocity[k].y = -collisionBounciness * particleVelocity[k].y;
                    }
                }
                else if (particlePosition[k].y > worldHeight - c)
                {
                    var dy = particlePosition[k].y - worldHeight + c;
                    particlePosition[k].y -= dy;
                    particleVelocity[k].y -= dy / dt;
                    if (particleVelocity[k].y > 0)
                    {
                        particleVelocity[k].y = -collisionBounciness * particleVelocity[k].y;
                    }
                }
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
                var w = cellSizeInverse * particlePosition[k].x;
                var h = cellSizeInverse * particlePosition[k].y;
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
                //(i.e. decrementing the cellParticleCount with each placement)
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
            float tolerance = 2.1f * particleRadius;
            float toleranceSqrd = tolerance * tolerance;
            for (int k = 0; k < spawnPointer; k++)
            {
                var c = cellContainingParticle[k];

                var x = particlePosition[k].x;
                var y = particlePosition[k].y;

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
                    var count = cell < numCells - 1 ? cellStart[cell + 1] - cellStart[cell] : end - start;
                    for (int j = start; j < start + count - 1; j++)
                    {
                        var k1 = particlesByCell[j];
                        if (k1 != k)
                        {
                            var x1 = particlePosition[k1].x - x;
                            var y1 = particlePosition[k1].y - y;
                            var d = x1 * x1 + y1 * y1;
                            if (d < toleranceSqrd)
                            {
                                d = Mathf.Sqrt(d);
                                var diff = 0.5f * (tolerance - d);
                                float a, b;
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

                                var push = new Vector2(a, b);
                                particlePosition[k] -= push;
                                particlePosition[k1] += push;
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

        Array.Copy(velocity, prevVelocity, velocity.Length);
        Array.Fill(velocity, Vector2.zero);
        Array.Fill(cellParticleCount, 0);
        Array.Fill(density, 0);

        for (int k = 0; k < spawnPointer; k++)
        {
            var w = particlePosition[k].x * cellSizeInverse;
            var h = particlePosition[k].y * cellSizeInverse;
            var shiftedGridCoordX = Mathf.Clamp(w, 0, width) - 0.5f;
            var shiftedGridCoordY = Mathf.Clamp(h, 0, height) - 0.5f;
            var i = (int)Mathf.Floor(shiftedGridCoordY);
            var j = (int)Mathf.Floor(shiftedGridCoordX);
            var boxCoordX = shiftedGridCoordX - j;
            var boxCoordY = shiftedGridCoordY - i;

            if (!(i < 0))
            {
                if (!(j < 0))
                {
                    var wt = (1 - boxCoordX) * (1 - boxCoordY);
                    velocity[Index(i, j)] += wt * particleVelocity[k];
                    density[Index(i, j)] += wt;
                }
                if (j + 1 < width)
                {
                    var wt = boxCoordX * (1 - boxCoordY);
                    velocity[Index(i, j + 1)] += wt * particleVelocity[k];
                    density[Index(i, j + 1)] += wt;
                }
            }
            if (i + 1 < height)
            {
                if (!(j < 0))
                {
                    var wt = (1 - boxCoordX) * boxCoordY;
                    velocity[Index(i + 1, j)] += wt * particleVelocity[k];
                    density[Index(i + 1, j)] += wt;
                }
                if (j + 1 < width)
                {
                    var wt = boxCoordX * boxCoordY;
                    velocity[Index(i + 1, j + 1)] += wt * particleVelocity[k];
                    density[Index(i + 1, j + 1)] += wt;
                }
            }
        }

        for (int k = 0; k < numCells; k++)
        {
            if (density[k] > 0)
            {
                velocity[k] /= density[k];
            }
        }
    }

    private void TransferGridVelocitiesToParticles(float dt, float flipWeight)
    {
        //each particle's new velocity is a weighted average of the velocities at the nearest four cell centers
        for (int k = 0; k < spawnPointer; k++)
        {
            var w = particlePosition[k].x * cellSizeInverse;
            var h = particlePosition[k].y * cellSizeInverse;
            var shiftedGridCoordX = Mathf.Clamp(w, 0, width) - 0.5f;
            var shiftedGridCoordY = Mathf.Clamp(h, 0, height) - 0.5f;
            var i = (int)Mathf.Floor(shiftedGridCoordY);
            var j = (int)Mathf.Floor(shiftedGridCoordX);
            var boxCoordX = shiftedGridCoordX - j;
            var boxCoordY = shiftedGridCoordY - i;

            Vector2 picSum = Vector2.zero;
            Vector2 flipSum = Vector2.zero;
            float denom = 0;

            if (!(i < 0))
            {
                if (!(j < 0) && density[Index(i, j)] > 0)
                {
                    var wt = (1 - boxCoordX) * (1 - boxCoordY);
                    picSum += wt * velocity[Index(i, j)];
                    flipSum += wt * (velocity[Index(i, j)] - prevVelocity[Index(i, j)]);
                    denom += wt;
                }
                if (j + 1 < width && density[Index(i, j + 1)] > 0)
                {
                    var wt = boxCoordX * (1 - boxCoordY);
                    picSum += wt * velocity[Index(i, j + 1)];
                    flipSum += wt * (velocity[Index(i, j + 1)] - prevVelocity[Index(i, j + 1)]);
                    denom += wt;
                }
            }
            if (i + 1 < height)
            {
                if (!(j < 0) && density[Index(i + 1, j)] > 0)
                {
                    var wt = (1 - boxCoordX) * boxCoordY;
                    picSum += wt * velocity[Index(i + 1, j)];
                    flipSum += wt * (velocity[Index(i + 1, j)] - prevVelocity[Index(i + 1, j)]);
                    denom += wt;
                }
                if (j + 1 < width && density[Index(i + 1, j + 1)] > 0)
                {
                    var wt = boxCoordX * boxCoordY;
                    picSum += wt * velocity[Index(i + 1, j + 1)];
                    flipSum += wt * (velocity[Index(i + 1, j + 1)] - prevVelocity[Index(i + 1, j + 1)]);
                    denom += wt;
                }
            }

            if (denom != 0)
            {
                picSum /= denom;
                flipSum = particleVelocity[k] + dt * (flipSum / denom);
                particleVelocity[k] = Vector2.Lerp(picSum, flipSum, flipWeight);
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

                    var d = velocity[Index(i, j + 1)].x - velocity[Index(i, j)].x + velocity[Index(i + 1, j)].y - velocity[Index(i, j)].y;
                    var goalDensity = obstacle[Index(i, j)] ? 
                        obstacle[Index(i, j)].attachedRigidbody ? DynamicObstacleDensity() : 0
                        : fluidDensity;
                    d -= density[Index(i, j)] - goalDensity;
                    d *= overRelaxation / denom;


                    velocity[Index(i, j + 1)].x -= d;
                    velocity[Index(i + 1, j)].y -= d;
                    if (j > 1)
                    {
                        velocity[Index(i, j)].x += d;
                    }
                    if (i > 1)
                    {
                        velocity[Index(i, j)].y += d;
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