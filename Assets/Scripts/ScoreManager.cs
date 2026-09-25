using UnityEngine;

namespace Gluttony
{
    public class ScoreManager : MonoBehaviour
    {
        private const string BestKey = "record";

        public static ScoreManager Instance { get; private set; }

        [SerializeField] private int pointsPerUnit = 3;
        [SerializeField] private int skewerPoints = 5;
        [SerializeField] private int bitePoints = 5;
        [SerializeField] private int feastStep = 16;

        public int Score => heightPoints + bonusPoints;
        public int Best { get; private set; }

        private int heightPoints;
        private int bonusPoints;
        private float top;
        private float climbed;

        private void Awake()
        {
            Instance = this;
            Best = PlayerPrefs.GetInt(BestKey, 0);
        }

        public void BeginRun(float height)
        {
            top = height;
            climbed = 0f;
            heightPoints = 0;
            bonusPoints = 0;
            Best = PlayerPrefs.GetInt(BestKey, 0);
        }

        public void SetHeight(float height)
        {
            if (height <= top)
                return;
            climbed += height - top;
            top = height;
            heightPoints = Mathf.FloorToInt(climbed * pointsPerUnit);
        }

        public void SkipTo(float height) => top = Mathf.Max(top, height);

        public void AddSkewer() => bonusPoints += skewerPoints;

        public int AddBite(int feastSize)
        {
            int points = bitePoints * (1 + feastSize / Mathf.Max(1, feastStep));
            bonusPoints += points;
            return points;
        }

        public void Commit()
        {
            if (Score <= Best)
                return;
            Best = Score;
            PlayerPrefs.SetInt(BestKey, Best);
            PlayerPrefs.Save();
        }
    }
}
