using UnityEngine;

[System.Serializable]
public class GridCell
{
    public Vector2Int GridPosition;
    public CellState State;
    public GameObject Occupant;
    public bool IsWalkable;

    public GridCell(Vector2Int position)
    {
        GridPosition = position;
        State = CellState.Empty;
        Occupant = null;
        IsWalkable = true;
    }
}

public enum CellState { Empty, Occupied, Blocked, Reserved }