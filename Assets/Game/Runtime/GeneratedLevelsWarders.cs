using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Roguelike
{
    /// <summary>
    /// The warder in the practice room (work order 079, 2026-11-22), as part of <see cref="GeneratedLevels"/>: V spawns one
    /// warder as the other kinds' keys do; Shift and V a small pack with one, because alone it shows nothing: two swarmers and
    /// a guard about <see cref="GeneratedLevels.SpawnDistance"/> from her, and the warder <see cref="PackWarderBehind"/> behind
    /// them, so that he meets the ward within ten seconds. Both are developer keys (work order 066, <see cref="DeveloperKeys"/>).
    /// </summary>
    public sealed partial class GeneratedLevels
    {
        /// <summary>
        /// Work order 079: a warder in the practice room (with Shift, a pack with one). V: no binding of
        /// <see cref="InputBindings"/> uses it and no Editor shortcut takes it while the Game view has the keys. A developer's
        /// key (work order 066, <see cref="DeveloperKeys"/>).
        /// </summary>
        public const Key WarderKey = Key.V;
        /// <summary>The pack's swarmers with a warder (work order 079: two swarmers and a guard and one warder).</summary>
        public const int PackWarderSwarmers = 2;
        /// <summary>
        /// m behind the pack's middle, away from her, that the pack's warder stands: 13.5 m from her, inside its aggro range of
        /// 14 m, so it wakes on its own at once and wards the pack (the swarmers and the guard, 9 m, sleep until she comes).
        /// </summary>
        public const float PackWarderBehind = 1.5f;

        /// <summary>The warder's key: one warder, or with <paramref name="pack"/> the pack with one.</summary>
        public List<Enemy> SpawnWarder(bool pack) => pack ? SpawnWarderPack() : SpawnPractice(EnemyKind.Warder);

        /// <summary>
        /// Spawns a pack with a warder (work order 079): <see cref="PackWarderSwarmers"/> swarmers and a guard round a point about
        /// <see cref="SpawnDistance"/> from her, and a warder behind them. Returns what it made, the warder last; nothing while
        /// she is in a generated level, without a character or without the practice enemy to copy.
        /// </summary>
        public List<Enemy> SpawnWarderPack()
        {
            var made = new List<Enemy>();
            PlayerCharacter player = FindPlayer();
            EnemyBrute template = PracticeEnemy();
            if (player == null || template == null || _level != null) return made;
            if (_spawnRoot == null)
            {
                _spawnRoot = new GameObject("PracticeSpawns").transform;
                SceneManager.MoveGameObjectToScene(_spawnRoot.gameObject, player.gameObject.scene);
            }
            Vector3 middle = SpawnPoint(player.Position);
            List<Vector3> places = EnemyFactory.PackPlaces(middle, PackWarderSwarmers + 1, Values.PackRadius);
            for (int i = 0; i < places.Count; i++)
                Spawn(i < PackWarderSwarmers ? EnemyKind.Swarmer : EnemyKind.Guard, places[i], template, made);
            Vector3 away = new Vector3(middle.x - player.Position.x, 0f, middle.z - player.Position.z);
            away = away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.forward;
            Vector3 back = middle + away * PackWarderBehind;
            if (NavMesh.SamplePosition(back, out NavMeshHit hit, 2f, NavMesh.AllAreas)) back = new Vector3(hit.position.x, 0f, hit.position.z);
            else back = middle;
            Spawn(EnemyKind.Warder, back, template, made);
            return made;
        }

        void Spawn(EnemyKind kind, Vector3 at, EnemyBrute template, List<Enemy> made)
        {
            Enemy e = EnemyFactory.Make(kind, at, _spawnRoot, template, "Spawned_" + kind + "_" + (_spawned.Count + 1));
            if (e == null) return;
            made.Add(e);
            _spawned.Add(e);
        }
    }
}
