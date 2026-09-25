using System.Collections.Generic;
using UnityEngine;

namespace Gluttony
{
    public abstract class Enemy : BeatBehaviour
    {
        public static readonly List<Enemy> All = new List<Enemy>();

        [SerializeField] protected SpriteRenderer sprite;
        [SerializeField] private Sprite chunk;
        [SerializeField] private Vector2 hitSize = new Vector2(0.6f, 0.6f);

        public LaunchInfo Launch { get; set; }
        public double ImpactBeats { get; set; }
        public bool Committed { get; set; }
        public bool Missed { get; private set; }
        public Sprite Chunk => chunk;
        public virtual bool Solid => !Missed;
        public virtual bool Edible => true;

        public virtual Rect HitRect
        {
            get
            {
                Vector2 center = transform.position;
                return new Rect(center - hitSize * 0.5f, hitSize);
            }
        }

        public double ImpactBeat => Launch != null && Launch.Beat != int.MinValue ? Launch.Beat + ImpactBeats : double.NaN;

        protected static CatController Cat => CatController.Instance;

        protected virtual void OnEnable() => All.Add(this);
        protected virtual void OnDisable() => All.Remove(this);

        public virtual void PlaceForImpact(float x, float impactY)
        {
            transform.position = new Vector3(x, impactY + hitSize.y * 0.5f, 0f);
        }

        public virtual void OnCatContact(Skewer skewer) { }

        public void Miss() => Missed = true;

        public void Consume()
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        protected static int Mod(int a, int n) => ((a % n) + n) % n;
    }
}
