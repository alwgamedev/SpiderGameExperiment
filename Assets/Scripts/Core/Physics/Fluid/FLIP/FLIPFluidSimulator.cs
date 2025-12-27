using System;
using UnityEngine;

public class FLIPFluidSimulator
{
    public readonly int width;//grid width in # of cells
    public readonly int height;
    public readonly int numCells;
    public readonly float cellSize;
    public readonly float cellSizeInverse;
    public readonly float cellArea;
    public readonly float worldWidth;
    public readonly float worldHeight;

    //PARTICLES
    public readonly int numParticles;
    public readonly float gravity;
    public readonly float particleRadius;
    public readonly float particleToCellAreaRatio;
    public readonly Vector2[] particlePosition;//local to grid (with (0,0) being the lower left corner)
    public readonly Vector2[] particleVelocity;

    //these are used only for "PushParticlesApart" (and may be inaccurate afterwards)
    readonly int[] cellContainingParticle;
    readonly int[] particlesByCell;
    readonly int[] cellStart;
    readonly int[] cellParticleCount;

    int spawnPointer;//particles at index >= spawnPointer are inactive and not simulated

    //GRID -- note: row 0 is at the bottom of the grid, row 1 above it, etc.
    readonly float[] particleDensity;//particles per particle area (so 1 means a cell is completely packed with particles)
    readonly float[] transferWeight;
    readonly Vector2[] velocity;
    readonly Vector2[] prevVelocity;

    readonly int collisionMask;
    readonly Collider2D[] obstacle;

    public FLIPFluidSimulator(int width, int height, float cellSize, int numParticles, float gravity, float particleRadius, int collisionMask)
    {
        this.width = width;
        this.height = height;
        numCells = width * height;
        this.cellSize = cellSize;
        cellSizeInverse = 1 / cellSize;
        cellArea = cellSize * cellSize;
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

        particleToCellAreaRatio = 4 * particleRadius * particleRadius / (cellSize * cellSize);
        particleDensity = new float[numCells];
        transferWeight = new float[numCells];
        velocity = new Vector2[numCells];
        prevVelocity = new Vector2[numCells];

        this.collisionMask = collisionMask;
        obstacle = new Collider2D[numCells];
    }

    int Index(int i, int j) => i * width + j;

