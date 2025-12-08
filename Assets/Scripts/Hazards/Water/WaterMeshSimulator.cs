using System;
using UnityEngine;

[Serializable]
public class WaterMeshSimulator : SpringyMeshSimulator
{
    [SerializeField] float width;
    [SerializeField] float height;
    [SerializeField] int numVerticesX;
    [SerializeField] int numVerticesY;

    public float Width => width;
    public float Height => height;
    public int NumVerticesX => numVerticesX;
    public int NumVerticesY => numVerticesY;
    public int NumQuads => (numVerticesX - 1) * (numVerticesY - 1);

    public float FluidHeight(float localX)
    {
        for (int j = 1; j < numVerticesX; j++)
        {
            if (vertices[j].position.x > localX)
            {
                return Mathf.Lerp(vertices[j - 1].position.y, vertices[j].position.y, 
                    (localX - vertices[j - 1].position.x) / (vertices[j].position.x - vertices[j - 1].position.x));
            }
        }

        return -0.5f * height;
    }

    public void HandleDisplacement(Collider2D c, Vector3 waterWorldCenter, float agitationScale, float dt)
    {
        //2do
    }

    public void Initialize()
    {
        var upperLeftCorner = new Vector3(-0.5f * width, 0.5f * height);//vertex positions are in local space
        float quadWidth = width / (numVerticesX - 1);
        float quadHeight = height / (numVerticesY - 1);

        //initialize vertices
        vertices = new SpringyMeshVertex[numVerticesX * numVerticesY];
        restPositions = new Vector3[vertices.Length];
        int k = -1;
        for (int i = 0; i < numVerticesY; i++)
        {
            for (int j = 0; j < numVerticesX; j++)
            {
                vertices[++k] = new(upperLeftCorner + i * quadHeight * Vector3.down + j * quadWidth * Vector3.right);
                restPositions[k] = vertices[k].position;
                //note k = i * numVerticesX + j
            }
        }

        //freeze bottom and sides of rectangle
        for (int j = 0; j < numVerticesX; j++)
        {
            vertices[(numVerticesY - 1) * numVerticesX + j].FreezePosition();
            //v.freezeX = true;
            //v.freezeY = true;
            //v.freezeZ = true;
        }
        for (int i = 0; i < numVerticesY; i++)
        {
            vertices[i * numVerticesX].freezeX = true;
            vertices[(i + 1) * numVerticesX - 1].freezeX = true;
        }

        //initialize quad indices
        int numQuads = NumQuads;
        quads = new int[4 * numQuads];
        k = -1;
        for (int i = 0; i < numVerticesY - 1; i++)
        {
            for (int j = 0; j < numVerticesX - 1; j++)
            {
                //(i,j) the upper left corner of the quad
                int m = i * numVerticesX + j;//index of that upper left vertex in the vertices array
                quads[++k] = m;
                quads[++k] = m + numVerticesX;
                quads[++k] = m + numVerticesX + 1;
                quads[++k] = m + 1;
                //quad vertices listed in CCW order starting in upper left corner;
            }
        }

        //set edges of shape
        //edgesAtRest = new Vector3[6 * numQuads];
        //k = -1;
        //for (int i = 0; i < numQuads; i++)
        //{
        //    int j = 4 * i;
        //    //left vertical edge
        //    edgesAtRest[++k] = vertices[quads[j + 1]].position - vertices[quads[j]].position;
        //    //right vertical edge
        //    edgesAtRest[++k] = vertices[quads[j + 2]].position - vertices[quads[j + 3]].position;
        //    //top horizontal edge
        //    edgesAtRest[++k] = vertices[quads[j + 3]].position - vertices[quads[j]].position;
        //    //bottom horizontal edge
        //    edgesAtRest[++k] = vertices[quads[j + 2]].position - vertices[quads[j + 1]].position;
        //    //02 diagonal edge
        //    edgesAtRest[++k] = vertices[quads[j + 2]].position - vertices[quads[j]].position;
        //    //13 diagonal edge
        //    edgesAtRest[++k] = vertices[quads[j + 3]].position - vertices[quads[j + 1]].position;
        //}
    }
}