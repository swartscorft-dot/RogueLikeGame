using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// The rules of work order 080 (weight in a hit, a kill and a clean press): which sound a hit of each weight chooses,
    /// no variation twice in a row, the per-frame and per-window limits of the impacts, the steps of a recast chain and
    /// its all-clean ending, and the nine sounds' place among the placeholders. Nobody can hear here whether they sound right.
    /// </summary>
    public class CombatWeightRulesTests
    {
        static readonly PlaceholderSound[] TheNine =
        {
            PlaceholderSound.HitBasic, PlaceholderSound.HitHeavy, PlaceholderSound.HitCrit, PlaceholderSound.HitCleanEdge,
            PlaceholderSound.Kill, PlaceholderSound.RoomWon, PlaceholderSound.CleanTick, PlaceholderSound.ChainTop, PlaceholderSound.OffTick,
        };

        // ---------------- The weights ----------------

        [TestCase(-1, false, HitWeight.Basic, PlaceholderSound.HitBasic)]
        [TestCase(0, false, HitWeight.Heavy, PlaceholderSound.HitHeavy)]
        [TestCase(3, false, HitWeight.Heavy, PlaceholderSound.HitHeavy)]
        [TestCase(-1, true, HitWeight.Crit, PlaceholderSound.HitCrit)]
        [TestCase(1, true, HitWeight.Crit, PlaceholderSound.HitCrit)]
        public void AHit_ChoosesItsWeightsSound(int slot, bool crit, HitWeight weight, PlaceholderSound sound)
        {
            Assert.That(CombatWeightRules.WeightOf(slot, crit), Is.EqualTo(weight));
            Assert.That(CombatWeightRules.SoundOf(weight), Is.EqualTo(sound));
        }

        [Test]
        public void TheThreeWeights_AreThreeSounds_AndTheKill_IsNotTheHits()
        {
            var hits = new[] { HitWeight.Basic, HitWeight.Heavy, HitWeight.Crit }.Select(CombatWeightRules.SoundOf).ToList();
            Assert.That(hits.Distinct().Count(), Is.EqualTo(3));
            Assert.That(hits, Has.No.Member(CombatWeightRules.KillSound(false)));
            Assert.That(CombatWeightRules.KillSound(false), Is.EqualTo(PlaceholderSound.Kill));
            Assert.That(CombatWeightRules.KillSound(true), Is.EqualTo(PlaceholderSound.RoomWon));
            Assert.That(PlaceholderRecipes.Seconds(PlaceholderSound.RoomWon), Is.GreaterThan(PlaceholderRecipes.Seconds(PlaceholderSound.Kill)), "the room won a little longer");
        }

        [Test]
        public void TheNine_AreSoundsByName_AfterTheTwentyNine_InTheTable_QuietBesideTheGuns()
        {
            var v = new PlaceholderSoundValues();
            foreach (PlaceholderSound s in TheNine)
            {
                Assert.That((int)s, Is.GreaterThanOrEqualTo(29), s + ": after the twenty-nine, which keep their places");
                Assert.That(PlaceholderSoundRules.ResourcePath(s), Is.EqualTo("Sounds/Placeholders/" + s), "his recording replaces it by its name");
                Assert.That(v.Sounds.Count(x => x.Sound == s), Is.EqualTo(1), s + ": its own line");
                Assert.That(v.Volume(s), Is.InRange(0.2f, 0.4f), s + ": under the guns");
                Assert.That(PlaceholderSoundRules.MayStart(s, paused: true), Is.False, "paused is silent");
                Assert.That(PlaceholderRecipes.Seconds(s), Is.LessThanOrEqualTo(0.45f), s + ": short");
            }
        }

        // ---------------- No variation twice in a row ----------------

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void AVariation_IsNeverTheOneBefore_AndEveryOneComes(int count)
        {
            var pick = new NoRepeat(80u);
            int last = -1;
            var seen = new HashSet<int>();
            for (int i = 0; i < 400; i++)
            {
                int v = pick.Next(count);
                Assert.That(v, Is.InRange(0, count - 1));
                Assert.That(v, Is.Not.EqualTo(last), "not twice in a row, at " + i);
                seen.Add(v);
                last = v;
            }
            Assert.That(seen.Count, Is.EqualTo(count), "every variation is heard");
        }

        [Test]
        public void OneVariation_IsAlwaysIt_AndThePitches_AreFourApart()
        {
            var pick = new NoRepeat(1u);
            Assert.That(pick.Next(1), Is.EqualTo(0));
            Assert.That(pick.Next(0), Is.EqualTo(0));
            float[] pitches = new HitWeightValues().Pitches;
            Assert.That(pitches.Length, Is.InRange(3, 4), "three or four variations");
            Assert.That(pitches.Distinct().Count(), Is.EqualTo(pitches.Length), "each its own");
            Assert.That(CombatWeightRules.PitchOf(pitches, 0), Is.EqualTo(1f));
            Assert.That(CombatWeightRules.PitchOf(pitches, 99), Is.EqualTo(1f), "none: as it is");
            Assert.That(new KillWeightValues().Pitches.Length, Is.InRange(3, 4));
        }

        // ---------------- The limits ----------------

        [Test]
        public void EightHitsInOneFrame_AreOneThickHit_TheHeaviest_StandingForAll()
        {
            var gate = new HitGate();
            var v = new HitWeightValues();
            for (int i = 0; i < 6; i++) gate.Note(HitWeight.Heavy, false);
            gate.Note(HitWeight.Crit, false);
            gate.Note(HitWeight.Basic, true);
            IReadOnlyList<HitStart> starts = gate.Flush(0f, v);
            Assert.That(v.PerFrame, Is.EqualTo(1));
            Assert.That(starts.Count, Is.EqualTo(1), "one impact");
            Assert.That(starts[0].Weight, Is.EqualTo(HitWeight.Crit), "the heaviest");
            Assert.That(starts[0].Hits, Is.EqualTo(8), "standing for the eight");
            Assert.That(starts[0].Clean, Is.True, "a clean press's edge goes with it");
            Assert.That(gate.Waiting, Is.False);
            Assert.That(gate.Flush(0.01f, v).Count, Is.EqualTo(0), "nothing more");
        }

        [Test]
        public void TwoAFrame_LetsTheTwoHeaviestThrough()
        {
            var gate = new HitGate();
            var v = new HitWeightValues { PerFrame = 2, PerWindow = 10 };
            gate.Note(HitWeight.Basic, false);
            gate.Note(HitWeight.Basic, false);
            gate.Note(HitWeight.Heavy, false);
            gate.Note(HitWeight.Crit, false);
            IReadOnlyList<HitStart> starts = gate.Flush(0f, v);
            Assert.That(starts.Select(s => s.Weight), Is.EqualTo(new[] { HitWeight.Crit, HitWeight.Heavy }));
            Assert.That(starts[0].Hits + starts[1].Hits, Is.EqualTo(4), "the dropped basics stand under the heaviest");
        }

        [Test]
        public void InAWindow_OnlySoMany_ThenAgainWhenItHasPassed()
        {
            var gate = new HitGate();
            var v = new HitWeightValues();
            float dt = 1f / 60f;
            int started = 0;
            float t = 0f;
            for (; t < v.Window - 1e-3f; t += dt)
            {
                gate.Note(HitWeight.Basic, false);
                started += gate.Flush(t, v).Count;
            }
            Assert.That(started, Is.EqualTo(v.PerWindow), "a hit every frame: no more than the window's");
            Assert.That(v.PerWindow, Is.EqualTo(3));
            Assert.That(v.Window, Is.EqualTo(0.25f));
            gate.Note(HitWeight.Basic, false);
            Assert.That(gate.Flush(v.Window + 0.001f, v).Count, Is.EqualTo(1), "the window passed: the next is heard");
        }

        [Test]
        public void AClockThatWentBack_ForgetsTheWindow()
        {
            var gate = new HitGate();
            var v = new HitWeightValues();
            for (int i = 0; i < 5; i++)
            {
                gate.Note(HitWeight.Basic, false);
                gate.Flush(10f, v);
            }
            gate.Note(HitWeight.Basic, false);
            Assert.That(gate.Flush(0f, v).Count, Is.EqualTo(1), "a new run of the clock (a scene loaded): a new start");
        }

        // ---------------- The chain's steps ----------------

        static ChainTick Press(CleanRun run, int shot, bool clean, bool last, ChainTickValues v = null) =>
            run.Press(0, shot, clean, last, v ?? new ChainTickValues());

        [Test]
        public void TripleTap_AllClean_RisesAStep_ThenEndsOnTheTopNote()
        {
            var run = new CleanRun();
            ChainTick second = Press(run, 2, true, false);
            ChainTick third = Press(run, 3, true, true);
            Assert.That(second.Kind, Is.EqualTo(ChainTickKind.Step));
            Assert.That(second.Step, Is.EqualTo(1));
            Assert.That(third.Kind, Is.EqualTo(ChainTickKind.Top), "clean all through: the top note");
            Assert.That(CombatWeightRules.SoundOf(third.Kind), Is.EqualTo(PlaceholderSound.ChainTop));
            Assert.That(CombatWeightRules.SoundOf(second.Kind), Is.EqualTo(PlaceholderSound.CleanTick));
        }

        [Test]
        public void ALongerChain_CleanInARow_RisesStepByStep()
        {
            var v = new ChainTickValues();
            var run = new CleanRun();
            float before = 0f;
            for (int shot = 2; shot <= 5; shot++)
            {
                ChainTick t = Press(run, shot, true, false, v);
                Assert.That(t.Kind, Is.EqualTo(ChainTickKind.Step));
                Assert.That(t.Step, Is.EqualTo(shot - 1));
                float pitch = CombatWeightRules.StepPitch(t.Step, v);
                Assert.That(pitch, Is.GreaterThan(before), "each a step higher than the one before");
                before = pitch;
            }
            Assert.That(CombatWeightRules.StepPitch(1, v), Is.EqualTo(1f), "the first at its own pitch");
            Assert.That(CombatWeightRules.StepPitch(2, v), Is.EqualTo((float)System.Math.Pow(2.0, 3.0 / 12.0)).Within(1e-5f), "a minor third up");
            Assert.That(CombatWeightRules.StepPitch(50, v), Is.EqualTo(CombatWeightRules.StepPitch(v.MostSteps, v)), "it stops rising at the most");
        }

        [Test]
        public void AnUncleanPress_ResetsTheRow_AndIsSilent_AndTheChainEndsOnAStepNotTheTop()
        {
            var run = new CleanRun();
            Assert.That(Press(run, 2, false, false).Kind, Is.EqualTo(ChainTickKind.None), "off its clean part: silent");
            ChainTick third = Press(run, 3, true, true);
            Assert.That(third.Kind, Is.EqualTo(ChainTickKind.Step), "clean, but the chain was not clean all through");
            Assert.That(third.Step, Is.EqualTo(1), "the row began again");
            Assert.That(new ChainTickValues().DullTickWhenOff, Is.False, "decided: silent when off");
        }

        [Test]
        public void ACleanSecond_ThenAnUncleanThird_ResetsTheRow()
        {
            var run = new CleanRun();
            Press(run, 2, true, false);
            Assert.That(Press(run, 3, false, true).Kind, Is.EqualTo(ChainTickKind.None));
            Assert.That(run.Run, Is.EqualTo(0));
        }

        [Test]
        public void WithTheDullTick_APressOffItsCleanPart_TicksDull()
        {
            var v = new ChainTickValues { DullTickWhenOff = true };
            var run = new CleanRun();
            ChainTick t = Press(run, 2, false, false, v);
            Assert.That(t.Kind, Is.EqualTo(ChainTickKind.Off));
            Assert.That(CombatWeightRules.SoundOf(t.Kind), Is.EqualTo(PlaceholderSound.OffTick));
        }

        [Test]
        public void ANewChain_StartsANewRow_AndAChainOfTwo_HasNoTopNote()
        {
            var run = new CleanRun();
            Press(run, 2, true, false);
            ChainTick again = Press(run, 2, true, false);
            Assert.That(again.Step, Is.EqualTo(1), "press 2 again: a new chain");
            // Backstep's chain of two: its one clean press is a step, as the game's full clean chain needs three presses.
            var backstep = new CleanRun();
            Assert.That(backstep.Press(1, 2, true, true, new ChainTickValues()).Kind, Is.EqualTo(ChainTickKind.Step));
            // Another slot's press is another row.
            Press(run, 3, true, false);
            Assert.That(run.Press(2, 3, true, false, new ChainTickValues()).Step, Is.EqualTo(1));
        }

        // ---------------- The switch ----------------

        [Test]
        public void TheSwitch_AndEachPart_AreOnByDefault_AndReachable()
        {
            var w = new CombatWeightValues();
            Assert.That(w.On, Is.True);
            Assert.That(w.Hits.On && w.Hits.CleanEdge, Is.True);
            Assert.That(w.Kills.On && w.Kills.Burst, Is.True);
            Assert.That(w.Chain.On && w.Chain.TakesTheCleanTone, Is.True);
            Assert.That(w.Kills.BurstPool, Is.GreaterThan(0));
            Assert.That(w.Kills.RoomWonScale, Is.GreaterThan(1f));
        }
    }
}
