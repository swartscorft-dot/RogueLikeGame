using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace Roguelike
{
    /// <summary>
    /// Makes enemies at run time (work order 008), for generated levels and for the practice room's spawn keys. No
    /// prefab or asset: a brute is a copy of the practice enemy in the scene, as before; the other kinds are stand-ins
    /// built here, a coloured capsule of the kind's size on a NavMeshAgent, with no collider (as the brute has none),
    /// a copy of the practice enemy's telegraph, and its strike material. Each kind reads at a glance from the camera:
    /// the swarmer small and pale, the shooter thin, tall and yellow, the guard large and dark red.
    ///
    /// Every one is made where it will stand, so that is its home, and is told who the player is from the practice
    /// enemy it copies; without a practice enemy in the scene nothing is made.
    ///
    /// Since work order 011 each wears its kind's model over the capsule when the practice enemy's values have
    /// <see cref="EnemyValues.UseModels"/> on (<see cref="EnemyLook"/>, put on by <see cref="Enemy"/> as it wakes): the
    /// capsule is still made, and switched off. A brute copied from the practice enemy takes off the model it brought
    /// along and puts on its own.
    /// </summary>
    public static class EnemyFactory
    {
        /// <summary>The start of the name of a material made here for one enemy; it goes with the enemy.</summary>
        public const string OwnMaterialPrefix = "EnemyBody_";

        public static readonly Color SwarmerColor = new Color(0.88f, 0.86f, 0.78f);
        public static readonly Color ShooterColor = new Color(0.95f, 0.80f, 0.15f);
        public static readonly Color GuardColor = new Color(0.45f, 0.08f, 0.08f);
        public static readonly Color BossColor = new Color(0.25f, 0.22f, 0.30f); // work order 014: the act boss's capsule
        public static readonly Color CasterColor = new Color(0.55f, 0.35f, 0.85f); // work order 027: the caster's capsule
        /// <summary>
        /// The bomber's colour (work order 073): ember orange, its capsule's and the tint over the swarmer's model it wears;
        /// redder than the angry boss's orange (1, 0.55, 0.32) and far from the elite's gold.
        /// </summary>
        public static readonly Color BomberTint = new Color(1f, 0.38f, 0.1f);
        /// <summary>
        /// The warder's colour (work order 079): a deep green, its capsule's and the tint over the caster's model it wears; its
        /// wards' light is the brighter green of <see cref="EnemyWarder.WardColor"/>. Far from the daze's pale blue, the
        /// elite's gold and the bomber's ember.
        /// </summary>
        public static readonly Color WarderTint = new Color(0.25f, 0.75f, 0.45f);
        /// <summary>
        /// An elite's tint (work order 017): a bright gold, warm, and far from the daze's pale blue and the angry boss's
        /// orange (1, 0.55, 0.32).
        /// </summary>
        public static readonly Color EliteTint = new Color(1f, 0.86f, 0.22f);

        /// <summary>
        /// One enemy of a kind, standing at <paramref name="at"/> (on the ground), under <paramref name="parent"/>.
        /// It stays dead once killed. Null without a template.
        /// </summary>
        public static Enemy Make(EnemyKind kind, Vector3 at, Transform parent, EnemyBrute template, string name)
        {
            if (template == null) return null;
            Enemy made;
            if (kind == EnemyKind.Brute)
            {
                // Made where it will stand, so that is its home (Enemy reads it in Awake).
                GameObject copy = Object.Instantiate(template.gameObject, new Vector3(at.x, template.transform.position.y, at.z),
                                                     Quaternion.LookRotation(Vector3.back), parent);
                // The practice enemy may be dead and hidden at this moment; its copy starts alive and seen.
                foreach (Renderer r in copy.GetComponentsInChildren<Renderer>(true))
                    if (!(r is LineRenderer)) r.enabled = true;
                // A copy of the practice enemy carries the pips of its marks, if it was ever marked: not this one's.
                foreach (MarkPips pips in copy.GetComponentsInChildren<MarkPips>(true)) Object.Destroy(pips.gameObject);
                made = copy.GetComponent<EnemyBrute>();
            }
            else
            {
                made = Build(kind, at, parent, template);
            }
            made.name = name;
            made.ComesBack = false;
            return made;
        }

        static Enemy Build(EnemyKind kind, Vector3 at, Transform parent, EnemyBrute template)
        {
            var go = new GameObject(kind.ToString());
            go.SetActive(false); // Awake waits until it is put together
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(new Vector3(at.x, 0f, at.z), Quaternion.LookRotation(Vector3.back));

            float radius, height;
            Color color;
            Enemy enemy;
            switch (kind)
            {
                case EnemyKind.Swarmer:
                {
                    var s = go.AddComponent<EnemySwarmer>();
                    radius = s.Tuning.BodyRadius;
                    height = s.Tuning.BodyHeight;
                    color = SwarmerColor;
                    enemy = s;
                    break;
                }
                case EnemyKind.Shooter:
                {
                    var s = go.AddComponent<EnemyShooter>();
                    radius = s.Tuning.BodyRadius;
                    height = s.Tuning.BodyHeight;
                    color = ShooterColor;
                    enemy = s;
                    break;
                }
                case EnemyKind.Caster:
                {
                    // Work order 027: thin, of a person's height, violet.
                    var c = go.AddComponent<EnemyCaster>();
                    radius = c.Tuning.BodyRadius;
                    height = c.Tuning.BodyHeight;
                    color = CasterColor;
                    enemy = c;
                    break;
                }
                case EnemyKind.Bomber:
                {
                    // Work order 073: small, fast, ember orange; shown smaller than a swarmer, whose model it wears.
                    var b = go.AddComponent<EnemyBomber>();
                    radius = b.Tuning.BodyRadius;
                    height = b.Tuning.BodyHeight;
                    color = BomberTint;
                    go.transform.localScale = Vector3.one * Mathf.Max(0.1f, b.Tuning.Shown);
                    enemy = b;
                    break;
                }
                case EnemyKind.Warder:
                {
                    // Work order 079: slow, at the back, green; shown larger than a caster, whose model it wears.
                    var w = go.AddComponent<EnemyWarder>();
                    radius = w.Tuning.BodyRadius;
                    height = w.Tuning.BodyHeight;
                    color = WarderTint;
                    go.transform.localScale = Vector3.one * Mathf.Max(0.1f, w.Tuning.Shown);
                    enemy = w;
                    break;
                }
                case EnemyKind.Boss:
                {
                    // Work order 014: the act boss, its capsule as large as the knight.
                    var b = go.AddComponent<EnemyBoss>();
                    radius = b.Tuning.BodyRadius;
                    height = b.Tuning.BodyHeight;
                    color = BossColor;
                    b.Template = template;
                    enemy = b;
                    break;
                }
                default:
                {
                    var g = go.AddComponent<EnemyGuard>();
                    radius = g.Tuning.BodyRadius;
                    height = g.Tuning.BodyHeight;
                    color = GuardColor;
                    enemy = g;
                    break;
                }
            }

            var damageable = go.GetComponent<Damageable>();
            damageable.Radius = radius;
            NavMeshAgent agent = go.GetComponent<NavMeshAgent>();
            agent.radius = radius;
            agent.height = height;
            agent.baseOffset = 0f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            agent.avoidancePriority = kind == EnemyKind.Boss ? 20 : kind == EnemyKind.Guard ? 30 : kind == EnemyKind.Swarmer ? 70 : 50;

            // The body: a capsule standing on the ground, no collider.
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            Object.Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            body.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            var bodyRenderer = body.GetComponent<Renderer>();
            Renderer templateBody = BodyOf(template);
            Material material = templateBody != null && templateBody.sharedMaterial != null
                ? new Material(templateBody.sharedMaterial)
                : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.name = OwnMaterialPrefix + kind;
            Paint(material, color);
            bodyRenderer.sharedMaterial = material;
            var flash = go.AddComponent<HitFlash>();
            flash.Target = bodyRenderer;

            // The telegraph: a copy of the practice enemy's, so it looks the same.
            LineRenderer telegraph = null;
            if (template.Telegraph != null)
            {
                GameObject t = Object.Instantiate(template.Telegraph.gameObject, go.transform, false);
                t.name = "Telegraph";
                telegraph = t.GetComponent<LineRenderer>();
            }

            enemy.Player = template.Player;
            enemy.PlayerHealth = template.PlayerHealth;
            enemy.Telegraph = telegraph;
            enemy.StrikeMaterial = template.StrikeMaterial;
            enemy.BodyRenderer = bodyRenderer;
            enemy.BodyColor = color;
            // The model or the capsule as the practice enemy has it (work order 011): put on in Awake, which follows.
            enemy.Values.UseModels = template.Values.UseModels;
            go.SetActive(true);

            if (damageable.Stagger != null && template.Player != null)
                StaggerBarView.For(damageable, template.Player.ShotFxMaterial);
            return enemy;
        }

        /// <summary>
        /// Makes an enemy just made the elite of an elite room (work order 017), before its first frame: this much health,
        /// its strikes' damage times <paramref name="damageScale"/>, shown <paramref name="sizeScale"/> times as large with its
        /// body as much wider, tinted <see cref="EliteTint"/>, and a stagger bar if its kind has none, of
        /// <paramref name="staggerShare"/> of the guard's (its daze as the guard's). Never the act boss.
        /// </summary>
        public static void MakeElite(Enemy enemy, int health, float damageScale, float sizeScale, float staggerShare)
        {
            if (enemy == null || enemy.Kind == EnemyKind.Boss) return;
            enemy.IsElite = true;
            enemy.HealthOverride = health;
            enemy.DamageScale = damageScale;
            enemy.transform.localScale *= sizeScale;
            Damageable body = enemy.GetComponent<Damageable>();
            body.Radius *= sizeScale;
            if (body.Stagger == null)
            {
                StaggerValues guard = new GuardValues().Stagger;
                body.Stagger = new StaggerBar(new StaggerValues
                {
                    Max = guard.Max * staggerShare, DazeSeconds = guard.DazeSeconds, DazedDamageMultiplier = guard.DazedDamageMultiplier,
                });
                if (enemy.Player != null) StaggerBarView.For(body, enemy.Player.ShotFxMaterial);
            }
            // A bar made before it was scaled (a guard's, made with it) is placed over it as it is shown now (work order 033).
            else if (body.GetComponentInChildren<StaggerBarView>(true) is StaggerBarView bar) bar.PlaceAgain();
            // A brute copied from the practice enemy without its model has no body renderer of its own: it gets one, with
            // its own material, so the tint is on this enemy only.
            if (enemy.BodyRenderer == null)
            {
                foreach (Renderer r in enemy.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is LineRenderer || r.GetComponent<MarkPips>() != null || r.GetComponentInParent<StaggerBarView>() != null || r.sharedMaterial == null) continue;
                    r.sharedMaterial = new Material(r.sharedMaterial) { name = OwnMaterialPrefix + "Elite" };
                    enemy.BodyRenderer = r;
                    break;
                }
            }
            enemy.BodyColor = EliteTint;
            if (enemy.BodyRenderer != null) Paint(enemy.BodyRenderer.sharedMaterial, EliteTint);
        }

        /// <summary>The practice enemy's body: its first renderer that is not a line.</summary>
        static Renderer BodyOf(EnemyBrute template)
        {
            foreach (Renderer r in template.GetComponentsInChildren<Renderer>(true))
                if (!(r is LineRenderer) && r.GetComponent<MarkPips>() == null) return r;
            return null;
        }

        /// <summary>Sets a material's colour, in both the URP name and the old one.</summary>
        public static void Paint(Material m, Color c)
        {
            if (m == null) return;
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        }

        /// <summary>
        /// Where the members of a pack stand: <paramref name="count"/> points evenly round a circle of
        /// <paramref name="radius"/> about the pack's point, the first to the north. One alone stands on the point.
        /// </summary>
        public static List<Vector3> PackPlaces(Vector3 point, int count, float radius)
        {
            var places = new List<Vector3>(count);
            if (count <= 1)
            {
                places.Add(point);
                return places;
            }
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2f / count;
                places.Add(point + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius);
            }
            return places;
        }

        /// <summary>A flat wedge on the ground, for the strike of a cone: gone after <paramref name="seconds"/>.</summary>
        public static GameObject Wedge(Vector3 origin, Vector3 dir, float length, float angle, float seconds)
        {
            const int segments = 16;
            var go = new GameObject("Wedge");
            go.transform.position = origin;
            var mesh = new Mesh { name = "Wedge" };
            var verts = new Vector3[segments + 2];
            var tris = new int[segments * 3];
            verts[0] = Vector3.zero;
            for (int i = 0; i <= segments; i++)
            {
                float yaw = -angle * 0.5f + angle * i / segments;
                verts[i + 1] = Quaternion.Euler(0f, yaw, 0f) * dir.normalized * length;
            }
            for (int i = 0; i < segments; i++)
            {
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = i + 2;
            }
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            Object.Destroy(mesh, seconds);
            return go;
        }
    }
}
