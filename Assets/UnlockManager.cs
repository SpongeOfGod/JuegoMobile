using Gluttony;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnlockManager : MonoBehaviour
{
    HashSet<string> unlockedCards = new();
    [SerializeField] PixelLabel MonsterName;
    [SerializeField] PixelLabel UnlockedMonsters;
    [SerializeField] string UnlockedMonstersText;
    public void UnlockCard(string cardName) 
    {
        if (unlockedCards.Contains(cardName))
            return;

        unlockedCards.Add(cardName);
        UnlockedMonsters.Text = UnlockedMonstersText + $"{unlockedCards.Count}";
    }
    public void SetMonster(string name) => MonsterName.Text = name;
    public void ClearMonster() => MonsterName.Text = "------";
}
