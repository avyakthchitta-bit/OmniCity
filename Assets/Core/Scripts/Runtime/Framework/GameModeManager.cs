
using UnityEngine;

[DefaultExecutionOrder(1000)]
public class GameModeManager : MonoBehaviour
{
    private void Start()
    {
        NPCBrain[] npcs =
            FindObjectsByType<NPCBrain>();

        NPCBrain.Behavior targetBehavior;

        switch (GameModeSelection.SelectedMode)
        {
            case GameModeSelection.Mode.Explore:
                targetBehavior = NPCBrain.Behavior.Wander;
                break;

            case GameModeSelection.Mode.Chase:
                targetBehavior = NPCBrain.Behavior.Chase;
                break;

            default:
                targetBehavior = NPCBrain.Behavior.Idle;
                break;
        }

        foreach (NPCBrain npc in npcs)
            npc.SetBehavior(targetBehavior);

        Debug.Log(
            $"[GameModeManager] Initialized {npcs.Length} NPCs with behavior: {targetBehavior}"
        );
    }
}
