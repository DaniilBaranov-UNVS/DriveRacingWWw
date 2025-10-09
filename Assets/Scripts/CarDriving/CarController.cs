using UnityEngine;
using System.Collections;

public class CarController : MonoBehaviour
{
    [Header("Настройки движения")]
    public float maxMotorTorque = 2000f;
    public float maxSteeringAngle = 35f;

    [Header("Торможение")]
    public float brakeForce = 25000f;
    public float handbrakeForce = 35000f;
    public float coastingBrakeForce = 800f;
    public float downforce = 50f;
    public float gripFactor = 1.2f;

    [Header("Коробка передач")]
    public int currentGear = 0; // 0 - нейтраль, -1 - задняя, 1-5 - передние
    public float[] gearRatios = { 3.8f, 2.8f, 1.9f, 1.3f, 0.9f }; // 5 передач
    public float finalDriveRatio = 3.5f;
    public float clutchEngagementSpeed = 0.5f;
    public float rpmRedline = 6500f;
    public float rpmIdle = 800f;
    public float rpmOptimalMin = 1500f;
    public float rpmOptimalMax = 4500f;

    // Максимальные скорости для каждой передачи (км/ч)
    private float[] gearMaxSpeeds = { 10f, 20f, 30f, 40f, 50f };
    private float reverseMaxSpeed = 8f;

    [Header("Колеса")]
    public WheelCollider frontLeftWheel;
    public WheelCollider frontRightWheel;
    public WheelCollider rearLeftWheel;
    public WheelCollider rearRightWheel;

    [Header("Руль")]
    public Transform steeringWheel;
    public float maxSteeringWheelAngle = 180f;

    [Header("Центр массы")]
    public Vector3 centerOfMassOffset = new Vector3(0f, -0.5f, 0f);

    private Rigidbody carRigidbody;
    private float currentMotorTorque;
    private float currentSteeringAngle;
    private bool isBraking;
    private bool isHandbraking;
    private bool isCoasting;
    private float currentSpeed;

    // Переменные для КПП
    private float clutchInput = 0f;
    private float engineRPM = 800f;
    private bool isClutchEngaged = true;
    private float wheelRPM;
    private bool shiftInProgress = false;
    private float shiftTimer = 0f;
    private float shiftTime = 0.3f;
    private bool shouldLimitSpeedAfterShift = false;
    private float targetSpeedAfterShift = 0f;

    // Статистика
    private float currentRPM;
    private string currentGearText = "N";

    // Клавиши управления
    public KeyCode clutchKey = KeyCode.LeftShift;
    private KeyCode shiftUpKey = KeyCode.E;
    private KeyCode shiftDownKey = KeyCode.Q;

    void Start()
    {
        carRigidbody = GetComponent<Rigidbody>();
        carRigidbody.centerOfMass += centerOfMassOffset;
        SetupWheelFriction();
        engineRPM = rpmIdle;
        currentGear = 0;
        UpdateGearText();
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

        // Управление сцеплением и переключением передач
        HandleGearboxInput();

        isCoasting = Mathf.Abs(verticalInput) < 0.1f && !brakeInput && !handbrakeInput;
        currentSpeed = carRigidbody.linearVelocity.magnitude * 3.6f;

        HandleMovement(verticalInput, horizontalInput, brakeInput, handbrakeInput);
        ApplyDownforce();
        UpdateWheelVisuals();
        UpdateSteeringWheel();
        UpdateRPM();

        // Обработка ограничения скорости после переключения
        HandleSpeedLimitingAfterShift();
    }

    void HandleGearboxInput()
    {
        // Управление сцеплением
        clutchInput = Input.GetKey(clutchKey) ? 1f : 0f;
        isClutchEngaged = clutchInput < 0.1f;

        // Переключение передач только при выжатом сцеплении - БЕЗ ОГРАНИЧЕНИЙ
        if ((Input.GetKeyDown(clutchKey) || clutchInput > 0.9f) && !shiftInProgress)
        {
            if (Input.GetKeyDown(shiftUpKey) && currentGear < 5)
            {
                // Можно всегда переключаться на повышенную передачу
                StartCoroutine(ShiftGear(currentGear + 1));
            }
            else if (Input.GetKeyDown(shiftDownKey) && currentGear > -1)
            {
                // Можно всегда переключаться на пониженную передачу
                StartCoroutine(ShiftGear(currentGear - 1));
            }
        }

        // Обработка процесса переключения
        if (shiftInProgress)
        {
            shiftTimer += Time.deltaTime;
            if (shiftTimer >= shiftTime)
            {
                shiftInProgress = false;
                shiftTimer = 0f;
            }
        }
    }

