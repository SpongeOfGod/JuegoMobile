using UnityEngine;

namespace Gluttony
{
    [DefaultExecutionOrder(10000)]
    public class PixelSnap : MonoBehaviour
    {
        private Vector3 anchor;

        private void Awake() => anchor = transform.localPosition;

        private void LateUpdate()
        {
            var parent = transform.parent;
            Vector3 world = parent != null ? parent.TransformPoint(anchor) : anchor;
            transform.position = new Vector3(PixelPerfectRig.Snap(world.x), PixelPerfectRig.Snap(world.y), world.z);
        }
    }
}
