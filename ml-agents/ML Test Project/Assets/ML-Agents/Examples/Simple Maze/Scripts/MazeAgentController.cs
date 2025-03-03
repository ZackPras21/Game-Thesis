using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class MazeAgentController : Agent
{
    [SerializeField] private Transform target;

    public override void CollectObservations(VectorSensor sensor)
    (
        sensor.AddObservation(transform.localPosition);
        sensor.AddObservation(target.localPosition);
    )
 
    public override void OnActionRecieved(ActionBuffers actions)
    (
        float moveMaze = actions.ContinousAction[0];
        float moveSpeed = 2f;

    transform.localPosition += new Vector(move,0f) * Time.deltaTime* moveSpeed;
    )
}
