using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;

using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

public class TouchManager : MonoBehaviour
{
    private Camera mainCamera;
    [SerializeField] private GameObject prefabTouchIndicator;
    [SerializeField] private Transform TouchParent;
    List<GameObject> TouchIndicators = new();
    Dictionary<GameObject, TextMeshProUGUI> IndicatorAndStatus = new();
    private void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
    }
    private void Update()
    {
        if (Touch.activeTouches.Count == 0) 
        {
            DeactivateAllIndicators();
            return;
        }

        Touch touch = Touch.activeTouches[0];

        int lastIndicator = 0;
        for (int i = 0; i < Touch.activeTouches.Count; i++)
        {
            lastIndicator = i;
            var indicator = GetAvailableIndicator(i);

            if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Began)
                ShowTouch(touch.screenPosition, indicator);
        }
        DeactivateUnusedIndicators(lastIndicator);
    }
    private GameObject GetAvailableIndicator(int TouchIndex)
    {
        GameObject indicator = null;
        if (TouchIndex > TouchIndicators.Count - 1)
        {
            indicator = Instantiate(prefabTouchIndicator, TouchParent);
            TouchIndicators.Add(indicator);
            IndicatorAndStatus.Add(indicator, indicator.GetComponentInChildren<TextMeshProUGUI>());
        }
        else
            indicator = TouchIndicators[TouchIndex];
        return indicator;
    }
    private void DeactivateUnusedIndicators(int TouchIndex) 
    {
        if (TouchIndicators.Count - 1 > TouchIndex) 
            for (int i = TouchIndex; i < TouchIndicators.Count; i++)
                if (TouchIndicators[i].gameObject.activeSelf)
                    TouchIndicators[i].gameObject.SetActive(false);
    }

    private void DeactivateAllIndicators() 
    {
        for (int i = 0; i < TouchIndicators.Count; i++)
            if (TouchIndicators[i].gameObject.activeSelf)
                TouchIndicators[i].gameObject.SetActive(false);
    }
    private void ShowTouch(Vector2 screenPosition, GameObject touchIndicator)
    {
        Vector3 worldPosition = ScreenToWorldPosition(screenPosition, touchIndicator);

        touchIndicator.transform.position = worldPosition;
        touchIndicator.SetActive(true);

        if (IndicatorAndStatus[touchIndicator] != null)
            IndicatorAndStatus[touchIndicator].text = $"Pantalla: {screenPosition}\n" + $"Mundo: {worldPosition}";
    }

    private Vector3 ScreenToWorldPosition(Vector2 screenPosition, GameObject Indicator)
    {
        float indicatorZ = Indicator.transform.position.z;
        float distanceFromCamera = Mathf.Abs(indicatorZ - mainCamera.transform.position.z);
        Vector3 worldPosition = mainCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, distanceFromCamera));
        worldPosition.z = indicatorZ;

        return worldPosition;
    }
    private void OnEnable() => EnhancedTouchSupport.Enable();
    private void OnDisable() => EnhancedTouchSupport.Disable();
}
