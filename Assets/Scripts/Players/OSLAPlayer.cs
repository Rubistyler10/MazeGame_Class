using Unity.VisualScripting;
using UnityEngine;

public class OSLAPlayer : Player
{
    ForwardModel forwardModel = new ForwardModel();
    private Heuristic heuristic = null;

    public override Action Think(Observation observation, int budget)
    {
        Action[] list_actions = observation.GetListActions();
        Action best_action = list_actions[0];
        float highest_score = -Mathf.Infinity;
        foreach (Action action in list_actions) {
            Observation clone_obs = observation.Clone();
            forwardModel.Test(clone_obs, action);
            float current_score = heuristic.GetScore(clone_obs);
            Debug.Log("[OSLAPLAYER][THINK] Current action in test: " + action.ToString());
            Debug.Log("[OSLAPLAYER][THINK] Highest score: " + highest_score);
            Debug.Log("[OSLAPLAYER][THINK] Current score: " + current_score);
            if (current_score > highest_score)
            {
                highest_score = current_score;
                best_action = action;
            }
        }

        return best_action;
    }

    public void SetHeuristic(Heuristic new_heuristic)
    {
        heuristic = new_heuristic;
    }

    public override void Reset()
    {
        heuristic = GetComponent<Heuristic>();
        if (heuristic == null) throw new System.Exception("OSLAPlayer requires a Heuristic component to function.");
        Debug.Log("[OSLAPLAYER][RESET] Heuristic component found: " + heuristic.ToString());
    }

    public override string ToString()
    {
        return "OSLAPlayer";
    }
}