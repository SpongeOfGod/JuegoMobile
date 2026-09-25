using UnityEngine;

namespace Gluttony
{
    public class SeaMine : Enemy
    {
        public override bool Edible => false;
        public override bool Solid => true;

        protected override void OnBeat(int beat) { }

        public override void OnCatContact(Skewer skewer)
        {
            if (!Cat.Health.TakeHit())
                return;
            Sfx.Play(SfxId.Explosion);
            Fx.Burst(HitRect.center, Palette.Red, 24);
            Consume();
        }
    }
}
