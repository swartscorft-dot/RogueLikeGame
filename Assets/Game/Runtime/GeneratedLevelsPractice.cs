using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Roguelike
{
    /// <summary>
    /// The practice room's spawns of <see cref="GeneratedLevels"/> (work order 008): the keys' enemies about her, and their
    /// removal (work order 081 moved them here from GeneratedLevels.cs, code unchanged).
    /// </summary>
    public sealed partial class GeneratedLevels
    {
        // ---------------- Practice spawns (work order 008) ----------------

        Transform _spawnRoot;
        readonly List<Enemy> _spawned = new List<Enemy>();

        /// <summary>The enemies spawned in the practice room and not yet cleared, dead ones too.</summary>
        public IReadOnlyList<Enemy> Spawned => _spawned;

        /// <summary>
        /// Spawns enemies of a kind about <see cref="SpawnDistance"/> from her on the practice floor: for swarmers a pack
        /// (<see cref="SwarmerValues.PracticePack"/>, round a circle of the level values' pack radius), else one. Returns
        /// what it made; nothing while she is in a generated level, without a character or without the practice enemy
        /// to copy.
        /// </summary>
        public List<Enemy> SpawnPractice(EnemyKind kind)
        {
            var made = new List<Enemy>();
            PlayerCharacter player = FindPlayer();
            EnemyBrute template = PracticeEnemy();
            if (player == null || template == null || _level != null) return made;
            if (_spawnRoot == null)
            {
                // In the scene, not under this object that lives across scene loads: a new scene takes them away.
                _spawnRoot = new GameObject("PracticeSpawns").transform;
                SceneManager.MoveGameObjectToScene(_spawnRoot.gameObject, player.gameObject.scene);
            }
            Vector3 middle = SpawnPoint(player.Position);
            int count = kind == EnemyKind.Swarmer ? new SwarmerValues().PracticePack : 1;
            List<Vector3> places = EnemyFactory.PackPlaces(middle, count, Values.PackRadius);
            for (int i = 0; i < places.Count; i++)
            {
                Enemy e = EnemyFactory.Make(kind, places[i], _spawnRoot, template, "Spawned_" + kind + "_" + (_spawned.Count + 1));
                if (e == null) continue;
                if (e is EnemyBoss boss) boss.HealthOverride = Mathf.RoundToInt(boss.Tuning.Enemy.Health * boss.Tuning.PracticeHealthShare);
                made.Add(e);
                _spawned.Add(e);
            }
            return made;
        }

        /// <summary>Removes everything spawned in the practice room.</summary>
        public void ClearSpawned()
        {
            _spawned.Clear();
            if (_spawnRoot == null) return;
            _spawnRoot.gameObject.SetActive(false);
            Destroy(_spawnRoot.gameObject);
            _spawnRoot = null;
        }

        /// <summary>
        /// A point on the NavMesh about <see cref="SpawnDistance"/> from her: the first of eight ways round (north first)
        /// whose straight line from her stays on the NavMesh all the way, else the farthest one gets. Ground height.
        /// </summary>
        static Vector3 SpawnPoint(Vector3 from)
        {
            Vector3 feet = new Vector3(from.x, 0f, from.z);
            if (NavMesh.SamplePosition(feet, out NavMeshHit start, 2f, NavMesh.AllAreas)) feet = start.position;
            Vector3 best = feet;
            float bestDistance = -1f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
                Vector3 to = feet + dir * SpawnDistance;
                if (!NavMesh.Raycast(feet, to, out NavMeshHit hit, NavMesh.AllAreas)) return new Vector3(to.x, 0f, to.z);
                float d = Vector3.Distance(feet, hit.position);
                if (d > bestDistance)
                {
                    bestDistance = d;
                    best = hit.position - dir * 1.5f; // off the edge a little, so a pack fits
                }
            }
            return new Vector3(best.x, 0f, best.z);
        }
    }
}
