public class OptimizationResult
{
    private int elementId;
    public int eIdx
    {
        get { return elementId; }
    }

    private int[] assignment;
    public int cIdx
    {
        get { return assignment[0]; }
    }
    public int xIdx
    {
        get { return assignment[1]; }
    }
    public int yIdx
    {
        get { return assignment[2]; }
    }
    public int zIdx
    {
        get { return assignment[3]; }
    }

    public OptimizationResult(int elementId, int cIdx, int xIdx, int yIdx, int zIdx)
    {
        this.elementId = elementId;
        this.assignment = new int[] { cIdx, xIdx, yIdx, zIdx };
    }
}