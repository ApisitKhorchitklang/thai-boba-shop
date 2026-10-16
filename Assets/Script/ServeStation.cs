using UnityEngine;

public class ServeStation : MonoBehaviour, IInteractable
{
    // Customer currently at the counter
    private CustomerController currentCustomer;

    public AudioClip serveSfx;

    // Front of the queue wins; the trigger is a fallback for scenes without queuing
    public CustomerController CurrentCustomer
    {
        get
        {
            CustomerController front = CustomerController.Front;
            if (front != null && front.IsWaitingAtCounter) return front;
            return currentCustomer;
        }
    }

    // Called by trigger when customer arrives
    void OnTriggerEnter(Collider other)
    {
        CustomerController cc = other.GetComponent<CustomerController>();
        if (cc != null)
        {
            currentCustomer = cc;
            Debug.Log("Customer arrived at station: " + cc.name);
        }
    }

    void OnTriggerExit(Collider other)
    {
        CustomerController cc = other.GetComponent<CustomerController>();
        if (cc != null && currentCustomer == cc)
        {
            currentCustomer = null;
            Debug.Log("Customer left station: " + cc.name);
        }
    }

    // Serve a prepared cup to the current customer. Returns true if served.
    public bool ServeCup(CupController cup)
    {
        CustomerController customer = CurrentCustomer;
        if (customer == null)
        {
            ShopHUD.ToastNow(Loc.T("ยังไม่มีลูกค้าที่เคาน์เตอร์", "No customer at the counter yet"));
            return false;
        }

        if (cup == null)
        {
            ShopHUD.ToastNow(Loc.T("ถือแก้วที่ทำเสร็จแล้วก่อนนะ", "Hold a finished drink first"));
            return false;
        }

        bool ok = customer.TryServe(cup);
        if (!ok) GameAudio.Play(Sfx.Reject);
        if (ok)
        {
            if (serveSfx != null)
                AudioSource.PlayClipAtPoint(serveSfx, transform.position);
            Destroy(cup.gameObject);
        }
        return ok;
    }

    // IInteractable: look at the counter and press Interact while holding a cup
    public string Prompt(RaycastAim player)
    {
        CustomerController c = CurrentCustomer;
        if (c == null) return Loc.T("เคาน์เตอร์ (รอลูกค้า)", "Counter (waiting for customers)");
        return player.HeldCup != null ? Loc.T("เสิร์ฟให้ลูกค้า", "Serve the customer") : Loc.T("ลูกค้าสั่ง: ", "Order: ") + c.order.ToThaiString();
    }

    public void Interact(RaycastAim player)
    {
        ServeCup(player.HeldCup);
    }
}
