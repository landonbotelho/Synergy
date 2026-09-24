using System.Collections.Generic;
using UnityEngine;

public class HallwayBuilder
{
    private Dictionary<int, HashSet<Vector3Int>> roomDoorPositions = new Dictionary<int, HashSet<Vector3Int>>();
    private List<HallwayConnection> hallwayConnections = new List<HallwayConnection>();

    private HashSet<Vector3Int> hallwayFloorCells = new HashSet<Vector3Int>();
    private HashSet<Vector3Int> hallwayWallCells = new HashSet<Vector3Int>();
    private HashSet<Vector3Int> outsideDoorwayCells = new HashSet<Vector3Int>();

    public HashSet<Vector3Int> HallwayFloorCells
    {
        get { return hallwayFloorCells; }
    }

    public HashSet<Vector3Int> HallwayWallCells
    {
        get { return hallwayWallCells; }
    }

    public void ResetRoomDoorPositions(List<RoomNode> roomGraph)
    {
        roomDoorPositions.Clear();

        for (int i = 0; i < roomGraph.Count; i++)
        {
            roomDoorPositions[roomGraph[i].id] = new HashSet<Vector3Int>();
        }
    }

    public HashSet<Vector3Int> GetDoorPositions(int roomId)
    {
        if (!roomDoorPositions.ContainsKey(roomId))
        {
            roomDoorPositions[roomId] = new HashSet<Vector3Int>();
        }

        return roomDoorPositions[roomId];
    }

    public List<HallwayConnection> GetHallwayConnections()
    {
        return new List<HallwayConnection>(hallwayConnections);
    }

    public void BuildHallways(List<RoomNode> roomGraph, HashSet<Vector3Int> roomInteriorCells)
    {
        ResetRoomDoorPositions(roomGraph);
        CalculateDoorsAndHallwaysFromAllConnections(roomGraph);
        BuildHallwayCellsFromStoredConnections(roomInteriorCells);
    }

    void CalculateDoorsAndHallwaysFromAllConnections(List<RoomNode> roomGraph)
    {
        //reset stored hallway data before building new connections.
        hallwayConnections.Clear();
        outsideDoorwayCells.Clear();

        //stores room connections already considered so hallway is only added once per edge
        HashSet<string> seenConnections = new HashSet<string>();

        for (int i = 0; i < roomGraph.Count; i++)
        {
            RoomNode room = roomGraph[i];

            //convert each room graph edge into hallway connection.
            for (int j = 0; j < room.neighbors.Count; j++)
            {
                AddHallwayConnection(room, room.neighbors[j].target, seenConnections);
            }
        }
    }


    void AddHallwayConnection(RoomNode a, RoomNode b, HashSet<string> seenConnections)
    {
        int low = Mathf.Min(a.id, b.id);
        int high = Mathf.Max(a.id, b.id);
        string key = low + "_" + high;

        if (seenConnections.Contains(key))
            return;

        seenConnections.Add(key);

        Vector3Int doorA = GetDoorPositionToward(a.bounds, b.center);
        Vector3Int doorB = GetDoorPositionToward(b.bounds, a.center);

        Vector3Int hallStart = GetOutsideDoorTile(doorA, a.bounds, b.center);
        Vector3Int hallEnd = GetOutsideDoorTile(doorB, b.bounds, a.center);

        List<Vector3Int> wideDoorsA = GetWideDoorTiles(a.bounds, doorA, b.center);
        List<Vector3Int> wideDoorsB = GetWideDoorTiles(b.bounds, doorB, a.center);

        for (int d = 0; d < wideDoorsA.Count; d++)
            GetDoorPositions(a.id).Add(wideDoorsA[d]);

        for (int d = 0; d < wideDoorsB.Count; d++)
            GetDoorPositions(b.id).Add(wideDoorsB[d]);

        List<Vector3Int> outsideDoorwayA = GetOutsideWideDoorTiles(a.bounds, doorA, b.center);
        List<Vector3Int> outsideDoorwayB = GetOutsideWideDoorTiles(b.bounds, doorB, a.center);

        for (int d = 0; d < outsideDoorwayA.Count; d++)
            outsideDoorwayCells.Add(outsideDoorwayA[d]);

        for (int d = 0; d < outsideDoorwayB.Count; d++)
            outsideDoorwayCells.Add(outsideDoorwayB[d]);

        HallwayConnection connection = new HallwayConnection();
        connection.roomA = a;
        connection.roomB = b;
        connection.doorA = doorA;
        connection.doorB = doorB;
        connection.hallStart = hallStart;
        connection.hallEnd = hallEnd;
        connection.outsideDoorwayA.AddRange(outsideDoorwayA);
        connection.outsideDoorwayB.AddRange(outsideDoorwayB);

        hallwayConnections.Add(connection);
    }

