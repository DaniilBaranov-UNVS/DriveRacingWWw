using UnityEngine;

public class CarController : MonoBehaviour
{
    [Header("Двигатель и трансмиссия")]
    public float maxEngineRPM = 7000f;
    public float minEngineRPM = 800f;
    public float engineBrake = 2f;
    public float[] gearRatios = { 3.5f, 2.5f, 1.8f, 1.3f, 1.0f, 0.8f, 0.6f }; // 7 передач
    public float finalDriveRatio = 3.5f;
    public float clutchEngagementSpeed = 0.5f;

    [Header("Колеса")]
    public WheelCollider[] driveWheels;
    public WheelCollider[] steeringWheels;
    public GameObject[] ddriveWheels;
    public GameObject[] gdriveWheels;

    public float maxSteerAngle = 30f;
    public float wheelTorque = 1000f;
    public float brakeTorque = 2000f;

    [Header("Физика")]
    public float downforce = 50f;
    public float dragCoefficient = 0.3f;

    // Текущие параметры
    private float currentRPM;
    private int currentGear = 1; // 1 передача (нейтралка = 0)
    private float clutchPosition = 0f; // 0 - выжато, 1 - отпущено
    private float throttleInput = 0f;
    private float brakeInput = 0f;
    private float steeringInput = 0f;
    private float clutchInput = 0f;

    // Ссылки
    private Rigidbody carRigidbody;

    // Свойства для внешнего доступа
    public float CurrentRPM => currentRPM;
    public int CurrentGear => currentGear;
    public float ClutchPosition => clutchPosition;
    public float Speed => carRigidbody.linearVelocity.magnitude * 3.6f; // км/ч

    void Start()
    {
        carRigidbody = GetComponent<Rigidbody>();
        currentRPM = minEngineRPM;
    }

    void Update()
    {
        GetInput();
        UpdateClutch();
        UpdateEngineRPM();
        ApplyForces();
    }

    void FixedUpdate()
    {
        ApplyWheelForces();
    }

    // Получение ввода с клавиатуры
    void GetInput()
    {
        // Газ и тормоз
        throttleInput = Input.GetKey(KeyCode.W) ? 1f : 0f;
        brakeInput = Input.GetKey(KeyCode.S) ? 1f : 0f;

        // Сцепление
        clutchInput = Input.GetKey(KeyCode.LeftShift) ? 1f : 0f;

        // Рулевое управление
        steeringInput = Input.GetAxis("Horizontal");

        // Переключение передач
        HandleGearShifting();
    }

    // Обновление положения сцепления
    void UpdateClutch()
    {
        if (clutchInput > 0.1f)
        {
            // Плавное выжимание сцепления
            clutchPosition = Mathf.Lerp(clutchPosition, 0f, clutchEngagementSpeed * Time.deltaTime);
        }
        else
        {
            // Плавное отпускание сцепления
            clutchPosition = Mathf.Lerp(clutchPosition, 1f, clutchEngagementSpeed * Time.deltaTime);
        }
    }

    // Обработка переключения передач
    void HandleGearShifting()
    {
        // Переключение вверх
        if (Input.GetKeyDown(KeyCode.UpArrow) && currentGear < gearRatios.Length)
        {
            TryShiftGear(currentGear + 1);
        }

        // Переключение вниз
        if (Input.GetKeyDown(KeyCode.DownArrow) && currentGear > -1)
        {
            TryShiftGear(currentGear - 1);
        }

        // Нейтральная передача
        if (Input.GetKeyDown(KeyCode.N))
        {
            TryShiftGear(0);
        }

        // Задняя передача
        if (Input.GetKeyDown(KeyCode.R))
        {
            TryShiftGear(-1);
        }
    }

