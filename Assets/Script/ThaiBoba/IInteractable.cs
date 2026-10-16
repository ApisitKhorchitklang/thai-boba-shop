// Anything the player can look at and press "Interact" on (RaycastAim.Interact).
public interface IInteractable
{
    // Short hint shown on the HUD while aiming at this object
    string Prompt(RaycastAim player);

    void Interact(RaycastAim player);
}
