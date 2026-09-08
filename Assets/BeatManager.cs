using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class BeatManager : MonoBehaviour
{
    public static BeatManager Instance;
    [SerializeField] private int bpm;
    private float timePassed;
    public UnityEvent BeatEvent;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else if (Instance != this) 
        {
            Destroy(gameObject);
            return;
        }
    }
    void Update()
    {
        timePassed += Time.deltaTime;
        if (timePassed > 60f / bpm)
        {
            timePassed = 0;
            BeatEvent?.Invoke();
        }
    }
}
