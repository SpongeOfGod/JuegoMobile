using System;
using UnityEngine;

namespace Gluttony
{
    public class CatHealth : MonoBehaviour
    {
        [SerializeField] private int maxLives = 3;
        [SerializeField] private float invulnerableSeconds = 1.2f;
        [SerializeField] private SpriteRenderer sprite;

        public event Action<int> LivesChanged;
        public event Action Died;

        public int Lives { get; private set; }
        public int MaxLives => maxLives;
        public bool Invulnerable => Time.time < invulnerableUntil;

        private float invulnerableUntil;
        private CatController cat;

        private void Awake() => cat = GetComponent<CatController>();

        public void ResetLives()
        {
            Lives = maxLives;
            invulnerableUntil = 0f;
            LivesChanged?.Invoke(Lives);
        }

        public bool TakeHit()
        {
            if (Lives <= 0 || Invulnerable || !cat.ControlEnabled)
                return false;

            Lives--;
            invulnerableUntil = Time.time + invulnerableSeconds;
            Sfx.Play(SfxId.Hurt);
            Fx.Burst(transform.position + Vector3.up * 0.4f, Palette.Red, 14);
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
            LivesChanged?.Invoke(Lives);
            if (Lives <= 0)
                Died?.Invoke();
            return true;
        }

        private void Update()
        {
            if (sprite != null)
                sprite.enabled = !Invulnerable || Mathf.Repeat(Time.time * 12f, 1f) > 0.4f;
        }
    }
}
