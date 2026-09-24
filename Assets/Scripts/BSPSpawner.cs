using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;

public class BSPSpawner : MonoBehaviour
{
    [Header("BSP Settings")]
    public int maxDepth = 6;

    [Header("Room Spacing")]
    [Tooltip("How much to shrink each leaf into an actual room. Bigger = more hallway space.")]
    public float roomPadding = 2f;

    [Header("Prefabs")]
    public GameObject floorPrefab;
    public GameObject wallPrefab;
    public GameObject hallwayPrefab;

    [Header("Branch Loot Settings")]
    [Tooltip("Rooms this many steps away from the main path count as rare side branches.")]
    [Min(2)]
    public int rareBranchDepthThreshold = 2;

    [Header("Placement Settings")]
    public float hallwayY = 0f;
    public float wallY = 1f;
    public bool buildHallways = true;
    [Header("Room Shape Control")]
    public float minLeafWidth = 10f;
    public float minLeafHeight = 10f;
    public float maxAspectRatio = 1.8f;
    public int splitTries = 10;

    private BSPNode root;

    private List<Bounds> leafRooms = new List<Bounds>();
    private List<RoomNode> roomGraph = new List<RoomNode>();

    public RoomNode startRoom;
    public RoomNode endRoom;
    public List<RoomNode> mainPath = new List<RoomNode>();

    private HashSet<int> mainPathRoomIds = new HashSet<int>();
    private HashSet<int> branchRoomIds = new HashSet<int>();
    private HashSet<int> rareBranchRoomIds = new HashSet<int>();
    private Dictionary<int, int> branchDepthByRoomId = new Dictionary<int, int>();
    private Dictionary<int, float> branchDistanceByRoomId = new Dictionary<int, float>();
    private Dictionary<int, int> nearestMainPathRoomByRoomId = new Dictionary<int, int>();
    private int farthestBranchRoomId = -1;

    private HashSet<Vector3Int> roomInteriorCells = new HashSet<Vector3Int>();
    private HallwayBuilder hallwayBuilder = new HallwayBuilder();
    private NavMeshSurface navMeshSurface;

    void Start()
    {
        BoxCollider area = GetComponent<BoxCollider>();
        if (area == null)
        {
            Debug.LogError("BSPSpawner requires a BoxCollider on the same GameObject.");
            return;
        }

        navMeshSurface = GetComponent<NavMeshSurface>();

        do
        {
            root = BuildTree(area.bounds, 0);
        }
        while (root.IsLeaf);

        leafRooms.Clear();
        CollectLeavesAsRooms(root, leafRooms);

        BuildRoomGraph();
        BuildRoomInteriorCells();

        if (buildHallways)
        {
            ConnectSiblingSubtrees(root);

            ChooseStartAndEndRooms();
            mainPath = DungeonPathfinder.DjikstrasShortestPath(startRoom, endRoom, roomGraph);

            CacheMainPathIds();
            AnalyzeBranchRooms();

            hallwayBuilder.BuildHallways(roomGraph, roomInteriorCells);
        }

        SpawnRoomsWithWalls();

        if (buildHallways)
        {
            SpawnHallwayCells();
        }

        RebuildNavMesh();

        DebugLogPath();
    }

