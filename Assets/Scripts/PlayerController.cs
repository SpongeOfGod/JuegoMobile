using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using static PlayerController;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;
public class PlayerController : MonoBehaviour
{
    [SerializeField] float distanceFromCamera;
    [System.Serializable] public enum PlayerState {idle, move, chargingJump, Jump }
    [SerializeField] public PlayerState playerState;
    [SerializeField] private float thressholdTapMovement;
    [SerializeField] private float timeLimitTap;
    private float timeTap;
    private Camera mainCamera;
    private Vector2 touchInitialPosition;
    float playerYPosition;
    private void Start() 
    {
        mainCamera = Camera.main;
        BeatManager.Instance.BeatEvent.AddListener(CheckInput);
        playerYPosition = transform.position.y;
    }
    private void Update()
    {
        if (Touch.activeTouches.Count < 1)
            return;
        Touch touch = Touch.activeTouches[0];

        if (touch.phase == TouchPhase.Began)
            touchInitialPosition = touch.screenPosition;

        switch (playerState)
        {
            case PlayerState.idle:
                if (timeTap < timeLimitTap) 
                {
                    timeTap += Time.deltaTime;
                    return;
                }
                timeTap = 0;
                playerState = Vector3.Distance(touchInitialPosition, touch.screenPosition) < thressholdTapMovement ? PlayerState.chargingJump : PlayerState.move;
                break;

            case PlayerState.move:
                MovementLogic(touch);
                break;

            case PlayerState.chargingJump:
                JumpChargeLogic(touch);
                break;
        }
    }

    private void MovementLogic(Touch touch)
    {
        if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Began)
        {
            var touchPos = touch.screenPosition;
            var worldPos = mainCamera.ScreenToWorldPoint(new Vector3(touchPos.x, touchPos.y, distanceFromCamera));
            worldPos.y = playerYPosition;
            //worldPos.z = distanceFromCamera;
            var desired = worldPos;
            transform.position = desired;
            Debug.Log($"Touch: {touchPos} - World: {worldPos}");
        }
        else if (touch.phase == TouchPhase.Ended)
            playerState = PlayerState.idle;
    }

    private void JumpChargeLogic(Touch touch) 
    {
        if (touch.phase == TouchPhase.Ended)
            playerState = PlayerState.idle;
    }

    private void CheckInput() 
    {
        
    }
}