using UnityEngine;

public class CarController : MonoBehaviour
{
    [Header("Настройки движения")]
    public float maxMotorTorque = 2000f;
    public float maxSteeringAngle = 35f;
    public float maxSpeed = 150f;

    [Header("Торможение ")]
    public float brakeForce = 25000f;
    public float handbrakeForce = 35000f;
    public float coastingBrakeForce = 800f;
    public float downforce = 50f;
    public float gripFactor = 1.2f;

    [Header("Колеса")]
    public WheelCollider frontLeftWheel;
    public WheelCollider frontRightWheel;
    public WheelCollider rearLeftWheel;
    public WheelCollider rearRightWheel;

    [Header("Руль")]
    public Transform steeringWheel; // Ссылка на руль
    public float maxSteeringWheelAngle = 180f; // Максимальный угол поворота руля в градусах

    [Header("Центр массы")]
    public Vector3 centerOfMassOffset = new Vector3(0f, -0.5f, 0f);

    private Rigidbody carRigidbody;
    private float currentMotorTorque;
    private float currentSteeringAngle;
    private bool isBraking;
    private bool isHandbraking;
    private bool isCoasting;
    private float currentSpeed;

    void Start()
    {
        carRigidbody = GetComponent<Rigidbody>();
        carRigidbody.centerOfMass += centerOfMassOffset;

        // Настройка трения колес для лучшего сцепления
        SetupWheelFriction();
    }

    void SetupWheelFriction()
    {
        WheelFrictionCurve forwardFriction = new WheelFrictionCurve();
        forwardFriction.extremumSlip = 0.4f;
        forwardFriction.extremumValue = 1f;
        forwardFriction.asymptoteSlip = 0.8f;
        forwardFriction.asymptoteValue = 0.5f;
        forwardFriction.stiffness = gripFactor;

        WheelFrictionCurve sidewaysFriction = new WheelFrictionCurve();
        sidewaysFriction.extremumSlip = 0.2f;
        sidewaysFriction.extremumValue = 1f;
        sidewaysFriction.asymptoteSlip = 0.5f;
        sidewaysFriction.asymptoteValue = 0.75f;
        sidewaysFriction.stiffness = gripFactor;

        WheelCollider[] wheels = { frontLeftWheel, frontRightWheel, rearLeftWheel, rearRightWheel };
        foreach (WheelCollider wheel in wheels)
        {
            wheel.forwardFriction = forwardFriction;
            wheel.sidewaysFriction = sidewaysFriction;
        }
    }

    void Update()
    {
        // Получаем ввод от игрока
        float verticalInput = Input.GetAxis("Vertical");
        float horizontalInput = Input.GetAxis("Horizontal");
        bool brakeInput = Input.GetKey(KeyCode.S);
        bool handbrakeInput = Input.GetKey(KeyCode.Space);

        // Определяем, отпущены ли все педали
        isCoasting = Mathf.Abs(verticalInput) < 0.1f && !brakeInput && !handbrakeInput;

        currentSpeed = carRigidbody.linearVelocity.magnitude * 3.6f;

        HandleMovement(verticalInput, horizontalInput, brakeInput, handbrakeInput);
        ApplyDownforce();
        UpdateWheelVisuals();
        UpdateSteeringWheel(); // Обновляем поворот руля
    }

    void HandleMovement(float throttle, float steering, bool brake, bool handbrake)
    {
        isBraking = brake;
        isHandbraking = handbrake;

        // Рулевое управление - уменьшаем угол на высокой скорости
        float speedFactor = Mathf.Clamp01(1f - (currentSpeed / maxSpeed) * 0.7f);
        currentSteeringAngle = steering * maxSteeringAngle * speedFactor;

        // Двигатель - ограничиваем мощность на высокой скорости
        float motorFactor = Mathf.Clamp01(1f - (currentSpeed / maxSpeed));
        currentMotorTorque = throttle * maxMotorTorque * motorFactor;

        // Применяем управление
        ApplySteering();
        ApplyMotor();
        ApplyBrakes();
    }