    // Попытка переключения передачи
    void TryShiftGear(int targetGear)
    {
        // Проверка на валидность передачи
        if (targetGear < -1 || targetGear > gearRatios.Length)
            return;

        // Для переключения нужно выжать сцепление
        if (clutchPosition < 0.3f)
        {
            currentGear = targetGear;
            Debug.Log($"Переключение на передачу: {(targetGear == -1 ? "R" : targetGear == 0 ? "N" : targetGear.ToString())}");
        }
        else
        {
            Debug.Log("Выжмите сцепление для переключения передачи!");
        }
    }

    // Обновление RPM двигателя
    void UpdateEngineRPM()
    {
        if (currentGear == 0) // Нейтральная передача
        {
            // Двигатель работает на холостых
            currentRPM = Mathf.Lerp(currentRPM, minEngineRPM + throttleInput * 2000f, Time.deltaTime);
            return;
        }

        // Расчет RPM на основе скорости колес и передачи
        float wheelRPM = driveWheels[0].rpm;
        float transmissionRatio = currentGear == -1 ? -gearRatios[0] * finalDriveRatio :
                                gearRatios[currentGear - 1] * finalDriveRatio;

        float targetRPM = Mathf.Abs(wheelRPM * transmissionRatio);

        // Учет сцепления
        float engagedRPM = Mathf.Lerp(currentRPM, targetRPM, clutchPosition);

        // Учет газа
        float throttleRPM = minEngineRPM + (maxEngineRPM - minEngineRPM) * throttleInput;

        // Финальное RPM
        currentRPM = Mathf.Lerp(engagedRPM, throttleRPM, 1f - clutchPosition);
        currentRPM = Mathf.Clamp(currentRPM, minEngineRPM, maxEngineRPM);
    }

    // Применение сил к колесам
    void ApplyWheelForces()
    {
        // Рулевое управление
        foreach (var wheel in steeringWheels)
        {
            wheel.steerAngle = steeringInput * maxSteerAngle;
        }

        // Если сцепление выжато или нейтральная передача - не передаем крутящий момент
        if (clutchPosition < 0.1f || currentGear == 0)
            return;

        // Расчет крутящего момента
        float torque = CalculateTorque();

        // Применение тормозов
        ApplyBrakes();

        // Применение крутящего момента
        foreach (var wheel in driveWheels)
        {
            wheel.motorTorque = torque;
        }
    }

    // Расчет крутящего момента
    float CalculateTorque()
    {
        if (currentGear == -1) // Задняя передача
        {
            return -throttleInput * wheelTorque * 0.5f;
        }

        // Нормализованные RPM для кривой мощности
        float normalizedRPM = (currentRPM - minEngineRPM) / (maxEngineRPM - minEngineRPM);
        float powerCurve = Mathf.Clamp01(1.0f - Mathf.Abs(normalizedRPM - 0.7f) * 2f);

        return throttleInput * wheelTorque * powerCurve * clutchPosition;
    }

    // Применение тормозов
    void ApplyBrakes()
    {
        float brakeTorqueTotal = brakeInput * brakeTorque;

        foreach (var wheel in driveWheels)
        {
            wheel.brakeTorque = brakeTorqueTotal;
        }

        foreach (var wheel in steeringWheels)
        {
            wheel.brakeTorque = brakeTorqueTotal;
        }
    }

    // Применение аэродинамических сил
    void ApplyForces()
    {
        // Прижимная сила
        float speed = carRigidbody.linearVelocity.magnitude;
        Vector3 downForce = -transform.up * downforce * speed * speed;
        carRigidbody.AddForce(downForce);

        // Сопротивление воздуха
        Vector3 dragForce = -carRigidbody.linearVelocity * dragCoefficient * speed;
        carRigidbody.AddForce(dragForce);
    }

    // Методы для внешнего управления (для будущего руля)
    public void SetThrottle(float value) => throttleInput = Mathf.Clamp01(value);
    public void SetBrake(float value) => brakeInput = Mathf.Clamp01(value);
    public void SetSteering(float value) => steeringInput = Mathf.Clamp(value, -1f, 1f);
    public void SetClutch(float value) => clutchInput = Mathf.Clamp01(value);
    public void ShiftGear(int gear) => TryShiftGear(gear);
}