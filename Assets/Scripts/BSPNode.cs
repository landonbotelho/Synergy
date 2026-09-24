using UnityEngine;

public class BSPNode
{
    public Bounds bounds;
    public BSPNode left;
    public BSPNode right;
    public bool splitVertical;

    public BSPNode(Bounds bounds)
    {
        this.bounds = bounds;
    }

    public bool IsLeaf
    {
        get { return left == null && right == null; }
    }
}