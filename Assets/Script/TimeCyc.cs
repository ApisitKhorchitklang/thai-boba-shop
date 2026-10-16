using System;
using UnityEngine;
using TMPro;

public class TimeCyc : MonoBehaviour
{
    [Header("Time Settings")]
    public float timeScale = 1.0f; // How fast time passes (higher = faster)
    [Tooltip("Hour each day starts at")] public int startHour = 6;
    public TMP_Text timerText;
    public TMP_Text day;

    private int days = 1;
    private int hours = 6;
    private int minutes = 0;
    private float timer = 0f;

    [Header("Rotation Settings")]
    public float rotationSpeed = 0.25f;

    // Expose time to other systems
    public int Hours => hours;
    public int Minutes => minutes;
    public int Days => days;
    // Fractional hour (e.g. 18.5 = 18:30), smooth between minute ticks; DayNightCycle uses this
    public float TimeOfDay => hours + (minutes + Mathf.Clamp01(timer)) / 60f;
    [Tooltip("Old fixed-speed sun rotation. DayNightCycle turns this off and drives the light from the clock instead.")]
    public bool rotateSun = true;
    public bool IsDayActive => hours < 22;

    // Event fired once when the day ends (>= 22:00)
    public event Action OnDayEnd;
    private bool dayEndSent = false;

    // Event fired when a new day starts (after AdvanceToNextDay / SetDay)
    public event Action<int> OnNewDay;

    void Start()
    {
        rotationSpeed = 0.25f;
        hours = startHour;
        // Initial rotation setup (Sunrise position)
        transform.rotation = Quaternion.Euler(0f, -30f, 0f);
        UpdateTimerUI();
        if (day != null) day.text = Loc.T("วันที่ ", "Day ") + days;
    }

    void Update()
    {
        if (!GameMenus.GameRunning) return;     // clock waits while the main menu is open

        // Only run time progression if before 22:00
        if (hours < 22)
        {
            UpdateTime();
            if (rotateSun) RotateSun();
        }
        else
        {
            // On reaching or passing 22:00, ensure day-end is broadcast once
            if (!dayEndSent)
            {
                dayEndSent = true;
                OnDayEnd?.Invoke();
            }

            if (timerText != null)
                timerText.text = "22:00 - Night";
        }
    }

    void UpdateTime()
    {
        timer += Time.deltaTime * timeScale;

        // If one "game minute" has passed (1.0f represents 1 second in real life)
        if (timer >= 1f)
        {
            timer = 0;
            minutes++;

            if (minutes >= 60)
            {
                minutes = 0;
                hours++;
            }

            UpdateTimerUI();
        }
    }

    void UpdateTimerUI()
    {
        if (timerText != null)
            timerText.text = string.Format("{0:00}:{1:00}", hours, minutes);
    }

    void RotateSun()
    {
        // Rotates the object (Directional Light) over time
        float rotX = rotationSpeed * Time.deltaTime;
        transform.Rotate(Vector3.right * rotX, Space.World);
    }

    // Optional helper: advance to next day (resets clock)
    public void AdvanceToNextDay()
    {
        days++;
        hours = startHour;
        minutes = 0;
        dayEndSent = false;
        UpdateTimerUI();
        if (day != null) day.text = Loc.T("วันที่ ", "Day ") + days;
        OnNewDay?.Invoke(days);
    }

    // Jump the clock (debug / tests / skipping to evening)
    public void SetTime(int newHours, int newMinutes)
    {
        hours = Mathf.Clamp(newHours, 0, 23);
        minutes = Mathf.Clamp(newMinutes, 0, 59);
        timer = 0f;
        if (hours < 22) dayEndSent = false;
        UpdateTimerUI();
    }

    // Used by SaveSystem to restore the saved day
    public void SetDay(int newDay)
    {
        days = Mathf.Max(1, newDay);
        hours = startHour;
        minutes = 0;
        dayEndSent = false;
        UpdateTimerUI();
        if (day != null) day.text = Loc.T("วันที่ ", "Day ") + days;
        OnNewDay?.Invoke(days);
    }
}