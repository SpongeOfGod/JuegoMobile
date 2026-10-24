using UnityEngine;
using UnityEngine.UI;

public class ARModeManager : MonoBehaviour
{
    enum currentMode {scanning, placing }
    currentMode CurrentMode;
    [SerializeField] GameObject ScanningOBJ;
    [SerializeField] GameObject PlacingOBJ;
    [SerializeField] Image ButtonSprite;
    [SerializeField] Sprite ScanningSprite;
    [SerializeField] Sprite PlacingSprite;

    public void SwitchModes() 
    {
        CurrentMode = CurrentMode == currentMode.scanning ? currentMode.placing : currentMode.scanning;
        bool scanning = CurrentMode == currentMode.placing;
        ButtonSprite.sprite = scanning ? ScanningSprite : PlacingSprite;
        ScanningOBJ.SetActive(scanning);
        PlacingOBJ.SetActive(!scanning);
    }
}
