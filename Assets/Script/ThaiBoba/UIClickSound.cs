using UnityEngine;
using UnityEngine.UI;

// Plays the UI click sound when this button is pressed.
[RequireComponent(typeof(Button))]
public class UIClickSound : MonoBehaviour
{
    void Awake()
    {
        GetComponent<Button>().onClick.AddListener(() => GameAudio.Play(Sfx.Click));
    }
}
