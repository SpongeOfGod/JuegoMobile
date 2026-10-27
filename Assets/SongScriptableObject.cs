using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SongData", menuName = "Data/MusicShop", order = 1)]
public class SongScriptableObject : ScriptableObject
{
    public Song[] AllSongs;
}
[System.Serializable]
public class Song
{
    public string songName;
    public float cost;
    public string ID;
    [HideInInspector] public bool freeTrial;
}