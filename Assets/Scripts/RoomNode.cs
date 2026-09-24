using System.Collections.Generic;
using UnityEngine;

public class RoomNode
{
    public int id;
    public Bounds bounds;
    public Vector3 center;
    public List<RoomEdge> neighbors = new List<RoomEdge>();

    public RoomNode(int id, Bounds bounds)
    {
        this.id = id;
        this.bounds = bounds;
        this.center = bounds.center;
    }
}