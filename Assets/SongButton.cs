using Gluttony;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SongButton : MonoBehaviour
{
    ShopController shopController;
    [SerializeField] PixelLabel pixelLabel;
    [SerializeField] GameObject FreeTrialIcon;
    [SerializeField] Button button;
    Song song;

    public void Initialize()
    {
        shopController = GetComponentInParent<ShopController>();
        song = shopController.GetAvailableSong();
        pixelLabel.Text = song.songName;
        button.onClick.AddListener(() => 
        {
            if (PlayerPrefs.GetString(song.ID) == string.Empty)
                shopController.ToggleBuySongUI(true);
        }
        );
        button.onClick.AddListener(() => shopController.SelectSong(song));
    }
    public void SetStyleFreeTrial(bool toggle) => FreeTrialIcon.SetActive(toggle);
}