using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class TouchScreenCamera : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [HideInInspector]
    public Vector2 TouchDist;
    [HideInInspector]
    public Vector2 PointerOld;
    [HideInInspector]
    protected int PointerId = -1;
    [HideInInspector]
    public bool Pressed;

    // Use this for initialization
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (Pressed)
        {
            if (PointerId >= 0)
            {
                bool found = false;
                for (int i = 0; i < Input.touches.Length; i++)
                {
                    var t = Input.touches[i];
                    if (t.fingerId == PointerId)
                    {
                        TouchDist = t.position - PointerOld;
                        PointerOld = t.position;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    // The locked touch is gone (lifted or cancelled) — stop pressing.
                    Pressed = false;
                    TouchDist = Vector2.zero;
                }
            }
            else
            {
                TouchDist = new Vector2(Input.mousePosition.x, Input.mousePosition.y) - PointerOld;
                PointerOld = Input.mousePosition;
            }
        }
        else
        {
            TouchDist = new Vector2();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // Lock the first pointer that presses this control. Ignore others.
        if (!Pressed)
        {
            Pressed = true;
            PointerOld = eventData.position;

            // Prefer resolving the touch's actual fingerId by matching positions
            // (some platforms may not map PointerEventData.pointerId to Touch.fingerId reliably).
            PointerId = eventData.pointerId;
            if (Input.touchCount > 0)
            {
                float bestDist = float.MaxValue;
                int bestFinger = -1;
                for (int i = 0; i < Input.touches.Length; i++)
                {
                    var t = Input.touches[i];
                    float d = (t.position - eventData.position).sqrMagnitude;
                    if (d < bestDist)
                    {
                        bestDist = d;
                        bestFinger = t.fingerId;
                    }
                }

                // If a close touch was found, use its fingerId (threshold ~ 50 pixels squared)
                if (bestFinger >= 0 && bestDist <= 50f * 50f)
                {
                    PointerId = bestFinger;
                }
            }
        }
    }


    public void OnPointerUp(PointerEventData eventData)
    {
        // Only release when the locked pointer is lifted.
        if (Pressed && eventData.pointerId == PointerId)
        {
            Pressed = false;
        }
    }
}
