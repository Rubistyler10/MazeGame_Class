using System.Collections.Generic;
using UnityEngine;
using Utils;

public class JPSPlayer : Player
{
    private ForwardModel forwardModel = new ForwardModel();
    private Stack<Action> actionStack = new Stack<Action>();
    // Vector2Int compares row and column values, unlike int[] which compares array references by default.
    private Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
    private Dictionary<Vector2Int, Action> cameFromAction = new Dictionary<Vector2Int, Action>();
    private Dictionary<Vector2Int, float> costSoFar = new Dictionary<Vector2Int, float>();
    private Heuristic heuristic = null;

    private Vector2Int ToPositionKey(int[] position)
    {
        return new Vector2Int(position[0], position[1]);
    }
    private int[] ToPositionArray(Vector2Int position)
    {
        return new int[] { position.x, position.y };
    }

    void ReconstructPath(Vector2Int start, Vector2Int goal)
    {
        Debug.Log("[JPSPlayer][ReconstructPath] Reconstructing path from " + start.x + "," + start.y + " to " + goal.x + "," + goal.y);
        Vector2Int currentPos = goal;

        while (currentPos != start)
        {
            int[] currentPositionArray = ToPositionArray(currentPos);
            int[] parentPositionArray = ToPositionArray(cameFrom[currentPos]);
            for (int i = 0; i < GetDistanceBetweenStraightPositions(currentPositionArray, parentPositionArray, cameFromAction[currentPos]); i++)
            {
                actionStack.Push(cameFromAction[currentPos]);
            }
            //actionStack.Push(cameFromAction[currentPos]);
            currentPos = cameFrom[currentPos];
        }

        Debug.Log("[JPSPlayer][ReconstructPath] Path reconstructed with " + actionStack.Count + " actions.");
        int j = 0;
        foreach (Action action in actionStack)
        {
            Debug.Log("[JPSPlayer][ReconstructPath] Action nº " + j + ": " + action.ToString());
            j++;
        }
    }

    bool HasForcedNeighbour(Observation observation, Action action)
    {

        bool CheckForcedNeighbours(Observation observation, Action action, Action testAction)
        {
            /*  
                Check if Position + TestAction is walkable, to know if there is a space to our current sides.
                If not, we don't need to check for forced neighbours in that direction.

                Instead of checking on every wall to our side if there is a space past it,
                we check if there is a space to our side and _then_ we check if there was a wall to our side in the previous cell.
            */
            Observation currentSideObs = observation.Clone();
            forwardModel.Test(currentSideObs, testAction);

            if (currentSideObs.IsInvalidPosition() || currentSideObs.IsInHole())
                return false;

            /* 
                Check if Position + TestAction - Action is walkable
                If there is a path to the side of the current direction, it is then worth checking if there was a wall
                to the side of the original position (currentPos in FindPath).

                > Remember in Algorithms when you where going to far ahead in arcs doing "nodo->siguiente->siguiente->siguiente" for no reason.
            */
            Action reversedAction = new Action(action.action_id);
            reversedAction.Reverse();
            Observation previousSideObs = observation.Clone();
            forwardModel.Test(previousSideObs, reversedAction); // Move back to the previous position
            forwardModel.Test(previousSideObs, testAction); // Move to the side of the previous position

            return previousSideObs.IsInvalidPosition() || previousSideObs.IsInHole();
        }

        Action testAction = new Action();

        //Horizontal movement
        if (action.IsLeft() || action.IsRight())
        {
            // Check wall above and open space past it
            testAction.SetUp();
            if (CheckForcedNeighbours(observation, action, testAction))
                return true;
            // Check wall below and open space past it
            testAction.SetDown();
            if (CheckForcedNeighbours(observation, action, testAction))
                return true;
        }
        //Vertical movement
        if (action.IsUp() || action.IsDown())
        {
            // Check wall to the left and open space past it
            testAction.SetLeft();
            if (CheckForcedNeighbours(observation, action, testAction))
                return true;
            // Check wall to the right and open space past it
            testAction.SetRight();
            if (CheckForcedNeighbours(observation, action, testAction))
                return true;
        }
        return false;
    }

