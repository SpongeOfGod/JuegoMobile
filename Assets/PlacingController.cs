using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class PlaceableActor 
{
    public string actorName;
    public Sprite sprite;
    public GameObject actorObj;
}
public class PlacingController : MonoBehaviour
{
    [SerializeField] PlaceableActor[] actors;
    [SerializeField] Image SpriteUsed;
    Dictionary<string, PlaceableActor> actorsDatabase = new();
    List<PlaceableActor> Availableactors = new();
    UnlockManager unlockManager;
    int index = 0;
    bool initialized;
    private void Start()
    {
        if (!initialized)
            Initialize();
    }
    private void Initialize()
    {
        unlockManager = FindAnyObjectByType<UnlockManager>();
        for (int i = 0; i < actors.Length; i++)
        {
            var actor = actors[i];
            actorsDatabase.TryAdd(actor.actorName, actor);
        }
        initialized = true;
    }

    private void OnEnable()
    {
        if (!initialized)
            Initialize();

        Availableactors = new();
        for (int i = 0; i < unlockManager.UnlockedCards.Count; i++)
        {
            var key = unlockManager.UnlockedCards[i];
            if (actorsDatabase.ContainsKey(key))
                Availableactors.Add(actorsDatabase[key]);
        }

        if (Availableactors.Count == 0)
            return;

        Availableactors[index].actorObj.SetActive(true);
        SpriteUsed.sprite = Availableactors[index].sprite;
    }

    public void ChangeIndex(int change) 
    {
        if (Availableactors.Count == 0)
            return;

        Availableactors[index].actorObj.SetActive(false);
        var result = index + change;
        index = result >= Availableactors.Count ? 0 : result < 0 ? Availableactors.Count - 1 : result;
        Availableactors[index].actorObj.SetActive(true);
        SpriteUsed.sprite = Availableactors[index].sprite;
    }
}
