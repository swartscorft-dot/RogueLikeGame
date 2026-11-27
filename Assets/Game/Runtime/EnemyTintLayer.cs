using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// Work order 151: a tinted enemy's added picture on a floor carries its tint (<see cref="EnemyTintValues"/>, where the finding and
    /// the repair are written out). On a floor the rooms' mood draws each mesh of an enemy's model once more (<c>RoomMood.FloorEnemy</c>:
    /// a material slot after its own, the mesh's texture added at the floor's light floor for enemies, one material shared by every
    /// enemy with that texture). This lays, on that slot alone, a property block of _BaseColor: the mood's own colour for it times the
    /// mesh's own colour (its material's, which every tint of the game is painted into: <see cref="EnemyFactory.Paint"/>), in linear
    /// light. The mesh's own slots are left as they are (their material carries the tint already); an untinted mesh gets nothing.
    ///
    /// Owned by the enemy's <see cref="HitFlash"/> and ticked from its LateUpdate, after every Update of the frame, so it sees the frame's
    /// flash, daze and paint. While the flash or another block over the whole renderer (the Grave Ram's daze,
    /// <see cref="EnemyGraveRam.ShowsDazedTint"/>) is on, every block of its own is taken off, and theirs is exactly today's; when they
    /// end, the tint is laid again. Nothing is allocated in a frame: one block, made the first time one is laid, and one list of slots
    /// shared by all; a block is set only when what it carries has changed.
    /// </summary>
    public sealed class EnemyTintLayer
    {
        static readonly int s_BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int s_BaseMap = Shader.PropertyToID("_BaseMap");
        static readonly List<Material> s_slots = new List<Material>(8);

        /// <summary>What was laid on one mesh: the slot (-1: nothing), and what it was made from.</summary>
        struct Laid
        {
            public int Slot;
            public Material Added;
            public Color Own, Level;
        }

        readonly HitFlash _flash;
        Enemy _enemy;
        bool _looked;
        MaterialPropertyBlock _block;
        Renderer[] _seen;          // the renderers the states below are of (the flash's targets as last seen)
        Renderer _seenOne;         // or its one target
        Laid[] _laid = new Laid[0];
        int _laidCount;            // meshes with a block of this order's on
        bool _forget;              // the flash or the Ram set a block over the whole renderer: what was laid is not known to be there

        public EnemyTintLayer(HitFlash flash) => _flash = flash;

        /// <summary>Meshes that carry a block of this order's now; for tests.</summary>
        public int LaidCount => _laidCount;

        /// <summary>The slot of mesh <paramref name="i"/> (of the flash's targets) a block is laid on, -1 for none; for tests.</summary>
        public int SlotOf(int i) => i >= 0 && i < _laid.Length && _laid[i].Slot >= 0 ? _laid[i].Slot : -1;

        /// <summary>The flash set or cleared its block over the whole renderer this frame: lay everything again at the next tick.</summary>
        public void Touched() => _forget = true;

        /// <summary>Follows the switch, the tint of each mesh and its added picture; <paramref name="flashing"/>: the hit's flash is on.</summary>
        public void Tick(bool flashing)
        {
            if (!EnemyTintValues.Show && _laidCount == 0) return; // off from the start: nothing looked at, nothing laid (today's)
            Renderer[] targets = _flash.Targets;
            Renderer one = targets == null ? _flash.Target : null;
            if (targets != _seen || one != _seenOne)
            {
                TakeOffAll();
                _seen = targets;
                _seenOne = one;
                int n = targets != null ? targets.Length : 1;
                if (_laid.Length != n) _laid = new Laid[n]; // only when its renderers are set (its model put on)
                for (int i = 0; i < _laid.Length; i++) _laid[i].Slot = -1;
            }
            if (!_looked)
            {
                _looked = true;
                _enemy = _flash.GetComponent<Enemy>();
            }
            bool over = _enemy is EnemyGraveRam ram && ram.ShowsDazedTint;
            if (!EnemyTintValues.Show || flashing || over)
            {
                if (_laidCount > 0) TakeOffAll();
                return;
            }
            if (_forget)
            {
                _forget = false;
                TakeOffAll(); // put on afresh below
            }
            for (int i = 0; i < _laid.Length; i++) Lay(i, targets != null ? targets[i] : one);
        }

        void Lay(int i, Renderer r)
        {
            if (r == null)
            {
                Forget(i);
                return;
            }
            Material own = r.sharedMaterial;
            Color tint = own != null && own.HasProperty(s_BaseColor) ? own.GetColor(s_BaseColor) : Color.white;
            bool tinted = EnemyTintRules.Tinted(new Rgb(tint.r, tint.g, tint.b));
            if (!tinted && _laid[i].Slot < 0) return; // the common case: an untinted mesh, nothing on it

            r.GetSharedMaterials(s_slots);
            int slot = -1;
            for (int k = s_slots.Count - 1; k >= 1 && slot < 0; k--)
                if (s_slots[k] != null && s_slots[k] != s_slots[0]) slot = k; // the mood's slot: not the mesh's own material
            Material added = slot >= 0 ? s_slots[slot] : null;
            int count = s_slots.Count;
            s_slots.Clear();
            bool textured = added != null && added.HasProperty(s_BaseMap) && added.GetTexture(s_BaseMap) != null && added.HasProperty(s_BaseColor);
            if (!EnemyTintRules.Lays(true, tinted, textured, false, false))
            {
                TakeOff(i, r, count);
                return;
            }
            Color level = added.GetColor(s_BaseColor);
            Laid was = _laid[i];
            if (was.Slot == slot && was.Added == added && was.Own == tint && was.Level == level) return; // as laid: nothing to do
            if (was.Slot >= 0 && was.Slot != slot) TakeOff(i, r, count);

            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            Color l = linear ? level.linear : level, t = linear ? tint.linear : tint;
            Rgb c = EnemyTintRules.Added(new Rgb(l.r, l.g, l.b), new Rgb(t.r, t.g, t.b));
            _block ??= new MaterialPropertyBlock();
            _block.Clear();
            // As a vector: the value the shader reads, in linear light already (a colour would be turned into linear light again).
            _block.SetVector(s_BaseColor, new Vector4(c.R, c.G, c.B, level.a));
            r.SetPropertyBlock(_block, slot);
            if (_laid[i].Slot < 0) _laidCount++;
            _laid[i] = new Laid { Slot = slot, Added = added, Own = tint, Level = level };
        }

        /// <summary>Takes the block of mesh <paramref name="i"/> off its slot, if the slot is still there.</summary>
        void TakeOff(int i, Renderer r, int count)
        {
            int slot = _laid[i].Slot;
            if (slot < 0) return;
            if (r != null && slot < count) r.SetPropertyBlock(null, slot);
            Forget(i);
        }

        void Forget(int i)
        {
            if (_laid[i].Slot >= 0) _laidCount--;
            _laid[i] = new Laid { Slot = -1 };
        }

        /// <summary>Every block of this order's taken off (the switch off, a flash, the Ram's daze, its renderers changed).</summary>
        public void TakeOffAll()
        {
            for (int i = 0; i < _laid.Length; i++)
            {
                if (_laid[i].Slot < 0) continue;
                Renderer r = _seen != null ? (i < _seen.Length ? _seen[i] : null) : _seenOne;
                int count = 0;
                if (r != null)
                {
                    r.GetSharedMaterials(s_slots);
                    count = s_slots.Count;
                    s_slots.Clear();
                }
                TakeOff(i, r, count);
            }
            _laidCount = 0;
        }
    }
}
