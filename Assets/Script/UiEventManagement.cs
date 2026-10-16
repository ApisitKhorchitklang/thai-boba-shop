using UnityEngine;
using UnityEngine.UI;
public class UiEventManagement : MonoBehaviour
{

    public GameObject PausePanel;

    public GameObject EndDayPanel;


    public void Pause(){
        PausePanel.SetActive(false);
    }

    public void ResumeButton(){
        PausePanel.SetActive(true);
    }

    // Called by UI when player chooses to end the day
    public void EndDayButton()
    {
        // Trigger save and any end-of-day workflows
        SaveSystem.SaveGame();

        // Optionally show a panel confirming the day ended
        if (EndDayPanel != null)
            EndDayPanel.SetActive(true);
    }

}