    //for use in density shader
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
            sum += particleDensity[Index(i, j)];
        }
        if (i > 0 && j < width)
        {
            count++;
            sum += particleDensity[Index(i - 1, j)];
        }
        if (i > 0 && j > 0)
        {
            count++;
            sum += particleDensity[Index(i - 1, j - 1)];
        }
        if (i < height && j > 0)
        {
            count++;
            sum += particleDensity[Index(i, j - 1)];
        }

        return sum / count;
    }

    //good enough for now, but doesn't work very well when obstacle is right on the surface
    //(we're gonna move to completely different rendering approach anyway)
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
                        sum += transferWeight[Index(i + 1, j)];
                        ct++;
                    }
                    if (j > 0)
                    {
                        sum += transferWeight[Index(i, j - 1)];
                        ct++;
                    }

                    if (ct != 0)
                    {
                        transferWeight[Index(i, j)] = sum / ct;
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
            particlePosition[spawnPointer++] = new(position.x + MathTools.RandomFloat(-spread, spread), position.y + MathTools.RandomFloat(-spread, spread));
            k++;
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
                Vector3 v = velocity[Index(i, j)].normalized;
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
        int pushApartIterations, float pushApartTolerance,
        float collisionBounciness, float flipWeight,
        int gaussSeidelIterations, float overRelaxation,
        float fluidDensity, float fluidDrag,
        float normalizedFluidDensity, float densitySpringConstant, float agitationPower, float obstacleVelocityNormalizer)
    {
        IntegrateParticles(dt);
        PushParticlesApart(pushApartIterations, pushApartTolerance);
        UpdateObstacles(worldPosition);
        ResolveCollisions(/*dt, worldPosition,*/ collisionBounciness);
        TransferParticleVelocitiesToGrid();//this also updates cell density (particleDensity)
        ApplyBuoyanceForces(fluidDensity, fluidDrag);//after we have computed cell densities
        SolveContinuity(gaussSeidelIterations, overRelaxation, normalizedFluidDensity, densitySpringConstant, agitationPower, obstacleVelocityNormalizer);
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


    private void UpdateObstacles(Vector2 worldPosition)
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

    private void ApplyBuoyanceForces(float fluidDensity, float fluidDrag)
    {
        var fullCellMass = fluidDensity * cellArea;
        var dragUnit = fluidDrag * fluidDensity * cellSize;

        for (int k = 0; k < numCells; k++)
        {
            if (obstacle[k] && obstacle[k].attachedRigidbody && particleDensity[k] != 0)
            {
                var rb = obstacle[k].attachedRigidbody;
                if (particleDensity[k] != 0)
                {
                    rb.AddForce(new Vector2(0, -fullCellMass * particleDensity[k] * gravity));
                }
                var v = rb.linearVelocity;

                //apply drag:
                //we need width of the object when viewed "from front" (wrt its velocity).
                //we'll go one cell at a time: check if the cell in front (in direction of velocity)
                //is vacant, then we'll include that cell's width as part of the frontal cross section
                //and apply the drag force for that piece
                if (v != Vector2.zero)
                {
                    if (v.y > v.x)
                    {
                        if (v.y  > -v.x && k + width < numCells && obstacle[k + width])//forward cell is up
                        {
                            rb.AddForce(-dragUnit * Mathf.Sqrt(1 + v.x * v.x / (v.y * v.y)) *particleDensity[k] * v.magnitude * v);
                        }
                        else if (!(v.y > -v.x) && k % width > 0 && obstacle[k - 1])//forward cell is left
                        {
                            rb.AddForce(-dragUnit * Mathf.Sqrt(1 + v.y * v.y / (v.x * v.x)) * particleDensity[k] * v.magnitude * v);
                        }
                    }
                    else
                    {
                        if (v.y > -v.x && k % width < width - 1 && obstacle[k + 1])//forward cell is right
                        {
                            rb.AddForce(-dragUnit * Mathf.Sqrt(1 + v.y * v.y / (v.x * v.x)) * particleDensity[k] * v.magnitude * v);
                        }
                        else if (!(v.y > -v.x) && !(k - width < 0) && obstacle[k - width])//forward cell is down
                        {
                            rb.AddForce(-dragUnit * Mathf.Sqrt(1 + v.x * v.x / (v.y * v.y)) * particleDensity[k] * v.magnitude * v);
                        }
                    }
                }
            }
        }
    }

    private void ResolveCollisions(/*float dt,*/ /*Vector2 worldPosition,*/ float collisionBounciness)
    {
        //float dtInverse = 1 / dt;

        for (int k = 0; k < spawnPointer; k++)
        {
            //var i = (int)(particlePosition[k].y / cellSize);
            //var j = (int)(particlePosition[k].x / cellSize);
            //if (!(i < 0) && i < height && !(j < 0) && j < width)
            //{
            //        //treats all colliders like circles base on bounded boxes
            //        var o = obstacle[Index(i, j)];
            //        if (o)
            //        {
            //            var localPosX = particlePosition[k].x + worldPosition.x - o.bounds.center.x;
            //            var localPosY = particlePosition[k].y + worldPosition.y - o.bounds.center.y;
            //            var ellipticalCoordX = localPosX / o.bounds.extents.x;
            //            var ellipticalCoordY = localPosY / o.bounds.extents.y;
            //            var ellipticalR2 = ellipticalCoordX * ellipticalCoordX + ellipticalCoordY * ellipticalCoordY;
            //            var distToCenter2 = localPosX * localPosX + localPosY * localPosY;
            //            var boundaryR2 = distToCenter2 / ellipticalR2;

            //            if (float.IsNormal(boundaryR2))
            //            {
            //                var boundaryR = Mathf.Sqrt(boundaryR2);
            //                var distToCenter = Mathf.Sqrt(distToCenter2);
            //                var correction = boundaryR - distToCenter;

            //                if (correction > 0)
            //                {
            //                    Vector2 n = new(localPosX / distToCenter, localPosY / distToCenter);
            //                    var dP = correction * n;
            //                    particlePosition[k] += dP;

            //                    var dot = Vector2.Dot(particleVelocity[k], n);
            //                    if (dot < 0)
            //                    {
            //                        var t = n.CCWPerp();
            //                        particleVelocity[k] = -collisionBounciness * (particleVelocity[k] - 2 * Vector2.Dot(particleVelocity[k], t) * t);
            //                    }
            //                }
            //            }
            //        }


            //wall collisions 

            var c = cellSize;// + particleRadius;
            if (particlePosition[k].x < c)
            {
                var dx = particlePosition[k].x - c;
                particlePosition[k].x -= dx;
                if (particleVelocity[k].x < 0)
                {
                    particleVelocity[k].x = -collisionBounciness * particleVelocity[k].x;
                    particleVelocity[k].y = collisionBounciness * particleVelocity[k].y;
                }
            }
            else if (particlePosition[k].x > worldWidth - c)
            {
                var dx = particlePosition[k].x - worldWidth + c;
                particlePosition[k].x -= dx;
                if (particleVelocity[k].x > 0)
                {
                    particleVelocity[k].x = -collisionBounciness * particleVelocity[k].x;
                    particleVelocity[k].y = collisionBounciness * particleVelocity[k].y;
                }
            }

            if (particlePosition[k].y < c)
            {
                var dy = particlePosition[k].y - c;
                particlePosition[k].y -= dy;
                if (particleVelocity[k].y < 0)
                {
                    particleVelocity[k].x = collisionBounciness * particleVelocity[k].x;
                    particleVelocity[k].y = -collisionBounciness * particleVelocity[k].y;
                }
            }
            else if (particlePosition[k].y > worldHeight - c)
            {
                var dy = particlePosition[k].y - worldHeight + c;
                particlePosition[k].y -= dy;
                if (particleVelocity[k].y > 0)
                {
                    particleVelocity[k].x = collisionBounciness * particleVelocity[k].x;
                    particleVelocity[k].y = -collisionBounciness * particleVelocity[k].y;
                }
            }
        }
    }


    private void PushParticlesApart(int iterations, float toleranceMultiplier)
    {
        //float dtInverse = 1 / dt;

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
            }

            //now loop over all particles and check for overlap with particles in adjacent cells
            //we'll ignore the fact that particles may move to different cells during this process
            float tolerance = toleranceMultiplier * particleRadius;
            float toleranceSqrd = tolerance * tolerance;
            for (int k = 0; k < spawnPointer; k++)
            {
                var c = cellContainingParticle[k];

                var x = particlePosition[k].x;
                var y = particlePosition[k].y;

                int i = c / width;
                int j = c % width;

                var cellX = x - j * cellSize;
                var cellY = y - i * cellSize;

                CheckCell(c);
                if (cellX < particleRadius && j > 0)
                {
                    CheckCell(c - 1);
                }
                if (cellX > cellSize - particleRadius && j < width - 1)
                {
                    CheckCell(c + 1);
                }
                if (cellY < particleRadius && i > 0)
                {
                    CheckCell(c - width);
                }
                if (cellY > cellSize - particleRadius && i < height - 1)
                {
                    CheckCell(c + width);
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
                                    a = x1 != 0 ? diff * Mathf.Sign(x1) : 0;
                                    b = y1 != 0 ? diff * Mathf.Sign(y1) : 0;
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

                                    if (float.IsInfinity(a) || float.IsInfinity(b))
                                    {
                                        a = float.IsInfinity(a) ? diff * Mathf.Sign(a) : 0;
                                        b = float.IsInfinity(b) ? diff * Mathf.Sign(b) : 0;
                                        if (a != 0 && b != 0)
                                        {
                                            a *= MathTools.cos45;
                                            b *= MathTools.cos45;
                                        }
                                    }
                                }

                                particlePosition[k].x -= a;
                                particlePosition[k].y -= b;
                                particlePosition[k1].x += a;
                                particlePosition[k1].y += b;
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

        Array.Copy(velocity, prevVelocity, velocity.Length);
        Array.Fill(velocity, Vector2.zero);
        Array.Fill(particleDensity, 0);
        Array.Fill(transferWeight, 0);

        for (int k = 0; k < spawnPointer; k++)
        {
            var w = Mathf.Clamp(particlePosition[k].x * cellSizeInverse, 0, width);//clamping is unnecessary because this comes right after wall collisions
            var h = Mathf.Clamp(particlePosition[k].y * cellSizeInverse, 0, height);
            var i = Mathf.Clamp((int)h, 0, height - 1);
            var j = Mathf.Clamp((int)w, 0, width - 1);
            particleDensity[Index(i, j)] += particleToCellAreaRatio;

            w -= 0.5f;
            h -= 0.5f;
            i = (int)Mathf.Floor(h);
            j = (int)Mathf.Floor(w);
            var boxCoordX = w - j;
            var boxCoordY = h - i;

            if (!(i < 0))
            {
                if (!(j < 0))
                {
                    var wt = (1 - boxCoordX) * (1 - boxCoordY);
                    velocity[Index(i, j)] += wt * particleVelocity[k];
                    transferWeight[Index(i, j)] += wt;
                }
                if (j < width - 1)
                {
                    var wt = boxCoordX * (1 - boxCoordY);
                    velocity[Index(i, j + 1)] += wt * particleVelocity[k];
                    transferWeight[Index(i, j + 1)] += wt;
                }
            }
            if (i < height - 1)
            {
                if (!(j < 0))
                {
                    var wt = (1 - boxCoordX) * boxCoordY;
                    velocity[Index(i + 1, j)] += wt * particleVelocity[k];
                    transferWeight[Index(i + 1, j)] += wt;
                }
                if (j < width - 1)
                {
                    var wt = boxCoordX * boxCoordY;
                    velocity[Index(i + 1, j + 1)] += wt * particleVelocity[k];
                    transferWeight[Index(i + 1, j + 1)] += wt;
                }
            }
        }

        for (int k = 0; k < numCells; k++)
        {
            if (transferWeight[k] > 0)
            {
                velocity[k] /= transferWeight[k];
            }
        }
    }

    private void TransferGridVelocitiesToParticles(float dt, float flipWeight)
    {
        //each particle's new velocity is a weighted average of the velocities at the nearest four cell centers
        for (int k = 0; k < spawnPointer; k++)
        {
            var w = Mathf.Clamp(particlePosition[k].x * cellSizeInverse, 0, width) - 0.5f;
            var h = Mathf.Clamp(particlePosition[k].y * cellSizeInverse, 0, height) - 0.5f;
            var i = (int)Mathf.Floor(h);
            var j = (int)Mathf.Floor(w);
            var boxCoordX = w - j;
            var boxCoordY = h - i;

            Vector2 picSum = Vector2.zero;
            Vector2 flipSum = Vector2.zero;
            float denom = 0;

            if (!(i < 0))
            {
                if (!(j < 0) && particleDensity[Index(i, j)] > 0)
                {
                    var wt = (1 - boxCoordX) * (1 - boxCoordY);
                    picSum += wt * velocity[Index(i, j)];
                    flipSum += wt * (velocity[Index(i, j)] - prevVelocity[Index(i, j)]);
                    denom += wt;
                }
                if (j < width - 1 && particleDensity[Index(i, j + 1)] > 0)
                {
                    var wt = boxCoordX * (1 - boxCoordY);
                    picSum += wt * velocity[Index(i, j + 1)];
                    flipSum += wt * (velocity[Index(i, j + 1)] - prevVelocity[Index(i, j + 1)]);
                    denom += wt;
                }
            }
            if (i < height - 1)
            {
                if (j > 0 && particleDensity[Index(i + 1, j)] > 0)
                {
                    var wt = (1 - boxCoordX) * boxCoordY;
                    picSum += wt * velocity[Index(i + 1, j)];
                    flipSum += wt * (velocity[Index(i + 1, j)] - prevVelocity[Index(i + 1, j)]);
                    denom += wt;
                }
                if (j < width - 1 && particleDensity[Index(i + 1, j + 1)] > 0)
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

    private void SolveContinuity(int solveIterations, float overRelaxation,
        float normalizedFluidDensity, float densitySpringConstant, float agitationPower, float obstacleVelocityNormalizer)
    {
        for (int k = 0; k < solveIterations; k++)
        {
            SolveWithLeftHandDerivatives(overRelaxation, normalizedFluidDensity, densitySpringConstant, agitationPower, obstacleVelocityNormalizer);
        }

        SetBoundary();
    }

    private void SetBoundary()
    {
        for (int j = 1; j < width - 1; j++)
        {
            velocity[Index(0, j)].y = -velocity[Index(1, j)].y;//can also just set these to 0
            velocity[Index(height - 1, j)].y = -velocity[Index(height - 2, j)].y;
        }

        for (int i = 1; i < height - 1; i++)
        {
            velocity[Index(i, 0)].x = -velocity[Index(i, 1)].x;
            velocity[Index(i, width - 1)].x = -velocity[Index(i, width - 2)].x;
        }

        velocity[Index(0, 0)] = 0.5f * (velocity[Index(1, 0)] + velocity[Index(0, 1)]);
        velocity[Index(0, width - 1)] = 0.5f * (velocity[Index(0, width - 2)] + velocity[Index(1, width - 1)]);
        velocity[Index(height - 1, width - 1)] = 0.5f * (velocity[Index(height - 2, width - 1)] + velocity[Index(height - 1, width - 2)]);
        velocity[Index(height - 1, 0)] = 0.5f * (velocity[Index(height - 1, 1)] + velocity[Index(height - 2, 0)]);
    }

    //nice way to fake interaction -- the faster the obstacle is moving, the more water will want to rush away from it
    //(and when the obstacle is not moving, fluid ignores it -- which is useful for 2d when you need fluid to flow in front of obstacle)
    private float DynamicObstacleDensity(int i, int j, float normalizedFluidDensity, float agitationPower, float obstacleVelocityNormalizer)
    {
        var s = Mathf.Pow(obstacle[Index(i, j)].attachedRigidbody.linearVelocity.sqrMagnitude, agitationPower);
        //^I like sqr mag because it exaggerates the effect at the high end and reduces the effect at the low end
        //(so when you are just moving about slowly it hardly disrupts the water) 
        return normalizedFluidDensity - s * obstacleVelocityNormalizer;
    }

    private void SolveWithLeftHandDerivatives(float overRelaxation, float normalizedFluidDensity, float densitySpringConstant, float agitationPower, float obstacleVelocityNormalizer)
    {
        for (int i = 1; i < height; i++)
        {
            for (int j = 1; j < width; j++)
            {
                if (particleDensity[Index(i, j)] == 0)
                {
                    continue;
                }

                var denom = (i > 1 ? 1 : 0) + (j > 1 ? 1 : 0) + (i < height - 1 ? 1 : 0) + (j < width - 1 ? 1 : 0);

                var d = velocity[Index(i, j)].x - velocity[Index(i, j - 1)].x + velocity[Index(i, j)].y - velocity[Index(i - 1, j)].y;
                var goalDensity = obstacle[Index(i, j)] ?
                    obstacle[Index(i, j)].attachedRigidbody ? DynamicObstacleDensity(i, j, normalizedFluidDensity, agitationPower, obstacleVelocityNormalizer) : 0
                    : normalizedFluidDensity;
                var densityResidual = particleDensity[Index(i, j)] - goalDensity;
                d = -d + densitySpringConstant * densityResidual;
                //^not physically accurate, but works.
                //whenever density is too high divergence will go negative (increasing density),
                //and whenever divergence is too low divergence will go positive (decreasing dens).
                d *= overRelaxation / denom;

                if (j < width - 1)
                {
                    velocity[Index(i, j)].x += d;
                }
                if (i < height - 1)
                {
                    velocity[Index(i, j)].y += d;
                }
                if (j > 1)
                {
                    velocity[Index(i, j - 1)].x -= d;
                }
                if (i > 1)
                {
                    velocity[Index(i - 1, j)].y -= d;
                }
            }
        }
    }

    //leads to left edge of surface always being a little too high for some reason?
    private void SolveWithRightHandDerivatives(float overRelaxation, float normalizedFluidDensity, float densitySpringConstant, float agitationPower, float obstacleVelocityNormalizer)
    {
        for (int i = 0; i < height - 1; i++)
        {
            for (int j = 0; j < width - 1; j++)
            {
                if (particleDensity[Index(i, j)] == 0)
                {
                    continue;
                }

                var denom = (i > 0 ? 1 : 0) + (j > 0 ? 1 : 0) + (i < height - 2 ? 1 : 0) + (j < width - 2 ? 1 : 0);

                var d = velocity[Index(i, j + 1)].x - velocity[Index(i, j)].x + velocity[Index(i + 1, j)].y - velocity[Index(i, j)].y;
                var goalDensity = obstacle[Index(i, j)] ?
                    obstacle[Index(i, j)].attachedRigidbody ? DynamicObstacleDensity(i, j, normalizedFluidDensity, agitationPower, obstacleVelocityNormalizer) : 0
                    : normalizedFluidDensity;
                var densityResidual = particleDensity[Index(i, j)] - goalDensity;
                d = -d + densitySpringConstant * densityResidual;
                //^not physically accurate, but works.
                //whenever density is too high divergence will go negative (increasing density),
                //and whenever divergence is too low divergence will go positive (decreasing dens).
                d *= overRelaxation / denom;

                if (j < width - 2)
                {
                    velocity[Index(i, j + 1)].x += d;
                }
                if (i < height - 2)
                {
                    velocity[Index(i + 1, j)].y += d;
                }
                if (j > 0)
                {
                    velocity[Index(i, j)].x -= d;
                }
                if (i > 0)
                {
                    velocity[Index(i, j)].y -= d;
                }
            }
        }
    }

    private void SolveWithCentralDerivatives(float overRelaxation, float normalizedFluidDensity, float densitySpringConstant, float agitationPower, float obstacleVelocityNormalizer)
    {
        for (int i = 1; i < height - 1; i++)
        {
            for (int j = 1; j < width - 1; j++)
            {
                if (particleDensity[Index(i, j)] == 0)
                {
                    continue;
                }

                var denom = (i > 1 ? 1 : 0) + (j > 1 ? 1 : 0) + (i < height - 2 ? 1 : 0) + (j < width - 2 ? 1 : 0);

                var d = velocity[Index(i, j + 1)].x - velocity[Index(i, j - 1)].x + velocity[Index(i + 1, j)].y - velocity[Index(i - 1, j)].y;
                var goalDensity = obstacle[Index(i, j)] ?
                    obstacle[Index(i, j)].attachedRigidbody ? DynamicObstacleDensity(i, j, normalizedFluidDensity, agitationPower, obstacleVelocityNormalizer) : 0
                    : normalizedFluidDensity;
                var densityResidual = particleDensity[Index(i, j)] - goalDensity;
                d = -d + densitySpringConstant * densityResidual;
                //^not physically accurate, but works.
                //whenever density is too high divergence will go negative (increasing density),
                //and whenever divergence is too low divergence will go positive (decreasing dens).
                d *= overRelaxation / denom;

                if (j < width - 2)
                {
                    velocity[Index(i, j + 1)].x += d;
                }
                if (i < height - 2)
                {
                    velocity[Index(i + 1, j)].y += d;
                }
                if (j > 1)
                {
                    velocity[Index(i, j - 1)].x -= d;
                }
                if (i > 1)
                {
                    velocity[Index(i - 1, j)].y -= d;
                }
            }
        }
    }
}