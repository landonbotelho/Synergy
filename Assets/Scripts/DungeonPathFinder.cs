using System.Collections.Generic;
using UnityEngine;

public static class DungeonPathfinder
{
    public class DijkstraResult
    {
        public Dictionary<RoomNode, float> distances = new Dictionary<RoomNode, float>();
        public Dictionary<RoomNode, RoomNode> previous = new Dictionary<RoomNode, RoomNode>();
        public Dictionary<RoomNode, int> hopCounts = new Dictionary<RoomNode, int>();
        public Dictionary<RoomNode, RoomNode> sourceMap = new Dictionary<RoomNode, RoomNode>();
    }

    public static RoomNode ChooseLeftmostRoom(List<RoomNode> roomGraph)
    {
        if (roomGraph == null || roomGraph.Count == 0)
            return null;

        RoomNode startRoom = roomGraph[0];

        for (int i = 1; i < roomGraph.Count; i++)
        {
            if (roomGraph[i].center.x < startRoom.center.x)
                startRoom = roomGraph[i];
        }

        return startRoom;
    }

    public static RoomNode ChooseFarthestRoom(RoomNode startRoom, List<RoomNode> roomGraph)
    {
        if (startRoom == null || roomGraph == null || roomGraph.Count == 0)
            return null;

        Dictionary<RoomNode, float> distances = RunDijkstraDistances(startRoom, roomGraph);

        float farthest = -1f;
        RoomNode endRoom = startRoom;

        foreach (var pair in distances)
        {
            if (pair.Value < float.MaxValue && pair.Value > farthest)
            {
                farthest = pair.Value;
                endRoom = pair.Key;
            }
        }

        return endRoom;
    }





    public static List<RoomNode> DjikstrasShortestPath(RoomNode source, RoomNode target, List<RoomNode> roomGraph)
    {
        List<RoomNode> path = new List<RoomNode>();
        //check if the start, end, or graph data is missing.
        if (source == null || target == null || roomGraph == null || roomGraph.Count == 0)
            return path;

        //stores the cheapest known cost from the source to each room.
        Dictionary<RoomNode, float> dist = new Dictionary<RoomNode, float>();
        //visited tracks rooms that have already had their shortest path finalized.
        Dictionary<RoomNode, bool> visited = new Dictionary<RoomNode, bool>();
        //stores the room used to reach each room through the cheapest known path.
        Dictionary<RoomNode, RoomNode> previous = new Dictionary<RoomNode, RoomNode>();

        //set default starting values to every room
        for (int i = 0; i < roomGraph.Count; i++)
        {
            dist[roomGraph[i]] = float.MaxValue;
            visited[roomGraph[i]] = false;
            previous[roomGraph[i]] = null;
        }
        //starting room starting cost set to 0
        dist[source] = 0f;
        SortedDictionary<float, Queue<RoomNode>> queue = new SortedDictionary<float, Queue<RoomNode>>();
        EnqueueRoom(queue, source, 0f);

        while (queue.Count > 0)
        {
            //queue always gives the unvisited room with the smallest known distance
            RoomNode current = DequeueRoom(queue);
            if (visited[current]) continue;
            if (current == target)
                break;
            visited[current] = true;

            //see if the current room has a cheaper path to any neighboring rooms
            for (int n = 0; n < current.neighbors.Count; n++)
            {
                RoomEdge edge = current.neighbors[n];
                RoomNode neighbor = edge.target;

                if (visited[neighbor]) continue;
                else
                {
                    float newCost = dist[current] + edge.cost;
                    //compares cost of chepest known room cost to current room cost
                    //if new cheapest is found, it is stored
                    if (newCost < dist[neighbor])
                    {
                        dist[neighbor] = newCost;
                        previous[neighbor] = current;
                        EnqueueRoom(queue, neighbor, newCost);
                    }
                }
            }
        }
        //verify that djikstras algorithm was able to get from start room to end room
        if (previous[target] == null && target != source)
        {
            Debug.LogWarning("DA could find the end room!!!!! - DungeonPathFinder.cs ~ ln 113");
            return path;
        }
        //trace back the path from found end room to start room
        //then flip it
        RoomNode step = target;
        while (step != null)
        {
            path.Add(step);
            step = previous[step];
        }
        path.Reverse();
        return path;
    }









    public static Dictionary<RoomNode, float> RunDijkstraDistances(RoomNode source, List<RoomNode> roomGraph)
    {
        // Reuse the multi-source Dijkstra method with only one source room.
        return RunDjikstrasFromMain(new List<RoomNode> { source }, roomGraph).distances;
    }

