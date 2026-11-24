using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Roguelike
{
    /// <summary>
    /// Light from the lamps on a generated floor (work order 085; Kristian, 2026-11-22, having played work order 083's dark
    /// cave and judged it "Riktig retning, mer arbeid": "det er mørkere i rommet. sånn at vi får sett mere flashing når man
    /// skyter, ikke standar lys over alt, men at lysilider rundt er hovedkilden til sol. så får vi mere hule følelse. vi må seff
    /// ha en grunnlys styurke da som gjør at det ikke blir helt bekmørkt"). The pause menu's "Light" line chooses as before or
    /// one of three lamplit levels (<see cref="LightSteps"/>); the floors' controller adds this to the floor she is on the first
    /// time a lamplit level is chosen there and asks it to follow the setting once a frame (<see cref="Set"/>), so the light can
    /// change in the middle of a run and the floor is never built again. With as before it is not there, or it is off, and
    /// nothing of the floor's light is changed.
    ///
    /// On, at a level:
    /// 1. The even light goes down to the level's base: the rooms' mood is told the level (<see cref="RoomMood.Lamplit"/>) and
    /// turns the scene's ambient and key light down to it, makes her own light a small one over her, lifts the light floor the
    /// enemies are drawn with, and leaves its own few real lights to the lamps here.
    /// 2. The lamps are the main light (<see cref="LampPlan"/>): a real point light without shadows at every thing that burns (the
    /// mood's braziers, basins, candelabras and candles, every lantern of the kit, a soft one over every chest), close ones
    /// sharing one, and sconces where a room has too little of its floor in a pool: a warm flame on an iron bracket on a far wall,
    /// drawn here, with no collider. The lamps of her room and of the rooms and passages a door from it are lit (what the cave
    /// draws), flickering slowly with their flames.
    /// 3. Her shots light the room: a short real light at the muzzle of every shot of hers, where each hit lands, and a larger
    /// one at a blast (<see cref="ShotWentOff"/>, told by every shot effect as it is made), from a pool made once.
    ///
    /// Everything it makes is under its own root beside the level (as the mood's), shown and hidden with the level and
    /// destroyed with it; the greybox view never sees it, and with the greybox on the floors' controller turns it off. Nothing
    /// has a collider and the NavMesh is never touched. Its frame allocates nothing; neither does a shot.
    /// </summary>
    public sealed class LamplitView : MonoBehaviour
    {
        public const string RootName = "Lamplit";

        /// <summary>The lamps' places, reach and colours and the flashes' (the same in every level).</summary>
        public static readonly LampValues Lamps = new LampValues();

        /// <summary>The view that is on now (her floor's, at a lamplit level), or null: the shots light nothing.</summary>
        public static LamplitView Active { get; private set; }

        /// <summary>True while a lamplit level lights her shots: the shot's weight's own muzzle light stands aside (one light at a muzzle, not two).</summary>
        public static bool TakesTheMuzzle => Active != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Active = null;

        struct LampLight
        {
            public Light Light;
            public int Room;
            public int Source;      // its place in the room's mood sources, for the flicker; -1 for none
            public float Intensity; // at level 1, with what it carries
            public float Phase;
        }

        struct Flash
        {
            public Light Light;
            public float At, Peak, Seconds;
            public int Frame;
        }

        GeneratedLevel _level;
        GameObject _root;
        LampPlan _plan;
        LamplitValues _on;
        bool _built;
        int _moodBuilds = -1;
        int _here = -2;
        bool[] _shown;
        PlayerCharacter _player;
        float _lookedForPlayer = float.NegativeInfinity;
        readonly List<LampLight> _lamps = new List<LampLight>();
        readonly List<Renderer> _sconceRenderers = new List<Renderer>();
        readonly List<int> _sconceRoom = new List<int>();
        readonly List<Flash> _flashes = new List<Flash>();
        readonly List<Object> _owned = new List<Object>();

        /// <summary>The level it is on now; null when off (as before, or the greybox view on).</summary>
        public LamplitValues On => _on;
        public GeneratedLevel Level => _level;
        /// <summary>Where the lamps stand, once it was first on; null before.</summary>
        public LampPlan Plan => _plan;
        /// <summary>The lamps' real lights, and how many are on now.</summary>
        public int LampLights => _lamps.Count;
        public int LampLightsOn
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _lamps.Count; i++) if (_lamps[i].Light != null && _lamps[i].Light.enabled) n++;
                return n;
            }
        }
        /// <summary>The sconces' renderers (one bracket mesh and one flame mesh a room that has sconces).</summary>
        public IReadOnlyList<Renderer> SconceRenderers => _sconceRenderers;
        /// <summary>The flash lights made (the pool's size) and how many are lit now.</summary>
        public int FlashesMade => _flashes.Count;
        public int FlashesLit
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _flashes.Count; i++) if (_flashes[i].Light != null && _flashes[i].Light.enabled) n++;
                return n;
            }
        }
        /// <summary>The brightest flash lit now (its intensity), 0 for none: for tests.</summary>
        public float BrightestFlash
        {
            get
            {
                float most = 0f;
                for (int i = 0; i < _flashes.Count; i++)
                    if (_flashes[i].Light != null && _flashes[i].Light.enabled) most = Mathf.Max(most, _flashes[i].Light.intensity);
                return most;
            }
        }
        /// <summary>The flashes asked for since it was made (each a muzzle, a hit or a blast).</summary>
        public int FlashesAsked { get; private set; }
        /// <summary>The root everything is under.</summary>
        public GameObject Root => _root;

        /// <summary>The view of a built floor, off.</summary>
        public static LamplitView Add(GeneratedLevel level)
        {
            var view = level.gameObject.AddComponent<LamplitView>();
            view._level = level;
            view._shown = new bool[level.Layout.Rooms.Count];
            return view;
        }

        /// <summary>Turns it on at a level, or off (null); nothing when it is so already. Allocates only the first time it is on.</summary>
        public void Set(LamplitValues level)
        {
            if (_level == null || level == _on) return;
            if (level != null && !_built) Build();
            _on = level;
            if (_level.Mood != null) _level.Mood.Lamplit = level;
            if (level == null)
            {
                for (int i = 0; i < _lamps.Count; i++) if (_lamps[i].Light != null) _lamps[i].Light.enabled = false;
                for (int i = 0; i < _sconceRenderers.Count; i++) if (_sconceRenderers[i] != null) _sconceRenderers[i].enabled = false;
                for (int i = 0; i < _flashes.Count; i++) if (_flashes[i].Light != null) _flashes[i].Light.enabled = false;
                if (Active == this) Active = null;
                return;
            }
            if (isActiveAndEnabled) Active = this;
            _here = -2;
            Tick();
        }

        void OnEnable()
        {
            if (_root != null) _root.SetActive(true);
            if (_on != null) Active = this;
        }

        void OnDisable()
        {
            if (_root != null) _root.SetActive(false);
            if (Active == this) Active = null;
        }

        void OnDestroy()
        {
            if (Active == this) Active = null;
            if (_level != null && _level.Mood != null && _level.Mood.Lamplit == _on) _level.Mood.Lamplit = null;
            Teardown();
        }

        void Teardown()
        {
            foreach (Object o in _owned)
                if (o != null) Destroy(o);
            _owned.Clear();
            if (_root != null)
            {
                _root.SetActive(false);
                Destroy(_root);
            }
            _root = null;
            _lamps.Clear();
            _sconceRenderers.Clear();
            _sconceRoom.Clear();
            _flashes.Clear();
            _built = false;
        }

        // ---------------- Building ----------------

        void Build()
        {
            _built = true;
            RoomMood mood = _level.Mood;
            _moodBuilds = mood != null ? mood.Builds : -1;
            LampValues v = Lamps;
            _plan = LampPlan.Make(_level.Layout, _level.KitPlan, mood != null && mood.Built ? mood.Plan : null, v);
            _root = new GameObject(RootName + "_" + _level.Layout.SeedText) { layer = CryptKit.Layer };
            SceneManager.MoveGameObjectToScene(_root, _level.gameObject.scene);
            _root.SetActive(_level.gameObject.activeInHierarchy);

            for (int i = 0; i < _plan.Lamps.Count; i++)
            {
                Lamp lamp = _plan.Lamps[i];
                if (lamp.CarriedBy != null) continue;
                var go = new GameObject("Lamp_" + lamp.Kind + "_" + lamp.Room + "_" + _lamps.Count) { layer = CryptKit.Layer };
                go.transform.SetParent(_root.transform, false);
                go.transform.position = _level.ToWorld(lamp.At) + Vector3.up * (float)lamp.Height;
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.shadows = LightShadows.None; // no lamp casts a shadow: see the work order's report (cost, and the kit's walls already shade)
                v.Of(lamp.Kind, out _, out float range);
                light.range = range;
                Rgb c = v.ColourOf(lamp); // work order 100: an entrance brazier warm
                light.color = new Color(c.R, c.G, c.B, 1f);
                light.intensity = 0f;
                light.enabled = false;
                _lamps.Add(new LampLight { Light = light, Room = lamp.Room, Source = lamp.Source, Intensity = lamp.Intensity(v), Phase = i * 1.91f + lamp.Room * 0.37f });
            }
            BuildSconces();
            while (_flashes.Count < Mathf.Max(1, v.MostFlashes)) MakeFlash(); // the flashes' pool, made now: no shot makes one
        }

        /// <summary>The sconces: an iron bracket and a warm flame on the wall's face, one mesh of each a room; off until the view is on.</summary>
        void BuildSconces()
        {
            LampValues v = Lamps;
            CryptKitPlan kit = _level.KitPlan;
            if (kit == null) return;
            CryptKit.Model wall = CryptKit.Load(CryptPiece.Wall);
            double cs = _level.Layout.CellSize;
            var iron = new Dictionary<int, MoodShapes.Build>();
            var flames = new Dictionary<int, MoodShapes.Build>();
            Color core = MoodShapes.Linear(v.Warm, 2.4f), tip = MoodShapes.Linear(v.Warm, 0.8f); // the mood's flames' glow (MoodValues.FlameGlow)
            foreach (Lamp lamp in _plan.Lamps)
            {
                if (lamp.Kind != LampKind.Sconce || lamp.Section < 0) continue;
                CryptKitSection s = kit.Sections[lamp.Section];
                float face = wall == null ? _level.Values.WallThickness * 0.5f : lamp.Side == s.Facing ? wall.Bounds.max.z : -wall.Bounds.min.z;
                Vector3 into = s.Vertical ? new Vector3(lamp.Side, 0f, 0f) : new Vector3(0f, 0f, lamp.Side);
                Vector3 onWall = _level.ToWorld(LampPlan.OnFace(s, lamp.Side, cs, face));
                float h = v.SconceHeight;
                float yaw = Mathf.Atan2(into.x, into.z) * Mathf.Rad2Deg;
                if (!iron.TryGetValue(lamp.Room, out MoodShapes.Build b)) iron[lamp.Room] = b = new MoodShapes.Build();
                if (!flames.TryGetValue(lamp.Room, out MoodShapes.Build f)) flames[lamp.Room] = f = new MoodShapes.Build();
                // A plate on the wall, an arm out from it, a cup on the arm's end.
                MoodShapes.Slab(b, onWall + Vector3.up * (h - 0.45f) + into * 0.03f, 0.16f, 0.06f, 0.42f, yaw, MoodShapes.Paint.StoneDark, MoodShapes.Paint.StoneDark);
                MoodShapes.Slab(b, onWall + Vector3.up * (h - 0.2f) + into * 0.14f, 0.05f, 0.24f, 0.05f, yaw, MoodShapes.Paint.StoneDark, MoodShapes.Paint.StoneDark);
                MoodShapes.Prism(b, onWall + Vector3.up * (h - 0.14f) + into * 0.26f, 0.085f, 0.14f, 6, yaw, MoodShapes.Paint.StoneDark, MoodShapes.Paint.StoneDark);
                MoodShapes.Flame(f, onWall + Vector3.up * h + into * 0.26f, 0.34f, 0.085f, core, tip, out _, out _);
            }
            if (iron.Count == 0) return;
            Material lit = Own(MoodShapes.MakeLit(Own(MoodShapes.MakePalette())));
            lit.name = "Lamplit_Iron";
            Material glow = Own(MoodShapes.MakeUnlit("Lamplit_Flame", MoodShapes.Blend.Add, null, 3001));
            foreach (KeyValuePair<int, MoodShapes.Build> pair in iron)
            {
                Sconce("Lamplit_Sconces_" + pair.Key, pair.Value.ToMesh("Lamplit_Sconces_" + pair.Key, true), lit, pair.Key, true);
                Sconce("Lamplit_SconceFlames_" + pair.Key, flames[pair.Key].ToMesh("Lamplit_SconceFlames_" + pair.Key, false), glow, pair.Key, false);
            }
        }

        void Sconce(string name, Mesh mesh, Material material, int room, bool lit)
        {
            Own(mesh);
            var go = new GameObject(name) { layer = CryptKit.Layer };
            go.transform.SetParent(_root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = lit;
            r.enabled = false;
            _sconceRenderers.Add(r);
            _sconceRoom.Add(room);
        }

        T Own<T>(T o) where T : Object
        {
            _owned.Add(o);
            return o;
        }

        // ---------------- Every frame ----------------

        void Update() => Tick();

        static float Wave(float t, float speed, float phase) =>
            0.6f * Mathf.Sin(t * speed * 6.2831853f + phase) + 0.4f * Mathf.Sin(t * speed * 1.73f * 6.2831853f + phase * 2.1f);

        /// <summary>The lamps of the rooms the cave draws lit and flickering, the flashes fading. Allocates nothing (but when the mood was built again: then the lamps are too). For tests too.</summary>
        public void Tick()
        {
            if (_level == null) return;
            FadeFlashes();
            if (_on == null) return;
            RoomMood mood = _level.Mood;
            if (mood != null && mood.Builds != _moodBuilds)
            {
                LamplitValues on = _on;
                Teardown();
                Build();
                _on = on;
                _here = -2;
                mood.Lamplit = on;
            }
            if (_player == null && Time.unscaledTime - _lookedForPlayer > 1f)
            {
                _lookedForPlayer = Time.unscaledTime;
                _player = FindAnyObjectByType<PlayerCharacter>();
            }
            LevelRoom room = _player != null ? _level.RoomAt(_player.Position) : null;
            int here = room != null ? room.Order : _here < 0 ? (_level.Layout.Rooms.Count > 0 ? 0 : -1) : _here;
            if (here != _here)
            {
                _here = here;
                CaveSight.Shown(_level.Layout, here, _shown);
                for (int i = 0; i < _lamps.Count; i++)
                    if (_lamps[i].Light != null) _lamps[i].Light.enabled = Shown(_lamps[i].Room);
                for (int i = 0; i < _sconceRenderers.Count; i++)
                    if (_sconceRenderers[i] != null) _sconceRenderers[i].enabled = Shown(_sconceRoom[i]);
            }
            float t = Time.time, level = Mathf.Max(0f, _on.LampLevel);
            LampValues v = Lamps;
            for (int i = 0; i < _lamps.Count; i++)
            {
                LampLight l = _lamps[i];
                if (l.Light == null || !l.Light.enabled) continue;
                float factor = 1f + v.Flicker * Wave(t, v.FlickerSpeed, l.Phase);
                if (l.Source >= 0 && mood != null)
                {
                    RoomMood.RoomParts parts = mood.Of(l.Room);
                    if (parts != null && parts.Factor != null && l.Source < parts.Factor.Length) factor = parts.Factor[l.Source];
                }
                l.Light.intensity = l.Intensity * level * factor;
            }
        }

        bool Shown(int room) => room >= 0 && room < _shown.Length && _shown[room];

        // ---------------- Her shots ----------------

        /// <summary>
        /// A shot effect was made (<see cref="ShotFx"/> tells every one): with a lamplit level on, the muzzle's flash lights at the
        /// muzzle, a hit's spark where it lands, a blast's ring, cone or large spark wider and longer. Nothing otherwise. No allocation.
        /// </summary>
        public static void ShotWentOff(ShotFxKind kind, Vector3 origin, Vector3 direction, float size, Color core)
        {
            LamplitView view = Active;
            if (view == null || view._on == null) return;
            view.Shot(kind, origin, direction, size, core);
        }

        void Shot(ShotFxKind kind, Vector3 origin, Vector3 direction, float size, Color core)
        {
            LampValues v = Lamps;
            float level = Mathf.Max(0f, _on.FlashLevel);
            Color warm = new Color(v.FlashColour.R, v.FlashColour.G, v.FlashColour.B, 1f);
            switch (kind)
            {
                case ShotFxKind.Flash:
                {
                    float scale = Mathf.Clamp(size / Mathf.Max(0.01f, v.MuzzleSizeRef), 0.75f, Mathf.Max(0.75f, v.MuzzleMost));
                    LightUp(origin, v.MuzzleLight * level * scale, v.MuzzleRange, v.MuzzleSeconds, warm);
                    break;
                }
                case ShotFxKind.Spark:
                    if (size >= v.BlastSparkSize) LightUp(origin, v.BlastLight * level, size + v.BlastReach, v.BlastSeconds, Paint(core, warm, v.PaintShare));
                    else LightUp(origin, v.HitLight * level, v.HitRange, v.HitSeconds, Paint(core, warm, v.PaintShare));
                    break;
                case ShotFxKind.Ring:
                    LightUp(origin + Vector3.up, v.BlastLight * level, size + v.BlastReach, v.BlastSeconds, Paint(core, warm, v.PaintShare));
                    break;
                case ShotFxKind.Wedge:
                    LightUp(origin + direction * (size * 0.4f), v.BlastLight * level, size * 0.7f + v.BlastReach, v.BlastSeconds, Paint(core, warm, v.PaintShare));
                    break;
            }
        }

        /// <summary>The flash's colour: warm white with a share of the shot's own colour (its brightest channel brought to 1).</summary>
        static Color Paint(Color core, Color warm, float share)
        {
            float most = Mathf.Max(core.r, Mathf.Max(core.g, core.b));
            if (most <= 1e-4f) return warm;
            Color own = new Color(core.r / most, core.g / most, core.b / most, 1f);
            return Color.Lerp(warm, own, Mathf.Clamp01(share));
        }

        void LightUp(Vector3 at, float peak, float range, float seconds, Color colour)
        {
            if (peak <= 0f || seconds <= 0f || range <= 0f || _root == null) return;
            FlashesAsked++;
            float now = Time.unscaledTime;
            int frame = Time.frameCount;
            // A blast's ring and its spark come in the same frame at the same place: one light, the stronger.
            for (int i = 0; i < _flashes.Count; i++)
            {
                Flash f = _flashes[i];
                if (f.Light == null || !f.Light.enabled || f.Frame != frame || (f.Light.transform.position - at).sqrMagnitude > 2.25f) continue;
                if (peak > f.Peak)
                {
                    f.Peak = peak;
                    f.Light.intensity = peak;
                }
                f.Light.range = Mathf.Max(f.Light.range, range);
                f.Seconds = Mathf.Max(f.Seconds, seconds);
                _flashes[i] = f;
                return;
            }
            int pick = -1;
            for (int i = 0; i < _flashes.Count; i++)
                if (_flashes[i].Light != null && !_flashes[i].Light.enabled) { pick = i; break; }
            if (pick < 0 && _flashes.Count < Mathf.Max(1, Lamps.MostFlashes))
            {
                MakeFlash();
                pick = _flashes.Count - 1;
            }
            if (pick < 0)
            {
                pick = 0; // all lit: the oldest is taken
                for (int i = 1; i < _flashes.Count; i++) if (_flashes[i].At < _flashes[pick].At) pick = i;
            }
            Flash lit = _flashes[pick];
            if (lit.Light == null) return;
            lit.At = now;
            lit.Peak = peak;
            lit.Seconds = seconds;
            lit.Frame = frame;
            lit.Light.transform.position = at;
            lit.Light.range = range;
            lit.Light.color = colour;
            lit.Light.intensity = peak;
            lit.Light.enabled = true;
            _flashes[pick] = lit;
        }

        /// <summary>Makes the flash pool's lights now (a test warms it before it measures a shot); they stay off.</summary>
        public void WarmFlashes()
        {
            if (!_built) Build();
            while (_flashes.Count < Mathf.Max(1, Lamps.MostFlashes)) MakeFlash();
        }

        void MakeFlash()
        {
            var go = new GameObject("Lamplit_Flash") { layer = CryptKit.Layer };
            go.transform.SetParent(_root.transform, false);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.None;
            light.enabled = false;
            _flashes.Add(new Flash { Light = light, At = float.NegativeInfinity });
        }

        /// <summary>Each lit flash falls from its peak to dark over its seconds, in real time (a hit stop does not hold it), fast first.</summary>
        void FadeFlashes()
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < _flashes.Count; i++)
            {
                Flash f = _flashes[i];
                if (f.Light == null || !f.Light.enabled) continue;
                float k = 1f - (now - f.At) / Mathf.Max(1e-3f, f.Seconds);
                if (k <= 0f || _on == null) f.Light.enabled = false;
                else f.Light.intensity = f.Peak * k * k;
            }
        }
    }
}
