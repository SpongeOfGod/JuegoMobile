using Gluttony;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SongButton : MonoBehaviour
{
    ShopController shopController;
    [SerializeField] PixelLabel pixelLabel;
    [SerializeField] string NoSongs;
    Button button;
    Song song;

    private void Start()
    {
        button = GetComponent<Button>();
        shopController = GetComponentInParent<ShopController>();
        song = shopController.GetAvailableSong();
        pixelLabel.Text = song == null ? NoSongs : song.songName;
        button.onClick.AddListener(() => 
        {
            if (PlayerPrefs.GetString(song.ID) == string.Empty)
                shopController.ToggleBuySongUI(true);
        }
        );
        button.onClick.AddListener(() => shopController.SelectSong(song));
    }

    private void Update()
    {
        if (song == null)
            return;

        var value = PlayerPrefs.GetString(song.ID);
        pixelLabel.Text = value != string.Empty ? NoSongs : song.songName;
    }
}