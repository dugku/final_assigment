using UnityEngine;

public class TiltBehavior : MonoBehaviour
{
    [Header("Tilt Settings")]
    public float maxTilt = 15f;
    public float tiltSpeed = 5f;

    [Header("Smoothing")]
    [Range(0f, 1f)]
    public float smoothing = 0.1f;       // Lower = more responsive, higher = smoother
    public float deadzone = 0.05f;       // Ignore tiny movements below this threshold

    private Vector3 _smoothedAccel;
    private float _yRotation;            // Store Y separately to avoid gimbal issues

    void Start()
    {
        _smoothedAccel = Input.acceleration;
        _yRotation = transform.localEulerAngles.y;
    }

    void Update()
    {
        // Low-pass filter to reduce jitter
        _smoothedAccel = Vector3.Lerp(_smoothedAccel, Input.acceleration, 1f - smoothing);

        // Apply deadzone
        float rawX = Mathf.Abs(_smoothedAccel.y) > deadzone ? _smoothedAccel.y : 0f;
        float rawZ = Mathf.Abs(_smoothedAccel.x) > deadzone ? _smoothedAccel.x : 0f;

        // Map to tilt angles
        float tiltX = Mathf.Clamp(rawX * maxTilt, -maxTilt, maxTilt);
        float tiltZ = Mathf.Clamp(-rawZ * maxTilt, -maxTilt, maxTilt);

        Quaternion targetRotation = Quaternion.Euler(tiltX, _yRotation, tiltZ);
        transform.localRotation = Quaternion.Lerp(transform.localRotation, targetRotation, tiltSpeed * Time.deltaTime);
    }
}