    BSPNode BuildTree(Bounds bounds, int depth)
    {
        BSPNode node = new BSPNode(bounds);

        if (depth >= maxDepth)
            return node;

        bool canSplitVertically = bounds.size.x >= minLeafWidth * 2f;
        bool canSplitHorizontally = bounds.size.z >= minLeafHeight * 2f;

        if (!canSplitVertically && !canSplitHorizontally)
            return node;

        bool splitVertical;
        if (canSplitVertically && canSplitHorizontally)
        {
            splitVertical = bounds.size.x >= bounds.size.z;
        }
        else
        {
            splitVertical = canSplitVertically;
        }

        bool success = false;
        Bounds a = bounds;
        Bounds b = bounds;

        for (int attempt = 0; attempt < splitTries; attempt++)
        {
            if (splitVertical)
            {
                float split = Random.Range(minLeafWidth, bounds.size.x - minLeafWidth);

                a = bounds;
                b = bounds;

                a.size = new Vector3(split, bounds.size.y, bounds.size.z);
                b.size = new Vector3(bounds.size.x - split, bounds.size.y, bounds.size.z);

                a.center = bounds.center + Vector3.left * (bounds.size.x - split) / 2f;
                b.center = bounds.center + Vector3.right * split / 2f;
            }
            else
            {
                float split = Random.Range(minLeafHeight, bounds.size.z - minLeafHeight);

                a = bounds;
                b = bounds;

                a.size = new Vector3(bounds.size.x, bounds.size.y, split);
                b.size = new Vector3(bounds.size.x, bounds.size.y, bounds.size.z - split);
                a.center = bounds.center + Vector3.back * (bounds.size.z - split) / 2f;
                b.center = bounds.center + Vector3.forward * split / 2f;
            }

            if (IsGoodLeafShape(a) && IsGoodLeafShape(b))
            {
                success = true;
                break;
            }
        }

        if (!success)
        {
            return node;
        }
        node.splitVertical = splitVertical;
        node.left = BuildTree(a, depth + 1);
        node.right = BuildTree(b, depth + 1);
        return node;
    }


    bool IsGoodLeafShape(Bounds b)
    {
        float width = b.size.x;
        float height = b.size.z;

        if (width < minLeafWidth || height < minLeafHeight)
            return false;

        float aspect = Mathf.Max(width / height, height / width);
        return aspect <= maxAspectRatio;
    }

    void CollectLeavesAsRooms(BSPNode node, List<Bounds> rooms)
    {
        if (node == null) return;

        if (node.IsLeaf)
        {
            Bounds room = ShrinkBounds(node.bounds, roomPadding);

            if (room.size.x >= 3f && room.size.z >= 3f)
            {
                rooms.Add(room);
            }

            return;
        }

        CollectLeavesAsRooms(node.left, rooms);
        CollectLeavesAsRooms(node.right, rooms);
    }

    Bounds ShrinkBounds(Bounds original, float padding)
    {
        Bounds room = original;
        room.size = new Vector3(
            Mathf.Max(1f, original.size.x - padding * 2f),
            original.size.y,
            Mathf.Max(1f, original.size.z - padding * 2f)
        );
        return room;
    }

    void BuildRoomGraph()
    {
        roomGraph.Clear();
        mainPathRoomIds.Clear();
        branchRoomIds.Clear();
        rareBranchRoomIds.Clear();
        branchDepthByRoomId.Clear();
        branchDistanceByRoomId.Clear();

        for (int i = 0; i < leafRooms.Count; i++)
        {
            roomGraph.Add(new RoomNode(i, leafRooms[i]));
        }

    }

    void BuildRoomInteriorCells()
    {
        roomInteriorCells.Clear();

        for (int i = 0; i < roomGraph.Count; i++)
        {
            Bounds b = roomGraph[i].bounds;

            int xMin = Mathf.RoundToInt(b.min.x);
            int xMax = Mathf.RoundToInt(b.max.x);
            int zMin = Mathf.RoundToInt(b.min.z);
            int zMax = Mathf.RoundToInt(b.max.z);
            int y = Mathf.RoundToInt(b.center.y);

            for (int x = xMin; x <= xMax; x++)
            {
                for (int z = zMin; z <= zMax; z++)
                {
                    roomInteriorCells.Add(new Vector3Int(x, y, z));
                }
            }
        }
    }

    void ConnectSiblingSubtrees(BSPNode node)
    {
        if (node == null || node.IsLeaf) return;

        ConnectSiblingSubtrees(node.left);
        ConnectSiblingSubtrees(node.right);

        RoomNode leftRoom;
        RoomNode rightRoom;
        FindClosestRoomPair(node.left, node.right, out leftRoom, out rightRoom);

        if (leftRoom != null && rightRoom != null && leftRoom != rightRoom)
        {
            AddUndirectedEdge(leftRoom, rightRoom);
        }
    }

