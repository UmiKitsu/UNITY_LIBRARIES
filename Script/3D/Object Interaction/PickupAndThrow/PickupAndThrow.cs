using System.Collections.Generic;
using UnityEngine;

public class ItemInteractionController : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private Camera cam;
    [SerializeField] private float pickupRange = 3f;
    [SerializeField] private LayerMask pickupMask;

    [Header("Feel")]
    [SerializeField] private float holdSlerp = 20f;
    [SerializeField] private float throwForwardBoost = 2f;
    [SerializeField] private float angularThrowMultiplier = 1.5f;
    [SerializeField] private int velocitySampleCount = 8;

    [Header("Hold")]
    [SerializeField] private float holdDistance = 2f;

    [Header("Attach (pull-in)")]
    [SerializeField] private float attachDuration = 0.18f;
    [SerializeField]
    private AnimationCurve attachCurve =
        AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Ground Clamp")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundClearance = 0.02f;

    [Header("Feet Clamp")]
    [SerializeField] private bool clampAbovePlayerFeet = true;
    [SerializeField] private float feetClearance = 0.05f;

    [Header("Throw Obstruction")]
    [SerializeField] private float throwObstructionDistance = 1.0f;
    [SerializeField] private LayerMask throwObstructionMask = ~0;

    private Rigidbody heldRb;
    private Transform heldOriginalParent;
    private Queue<Vector3> linearVelSamples = new();
    private Vector3 lastHeldPos;
    private Quaternion lastCamRot;
    private Vector3 camAngularVelocity;

    private bool isAttaching;
    private float attachT;
    private Vector3 attachStartPos;
    private Quaternion attachStartRot;

    private CharacterController characterController;

    void Awake()
    {
        if (!cam) cam = Camera.main;

        characterController = GetComponentInParent<CharacterController>();

        lastCamRot = cam.transform.rotation;
        lastHeldPos = Vector3.zero;
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
            TryPickup();

        if (Input.GetMouseButtonUp(0))
            ReleaseAndThrow();

        if (heldRb)
        {
            Vector3 desiredPos = GetDesiredHoldPosition();
            desiredPos = ConstrainToAboveGroundAndFeet(desiredPos);

            if (isAttaching)
            {
                attachT += Time.deltaTime / Mathf.Max(attachDuration, 0.0001f);
                float t = Mathf.Clamp01(attachT);
                float k = attachCurve != null ? attachCurve.Evaluate(t) : t;

                Vector3 targetPos = Vector3.Lerp(attachStartPos, desiredPos, k);
                targetPos = ConstrainToAboveGroundAndFeet(targetPos);

                MoveHeldRigidbody(targetPos);

                Quaternion targetRot = Quaternion.Slerp(attachStartRot, cam.transform.rotation, k);
                heldRb.MoveRotation(targetRot);

                if (t >= 1f)
                    isAttaching = false;
            }
            else
            {
                MoveHeldRigidbody(desiredPos);
                heldRb.MoveRotation(Quaternion.Slerp(heldRb.rotation, cam.transform.rotation, Time.deltaTime * holdSlerp));
            }
        }

        // -- camera angular velocity --
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
        if (!heldRb) return;

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 currentPos = heldRb.position;

        Vector3 vel = (currentPos - lastHeldPos) / dt;
        lastHeldPos = currentPos;

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
                lastHeldPos = heldRb.position;
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

        bool obstructed = IsThrowObstructed();

        float lookDownDot = Vector3.Dot(cam.transform.forward, Vector3.down);
        if (lookDownDot > 0.35f)
            obstructed = true;

        Vector3 forwardBoost = Vector3.zero;
        Vector3 angular = Vector3.zero;

        if (!obstructed)
        {
            Vector3 dir = cam.transform.forward;
            dir.y = Mathf.Max(0f, dir.y);
            if (dir.sqrMagnitude < 0.0001f)
                dir = cam.transform.forward;

            dir.Normalize();
            forwardBoost = dir * throwForwardBoost;

            angular = camAngularVelocity * angularThrowMultiplier;
        }

        heldRb.useGravity = true;

        if (characterController != null)
        {
            Vector3 playerCenter = characterController.transform.TransformPoint(characterController.center);
            Vector3 dir = (heldRb.position - playerCenter);
            if (dir.sqrMagnitude < 0.001f)
                dir = cam.transform.forward;
            dir.Normalize();
            heldRb.position += dir * 0.05f;
        }

        if (obstructed)
        {
            heldRb.linearVelocity = Vector3.zero;
            heldRb.angularVelocity = Vector3.zero;
        }
        else
        {
            heldRb.linearVelocity = avgLinear + forwardBoost;
            heldRb.angularVelocity = angular;
        }

        heldRb = null;
        isAttaching = false;
        linearVelSamples.Clear();
    }

    Vector3 GetDesiredHoldPosition()
    {
        Vector3 origin = cam.transform.position;
        Vector3 dir = cam.transform.forward;

        float desiredDist = holdDistance;

        int mask = throwObstructionMask;

        if (Physics.Raycast(origin, dir, out RaycastHit hit, desiredDist, mask, QueryTriggerInteraction.Ignore))
        {
            if (!IsHeldCollider(hit.collider))
            {
                const float backOff = 0.1f;
                return hit.point - dir * backOff;
            }
        }

        return origin + dir * desiredDist;
    }

    void MoveHeldRigidbody(Vector3 targetPos)
    {
        if (!heldRb) return;

        Vector3 toTarget = targetPos - heldRb.position;
        Vector3 desiredVel = toTarget * holdSlerp;

        heldRb.linearVelocity = desiredVel;
    }

    Vector3 ConstrainToAboveGroundAndFeet(Vector3 desiredPosition)
    {
        if (!heldRb) return desiredPosition;

        float halfHeight = 0.5f;
        float radius = 0.25f;
        Collider col;
        if (heldRb.TryGetComponent(out col))
        {
            Bounds b = col.bounds;
            halfHeight = b.extents.y;
            radius = Mathf.Max(b.extents.x, b.extents.z);
        }

        float minAllowedY = float.NegativeInfinity;

        // --- Ground clamp ---
        int mask = groundMask;

        const float rayUpOffset = 0.5f;
        float upAmount = halfHeight + rayUpOffset;
        Vector3 rayOrigin = desiredPosition + Vector3.up * upAmount;
        float maxDistance = upAmount + 1f;

        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, maxDistance, mask, QueryTriggerInteraction.Ignore);
        float closestDist = float.PositiveInfinity;
        foreach (var h in hits)
        {
            if (IsHeldCollider(h.collider))
                continue;

            if (h.distance < closestDist)
            {
                closestDist = h.distance;
                minAllowedY = h.point.y + halfHeight + groundClearance;
            }
        }

        // --- Feet clamp ---
        if (characterController != null)
        {
            float feetY = characterController.transform.position.y
                          + characterController.center.y
                          - characterController.height * 0.5f;

            Vector3 feetPos = characterController.transform.position +
                              characterController.center -
                              Vector3.up * (characterController.height * 0.5f - characterController.radius);

            float feetMinY = feetY + halfHeight + feetClearance;
            if (float.IsNegativeInfinity(minAllowedY) || feetMinY > minAllowedY)
                minAllowedY = feetMinY;

            Vector3 horiz = desiredPosition - feetPos;
            horiz.y = 0f;
            float horizMag = horiz.magnitude;
            float minHorizDist = characterController.radius + radius + 0.02f;

            if (horizMag < minHorizDist)
            {
                Vector3 pushDir;
                if (horizMag > 0.0001f)
                    pushDir = horiz / horizMag;
                else
                    pushDir = new Vector3(cam.transform.forward.x, 0f, cam.transform.forward.z).normalized;

                Vector3 newHoriz = pushDir * minHorizDist;
                desiredPosition = new Vector3(feetPos.x + newHoriz.x, desiredPosition.y, feetPos.z + newHoriz.z);
            }
        }

        if (!float.IsNegativeInfinity(minAllowedY) && desiredPosition.y < minAllowedY)
            desiredPosition.y = Mathf.Lerp(desiredPosition.y, minAllowedY, 0.5f);

        return desiredPosition;
    }

    bool IsThrowObstructed()
    {
        if (!heldRb) return false;

        int mask = throwObstructionMask;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, throwObstructionDistance, mask, QueryTriggerInteraction.Ignore))
        {
            if (!IsHeldCollider(hit.collider))
                return true;
        }

        float halfHeight = 0.5f;
        Collider col;
        if (heldRb.TryGetComponent(out col))
            halfHeight = col.bounds.extents.y;

        Vector3 center = heldRb.worldCenterOfMass;

        float groundCheckDist = halfHeight + 0.05f;
        if (Physics.Raycast(center + Vector3.up * 0.01f, Vector3.down, out hit, groundCheckDist, mask, QueryTriggerInteraction.Ignore))
        {
            if (!IsHeldCollider(hit.collider))
                return true;
        }

        float proximityRadius = halfHeight + 0.05f;
        Collider[] hits2 = Physics.OverlapSphere(center, proximityRadius, mask, QueryTriggerInteraction.Ignore);
        foreach (var c in hits2)
        {
            if (!IsHeldCollider(c))
                return true;
        }

        return false;
    }

    bool IsHeldCollider(Collider c)
    {
        if (c == null) return false;
        if (heldRb == null) return false;

        if (c.attachedRigidbody == heldRb) return true;
        if (c.transform.IsChildOf(heldRb.transform)) return true;

        return false;
    }
}