    IEnumerator ShiftGear(int newGear)
    {
        shiftInProgress = true;
        shiftTimer = 0f;

        PlayGearShiftSound();

        // Определяем целевую скорость для новой передачи
        if (newGear == -1)
        {
            targetSpeedAfterShift = reverseMaxSpeed;
        }
        else if (newGear > 0)
        {
            targetSpeedAfterShift = gearMaxSpeeds[newGear - 1];
        }
        else
        {
            targetSpeedAfterShift = 0f;
        }

        // Если текущая скорость превышает допустимую для новой передачи, активируем ограничение
        if (currentSpeed > targetSpeedAfterShift && newGear != 0)
        {
            shouldLimitSpeedAfterShift = true;
        }

        // Небольшая задержка для имитации переключения
        yield return new WaitForSeconds(shiftTime * 0.3f);

        currentGear = newGear;
        UpdateGearText();

        // Сброс RPM после переключения
        if (currentGear > 0)
        {
            engineRPM = Mathf.Clamp(engineRPM * 0.7f, rpmIdle, rpmRedline);
        }

        yield return new WaitForSeconds(shiftTime * 0.7f);
        shiftInProgress = false;
    }

    void HandleSpeedLimitingAfterShift()
    {
        if (shouldLimitSpeedAfterShift && currentGear != 0)
        {
            // Применяем торможение для снижения скорости до целевой
            if (currentSpeed > targetSpeedAfterShift)
            {
                float brakePower = Mathf.Lerp(0.3f, 1f, (currentSpeed - targetSpeedAfterShift) / targetSpeedAfterShift);

                frontLeftWheel.brakeTorque = brakeForce * 0.7f * brakePower;
                frontRightWheel.brakeTorque = brakeForce * 0.7f * brakePower;
                rearLeftWheel.brakeTorque = brakeForce * 0.5f * brakePower;
                rearRightWheel.brakeTorque = brakeForce * 0.5f * brakePower;

                // Отключаем двигатель во время принудительного торможения
                rearLeftWheel.motorTorque = 0f;
                rearRightWheel.motorTorque = 0f;
            }
            else
            {
                // Скорость достигла целевой, выключаем ограничение
                shouldLimitSpeedAfterShift = false;
                targetSpeedAfterShift = 0f;
            }
        }
    }

    void UpdateGearText()
    {
        if (currentGear == -1) currentGearText = "R";
        else if (currentGear == 0) currentGearText = "N";
        else currentGearText = currentGear.ToString();
    }

