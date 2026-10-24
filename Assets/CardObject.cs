using UnityEngine;

public class CardObject : MonoBehaviour
{
    [SerializeField] string CardName;
    UnlockManager unlockManager;
    private void Start() => unlockManager = FindAnyObjectByType<UnlockManager>();
    public void UnlockCard() => unlockManager.UnlockCard(CardName);
    public void SetMonster() => unlockManager.SetMonster(CardName);
}
