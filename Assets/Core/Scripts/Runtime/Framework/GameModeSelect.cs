
using UnityEngine;

public class GameModeSelect : MonoBehaviour
{
    public void SelectExplore()
    {
        GameModeSelection.SelectedMode =
            GameModeSelection.Mode.Explore;

        Debug.Log("[GameMode] Selected Explore");
    }

    public void SelectChase()
    {
        GameModeSelection.SelectedMode =
            GameModeSelection.Mode.Chase;

        Debug.Log("[GameMode] Selected Chase");
    }
}
