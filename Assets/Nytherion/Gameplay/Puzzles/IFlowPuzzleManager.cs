namespace Nytherion.GamePlay.Puzzles
{
    public interface IFlowPuzzleManager
    {
        void OnTileMouseDown(FlowTileController clickedTile);
        void OnTileMouseEnter(FlowTileController enteredTile);
        void OnTileMouseUp(FlowTileController releasedTile);
    }
}