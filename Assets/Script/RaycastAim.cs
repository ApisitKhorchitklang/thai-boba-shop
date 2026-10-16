using UnityEngine;
using UnityEngine.UI; // Required if you want to modify the crosshair UI based on the hit
using UnityEngine.EventSystems;

public class RaycastAim : MonoBehaviour
{
    [Header("PickUpnRaycast")]
    public Transform handPos;
    public float maxDistance = 100f; // Max distance for the raycast
    public LayerMask layerMask; 
    public bool isEmpty = true;
    [Header("ThrowingSystem")]
    public float power = 5.0f;
    public Transform Player;
    public Transform CameraMain;

    [Header("Computer")]
    public GameObject computer;

    // Station / counter / spirit house the crosshair is currently on (for HUD prompts)
    public IInteractable CurrentTarget { get; private set; }

    // Cup currently in hand, if any
    public CupController HeldCup
    {
        get
        {
            Transform item = checkHand();
            return item != null ? item.GetComponent<CupController>() : null;
        }
    }


    void Start()
    {
        maxDistance = 3f;
        isEmpty = true;
    }

    void Update()
    {
        Transform HandItem = checkHand();
        RotatingItem(HandItem);

        

        // Cast a ray from the center of the screen
        Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, maxDistance, layerMask))
        {
            Debug.DrawLine(ray.origin, hit.point, Color.red); 
            CurrentTarget = hit.collider.GetComponentInParent<IInteractable>();


        }
        else
        {
            Debug.DrawLine(ray.origin, ray.origin + ray.direction * maxDistance, Color.green); 
            CurrentTarget = null;
        }
    }

    // Puts an item in the hand: freezes physics and disables its colliders so it doesn't block the aim ray
    public void HoldItem(Transform item){
        if(item == null || isEmpty == false) return;
        Rigidbody rb = item.GetComponent<Rigidbody>();
        if(rb != null) rb.constraints = RigidbodyConstraints.FreezeAll;
        foreach(Collider c in item.GetComponentsInChildren<Collider>()) c.enabled = false;
        item.position = handPos.position;
        item.parent = handPos;
        isEmpty = false;
    }

    public void PickUp(){
        // Cast a ray from the center of the screen
        Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, maxDistance, layerMask))
        {
            if(hit.transform.CompareTag("Item") && isEmpty == true){
                HoldItem(hit.transform);
            }
            else{
                Debug.Log("Not an Item");
            }

        }
        else
        {
            Debug.DrawLine(ray.origin, ray.origin + ray.direction * maxDistance, Color.green); 

        }
    }

    public Transform checkHand(){
        if(handPos.childCount > 0){
            isEmpty = false;
            Transform itemInhand = handPos.GetChild(0);
            return itemInhand;
        }
        else{
            isEmpty = true;
            return null;
        }
    }

    public void Throw(){
        if (isEmpty == false){
            Transform itemInhand = checkHand();
            Rigidbody item = itemInhand.GetComponent<Rigidbody>();
            itemInhand.parent = null;
            foreach(Collider c in itemInhand.GetComponentsInChildren<Collider>()) c.enabled = true;
            if(item != null){
                item.constraints = RigidbodyConstraints.None;
                item.AddForce(CameraMain.forward * power, ForceMode.Impulse);
            }
            isEmpty = true;
        }
        else{
            Debug.Log("No Item in hand");
        }
    }

    public void Interact(){
        Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, maxDistance, layerMask))
        {
            // Drink stations, counter, spirit house...
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
            if(interactable != null){
                interactable.Interact(this);
                return;
            }

            if(hit.transform.CompareTag("Computer")){
                computer.SetActive(true);
            }
            else{
                Debug.Log("No Computer");
            }
            
            if(hit.transform.CompareTag("Item") && isEmpty == true){
                HoldItem(hit.transform);
            }
            else{
                Debug.Log("Not an Item");
            }

        }
        else
        {

        }
    }
        
    public void RotatingItem(Transform itemInhand){
        if(handPos.childCount > 0){
            itemInhand.rotation = Player.rotation;
        }
        else{
        }
    }


}