    Vector3Int GetDoorPositionToward(Bounds room, Vector3 targetCenter)
    {
        int xMin = Mathf.RoundToInt(room.min.x);
        int xMax = Mathf.RoundToInt(room.max.x);
        int zMin = Mathf.RoundToInt(room.min.z);
        int zMax = Mathf.RoundToInt(room.max.z);

        int centerX = Mathf.RoundToInt(room.center.x);
        int centerZ = Mathf.RoundToInt(room.center.z);

        Vector3 dir = (targetCenter - room.center).normalized;

        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.z))
        {
            if (dir.x > 0)
                return new Vector3Int(xMax, Mathf.RoundToInt(room.center.y), centerZ);
            else
                return new Vector3Int(xMin, Mathf.RoundToInt(room.center.y), centerZ);
        }
        else
        {
            if (dir.z > 0)
                return new Vector3Int(centerX, Mathf.RoundToInt(room.center.y), zMax);
            else
                return new Vector3Int(centerX, Mathf.RoundToInt(room.center.y), zMin);
        }
    }

    Vector3Int GetOutsideDoorTile(Vector3Int door, Bounds room, Vector3 targetCenter)
    {
        Vector3 dir = (targetCenter - room.center).normalized;

        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.z))
        {
            if (dir.x > 0)
                return new Vector3Int(door.x + 1, door.y, door.z);
            else
                return new Vector3Int(door.x - 1, door.y, door.z);
        }
        else
        {
            if (dir.z > 0)
                return new Vector3Int(door.x, door.y, door.z + 1);
            else
                return new Vector3Int(door.x, door.y, door.z - 1);
        }
    }

    List<Vector3Int> GetWideDoorTiles(Bounds room, Vector3Int centerDoor, Vector3 targetCenter)
    {
        List<Vector3Int> result = new List<Vector3Int>();

        int xMin = Mathf.RoundToInt(room.min.x);
        int xMax = Mathf.RoundToInt(room.max.x);
        int zMin = Mathf.RoundToInt(room.min.z);
        int zMax = Mathf.RoundToInt(room.max.z);

        Vector3 dir = (targetCenter - room.center).normalized;

        result.Add(centerDoor);

        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.z))
        {
            Vector3Int up = new Vector3Int(centerDoor.x, centerDoor.y, centerDoor.z + 1);
            Vector3Int down = new Vector3Int(centerDoor.x, centerDoor.y, centerDoor.z - 1);

            if (up.z <= zMax) result.Add(up);
            if (down.z >= zMin) result.Add(down);
        }
        else
        {
            Vector3Int right = new Vector3Int(centerDoor.x + 1, centerDoor.y, centerDoor.z);
            Vector3Int left = new Vector3Int(centerDoor.x - 1, centerDoor.y, centerDoor.z);

            if (right.x <= xMax) result.Add(right);
            if (left.x >= xMin) result.Add(left);
        }

        return result;
    }

    List<Vector3Int> GetOutsideWideDoorTiles(Bounds room, Vector3Int centerDoor, Vector3 targetCenter)
    {
        List<Vector3Int> result = new List<Vector3Int>();

        Vector3Int outsideCenter = GetOutsideDoorTile(centerDoor, room, targetCenter);
        result.Add(outsideCenter);

        Vector3 dir = (targetCenter - room.center).normalized;

        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.z))
        {
            result.Add(new Vector3Int(outsideCenter.x, outsideCenter.y, outsideCenter.z + 1));
            result.Add(new Vector3Int(outsideCenter.x, outsideCenter.y, outsideCenter.z - 1));
        }
        else
        {
            result.Add(new Vector3Int(outsideCenter.x + 1, outsideCenter.y, outsideCenter.z));
            result.Add(new Vector3Int(outsideCenter.x - 1, outsideCenter.y, outsideCenter.z));
        }

        return result;
    }

    void BuildHallwayCellsFromStoredConnections(HashSet<Vector3Int> roomInteriorCells)
    {
        hallwayFloorCells.Clear();
        hallwayWallCells.Clear();

        if (hallwayConnections == null || hallwayConnections.Count == 0)
            return;

        for (int i = 0; i < hallwayConnections.Count; i++)
        {
            HallwayConnection c = hallwayConnections[i];
            AddWideLHallwayFloorCells(c.hallStart, c.hallEnd);
        }

        foreach (Vector3Int outsideDoorwayCell in outsideDoorwayCells)
        {
            hallwayFloorCells.Add(outsideDoorwayCell);
        }

        BuildHallwayWallsFromFloor(roomInteriorCells);
    }

    void AddWideLHallwayFloorCells(Vector3Int a, Vector3Int b)
    {
        Vector3Int corner = new Vector3Int(b.x, a.y, a.z);

        AddWideStraightHallwayFloorCells(a, corner);
        AddWideStraightHallwayFloorCells(corner, b);
    }

    void AddWideStraightHallwayFloorCells(Vector3Int from, Vector3Int to)
    {
        int x1 = from.x;
        int z1 = from.z;
        int x2 = to.x;
        int z2 = to.z;

        if (x1 != x2)
        {
            int step = x1 < x2 ? 1 : -1;

            for (int x = x1; x != x2 + step; x += step)
            {
                hallwayFloorCells.Add(new Vector3Int(x, from.y, z1 - 1));
                hallwayFloorCells.Add(new Vector3Int(x, from.y, z1));
                hallwayFloorCells.Add(new Vector3Int(x, from.y, z1 + 1));
            }
        }
        else if (z1 != z2)
        {
            int step = z1 < z2 ? 1 : -1;

            for (int z = z1; z != z2 + step; z += step)
            {
                hallwayFloorCells.Add(new Vector3Int(x1 - 1, from.y, z));
                hallwayFloorCells.Add(new Vector3Int(x1, from.y, z));
                hallwayFloorCells.Add(new Vector3Int(x1 + 1, from.y, z));
            }
        }
        else
        {
            hallwayFloorCells.Add(from);
        }
    }

    void BuildHallwayWallsFromFloor(HashSet<Vector3Int> roomInteriorCells)
    {
        Vector3Int[] directions = new Vector3Int[]
        {
            new Vector3Int( 1, 0, 0),
            new Vector3Int(-1, 0, 0),
            new Vector3Int( 0, 0, 1),
            new Vector3Int( 0, 0,-1)
        };

        foreach (Vector3Int floorCell in hallwayFloorCells)
        {
            for (int i = 0; i < directions.Length; i++)
            {
                Vector3Int candidate = floorCell + directions[i];

                if (hallwayFloorCells.Contains(candidate))
                    continue;

                if (roomInteriorCells.Contains(candidate))
                    continue;

                if (IsDoorwayCell(candidate))
                    continue;

                if (outsideDoorwayCells.Contains(candidate))
                    continue;

                hallwayWallCells.Add(candidate);
            }
        }
    }

    bool IsDoorwayCell(Vector3Int cell)
    {
        foreach (var kvp in roomDoorPositions)
        {
            if (kvp.Value.Contains(cell))
                return true;
        }

        return false;
    }
}
