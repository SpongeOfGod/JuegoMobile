using UnityEngine;

namespace Gluttony
{
    public class SeaMine : Enemy
    {
        private const float ExplosionFrameTime = 0.04f;

        [SerializeField] private Sprite[] explosionFrames = new Sprite[0];

        private bool exploding;
        private float explosionTime;

        public override bool Edible => false;
        public override bool Solid => !exploding;

        protected override void OnBeat(int beat) { }

        public override void OnCatContact(Skewer skewer)
        {
            if (!Cat.Health.TakeHit())
                return;
            Sfx.Play(SfxId.Explosion);
            Fx.Burst(HitRect.center, Palette.Red, 24);
            exploding = true;
        }

        protected override void LateUpdate()
        {
            if (!exploding)
            {
                base.LateUpdate();
                return;
            }
            int frame = (int)(explosionTime / ExplosionFrameTime);
            if (frame >= explosionFrames.Length)
            {
                Consume();
                return;
            }
            sprite.sprite = explosionFrames[frame];
            explosionTime += Time.deltaTime;
        }
    }
}
