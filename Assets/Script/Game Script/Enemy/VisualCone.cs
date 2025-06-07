using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class VisionCone : MonoBehaviour
{
    [Header("Vision Parameters")]
    [Range(10f, 180f)] public float angle = 60f;
    public float viewDistance = 10f;
    public int segments = 20;
    public float heightOffset = 0.5f; // Raise cone above origin point

    [Header("Visual Effects")]
    public float pulseSpeed = 1f;
    public float pulseAmount = 0.5f;
    public Gradient coneGradient;
    public bool alwaysUpdateCone = true;

    [Header("Detection")]
    public LayerMask obstacleMask;
    public LayerMask targetMask;
    public Transform target;
    
    private LineRenderer lr;
    private Vector3[] vertices;
    private bool isTargetVisible;
    private float lastConeUpdateTime;
    private const float coneUpdateInterval = 0.1f;

    public bool IsTargetVisible => isTargetVisible;

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = segments + 2;
        vertices = new Vector3[segments + 2];
        
        // Setup default gradient if none assigned
        if (coneGradient.alphaKeys.Length == 0)
        {
            coneGradient = new Gradient
            {
                alphaKeys = new[]
                {
                    new GradientAlphaKey(0.8f, 0.1f),
                    new GradientAlphaKey(0.4f, 0.5f),
                    new GradientAlphaKey(0.1f, 0.9f)
                },
                colorKeys = new[]
                {
                    new GradientColorKey(Color.yellow, 0f),
                    new GradientColorKey(Color.red, 1f)
                }
            };
        }
        lr.colorGradient = coneGradient;
    }

    void Update()
    {
        UpdateCone();
        CheckForTarget();
    }

    void UpdateCone()
    {
        if (!alwaysUpdateCone && Time.time - lastConeUpdateTime < coneUpdateInterval) 
            return;
        
        lastConeUpdateTime = Time.time;
        float pulsedDistance = viewDistance + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
        Vector3 origin = transform.position + Vector3.up * heightOffset;
        float halfAngle = angle / 2f;
        float step = angle / segments;

        // Start point (origin)
        vertices[0] = origin;

        // Arc points
        for (int i = 0; i <= segments; i++)
        {
            float currentAngle = -halfAngle + step * i;
            Vector3 dir = Quaternion.AngleAxis(currentAngle, transform.up) * transform.forward;
            
            if (Physics.Raycast(origin, dir, out RaycastHit hit, pulsedDistance, obstacleMask))
            {
                vertices[i + 1] = hit.point;
            }
            else
            {
                vertices[i + 1] = origin + dir * pulsedDistance;
            }
        }

        lr.SetPositions(vertices);
    }

    void CheckForTarget()
    {
        isTargetVisible = false;
        if (target == null) return;

        Vector3 origin = transform.position + Vector3.up * heightOffset;
        Vector3 dirToTarget = (target.position - origin).normalized;
        float distanceToTarget = Vector3.Distance(origin, target.position);
        float angleToTarget = Vector3.Angle(transform.forward, dirToTarget);

        // Fast distance/angle check first
        if (distanceToTarget > viewDistance || angleToTarget > angle / 2f) 
            return;

        // Check for obstacles
        if (!Physics.Raycast(origin, dirToTarget, distanceToTarget, obstacleMask))
        {
            isTargetVisible = true;
        }
    }

    // Optional: For smoother rotation, call this from EnemyAI script instead
    public void RotateTowardsTarget(float rotationSpeed)
    {
        if (isTargetVisible && target != null)
        {
            Vector3 direction = (target.position - transform.position).normalized;
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, 
                targetRotation, 
                rotationSpeed * Time.deltaTime
            );
        }
    }
}