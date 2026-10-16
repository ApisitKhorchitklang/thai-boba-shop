using UnityEngine;

public class TouchCtrl : MonoBehaviour
{
    public TouchScreenCamera TSC;
    public CameraMobile CAM;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        CAM.LockAxis = TSC.TouchDist;
    }
}