    void HandleMovement(float throttle, float steering, bool brake, bool handbrake)
    {
        isBraking = brake;
        isHandbraking = handbrake;

        // Рулевое управление - макс. скорость 50 км/ч
        float speedFactor = Mathf.Clamp01(1f - (currentSpeed / 50f) * 0.7f);
        currentSteeringAngle = steering * maxSteeringAngle * speedFactor;

        // Если активно принудительное ограничение скорости, игнорируем ввод газа
        if (shouldLimitSpeedAfterShift)
        {
            throttle = 0f;
        }

        // Расчет RPM колес
        wheelRPM = (rearLeftWheel.rpm + rearRightWheel.rpm) * 0.5f;

        // Расчет RPM двигателя в зависимости от передачи и сцепления
        if (!shiftInProgress && isClutchEngaged && currentGear != 0 && !shouldLimitSpeedAfterShift)
        {
            if (currentGear == -1) // Задняя передача
            {
                engineRPM = Mathf.Abs(wheelRPM) * gearRatios[0] * finalDriveRatio * 0.8f;
            }
            else if (currentGear > 0) // Передние передачи
            {
                engineRPM = Mathf.Abs(wheelRPM) * gearRatios[currentGear - 1] * finalDriveRatio;
            }

            engineRPM = Mathf.Clamp(engineRPM, rpmIdle, rpmRedline);
        }
        else
        {
            // При выжатом сцеплении или нейтрали RPM падают к холостому ходу
            engineRPM = Mathf.Lerp(engineRPM, rpmIdle + throttle * 2000f, Time.deltaTime * 2f);
        }

        // Расчет крутящего момента с учетом передачи, оборотов и ограничения скорости
        float motorFactor = 1f;
        float clampedThrottle = throttle;

        // Учет оборотов двигателя для определения эффективности
        float rpmEfficiency = GetRpmEfficiency();

        // Если активно ограничение скорости после переключения, блокируем ускорение
        if (shouldLimitSpeedAfterShift)
        {
            motorFactor = 0f;
            clampedThrottle = 0f;
        }
        // Ограничение скорости в зависимости от передачи
        else if (currentGear == -1) // Задняя передача
        {
            clampedThrottle = Mathf.Clamp01(throttle);
            if (currentSpeed > reverseMaxSpeed && clampedThrottle > 0.1f)
            {
                motorFactor = 0f;
            }
            else
            {
                float speedRatio = currentSpeed / reverseMaxSpeed;
                motorFactor = Mathf.Clamp01(1f - speedRatio * 0.8f);
            }

            clampedThrottle *= -1f;
        }
        else if (currentGear > 0) // Передние передачи
        {
            clampedThrottle = Mathf.Clamp01(throttle);
            if (currentSpeed > gearMaxSpeeds[currentGear - 1] && clampedThrottle > 0.1f)
            {
                motorFactor = 0f;
            }
            else
            {
                float speedRatio = currentSpeed / gearMaxSpeeds[currentGear - 1];
                motorFactor = Mathf.Clamp01(1f - speedRatio * 0.8f);
            }

            // ФИЗИЧЕСКОЕ ОГРАНИЧЕНИЕ: на высокой передаче с низкой скоростью - низкая мощность
            if (currentSpeed < 5f && currentGear > 2) // Если скорость очень низкая, а передача высокая (3,4,5)
            {
                // Сильно уменьшаем мощность - машина практически не поедет
                motorFactor *= 0.1f;
            }
            else if (currentSpeed < 10f && currentGear > 3) // Для 4-5 передач на низкой скорости
            {
                motorFactor *= 0.2f;
            }
            else if (currentSpeed < 15f && currentGear > 4) // Для 5 передачи на низкой скорости
            {
                motorFactor *= 0.3f;
            }
        }
        else // Нейтраль
        {
            clampedThrottle = 0f;
            motorFactor = 0f;
        }

        // Учет эффективности оборотов
        motorFactor *= rpmEfficiency;

        // Уменьшение мощности на низких и высоких оборотах
        float torqueMultiplier = 1f;
        if (engineRPM < 1500f)
            torqueMultiplier = engineRPM / 1500f;
        else if (engineRPM > 5500f)
            torqueMultiplier = Mathf.Clamp01(1f - (engineRPM - 5500f) / 1000f);

        // Крутящий момент применяется только при включенной передаче и отпущенном сцеплении
        if (currentGear != 0 && isClutchEngaged && !shiftInProgress && !shouldLimitSpeedAfterShift)
        {
            currentMotorTorque = clampedThrottle * maxMotorTorque * torqueMultiplier * motorFactor;

            if (currentGear == -1)
                currentMotorTorque *= gearRatios[0] * finalDriveRatio * 0.7f;
            else if (currentGear > 0)
                currentMotorTorque *= gearRatios[currentGear - 1] * finalDriveRatio;
        }
        else
        {
            currentMotorTorque = 0f;
        }

        ApplySteering();
        ApplyMotor();
        ApplyBrakes();
    }

