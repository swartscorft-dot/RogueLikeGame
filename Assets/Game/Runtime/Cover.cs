using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// Marks an object as cover: its colliders stop her shots and skills and the shooter's line (Kristian, 2026-11-19:
    /// "Skudd skal stoppes av vegg og søyler", shots are to be stopped by walls and pillars). A generated level puts it
    /// on every wall, at any height, and on every prop at least <see cref="PropValues.CoverHeight"/> tall
    /// (<see cref="LevelProp.IsCover"/>). A lower prop has none: its collider blocks walking, and she shoots over it.
    /// Characters, enemies and dummies never carry it. The project has no layer to spare, so this, not a layer, tells
    /// cover from the low props on the same (the walls') layer.
    ///
    /// The query (<see cref="ClearDistance(Vector3, Vector3, float, float)"/>) is a physics ray, level, at
    /// <see cref="RayHeight"/> over the ground (under the 1 m near walls' top, inside every collider that is cover),
    /// against the layers cover stands on, that counts only colliders with this on them or on a parent. With no cover
    /// anywhere (the practice room) it asks physics nothing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Cover : MonoBehaviour
    {
        /// <summary>Metres over the ground of the line her shots are tested for cover along.</summary>
        public const float RayHeight = 0.5f;

        static readonly RaycastHit[] s_hits = new RaycastHit[32];
        static int s_count;
        static int s_layers;
        static bool s_unsynced;

        /// <summary>The query for the rules: how far from one ground point toward another before cover.</summary>
        public static readonly CoverLine Line = ClearDistance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_count = 0;
            s_layers = 0;
            s_unsynced = false;
        }

        /// <summary>True while any cover is in the scene.</summary>
        public static bool Any => s_count > 0;

        void OnEnable()
        {
            s_count++;
            s_layers |= 1 << gameObject.layer;
            s_unsynced = true; // made this frame: its collider may not be where its transform is until physics is synced
        }

        void OnDisable() => s_count--;

        /// <summary>Metres from one ground point toward another, at <see cref="RayHeight"/>, before the first cover; the whole distance when clear.</summary>
        public static float ClearDistance(GroundPoint from, GroundPoint to)
        {
            Vector3 d = new Vector3(to.X - from.X, 0f, to.Z - from.Z);
            float length = d.magnitude;
            if (length < 1e-5f) return length;
            return ClearDistance(new Vector3(from.X, 0f, from.Z), d / length, length, RayHeight);
        }

        /// <summary>
        /// Metres along a level direction from a point on the ground, up to <paramref name="max"/>, before the first
        /// cover, at <paramref name="height"/> over the ground; <paramref name="max"/> when clear. A ray that starts
        /// inside a collider does not see it.
        /// </summary>
        public static float ClearDistance(Vector3 ground, Vector3 flatDir, float max, float height)
        {
            if (s_count <= 0 || max <= 0f) return max;
            if (s_unsynced)
            {
                Physics.SyncTransforms();
                s_unsynced = false;
            }
            Vector3 from = new Vector3(ground.x, height, ground.z);
            int n = Physics.RaycastNonAlloc(from, flatDir, s_hits, max, s_layers, QueryTriggerInteraction.Ignore);
            float nearest = max;
            for (int i = 0; i < n; i++)
            {
                Collider c = s_hits[i].collider;
                if (c == null || s_hits[i].distance >= nearest) continue;
                Cover cover = c.GetComponentInParent<Cover>();
                if (cover == null || !cover.enabled) continue;
                nearest = s_hits[i].distance;
            }
            return nearest;
        }
    }
}
