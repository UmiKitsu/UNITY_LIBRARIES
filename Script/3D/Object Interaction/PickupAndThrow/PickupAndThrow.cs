using System.Collections.Generic;
using UnityEngine;

public class PickupAndThrow : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private Camera cam;
    [SerializeField] private Transform holdPoint;
    [SerializeField] private float pickupRange = 3f;
    [SerializeField] private LayerMask pickupMask;

    [Header("Feel")]
    [SerializeField] private float holdSlerp = 20f;             
    [SerializeField] private float throwForwardBoost = 2f;
    [SerializeField] private float angularThrowMultiplier = 1.5f;
    [SerializeField] private int velocitySampleCount = 8;

    [Header("Attach (pull-in)")]
    [SerializeField] private float attachDuration = 0.18f;      
    [SerializeField]
    private AnimationCurve attachCurve =        
        AnimationCurve.EaseInOut(0, 0, 1, 1);

    private Rigidbody heldRb;
    private Transform heldOriginalParent;
    private Queue<Vector3> linearVelSamples = new();
    private Vector3 lastHoldPos;
    private Quaternion lastCamRot;
    private Vector3 camAngularVelocity;

    private bool isAttaching;
    private float attachT;
    private Vector3 attachStartPos;
    private Quaternion attachStartRot;

    const string HOLD_POINT_NAME = "HoldPoint";

    void Awake()
    {
        if (!cam) cam = Camera.main;
        if (!holdPoint)
        {
            GameObject hp = new(HOLD_POINT_NAME);
            holdPoint = hp.transform;
            holdPoint.SetParent(cam.transform);
            holdPoint.SetLocalPositionAndRotation(new Vector3(0f, -0.1f, 1f), Quaternion.identity);
        }

        lastHoldPos = holdPoint.position;
        lastCamRot = cam.transform.rotation;
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
            TryPickup();

        if (Input.GetMouseButtonUp(0))
            ReleaseAndThrow();

        if (heldRb)
        {
            if (isAttaching)
            {
                attachT += Time.deltaTime / Mathf.Max(attachDuration, 0.0001f);
                float t = Mathf.Clamp01(attachT);
                float k = attachCurve != null ? attachCurve.Evaluate(t) : t;

                Vector3 targetPos = Vector3.Lerp(attachStartPos, holdPoint.position, k);
                Quaternion targetRot = Quaternion.Slerp(attachStartRot, holdPoint.rotation, k);

                heldRb.MovePosition(targetPos);
                heldRb.MoveRotation(targetRot);

                if (t >= 1f)
                {
                    isAttaching = false; 
                }
            }
            else
            {
                heldRb.MovePosition(Vector3.Lerp(heldRb.position, holdPoint.position, Time.deltaTime * holdSlerp));
                heldRb.MoveRotation(Quaternion.Slerp(heldRb.rotation, holdPoint.rotation, Time.deltaTime * holdSlerp));
            }
        }

        Quaternion currentRot = cam.transform.rotation;
        Quaternion delta = currentRot * Quaternion.Inverse(lastCamRot);
        delta.ToAngleAxis(out float angleDeg, out Vector3 axis);
        if (angleDeg > 180f) angleDeg -= 360f;

        float angleRad = angleDeg * Mathf.Deg2Rad;
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        camAngularVelocity = axis.sqrMagnitude > 0.00001f ? axis.normalized * (angleRad / dt) : Vector3.zero;
        lastCamRot = currentRot;
    }

    void LateUpdate()
    {
        Vector3 currentPos = holdPoint.position;
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);

        Vector3 vel = (currentPos - lastHoldPos) / dt;
        lastHoldPos = currentPos;

        linearVelSamples.Enqueue(vel);
        while (linearVelSamples.Count > velocitySampleCount)
            linearVelSamples.Dequeue();
    }

    void TryPickup()
    {
        if (heldRb) return;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, pickupRange, pickupMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.rigidbody != null && !hit.rigidbody.isKinematic)
            {
                heldRb = hit.rigidbody;
                heldOriginalParent = heldRb.transform.parent;

                heldRb.useGravity = false;
                heldRb.isKinematic = false;
                heldRb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                heldRb.interpolation = RigidbodyInterpolation.Interpolate;

                isAttaching = true;
                attachT = 0f;
                attachStartPos = heldRb.position;
                attachStartRot = heldRb.rotation;

                linearVelSamples.Clear();
                lastHoldPos = holdPoint.position;
                lastCamRot = cam.transform.rotation;
            }
        }
    }

    void ReleaseAndThrow()
    {
        if (!heldRb) return;

        Vector3 avgLinear = Vector3.zero;
        int count = linearVelSamples.Count;
        foreach (var v in linearVelSamples) avgLinear += v;
        if (count > 0) avgLinear /= count;

        Vector3 forwardBoost = cam.transform.forward * throwForwardBoost;
        Vector3 angular = camAngularVelocity * angularThrowMultiplier;

        heldRb.useGravity = true;

        heldRb.position = holdPoint.position + cam.transform.forward * 0.05f;

        heldRb.linearVelocity = avgLinear + forwardBoost;
        heldRb.angularVelocity = angular;

        heldRb = null;
        isAttaching = false;
        linearVelSamples.Clear();
    }
}