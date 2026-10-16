using UnityEngine;

// Marks the ends of a footpath that PedestrianWalker NPCs walk between.
// Ends with the same group belong to the same footpath (0 = shop side, 1 = opposite side).
public class PedestrianEnd : MonoBehaviour
{
    public int group;
}
