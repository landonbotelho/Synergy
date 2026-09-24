using static BSPSpawner;

public class RoomEdge
{
    public RoomNode target;
    public float cost;

    public RoomEdge(RoomNode target, float cost)
    {
        this.target = target;
        this.cost = cost;
    }
}