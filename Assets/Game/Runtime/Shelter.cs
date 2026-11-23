using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// Marks something she can take cover behind (taking cover, Kristian 2026-11-19, an experiment): every wall and every
    /// prop that blocks walking, tall or low. A generated level puts it on each of them; the practice room has none.
    /// Whether it is tall cover is whether it also carries <see cref="Cover"/>, which stops shots for anyone; a low one
    /// covers her only while she is ducked behind it (<see cref="TakeCoverRules"/>).
    ///
    /// Its footprint (<see cref="TryShape"/>) comes from its collider: a box collider gives a turned box, an upright
    /// capsule a circle. Nothing here asks physics anything.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Shelter : MonoBehaviour
    {
        static readonly List<Shelter> s_all = new List<Shelter>();

        /// <summary>Every enabled shelter in the scene.</summary>
        public static IReadOnlyList<Shelter> All => s_all;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_all.Clear();

        void OnEnable() => s_all.Add(this);

        void OnDisable() => s_all.Remove(this);

        /// <summary>
        /// True when it stops shots for anyone, standing or ducked: it carries <see cref="Cover"/> that stops now (work
        /// order 043: not a prop the quick duck sorts low while its switch is on, <see cref="Cover.Stops"/>). A shelter
        /// that is not tall is a low prop: the quick duck works beside it.
        /// </summary>
        public bool IsTall => TryGetComponent(out Cover cover) && cover.Stops;

        /// <summary>
        /// A low prop by the one sorted table (<see cref="PropHeights.Low"/>, what the greybox draws blue; work order 092): the sprint
        /// can jump it. Set by the level for each prop it builds; a wall, and a shelter made elsewhere, is not one.
        /// </summary>
        public bool LowProp;

        /// <summary>Its footprint on the ground, from its box or upright capsule collider; false with neither.</summary>
        public bool TryShape(out CoverShape shape)
        {
            Vector3 scale = transform.lossyScale;
            if (TryGetComponent(out BoxCollider box) && box.enabled)
            {
                Vector3 c = transform.TransformPoint(box.center);
                shape = CoverShape.Box(new GroundPoint(c.x, c.z), box.size.x * Mathf.Abs(scale.x), box.size.z * Mathf.Abs(scale.z),
                                       transform.eulerAngles.y);
                return true;
            }
            if (TryGetComponent(out CapsuleCollider capsule) && capsule.enabled && capsule.direction == 1)
            {
                Vector3 c = transform.TransformPoint(capsule.center);
                shape = CoverShape.Circle(new GroundPoint(c.x, c.z), capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)));
                return true;
            }
            shape = default;
            return false;
        }
    }
}
