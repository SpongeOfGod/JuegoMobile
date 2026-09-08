using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;
public class PlayerController : MonoBehaviour
{
    
    private void Start() => BeatManager.Instance.BeatEvent.AddListener(CheckInput);    
    private void CheckInput() 
    {
        
    }
}