    int[] Jump(Observation observation, Action action)
    {
        forwardModel.Test(observation, action);

        // Bounds/Obstacle Check
        if (observation.IsInvalidPosition() || observation.IsInHole())
        {
            Debug.Log("[JPSPlayer][Jump] Jump to " + observation.GetPosition()[0] + "," + observation.GetPosition()[1] + " using action " + action.ToString() + " was invalid.");
            return null;
        }
        // Goal Check
        if (observation.ReachedGoal())
        {
            Debug.Log("[JPSPlayer][Jump] Jump to " + observation.GetPosition()[0] + "," + observation.GetPosition()[1] + " using action " + action.ToString() + " reached the goal.");
            return observation.GetPosition();
        }
        // Forced Neighbour Check
        if (HasForcedNeighbour(observation, action))
        {
            Debug.Log("[JPSPlayer][Jump] Jump to " + observation.GetPosition()[0] + "," + observation.GetPosition()[1] + " using action " + action.ToString() + " has a forced neighbour.");
            return observation.GetPosition();
        }
        // Perpendicular Jump Check
        /* 
            This version of JPS breaks in empty mazes because it can't move in diagonal directions.
            It can't move to a perpendicular direciton without a forced neighbour and continues all the way to the end of the maze.
            This addresses the edge case.

            The way we do it is by sending perpendicular jumps that will either find a forced neighbour or reach the goal,
            which will then mark the current cell as a turning Jump Point.
        */
        if (action.IsLeft() || action.IsRight())
        {
            Action perpendicularUp = new Action();
            perpendicularUp.SetUp();

            Action perpendicularDown = new Action();
            perpendicularDown.SetDown();

            if (Jump(observation.Clone(), perpendicularUp) != null || Jump(observation.Clone(), perpendicularDown) != null)
            {
                return observation.GetPosition(); // Mark current cell as a turning Jump Point
            }
        }

        // Continue Jumping in the same direction
        Debug.Log("[JPSPlayer][Jump] Jump to " + observation.GetPosition()[0] + "," + observation.GetPosition()[1] + " using action " + action.ToString() + " continues.");
        return Jump(observation, action);

    }

    void FindPath(Observation observation)
    {
        // Usual A*
        actionStack.Clear();
        cameFrom.Clear();
        cameFromAction.Clear();
        costSoFar.Clear();

        PriorityQueue<Vector2Int, float> openQueue = new PriorityQueue<Vector2Int, float>();
        Vector2Int start = ToPositionKey(observation.GetPosition());
        openQueue.Enqueue(start, 0);
        cameFrom[start] = default;
        cameFromAction[start] = null;
        costSoFar[start] = 0;

        while (openQueue.Count > 0)
        {
            Vector2Int currentPos = openQueue.Dequeue();

            Observation obsClone = observation.Clone();
            obsClone.SetPosition(currentPos.x, currentPos.y);

            if (obsClone.ReachedGoal())
            {
                ReconstructPath(start, currentPos);
                return;
            }

            foreach (Action action in obsClone.GetListActions())
            {
                // Change in A* to use Jump instead of just moving one step in the direction of the action
                int[] jumpPos = Jump(obsClone.Clone(), action);
                if (jumpPos != null)
                {
                    // Back to A*
                    Observation jumpObs = observation.Clone();
                    jumpObs.SetPosition(jumpPos[0], jumpPos[1]);

                    Vector2Int jumpKey = ToPositionKey(jumpPos);
                    int[] currentPositionArray = new int[] { currentPos.x, currentPos.y };
                    Debug.Log("[JPSPlayer][FindPath] Jumped from " + currentPos.x + "," + currentPos.y + " to " + jumpPos[0] + "," + jumpPos[1] + " using action " + action.ToString());
                    float tentativeCost = costSoFar[currentPos] + GetDistanceBetweenStraightPositions(currentPositionArray, jumpPos, action);
                    if (!costSoFar.ContainsKey(jumpKey) || tentativeCost < costSoFar[jumpKey])
                    {
                        cameFrom[jumpKey] = currentPos;
                        cameFromAction[jumpKey] = action;
                        costSoFar[jumpKey] = tentativeCost;

                        float priority = tentativeCost - heuristic.GetScore(jumpObs);

                        openQueue.Enqueue(jumpKey, priority);
                        Debug.Log($"[JPSPlayer][FindPath] Enqueued position {jumpPos[0]},{jumpPos[1]} with priority {priority}.");
                    }
                }
                else
                {
                    Debug.Log("[JPSPlayer][FindPath] Jump from " + currentPos.x + "," + currentPos.y + " using action " + action.ToString() + " was invalid.");
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
            Debug.Log("[JPSPlayer][Think] Path found with " + actionStack.Count + " actions.");
        }
        // If we have actions in the queue, we can return the next action
        if (actionStack.Count > 0)
        {
            return actionStack.Pop();
        }
        else
        {
            // Since returning null is not allowed, we return a default action (up) if we have no actions left in the queue.
            Debug.Log("[JPSPlayer][Think] No actions left in the queue, returning default action (up).");
            Action defaultAction = new Action();
            defaultAction.SetUp();
            return defaultAction;
        }
    }

    public override void Reset()
    {
        cameFrom.Clear();
        cameFromAction.Clear();
        costSoFar.Clear();
        actionStack.Clear();


        heuristic = GetComponent<Heuristic>();
        if (heuristic == null) throw new System.Exception("JPSPlayer requires a Heuristic component to function.");
        Debug.Log("[JPSPlayer][RESET] Heuristic component found: " + heuristic.ToString());
    }

}
