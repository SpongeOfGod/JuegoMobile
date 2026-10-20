using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShopController : MonoBehaviour
{
    [SerializeField] GameObject UIBuySong;
    [SerializeField] SongScriptableObject songScriptable;
    List<Song> AvailableSongs = new();
    Song SongSelected;
    public void ToggleBuySongUI(bool toggle) => UIBuySong.SetActive(toggle);
    public Song GetAvailableSong() 
    {
        for (int i = 0; i < songScriptable.AllSongs.Length; i++)
        {
            var song = songScriptable.AllSongs[i];
            if (AvailableSongs.Contains(song)) 
               continue; 
            AvailableSongs.Add(song);
            return song;
        }
        return null;
    }
    public void SelectSong(Song song) => SongSelected = song;
    public void BuySong() 
    {
        if (!AvailableSongs.Contains(SongSelected))
            return;

        PlayerPrefs.SetString(SongSelected.ID, "True");
        ToggleBuySongUI(false);
    }
}