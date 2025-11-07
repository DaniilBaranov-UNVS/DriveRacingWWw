using UnityEngine;
using System.Collections;
using LogitechG29.Sample.Input;
using UnityEngine.UI;
using TMPro;
public class CarControllerSample : MonoBehaviour
{
    [Header("Input Controller")]
    public InputControllerReader inputControllerReader;

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
    public int currentGear = 0; // 0 - нейтраль, -1 - задняя, 1-6 - передние
    public float[] gearRatios = { 3.8f, 2.8f, 1.9f, 1.3f, 0.9f, 0.7f }; // 6 передач
    public float finalDriveRatio = 3.5f;
    public float rpmRedline = 6500f;
    public float rpmIdle = 800f;
    public float rpmOptimalMin = 1500f;
    public float rpmOptimalMax = 4500f;

    // Максимальные скорости для каждой передачи (км/ч)
    private float[] gearMaxSpeeds = { 15f, 30f, 50f, 80f, 120f, 180f };
    private float reverseMaxSpeed = 15f;

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

    [Header("UI Элементы")]
    public TextMeshProUGUI speedText;

    private Rigidbody carRigidbody;
    private float currentMotorTorque;
    private float currentSteeringAngle;
    private bool isBraking;
    private bool isHandbraking;
    private bool isCoasting;
    private float currentSpeed;

    // Переменные для КПП
    public float clutchInput = 0f;
    public float engineRPM = 800f;
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

    // Переменные для управления шестеркой передач
    private bool[] previousShifterStates = new bool[7];

   
    private int[] shifterToGearMap = {1, 2, 3, 4, 5, 6, -1};

    void Start()
    {
        carRigidbody = GetComponent<Rigidbody>();
        if (carRigidbody != null)
        {
            carRigidbody.centerOfMass += centerOfMassOffset;
        }
        SetupWheelFriction();
        engineRPM = rpmIdle;
        currentGear = 0;
        UpdateGearText();

        // Инициализация состояний шифтера
        for (int i = 0; i < previousShifterStates.Length; i++)
        {
            previousShifterStates[i] = false;
        }
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
        if (inputControllerReader == null)
        {
            Debug.LogError("InputControllerReader не назначен!");
            return;
        }
        if (speedText != null)
        {
            speedText.text = $"Speed: {currentSpeed:F1}";
        }
        // Получаем ввод от контроллера
        float throttleInput = inputControllerReader.Throttle;
        float brakeInput = inputControllerReader.Brake;
        float steeringInput = inputControllerReader.Steering;
        float clutchInput = inputControllerReader.Clutch;
        float handbrakeInput = inputControllerReader.Handbrake;

        // Получаем состояние шифтера (механической КПП)
        bool[] shifterInputs = {
            inputControllerReader.Shifter1, 
            inputControllerReader.Shifter2, 
            inputControllerReader.Shifter3, 
            inputControllerReader.Shifter4, 
            inputControllerReader.Shifter5, 
            inputControllerReader.Shifter6, 
            inputControllerReader.Shifter7  
        };

        // Объединяем газ и тормоз в один вертикальный ввод (газ положительный, тормоз отрицательный)
        float verticalInput = throttleInput - brakeInput;

        // Управление сцеплением и переключением передач
        HandleGearboxInput(clutchInput, shifterInputs);

        isCoasting = Mathf.Abs(verticalInput) < 0.1f && brakeInput < 0.1f && handbrakeInput < 0.1f;
        currentSpeed = carRigidbody.linearVelocity.magnitude * 3.6f;

        HandleMovement(verticalInput, steeringInput, brakeInput > 0.1f, handbrakeInput > 0.1f);
        ApplyDownforce();
        UpdateWheelVisuals();
        UpdateSteeringWheel();
        UpdateRPM();

        // Обработка ограничения скорости после переключения
        HandleSpeedLimitingAfterShift();
    }

    void HandleGearboxInput(float clutch, bool[] shifterInputs)
    {
        // Управление сцеплением
        clutchInput = clutch;
        isClutchEngaged = clutchInput < 0.1f;

        // Проверяем изменения состояния шифтера
        for (int i = 0; i < shifterInputs.Length; i++)
        {
            // Если кнопка нажата сейчас, но не была нажата ранее
            if (shifterInputs[i] && !previousShifterStates[i])
            {
                // Переключаем передачу только если сцепление выжато
                if (clutchInput > 0.5f && !shiftInProgress)
                {
                    int newGear = shifterToGearMap[i];
                    if (newGear != currentGear)
                    {
                        StartCoroutine(ShiftGear(newGear));
                        break; // Выходим после первого найденного нажатия
                    }
                }
            }

            // Обновляем предыдущее состояние
            previousShifterStates[i] = shifterInputs[i];
        }

        // Автоматическое включение нейтрали при отпускании всех передач
        bool anyShifterActive = false;
        for (int i = 0; i < shifterInputs.Length; i++)
        {
            if (shifterInputs[i])
            {
                anyShifterActive = true;
                break;
            }
        }

        // Если ни одна передача не активна и у нас не нейтраль - включаем нейтраль
        if (!anyShifterActive && currentGear != 0 && !shiftInProgress && clutchInput > 0.5f)
        {
            StartCoroutine(ShiftGear(0));
        }
    }