    public static DijkstraResult RunDjikstrasFromMain(List<RoomNode> sources, List<RoomNode> roomGraph)
    {
        DijkstraResult result = new DijkstraResult();

        //return if no main path or associated room graph is found
        if (sources == null || sources.Count == 0 || roomGraph == null || roomGraph.Count == 0)
            return result;

        //stores the cheapest weighted distance from the main path to each room.
        Dictionary<RoomNode, float> dist = new Dictionary<RoomNode, float>();
        //tracks rooms that have already had their cheapest path finalized.
        Dictionary<RoomNode, bool> visited = new Dictionary<RoomNode, bool>();
        //stores which room was used to reach each room through the cheapest route.
        Dictionary<RoomNode, RoomNode> previous = new Dictionary<RoomNode, RoomNode>();
        // Hop count stores how many room connections away each room is from the main path.
        Dictionary<RoomNode, int> hopCounts = new Dictionary<RoomNode, int>();
        // Source map stores which main path room each side room is closest to.
        Dictionary<RoomNode, RoomNode> sourceMap = new Dictionary<RoomNode, RoomNode>();

        //set default starting vaues to every room
        for (int i = 0; i < roomGraph.Count; i++)
        {
            dist[roomGraph[i]] = float.MaxValue;
            visited[roomGraph[i]] = false;
            previous[roomGraph[i]] = null;
            hopCounts[roomGraph[i]] = int.MaxValue; //hop count tracks num of rooms from main path
            sourceMap[roomGraph[i]] = null;     //source map tracks which main path room the side room is connected to based on weights
        }

        //every room in the main path is treated as a source room at the same time.
        //making algorithm spread outward from the whole main path instead of one room.
        SortedDictionary<float, Queue<RoomNode>> queue = new SortedDictionary<float, Queue<RoomNode>>();
        for (int i = 0; i < sources.Count; i++)
        {
            RoomNode source = sources[i];
            if (source == null || !dist.ContainsKey(source))
                continue;

            //main path rooms begin at 0 bec they are already on the main path and treated as source
            dist[source] = 0f;
            hopCounts[source] = 0;
            sourceMap[source] = source;
            EnqueueRoom(queue, source, 0f);
        }

        while (queue.Count > 0)
        {
            //the queue picks the closest unvisited room and spreads outward from it.
            RoomNode current = DequeueRoom(queue);
            if (visited[current]) continue;

            visited[current] = true;

            // Check every connected room and see if this route reaches it more efficiently.
            for (int n = 0; n < current.neighbors.Count; n++)
            {
                RoomEdge edge = current.neighbors[n];
                RoomNode neighbor = edge.target;

                if (visited[neighbor]) continue;

                float newCost = dist[current] + edge.cost;
                int newHopCount = hopCounts[current] == int.MaxValue ? int.MaxValue : hopCounts[current] + 1;

                // Update the side room if this path is cheaper.
                // If the cost ties, prefer the route with fewer room-to-room hops.
                if (newCost < dist[neighbor] ||
                    (Mathf.Approximately(newCost, dist[neighbor]) && newHopCount < hopCounts[neighbor]))
                {
                    dist[neighbor] = newCost;
                    previous[neighbor] = current;
                    hopCounts[neighbor] = newHopCount;
                    // Keep track of which main path room this side room traces back to.
                    sourceMap[neighbor] = sourceMap[current];
                    EnqueueRoom(queue, neighbor, newCost);
                }
            }
        }

        // Return the final branch analysis data to BSPSpawner.
        result.distances = dist;
        result.previous = previous;
        result.hopCounts = hopCounts;
        result.sourceMap = sourceMap;
        return result;
    }

    private static void EnqueueRoom(SortedDictionary<float, Queue<RoomNode>> queue, RoomNode room, float cost)
    {
        if (!queue.ContainsKey(cost))
        {
            queue[cost] = new Queue<RoomNode>();
        }

        queue[cost].Enqueue(room);
    }

    private static RoomNode DequeueRoom(SortedDictionary<float, Queue<RoomNode>> queue)
    {
        float bestCost = 0f;
        Queue<RoomNode> rooms = null;

        foreach (var pair in queue)
        {
            bestCost = pair.Key;
            rooms = pair.Value;
            break;
        }

        if (rooms == null)
            return null;

        RoomNode room = rooms.Dequeue();
        if (rooms.Count == 0)
        {
            queue.Remove(bestCost);
        }

        return room;
    }

}
