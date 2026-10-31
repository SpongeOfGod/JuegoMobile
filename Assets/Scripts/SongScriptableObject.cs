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
    public string pack;
    public float cost;
    public string ID;
    public bool included;
    public Sprite cover;
    public AudioClip clip;
    public AudioClip dynamicLayer;
    public float bpm = 120f;
    public float firstBeatSeconds;
    public float volume = 1f;
    public float previewSeconds;
    public Vector2[] blackouts;
    public Vector2[] thunders;
    public bool redToGreen;
    public bool speedsUpWithScore;
}
