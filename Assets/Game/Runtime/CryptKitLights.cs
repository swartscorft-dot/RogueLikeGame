using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The lanterns' lights of a generated floor (work order 016, 2026-11-20), when <see cref="CryptKitValues.LanternLights"/>
    /// is on: small cyan point lights without shadows, at most <see cref="CryptKitValues.LightsPerRoom"/> a room, and only
    /// the room she is in has its lights on. It sits on the level's root and goes with it.
    /// </summary>
    public sealed class CryptKitLights : MonoBehaviour
    {
        readonly Dictionary<int, List<Light>> _byRoom = new Dictionary<int, List<Light>>();
        GeneratedLevel _level;
        PlayerCharacter _player;
        float _lookedForPlayer = float.NegativeInfinity;

        /// <summary>The room whose lights are on, by its place in the run; -1 for none.</summary>
        public int LitRoom { get; private set; } = -1;

        /// <summary>Every light, by room.</summary>
        public IReadOnlyDictionary<int, List<Light>> ByRoom => _byRoom;

        /// <summary>A light for a room at a world point, off until she is in the room.</summary>
        public Light Add(int room, Vector3 at, CryptKitValues values, Transform parent)
        {
            var go = new GameObject("Kit_Light_" + room + "_" + (_byRoom.TryGetValue(room, out List<Light> had) ? had.Count : 0)) { layer = CryptKit.Layer };
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = CryptKit.Picked(values.Cyan);
            light.range = values.LightRange;
            light.intensity = values.LightIntensity;
            light.shadows = LightShadows.None;
            light.enabled = false;
            if (had == null) _byRoom[room] = had = new List<Light>();
            had.Add(light);
            return light;
        }

        void Awake() => _level = GetComponent<GeneratedLevel>();

        void Update()
        {
            if (_player == null && Time.unscaledTime - _lookedForPlayer > 1f)
            {
                _lookedForPlayer = Time.unscaledTime;
                _player = FindAnyObjectByType<PlayerCharacter>();
            }
            LevelRoom room = _player != null && _level != null ? _level.RoomAt(_player.Position) : null;
            int order = room != null ? room.Order : -1;
            if (order == LitRoom) return;
            foreach (KeyValuePair<int, List<Light>> pair in _byRoom)
                foreach (Light l in pair.Value)
                    if (l != null) l.enabled = pair.Key == order;
            LitRoom = order;
        }
    }
}
