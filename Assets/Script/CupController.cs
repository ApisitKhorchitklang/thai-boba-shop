using UnityEngine;

public class CupController : MonoBehaviour
{
    public enum CupState { Empty, Held, BobaAdded, IceAdded, Filled, LidClosed }

    public CupState State { get; private set; } = CupState.Empty;

    public int bobaCount = 0;
    public bool hasIce = false;
    public string flavor = "";
    public int sweetness = 100;
    public int maxBoba = 3;

    // Fired whenever the cup contents change (CupVisual listens to this)
    public event System.Action<CupController> OnChanged;

    // Ingredients can go in any order until the lid is sealed
    bool Open => State != CupState.Empty && State != CupState.LidClosed;
    public bool CanAddBoba => Open && bobaCount < maxBoba;
    public bool CanAddIce => Open && !hasIce;
    public bool CanFill => Open && string.IsNullOrEmpty(flavor);
    public bool CanSeal => State == CupState.Filled;
    public bool CanChangeSweetness => State != CupState.Empty && State != CupState.LidClosed;

    void Changed()
    {
        OnChanged?.Invoke(this);
    }

    // Called when player grabs the cup
    public bool Grab()
    {
        if (State == CupState.Empty)
        {
            State = CupState.Held;
            Changed();
            return true;
        }
        return false;
    }

    // Add boba pearls - allowed when held
    public bool AddBoba(int amount)
    {
        if (CanAddBoba)
        {
            bobaCount = Mathf.Min(maxBoba, bobaCount + Mathf.Max(0, amount));
            if (State == CupState.Held) State = CupState.BobaAdded;
            Changed();
            return true;
        }
        return false;
    }

    // Add ice - allowed after boba or while held
    public bool AddIce()
    {
        if (CanAddIce)
        {
            hasIce = true;
            if (State == CupState.Held || State == CupState.BobaAdded) State = CupState.IceAdded;
            Changed();
            return true;
        }
        return false;
    }

    // Fill drink with a flavor
    public bool Fill(string drinkFlavor)
    {
        if (CanFill)
        {
            flavor = drinkFlavor;
            State = CupState.Filled;
            Changed();
            return true;
        }
        return false;
    }

    // Sweetness can be adjusted any time before sealing
    public bool SetSweetness(int level)
    {
        if (CanChangeSweetness)
        {
            sweetness = level;
            Changed();
            return true;
        }
        return false;
    }

    // Close the lid
    public bool CloseLid()
    {
        if (State == CupState.Filled)
        {
            State = CupState.LidClosed;
            Changed();
            return true;
        }
        return false;
    }

    // Reset cup to empty (e.g., after sale)
    public void ResetCup()
    {
        State = CupState.Empty;
        bobaCount = 0;
        hasIce = false;
        flavor = "";
        sweetness = 100;
        Changed();
    }

    // Quick validation against a simple order
    public bool MatchesOrder(int expectedBoba, bool expectedIce, string expectedFlavor)
    {
        return bobaCount == expectedBoba && hasIce == expectedIce && flavor == expectedFlavor && State == CupState.LidClosed;
    }

    public bool MatchesOrder(Order order)
    {
        return order != null && MatchesOrder(order.bobaCount, order.wantsIce, order.flavor) && sweetness == order.sweetness;
    }
}