    void ApplySteering()
    {
        frontLeftWheel.steerAngle = currentSteeringAngle;
        frontRightWheel.steerAngle = currentSteeringAngle;
    }

    void ApplyMotor()
    {
        rearLeftWheel.motorTorque = currentMotorTorque;
        rearRightWheel.motorTorque = currentMotorTorque;
    }

    void ApplyBrakes()
    {
        float frontBrake = 0f;
        float rearBrake = 0f;

        if (isHandbraking)
        {
            // Ручной тормоз (пробел) - блокируем только задние колеса
            rearBrake = handbrakeForce;
            frontBrake = brakeForce * 0.3f;
        }
        else if (isBraking)
        {
            // Основной тормоз (S) - все колеса
            frontBrake = brakeForce * 0.7f;
            rearBrake = brakeForce * 0.5f;
        }
        else if (isCoasting && currentSpeed > 5f)
        {
            // Автоматическое торможение при отпускании всех педалей
            float coastingFactor = Mathf.Clamp01(currentSpeed / 50f);
            frontBrake = coastingBrakeForce * 0.6f * coastingFactor;
            rearBrake = coastingBrakeForce * 0.4f * coastingFactor;
        }
        else if (Input.GetAxis("Vertical") < -0.1f && currentSpeed > 5f)
        {
            // Задний ход при движении вперед
            frontBrake = brakeForce * 0.8f;
            rearBrake = brakeForce * 0.6f;
        }

        // Применяем тормоза
        frontLeftWheel.brakeTorque = frontBrake;
        frontRightWheel.brakeTorque = frontBrake;
        rearLeftWheel.brakeTorque = rearBrake;
        rearRightWheel.brakeTorque = rearBrake;

        // Отключаем двигатель при торможении
        if (isBraking || isHandbraking || isCoasting)
        {
            rearLeftWheel.motorTorque = 0f;
            rearRightWheel.motorTorque = 0f;
        }
    }

    void UpdateSteeringWheel()
    {
        if (steeringWheel != null)
        {
            // Вычисляем угол поворота руля на основе угла поворота колес
            float steeringRatio = maxSteeringWheelAngle / maxSteeringAngle;
            float steeringWheelAngle = currentSteeringAngle * steeringRatio;

            // Вращаем руль вокруг его локальной оси Z
            steeringWheel.localRotation = Quaternion.Euler(0f, 0f, -steeringWheelAngle);
        }
    }

    void UpdateWheelVisuals()
    {
        // Обновляем визуальное положение и вращение колес
        UpdateWheelVisual(frontLeftWheel);
        UpdateWheelVisual(frontRightWheel);
        UpdateWheelVisual(rearLeftWheel);
        UpdateWheelVisual(rearRightWheel);
    }

    void UpdateWheelVisual(WheelCollider wheelCollider)
    {
        if (wheelCollider.transform.childCount == 0) return;

        Transform visualWheel = wheelCollider.transform.GetChild(0);

        wheelCollider.GetWorldPose(out Vector3 position, out Quaternion rotation);

        visualWheel.position = position;
        visualWheel.rotation = rotation;
    }

    void ApplyDownforce()
    {
        // Прижимная сила для лучшего сцепления с дорогой
        carRigidbody.AddForce(-transform.up * downforce * carRigidbody.linearVelocity.magnitude);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(transform.position + transform.rotation * centerOfMassOffset, 0.1f);
    }

    void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 200, 20), $"Speed: {currentSpeed:F1} km/h");
        GUI.Label(new Rect(10, 30, 300, 20), $"Steering: {currentSteeringAngle:F1}°");
        GUI.Label(new Rect(10, 50, 300, 20), $"Controls: W - Gas, S - Brake, Space - Handbrake");
    }
}