using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShopController : MonoBehaviour
{
    [SerializeField] GameObject UIBuySong;
    [SerializeField] SongButton SongButtonPrefab;
    [SerializeField] Transform SongContainer;
    [SerializeField] SongScriptableObject songScriptable;
    [SerializeField] GameObject FreeTrialButton;
    [SerializeField] int SongsInShop;
    List<SongButton> songButtons = new();
    List<string> AvailableSongs = new();
    Song SongSelected;
    bool FreeSongSelected;
    int indexButton;
    private void Start()
    {
        for (int i = 0; i < SongsInShop; i++)
        {
            var songButton = Instantiate(SongButtonPrefab, SongContainer);
            songButtons.Add(songButton);
        }
        for (int i = 0; i < songButtons.Count; i++) 
        {
            indexButton = i;
            songButtons[i].Initialize();
        }
    }
    public void ToggleBuySongUI(bool toggle) => UIBuySong.SetActive(toggle);
    public Song GetAvailableSong() 
    {
        for (int i = 0; i < songScriptable.AllSongs.Length; i++)
        {
            var song = songScriptable.AllSongs[i];
            var Song = new Song();
            Song.songName = song.songName;
            Song.cost = song.cost;
            Song.ID = song.ID;

            if (!FreeSongSelected)
                Song.freeTrial = Random.value > 0.5f;

            if (AvailableSongs.Contains(Song.songName)) 
               continue; 

            
            AvailableSongs.Add(Song.songName);
            if (!FreeSongSelected && !Song.freeTrial && indexButton == songButtons.Count - 1)
            {
                Song.freeTrial = true;
                FreeSongSelected = true;
            }
            if (Song.freeTrial) 
            {
                FreeSongSelected = true;
                songButtons[indexButton].SetStyleFreeTrial(true);
            }
            return Song;
        }
        return null;
    }
    public void SelectSong(Song song) 
    {
        SongSelected = song;
        FreeTrialButton.SetActive(song.freeTrial);
    }
    public void BuySong() 
    {
        if (!AvailableSongs.Contains(SongSelected.songName))
            return;

        PlayerPrefs.SetString(SongSelected.ID, "True");
        ToggleBuySongUI(false);
    }
}