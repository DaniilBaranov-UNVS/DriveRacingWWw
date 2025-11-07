using System;
using System.Collections;
using _2DOF;
using UnityEngine;

public class CarTelemetryHandler : MonoBehaviour
{
    private const float WAIT_TIME = SendingData.WAIT_TIME / 1000f;

    [Header("References")]
    [SerializeField] private Transform vehicleTransform;
    [SerializeField] private Rigidbody rigidbody;

    [Header("Pitch/Roll settings")]
    [Tooltip("")]
    [SerializeField] private float accelPitchFactor = 0.02f;
    [Tooltip("")]
    [SerializeField] private float cornerRollFactor = 0.02f;

    [Header("Impact settings")]
    [Tooltip("")]
    [SerializeField] private float impactFactor = 0.015f;
    [Tooltip("")]
    [SerializeField] private float impactDamping = 2.5f;

    [Header("Smoothing & Limits")]
    [SerializeField] private float smoothSpeed = 5f;
    [SerializeField] private float maxPitch = 10f;
    [SerializeField] private float maxRoll = 10f;

    private ObjectTelemetryData _telemetryDataData;
    private SendingData _sendingData;

    private Vector3 lastVelocity;
    private float currentPitch;
    private float currentRoll;
    private float impactPitch;
    private float impactRoll;

    private void Awake()
    {
        _sendingData = new SendingData();
        _telemetryDataData = _sendingData.ObjectTelemetryData;
    }

    public void OnEnable()
    {
        StartCoroutine(TelemetryHandler());
        _sendingData.SendingStart();

        lastVelocity = rigidbody.linearVelocity;
    }

    public void OnDisable()
    {
        StopCoroutine(TelemetryHandler());
        _sendingData.SendingStop();
    }

    private IEnumerator TelemetryHandler()
    {
        while (true)
        {
            if (_telemetryDataData == null)
            {
                yield return new WaitForSeconds(WAIT_TIME * 1000f);
                continue;
            }

            UpdatePlatformMotion();

            yield return new WaitForSeconds(WAIT_TIME);
        }
    }

    private void UpdatePlatformMotion()
    {

        Vector3 velocity = rigidbody.linearVelocity;
        Vector3 acceleration = (velocity - lastVelocity) / Time.fixedDeltaTime;

        float forwardAccel = Vector3.Dot(acceleration, vehicleTransform.forward);
        float lateralAccel = Vector3.Dot(acceleration, vehicleTransform.right);

        float targetPitch = -forwardAccel * accelPitchFactor;
        float targetRoll = -lateralAccel * cornerRollFactor;

        impactPitch = Mathf.Lerp(impactPitch, 0f, Time.deltaTime * impactDamping);
        impactRoll = Mathf.Lerp(impactRoll, 0f, Time.deltaTime * impactDamping);

        targetPitch += impactPitch;
        targetRoll += impactRoll;

        targetPitch = Mathf.Clamp(targetPitch, -maxPitch, maxPitch);
        targetRoll = Mathf.Clamp(targetRoll, -maxRoll, maxRoll);

        currentPitch = Mathf.Lerp(currentPitch, targetPitch, Time.deltaTime * smoothSpeed);
        currentRoll = Mathf.Lerp(currentRoll, targetRoll, Time.deltaTime * smoothSpeed);

        var euler = new Vector3(currentPitch, 0f, currentRoll);
        _telemetryDataData.Angles = euler;
        _telemetryDataData.Velocity = velocity;

        lastVelocity = velocity;
    }

    private void OnCollisionEnter(Collision collision)
    {
        float impactForce = collision.relativeVelocity.magnitude;

        impactPitch = -impactForce * impactFactor;
        impactRoll = UnityEngine.Random.Range(-impactForce, impactForce) * impactFactor * 0.5f;
    }
}