    float GetRpmEfficiency()
    {
        if (engineRPM < rpmOptimalMin)
        {
            return Mathf.Lerp(0.1f, 0.8f, engineRPM / rpmOptimalMin);
        }
        else if (engineRPM > rpmOptimalMax)
        {
            return Mathf.Lerp(1f, 0.7f, (engineRPM - rpmOptimalMax) / (rpmRedline - rpmOptimalMax));
        }
        else
        {
            return 1f;
        }
    }

    void ApplySteering()
    {
        frontLeftWheel.steerAngle = currentSteeringAngle;
        frontRightWheel.steerAngle = currentSteeringAngle;
    }

    void ApplyMotor()
    {
        if (!shouldLimitSpeedAfterShift)
        {
            rearLeftWheel.motorTorque = currentMotorTorque;
            rearRightWheel.motorTorque = currentMotorTorque;
        }
    }

    void ApplyBrakes()
    {
        if (shouldLimitSpeedAfterShift)
        {
            return;
        }

        float frontBrake = 0f;
        float rearBrake = 0f;

        if (isHandbraking)
        {
            rearBrake = handbrakeForce;
            frontBrake = brakeForce * 0.3f;
        }
        else if (isBraking || Input.GetKey(KeyCode.S))
        {
            frontBrake = brakeForce * 0.7f;
            rearBrake = brakeForce * 0.5f;
        }
        else if (isCoasting && currentSpeed > 5f)
        {
            float coastingFactor = Mathf.Clamp01(currentSpeed / 50f);
            frontBrake = coastingBrakeForce * 0.6f * coastingFactor;
            rearBrake = coastingBrakeForce * 0.4f * coastingFactor;
        }

        frontLeftWheel.brakeTorque = frontBrake;
        frontRightWheel.brakeTorque = frontBrake;
        rearLeftWheel.brakeTorque = rearBrake;
        rearRightWheel.brakeTorque = rearBrake;

        if ((isBraking || isHandbraking || isCoasting) && currentGear != -1)
        {
            rearLeftWheel.motorTorque = 0f;
            rearRightWheel.motorTorque = 0f;
        }
    }

    void UpdateRPM()
    {
        currentRPM = engineRPM;
    }

    void UpdateSteeringWheel()
    {
        if (steeringWheel != null)
        {
            float steeringRatio = maxSteeringWheelAngle / maxSteeringAngle;
            float steeringWheelAngle = currentSteeringAngle * steeringRatio;
            steeringWheel.localRotation = Quaternion.Euler(0f, 0f, -steeringWheelAngle);
        }
    }

    void UpdateWheelVisuals()
    {
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
        carRigidbody.AddForce(-transform.up * downforce * carRigidbody.linearVelocity.magnitude);
    }
    void PlayGearShiftSound()
    {
        CarSoundController soundController = GetComponent<CarSoundController>();
        if (soundController != null)
        {
            soundController.ManualGearShift();
        }
    }
    void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 200, 20), $"Speed: {currentSpeed:F1} km/h");
        GUI.Label(new Rect(10, 30, 200, 20), $"RPM: {currentRPM:F0}");
        GUI.Label(new Rect(10, 50, 200, 20), $"Gear: {currentGearText}");
        GUI.Label(new Rect(10, 70, 300, 20), $"Clutch: {(isClutchEngaged ? "Engaged" : "Disengaged")}");

        string efficiency = $"RPM Efficiency: {GetRpmEfficiency():P0}";
        GUI.Label(new Rect(10, 90, 300, 20), efficiency);

        string maxSpeedInfo = "Max Speed: ";
        if (currentGear == -1) maxSpeedInfo += $"{reverseMaxSpeed} km/h (R)";
        else if (currentGear == 0) maxSpeedInfo += "0 km/h (N)";
        else maxSpeedInfo += $"{gearMaxSpeeds[currentGear - 1]} km/h (Gear {currentGear})";

        if (shouldLimitSpeedAfterShift)
        {
            maxSpeedInfo += " [LIMITING]";
        }

        GUI.Label(new Rect(10, 110, 400, 20), maxSpeedInfo);
        GUI.Label(new Rect(10, 130, 400, 60), $"Controls: W - Gas, S - Brake, Space - Handbrake\nShift - Clutch, E - Shift Up, Q - Shift Down\nFree gear shifting - physics will limit movement");
    }
}