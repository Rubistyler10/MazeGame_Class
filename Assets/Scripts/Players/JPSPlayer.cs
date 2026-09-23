using System.Collections.Generic;
using UnityEngine;
using Utils;

public class JPSPlayer : Player
{
    private ForwardModel forwardModel = new ForwardModel();
    private Stack<Action> actionStack = new Stack<Action>();
    private Dictionary<int[], int[]> cameFrom = new Dictionary<int[], int[]>();
    private Dictionary<int[], Action> cameFromAction = new Dictionary<int[], Action>();
    private Dictionary<int[], float> costSoFar = new Dictionary<int[], float>();
    private Heuristic heuristic = null;


    void ReconstructPath(int[] start, int[] goal)
    {
        int[] currentPos = goal;

        while (currentPos != start)
        {
            int[] auxPos = currentPos;
            Action straightAction = cameFromAction[auxPos];
            while (cameFromAction[auxPos] == straightAction)
            {
                actionStack.Push(cameFromAction[auxPos]);
                auxPos = cameFrom[auxPos];
            }
        }
    }

    

    bool HasForcedNeighbour(Observation observation, Action action)
    {

        bool HasForcedNeighbour(Observation observation, Action action, Action testAction)
        {
            Observation obsClone = observation.Clone();
            forwardModel.Test(obsClone, testAction);

            if (obsClone.IsInvalidPosition() || obsClone.IsInHole())
            {
                forwardModel.Test(obsClone, action);
                if (!obsClone.IsInvalidPosition() && !obsClone.IsInHole())
                {
                    return true;
                }
            }
            return false;
        }

        Action testAction = new Action();

        //Horizontal movement
        if (action.IsLeft() || action.IsRight())
        {
            // Check wall above and open space past it
            testAction.SetUp();
            if (HasForcedNeighbour(observation, action, testAction))
                return true;
            // Check wall below and open space past it
            testAction.SetDown();
            if (HasForcedNeighbour(observation, action, testAction))
                return true;
        }
        //Vertical movement
        if (action.IsUp() || action.IsDown())
        {
            // Check wall to the left and open space past it
            testAction.SetLeft();
            if (HasForcedNeighbour(observation, action, testAction))
                return true;
            // Check wall to the right and open space past it
            testAction.SetRight();
            if (HasForcedNeighbour(observation, action, testAction))
                return true;
        }
        return false;
    }

    int[] Jump(Observation observation, Action action)
    {
        forwardModel.Test(observation, action);

        // Bounds/Obstacle Check
        if (observation.IsInvalidPosition() || observation.IsInHole())
            return null;
        // Goal Check
        if (observation.ReachedGoal())
            return observation.GetPosition();
        // Forced Neighbour Check
        if (HasForcedNeighbour(observation, action))
            return observation.GetPosition();
        // Continue Jumping in the same direction
        return Jump(observation, action);

    }

    void FindPath(Observation observation)
    {
        PriorityQueue<int[], float> openQueue = new PriorityQueue<int[], float>();
        int[] start = observation.GetPosition();
        openQueue.Enqueue(start, 0);
        cameFrom[start] = null;
        cameFromAction[start] = null;
        costSoFar[start] = 0;

        while (openQueue.Count > 0)
        {
            int[] currentPos = openQueue.Dequeue();

            Observation obsClone = observation.Clone();
            obsClone.SetPosition(currentPos[0], currentPos[1]);

            if (obsClone.ReachedGoal())
            {
                ReconstructPath(start, currentPos);
                return;
            }

            foreach (Action action in obsClone.GetListActions())
            {
                int[] jumpPos = Jump(obsClone.Clone(), action);
                if (jumpPos != null)
                {
                    float tentativeCost = costSoFar[currentPos] + GetDistanceBetweenStraightPositions(currentPos, jumpPos, action);
                    if (!costSoFar.ContainsKey(jumpPos) || tentativeCost < costSoFar[jumpPos])
                    {
                        cameFrom[jumpPos] = currentPos;
                        cameFromAction[jumpPos] = action;
                        costSoFar[jumpPos] = tentativeCost;
                        float priority = tentativeCost + heuristic.GetScore(obsClone);

                        openQueue.Enqueue(jumpPos, priority);
                    }
                }
            }
        }
    }

    int GetDistanceBetweenStraightPositions(int[] pos1, int[] pos2, Action action)
    {
        if (action.IsUp() || action.IsDown())
        {
            return Mathf.Abs(pos1[0] - pos2[0]);
        }
        if (action.IsLeft() || action.IsRight())
        {
            return Mathf.Abs(pos1[1] - pos2[1]);
        }
        return -1;
    }

    public override Action Think(Observation observation, int budget)
    {
        // If it's the first time we are thinking, we need to find a path
        if (actionStack.Count == 0)
        {
            FindPath(observation);
            if (actionStack.Count == 0) return null;
        }
        // If we have actions in the queue, we can return the next action
        if (actionStack.Count > 0)
        {
            return actionStack.Pop();
        }
        else
        {
            // If we have no actions in the queue, we can return a default action
            Action defaultAction = new Action();
            defaultAction.SetUp();
            return defaultAction;
        }
    }

    public override void Reset()
    {
        heuristic = GetComponent<Heuristic>();
        if (heuristic == null) throw new System.Exception("JPSPlayer requires a Heuristic component to function.");
        Debug.Log("[JPSPlayer][RESET] Heuristic component found: " + heuristic.ToString());
    }

}
