using UnityEngine;

public class RandomPlayer : Player
{
    public override void ResetPlayer()
    {
        // No reset needed for RandomPlayer
    }

    public override Action Think(Observation observation, int budget)
    {
        Action[] list_actions = observation.GetListActions();
        Action chosen_action = list_actions[Random.Range(0, list_actions.Length)];
        Debug.Log("[PLAYER][RANDOMPLAYER] RandomPlayer chose action: " + chosen_action.ToString());
        return chosen_action;
    }

    public override string ToString()
    {
        return "RandomPlayer";
    }
}