    void FindClosestRoomPair(BSPNode leftSubtree, BSPNode rightSubtree, out RoomNode bestLeft, out RoomNode bestRight)
    {
        List<RoomNode> leftRooms = GetLeafRoomNodes(leftSubtree);
        List<RoomNode> rightRooms = GetLeafRoomNodes(rightSubtree);

        float bestDist = float.MaxValue;
        bestLeft = null;
        bestRight = null;

        for (int i = 0; i < leftRooms.Count; i++)
        {
            for (int j = 0; j < rightRooms.Count; j++)
            {
                float dist = Vector3.Distance(leftRooms[i].center, rightRooms[j].center);

                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestLeft = leftRooms[i];
                    bestRight = rightRooms[j];
                }
            }
        }
    }

    List<RoomNode> GetLeafRoomNodes(BSPNode subtreeRoot)
    {
        List<Bounds> subtreeBounds = new List<Bounds>();
        CollectLeavesAsRooms(subtreeRoot, subtreeBounds);

        List<RoomNode> result = new List<RoomNode>();

        for (int i = 0; i < subtreeBounds.Count; i++)
        {
            RoomNode node = FindRoomNodeByBounds(subtreeBounds[i]);
            if (node != null)
                result.Add(node);
        }

        return result;
    }

    RoomNode FindRoomNodeByBounds(Bounds bounds)
    {
        for (int i = 0; i < roomGraph.Count; i++)
        {
            if (ApproximatelySameBounds(roomGraph[i].bounds, bounds))
                return roomGraph[i];
        }

        return null;
    }

    bool ApproximatelySameBounds(Bounds a, Bounds b)
    {
        return Vector3.Distance(a.center, b.center) < 0.01f &&
               Vector3.Distance(a.size, b.size) < 0.01f;
    }

    void AddUndirectedEdge(RoomNode a, RoomNode b)
    {
        //cost based on distance
        float cost = Vector3.Distance(a.center, b.center);

        //if 2 rooms  are not currently stored as neighbors add edge
        if (!HasNeighbor(a, b))
            a.neighbors.Add(new RoomEdge(b, cost));

        //similarly, check and add edge the other way
        if (!HasNeighbor(b, a))
            b.neighbors.Add(new RoomEdge(a, cost));
    }

    bool HasNeighbor(RoomNode a, RoomNode b)
    {
        for (int i = 0; i < a.neighbors.Count; i++)
        {
            if (a.neighbors[i].target == b)
                return true;
        }
        return false;
    }

    void ChooseStartAndEndRooms()
    {
        startRoom = DungeonPathfinder.ChooseLeftmostRoom(roomGraph);
        endRoom = DungeonPathfinder.ChooseFarthestRoom(startRoom, roomGraph);
    }

    void CacheMainPathIds()
    {
        mainPathRoomIds.Clear();

        for (int i = 0; i < mainPath.Count; i++)
        {
            mainPathRoomIds.Add(mainPath[i].id);
        }
    }

    void AnalyzeBranchRooms()
    {
        branchRoomIds.Clear();
        rareBranchRoomIds.Clear();
        branchDepthByRoomId.Clear();
        branchDistanceByRoomId.Clear();
        nearestMainPathRoomByRoomId.Clear();
        farthestBranchRoomId = -1;

        if (mainPath == null || mainPath.Count == 0)
            return;

        DungeonPathfinder.DijkstraResult branchAnalysis =
            DungeonPathfinder.RunDjikstrasFromMain(mainPath, roomGraph);

        for (int i = 0; i < roomGraph.Count; i++)
        {
            RoomNode room = roomGraph[i];

            if (mainPathRoomIds.Contains(room.id))
            {
                branchDepthByRoomId[room.id] = 0;
                branchDistanceByRoomId[room.id] = 0f;
                nearestMainPathRoomByRoomId[room.id] = room.id;
                continue;
            }

            if (!branchAnalysis.hopCounts.ContainsKey(room) || branchAnalysis.hopCounts[room] == int.MaxValue)
                continue;

            int branchDepth = branchAnalysis.hopCounts[room];
            float branchDistance = branchAnalysis.distances.ContainsKey(room)
                ? branchAnalysis.distances[room]
                : float.MaxValue;

            branchRoomIds.Add(room.id);
            branchDepthByRoomId[room.id] = branchDepth;
            branchDistanceByRoomId[room.id] = branchDistance;
            nearestMainPathRoomByRoomId[room.id] = branchAnalysis.sourceMap.ContainsKey(room) && branchAnalysis.sourceMap[room] != null
                ? branchAnalysis.sourceMap[room].id
                : -1;

            if (farthestBranchRoomId < 0 || IsFartherBranchRoom(room.id, farthestBranchRoomId))
            {
                farthestBranchRoomId = room.id;
            }

            if (branchDepth >= rareBranchDepthThreshold)
            {
                rareBranchRoomIds.Add(room.id);
            }
        }
    }

    bool IsFartherBranchRoom(int candidateRoomId, int currentBestRoomId)
    {
        int candidateDepth = GetBranchDepth(candidateRoomId);
        int currentDepth = GetBranchDepth(currentBestRoomId);

        if (candidateDepth != currentDepth)
        {
            return candidateDepth > currentDepth;
        }

        float candidateDistance = GetBranchDistance(candidateRoomId);
        float currentDistance = GetBranchDistance(currentBestRoomId);
        return candidateDistance > currentDistance;
    }

    void SpawnRoomsWithWalls()
    {
        for (int i = 0; i < roomGraph.Count; i++)
        {
            SpawnSingleRoom(roomGraph[i]);
        }
    }

    void SpawnSingleRoom(RoomNode room)
    {
        Bounds b = room.bounds;

        int xMin = Mathf.RoundToInt(b.min.x);
        int xMax = Mathf.RoundToInt(b.max.x);
        int zMin = Mathf.RoundToInt(b.min.z);
        int zMax = Mathf.RoundToInt(b.max.z);

        float floorY = b.center.y;

        HashSet<Vector3Int> doors = hallwayBuilder.GetDoorPositions(room.id);

        for (int x = xMin; x <= xMax; x++)
        {
            for (int z = zMin; z <= zMax; z++)
            {
                if (floorPrefab != null)
                {
                    Vector3 floorPos = new Vector3(x, floorY, z);
                    Instantiate(floorPrefab, floorPos, Quaternion.identity);
                }

                bool isPerimeter = (x == xMin || x == xMax || z == zMin || z == zMax);

                if (isPerimeter && wallPrefab != null)
                {
                    Vector3Int wallCell = new Vector3Int(x, Mathf.RoundToInt(floorY), z);

                    if (!doors.Contains(wallCell))
                    {
                        Vector3 wallPos = new Vector3(x, floorY + wallY, z);
                        Instantiate(wallPrefab, wallPos, Quaternion.identity);
                    }
                }
            }
        }
    }

    void SpawnHallwayCells()
    {
        GameObject floorToUse = hallwayPrefab != null ? hallwayPrefab : floorPrefab;

        if (floorToUse != null)
        {
            foreach (Vector3Int cell in hallwayBuilder.HallwayFloorCells)
            {
                Vector3 pos = new Vector3(cell.x, hallwayY, cell.z);
                Instantiate(floorToUse, pos, Quaternion.identity);
            }
        }

        if (wallPrefab != null)
        {
            foreach (Vector3Int cell in hallwayBuilder.HallwayWallCells)
            {
                Vector3 pos = new Vector3(cell.x, hallwayY + wallY, cell.z);
                Instantiate(wallPrefab, pos, Quaternion.identity);
            }
        }
    }

    void DebugLogPath()
    {
        if (startRoom == null || endRoom == null)
        {
            Debug.LogWarning("Start or end room missing.");
            return;
        }

        Debug.Log("Start Room ID: " + startRoom.id);
        Debug.Log("End Room ID: " + endRoom.id);
        Debug.Log("Branch Room Count: " + branchRoomIds.Count);
        Debug.Log("Rare Branch Room Count: " + rareBranchRoomIds.Count);

        string pathText = "Main Path: ";
        for (int i = 0; i < mainPath.Count; i++)
        {
            pathText += mainPath[i].id;
            if (i < mainPath.Count - 1)
                pathText += " -> ";
        }

        Debug.Log(pathText);

        foreach (int roomId in branchRoomIds)
        {
            int branchDepth = branchDepthByRoomId.ContainsKey(roomId) ? branchDepthByRoomId[roomId] : -1;
            float branchDistance = branchDistanceByRoomId.ContainsKey(roomId) ? branchDistanceByRoomId[roomId] : -1f;
            Debug.Log("Branch Room " + roomId + " depth=" + branchDepth + " dijkstraDistance=" + branchDistance);
        }
    }

    public List<Bounds> GetLeafRooms()
    {
        return new List<Bounds>(leafRooms);
    }

    public int GetRoomIdForBounds(Bounds bounds)
    {
        RoomNode room = FindRoomNodeByBounds(bounds);
        return room != null ? room.id : -1;
    }

    public int GetRoomIdForPosition(Vector3 position)
    {
        for (int i = 0; i < roomGraph.Count; i++)
        {
            Bounds bounds = roomGraph[i].bounds;
            if (position.x >= bounds.min.x
                && position.x <= bounds.max.x
                && position.z >= bounds.min.z
                && position.z <= bounds.max.z)
            {
                return roomGraph[i].id;
            }
        }
        return -1;
    }

    public Bounds GetRoomBounds(int roomId)
    {
        if (roomId < 0 || roomId >= roomGraph.Count)
        {
            return new Bounds();
        }

        return roomGraph[roomId].bounds;
    }

    public int GetMainPathIndex(int roomId)
    {
        for (int i = 0; i < mainPath.Count; i++)
        {
            if (mainPath[i] != null && mainPath[i].id == roomId)
            {
                return i;
            }
        }

        return -1;
    }

    public int GetMainPathRoomCount()
    {
        return mainPath != null ? mainPath.Count : 0;
    }

    public int GetEnemyTierForRoom(int roomId)
    {
        if (IsMainPathRoom(roomId))
        {
            int mainPathIndex = GetMainPathIndex(roomId);
            int mainPathCount = GetMainPathRoomCount();

            if (mainPathIndex < 0 || mainPathCount <= 0)
            {
                return 1;
            }

            int chunkSize = Mathf.CeilToInt(mainPathCount / 3f);
            return Mathf.Clamp(mainPathIndex / Mathf.Max(1, chunkSize) + 1, 1, 3);
        }

        int branchDepth = GetBranchDepth(roomId);
        if (branchDepth <= 1)
        {
            return 1;
        }

        if (branchDepth == 2)
        {
            return 2;
        }

        return 3;
    }

    public List<HallwayConnection> GetHallwayConnections()
    {
        return hallwayBuilder.GetHallwayConnections();
    }

    public bool IsMainPathRoom(int roomId)
    {
        return mainPathRoomIds.Contains(roomId);
    }

    public bool IsBranchRoom(int roomId)
    {
        return branchRoomIds.Contains(roomId);
    }

    public bool IsRareBranchRoom(int roomId)
    {
        return rareBranchRoomIds.Contains(roomId);
    }

    public int GetBranchDepth(int roomId)
    {
        return branchDepthByRoomId.ContainsKey(roomId) ? branchDepthByRoomId[roomId] : -1;
    }

    public float GetBranchDistance(int roomId)
    {
        return branchDistanceByRoomId.ContainsKey(roomId) ? branchDistanceByRoomId[roomId] : -1f;
    }

    public int GetFarthestBranchRoomId()
    {
        return farthestBranchRoomId;
    }

    public int GetNearestMainPathRoomId(int roomId)
    {
        return nearestMainPathRoomByRoomId.ContainsKey(roomId) ? nearestMainPathRoomByRoomId[roomId] : -1;
    }

    void RebuildNavMesh()
    {
        if (navMeshSurface == null)
            return;

        navMeshSurface.BuildNavMesh();
    }
}