    IEnumerator ShiftGear(int newGear)
    {
        shiftInProgress = true;
        shiftTimer = 0f;

        // Определяем целевую скорость для новой передачи
        if (newGear == -1)
        {
            targetSpeedAfterShift = reverseMaxSpeed;
        }
        else if (newGear > 0)
        {
            int gearIndex = Mathf.Min(newGear - 1, gearMaxSpeeds.Length - 1);
            targetSpeedAfterShift = gearMaxSpeeds[gearIndex];
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

        // Рулевое управление с учетом скорости
        float speedFactor = Mathf.Clamp01(1f - (currentSpeed / 100f) * 0.5f);
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
                int gearIndex = Mathf.Min(currentGear - 1, gearRatios.Length - 1);
                engineRPM = Mathf.Abs(wheelRPM) * gearRatios[gearIndex] * finalDriveRatio;
            }

            engineRPM = Mathf.Clamp(engineRPM, rpmIdle, rpmRedline);
        }
        else
        {
            // При выжатом сцеплении или нейтрали RPM падают к холостому ходу
            engineRPM = Mathf.Lerp(engineRPM, rpmIdle + Mathf.Abs(throttle) * 2000f, Time.deltaTime * 2f);
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
            int gearIndex = Mathf.Min(currentGear - 1, gearMaxSpeeds.Length - 1);
            if (currentSpeed > gearMaxSpeeds[gearIndex] && clampedThrottle > 0.1f)
            {
                motorFactor = 0f;
            }
            else
            {
                float speedRatio = currentSpeed / gearMaxSpeeds[gearIndex];
                motorFactor = Mathf.Clamp01(1f - speedRatio * 0.8f);
            }

            // на высокой передаче с низкой скоростью - низкая мощность
            if (currentSpeed < 10f && currentGear > 3)
            {
                motorFactor *= 0.1f;
            }
            else if (currentSpeed < 20f && currentGear > 4)
            {
                motorFactor *= 0.2f;
            }
            else if (currentSpeed < 30f && currentGear > 5)
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
            {
                currentMotorTorque *= gearRatios[0] * finalDriveRatio * 0.7f;
            }
            else if (currentGear > 0)
            {
                int gearIndex = Mathf.Min(currentGear - 1, gearRatios.Length - 1);
                currentMotorTorque *= gearRatios[gearIndex] * finalDriveRatio;
            }
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
        else if (isBraking)
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

    void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 200, 20), $"Speed: {currentSpeed:F1} km/h");
        GUI.Label(new Rect(10, 30, 200, 20), $"RPM: {currentRPM:F0}");
        GUI.Label(new Rect(10, 50, 200, 20), $"Gear: {currentGearText}");
        GUI.Label(new Rect(10, 70, 300, 20), $"Clutch: {clutchInput:F2}");
        GUI.Label(new Rect(10, 90, 300, 20), $"Throttle: {inputControllerReader.Throttle:F2}");
        GUI.Label(new Rect(10, 110, 300, 20), $"Brake: {inputControllerReader.Brake:F2}");

        string efficiency = $"RPM Efficiency: {GetRpmEfficiency():P0}";
        GUI.Label(new Rect(10, 130, 300, 20), efficiency);

        string maxSpeedInfo = "Max Speed: ";
        if (currentGear == -1) maxSpeedInfo += $"{reverseMaxSpeed} km/h (R)";
        else if (currentGear == 0) maxSpeedInfo += "0 km/h (N)";
        else
        {
            int gearIndex = Mathf.Min(currentGear - 1, gearMaxSpeeds.Length - 1);
            maxSpeedInfo += $"{gearMaxSpeeds[gearIndex]} km/h (Gear {currentGear})";
        }

        if (shouldLimitSpeedAfterShift)
        {
            maxSpeedInfo += " [LIMITING]";
        }

        GUI.Label(new Rect(10, 150, 400, 20), maxSpeedInfo);
        GUI.Label(new Rect(10, 170, 500, 80),
            $"Controls:\n" +
            $"Gas/Brake/Clutch - Pedals\n" +
            $"Steering - Wheel\n" +
            $"Handbrake - Button\n" +
            $"Manual Shifter - 1(R) 2(1) 3(2) 4(3) 5(4) 6(5) 7(6)");
    }
}