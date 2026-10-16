using UnityEngine;

// Rolls the Wheel_* children of a vehicle model at the vehicle's speed: TrafficCar.Speed when there is one,
// otherwise how far the vehicle moved along its forward axis (parked vehicles stay still).
public class WheelSpin : MonoBehaviour
{
    Transform[] wheels;
    Quaternion[] rest;
    float[] radius;
    float[] angle;
    TrafficCar car;
    Vector3 lastPos;

    public int WheelCount => wheels != null ? wheels.Length : 0;

    void Awake()
    {
        car = GetComponent<TrafficCar>();
        var list = new System.Collections.Generic.List<Transform>();
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
            if (t.name.StartsWith("Wheel_")) list.Add(t);
        wheels = list.ToArray();
        rest = new Quaternion[wheels.Length];
        radius = new float[wheels.Length];
        angle = new float[wheels.Length];
        for (int i = 0; i < wheels.Length; i++)
        {
            rest[i] = wheels[i].localRotation;
            Renderer r = wheels[i].GetComponent<Renderer>();
            radius[i] = r != null ? Mathf.Max(0.12f, r.bounds.extents.y) : 0.3f;
        }
        lastPos = transform.position;
    }

    void Update()
    {
        if (wheels.Length == 0 || Time.deltaTime <= 0f) return;
        float speed = car != null ? car.Speed : Vector3.Dot(transform.position - lastPos, transform.forward) / Time.deltaTime;
        lastPos = transform.position;
        if (Mathf.Abs(speed) < 0.01f) return;
        for (int i = 0; i < wheels.Length; i++)
        {
            Transform p = wheels[i].parent;
            // axle = the vehicle's right axis, expressed in the wheel's parent space (forward roll = top moves ahead)
            Vector3 axle = p != null ? p.InverseTransformDirection(transform.right) : Vector3.right;
            angle[i] = (angle[i] + speed / radius[i] * Mathf.Rad2Deg * Time.deltaTime) % 360f;
            wheels[i].localRotation = Quaternion.AngleAxis(angle[i], axle) * rest[i];
        }
    }
}
