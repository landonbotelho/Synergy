using UnityEngine;
using System.Collections.Generic;

public class HallwayConnection
{
    public RoomNode roomA;
    public RoomNode roomB;

    public Vector3Int doorA;
    public Vector3Int doorB;

    public Vector3Int hallStart;
    public Vector3Int hallEnd;

    public List<Vector3Int> outsideDoorwayA = new List<Vector3Int>();
    public List<Vector3Int> outsideDoorwayB = new List<Vector3Int>();
}
