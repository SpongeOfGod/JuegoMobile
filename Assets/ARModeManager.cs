using UnityEngine;
using UnityEngine.UI;

public class ARModeManager : MonoBehaviour
{
    enum currentMode {scanning, placing }
    currentMode CurrentMode;
    [SerializeField] GameObject[] ScanningObjects;
    [SerializeField] GameObject[] PlacingObjects;
    [SerializeField] Image ButtonSprite;
    [SerializeField] Sprite ScanningSprite;
    [SerializeField] Sprite PlacingSprite;

    public void SwitchModes() 
    {
        CurrentMode = CurrentMode == currentMode.scanning ? currentMode.placing : currentMode.scanning;
        bool scanning = CurrentMode == currentMode.scanning;
        ButtonSprite.sprite = scanning ? ScanningSprite : PlacingSprite;
        for (int i = 0; i < ScanningObjects.Length; i++)
            ScanningObjects[i].SetActive(scanning);
        for (int i = 0; i < PlacingObjects.Length; i++)
            PlacingObjects[i].SetActive(!scanning);
    }
}
