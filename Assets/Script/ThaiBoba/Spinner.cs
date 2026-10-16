using UnityEngine;

// Spins a child (fan blades, tuk-tuk wheels...) around a local axis.
public class Spinner : MonoBehaviour
{
    public Vector3 axis = Vector3.forward;
    public float degreesPerSecond = 720f;

    void Update()
    {
        transform.Rotate(axis, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
