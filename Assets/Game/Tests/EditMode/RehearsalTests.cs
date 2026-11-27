using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 148, the rehearsal, as rules: what is stored and read back is the same build, field for field (every public
    /// field of every class it holds, filled with values that are not their defaults); a build put on a fresh run's rules and
    /// taken off again is the same build, with the same stats and maximum; a missing or broken file is no build, throws
    /// nothing and is left as it was; a run that ended before floor 2, the room lab's run, a rehearsal and the switch off keep
    /// nothing. Files only under a temporary folder of this test.
    /// </summary>
    public class RehearsalTests
    {
        string _folder;

        [SetUp]
        public void MakeFolder() => _folder = Path.Combine(Path.GetTempPath(), "RehearsalTests-" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void RemoveFolder()
        {
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        string FilePath => Path.Combine(_folder, RehearsalValues.FileName);

        // ---------------- Filling every field ----------------

        /// <summary>Every public field of <paramref name="o"/> given a value that is not its default, different by <paramref name="seed"/>.</summary>
        static void Fill(object o, int seed)
        {
            FieldInfo[] fields = RehearsalJson.Fields(o.GetType());
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo f = fields[i];
                Type t = f.FieldType;
                object now = f.GetValue(o);
                if (t == typeof(float)) f.SetValue(o, 0.137f * (seed + 1) + 0.0071f * (i + 1));
                else if (t == typeof(int)) f.SetValue(o, seed * 10 + i + 2);
                else if (t == typeof(bool)) f.SetValue(o, !(bool)now);
                else if (t == typeof(string)) f.SetValue(o, "w" + seed + "-" + i);
                else if (t.IsEnum)
                {
                    Array values = Enum.GetValues(t);
                    f.SetValue(o, values.GetValue((Array.IndexOf(values, now) + 1 + seed) % values.Length));
                }
                else if (t == typeof(List<string>)) ((List<string>)now).AddRange(new[] { "a" + seed + "-" + i, "b" + seed + "-" + i });
                else if (t == typeof(string[])) f.SetValue(o, new[] { "x" + seed, "y" + i });
                else if (t == typeof(SkillPatch[]))
                {
                    var a = new SkillPatch();
                    var b = new SkillPatch();
                    Fill(a, seed + 1);
                    Fill(b, seed + 2);
                    f.SetValue(o, new[] { a, b });
                }
                else Assert.Fail("a field the test does not know how to fill: " + o.GetType().Name + "." + f.Name + " (" + t.Name + "); teach Fill, and check the stored build holds it");
            }
        }

        /// <summary>Every public field of the two, compared as the build's JSON writes it.</summary>
        static void SameFields(object a, object b, string what)
        {
            foreach (FieldInfo f in RehearsalJson.Fields(a.GetType()))
                Assert.That(RehearsalJson.Write(f.GetValue(b)), Is.EqualTo(RehearsalJson.Write(f.GetValue(a))), what + "." + f.Name);
        }

        static readonly Type[] HeldClasses =
        {
            typeof(Upgrade), typeof(SkillUpgradeDefinition), typeof(SkillPatch), typeof(RunMetaEffects), typeof(AttachmentEffects),
            typeof(RehearsalSkillTier), typeof(RehearsalShotTier),
        };

        /// <summary>A run's rules with something in every part a build holds.</summary>
        static void ARichRun(out RunModifiers m, out RunSkillUpgrades s, out RunFocusChoice f)
        {
            m = new RunModifiers { StatsAndTiers = true, StatValues = new StatValues() };
            m.Take(new Upgrade { Kind = UpgradeKind.MaxHealth, Amount = 10f, Heal = 10, Tier = UpgradeTier.Rare, Tiered = true });
            m.Take(new Upgrade { Kind = UpgradeKind.Damage, Amount = 0.05f, Tier = UpgradeTier.Common, Tiered = true });
            m.Take(new Upgrade { Kind = UpgradeKind.Cooldown, Amount = 0.1f, Skill = "Quick Shot" });
            m.Take(new Upgrade { Kind = UpgradeKind.CritChance, Amount = 0.04f, Tier = UpgradeTier.Epic, Tiered = true });
            m.Take(new Upgrade { Kind = UpgradeKind.AttackSpeed, Amount = 0.08f, Tiered = true });
            Fill(m.Meta, 3);
            Fill(m.Weapon, 5);
            m.BasicShot.Values = new BasicShotUpgradeValues();
            m.BasicShot.Take(BasicShotUpgradeKind.HotBarrel);
            m.BasicShot.Take(BasicShotUpgradeKind.PiercingRound);
            m.BasicShot.Take(BasicShotUpgradeKind.HotBarrel);
            m.BasicShot.ChamberFired = 4;
            s = new RunSkillUpgrades();
            s.Take(new SkillUpgradeDefinition("Quick Shot", "Double Tap", SkillUpgradeKind.Change, "two shots", new[] { "Heavy Tap" },
                                              SkillPatch.Set("RecastCount", 2f), SkillPatch.Scale("Cooldown", 0.9f)));
            s.TakeTier(new SkillTierOffer
            {
                Skill = "Backstep", Tier = UpgradeTier.Epic,
                Step = new SkillUpgradeDefinition { Skill = "Backstep", Name = "epic step", TierStep = true, Patches = new[] { SkillPatch.Scale("Damage", 1.3f) } },
                Change = new SkillUpgradeDefinition("Backstep", "Long Step", SkillUpgradeKind.Change, "farther", null, SkillPatch.Add("MoveDistance", 1.5f)),
            });
            f = new RunFocusChoice();
            f.Offer();
            f.Take(FocusMode.SteadyHand);
        }

        static readonly string[] Keys = { "Quick Shot", "Backstep", "", "", "Scatter Blast", "Powder Keg", "Long Shot", "" };
        static readonly string[] Had = { "Quick Shot", "Backstep", "Scatter Blast", "Powder Keg", "Long Shot", "Slide" };

        // ---------------- Tests ----------------

        [Test]
        public void EveryFieldOfEveryClassItHolds_ComesBackFromItsJson_AsItWas()
        {
            foreach (Type t in HeldClasses)
            {
                object o = Activator.CreateInstance(t);
                Fill(o, 7);
                object back = typeof(RehearsalJson).GetMethod("Copy").MakeGenericMethod(t).Invoke(null, new[] { o });
                Assert.That(back, Is.Not.SameAs(o), t.Name);
                SameFields(o, back, t.Name);
            }
        }

        [Test]
        public void WhatIsStored_AndReadBack_IsTheSameBuild_FieldForField()
        {
            ARichRun(out RunModifiers m, out RunSkillUpgrades s, out RunFocusChoice f);
            RehearsalBuild stored = RehearsalBuild.Capture(m, s, f, Keys, Had);
            stored.Run = "ABCD1234";
            stored.Floor = 4;
            stored.Ended = "death";
            stored.Saved = "2026-11-27 15:00:00";
            stored.MaxHealth = m.MaxHealthFor(100);
            stored.Stats = m.Stats.OneLine(0.4f, 100);

            Assert.That(RehearsalFile.Save(FilePath, stored, out string error), Is.True, error);
            RehearsalBuild read = RehearsalFile.Load(FilePath);
            Assert.That(read, Is.Not.Null, "read back");
            Assert.That(RehearsalRules.Differences(stored, read), Is.Empty, "field for field");
            foreach (FieldInfo field in RehearsalJson.Fields(typeof(RehearsalBuild)))
                Assert.That(RehearsalJson.Write(field.GetValue(read)), Is.EqualTo(RehearsalJson.Write(field.GetValue(stored))), field.Name + ", the run's own fields too");

            // The pieces of it, one by one, against the run they came from.
            Assert.That(read.Keys, Is.EqualTo(Keys));
            Assert.That(read.Skills, Is.EqualTo(Had));
            Assert.That(read.Upgrades.Count, Is.EqualTo(m.Taken.Count));
            for (int i = 0; i < m.Taken.Count; i++) SameFields(m.Taken[i], read.Upgrades[i], "upgrade " + i);
            Assert.That(read.SkillUpgrades.Count, Is.EqualTo(s.Taken.Count), "the tier's step and change among them");
            for (int i = 0; i < s.Taken.Count; i++) SameFields(s.Taken[i], read.SkillUpgrades[i], "skill upgrade " + i);
            Assert.That(read.SkillTiers.Single().Skill, Is.EqualTo("Backstep"));
            Assert.That(read.SkillTiers.Single().Tier, Is.EqualTo(UpgradeTier.Epic));
            Assert.That(read.BasicShot.Select(x => x.Kind + " " + x.Tier), Is.EqualTo(new[] { "HotBarrel 2", "PiercingRound 1" }), "in the order first taken, at the tier held");
            foreach (FieldInfo field in RehearsalJson.Fields(typeof(AttachmentEffects)))
                Assert.That(RehearsalJson.Write(field.GetValue(read.Weapon)), Is.EqualTo(RehearsalJson.Write(field.GetValue(m.Weapon))), "Weapon." + field.Name);
            foreach (FieldInfo field in RehearsalJson.Fields(typeof(RunMetaEffects)))
            {
                if (field.Name == "RedrawsLeft" || field.Name == "ResolvesLeft") continue; // stored full: below
                Assert.That(RehearsalJson.Write(field.GetValue(read.Meta)), Is.EqualTo(RehearsalJson.Write(field.GetValue(m.Meta))), "Meta." + field.Name);
            }
            Assert.That(read.Meta.RedrawsLeft, Is.EqualTo(m.Meta.Redraws), "Second Look's redraws left are stored full, as a run starts with them");
            Assert.That(read.Meta.ResolvesLeft, Is.EqualTo(m.Meta.Resolves), "Knight's Resolve's blows left: the same");
            Assert.That(read.Focus, Is.EqualTo(FocusMode.SteadyHand));
            Assert.That(read.FocusOffered && read.FocusTaken, Is.True);
        }

        [Test]
        public void ABuildPutOnAFreshRun_AndTakenOffAgain_IsTheSameBuild_WithTheSameStatsAndMaximum()
        {
            ARichRun(out RunModifiers m, out RunSkillUpgrades s, out RunFocusChoice f);
            RehearsalBuild stored = RehearsalFile.Parse(RehearsalJson.Write(RehearsalBuild.Capture(m, s, f, Keys, Had)));
            Assert.That(stored, Is.Not.Null);

            var m2 = new RunModifiers { StatsAndTiers = true, StatValues = new StatValues() };
            m2.Take(new Upgrade { Kind = UpgradeKind.RunSpeed, Amount = 0.5f }); // what a fresh run's rules held before: gone
            var s2 = new RunSkillUpgrades();
            var f2 = new RunFocusChoice();
            var shotValues = new BasicShotUpgradeValues();
            stored.ApplyTo(m2, s2, f2, shotValues, 5, 5);
            RehearsalBuild rebuilt = RehearsalBuild.Capture(m2, s2, f2, Keys, Had);

            Assert.That(RehearsalRules.Differences(stored, rebuilt), Is.Empty, "the rebuilt run holds the same build");
            Assert.That(m2.Stats.OneLine(0.4f, 100), Is.EqualTo(m.Stats.OneLine(0.4f, 100)), "her stats");
            Assert.That(m2.MaxHealthFor(100), Is.EqualTo(m.MaxHealthFor(100)), "her maximum");
            Assert.That(m2.DamageShare, Is.EqualTo(m.DamageShare));
            Assert.That(m2.CooldownShare("Quick Shot"), Is.EqualTo(m.CooldownShare("Quick Shot")));
            Assert.That(m2.DashCooldownShare, Is.EqualTo(m.DashCooldownShare));
            foreach (BasicShotUpgradeKind k in RunBasicShotUpgrades.All) Assert.That(m2.BasicShot.TierOf(k), Is.EqualTo(m.BasicShot.TierOf(k)), k.ToString());
            Assert.That(m2.BasicShot.Held, Is.EqualTo(m.BasicShot.Held));
            Assert.That(m2.BasicShot.Values, Is.SameAs(shotValues), "the run's switches and numbers, as a card puts them on");
            Assert.That(m2.BasicShot.ChamberFired, Is.EqualTo(0), "Sixth Chamber's count starts again");
            Assert.That(s2.Taken.Select(u => u.FullName), Is.EqualTo(s.Taken.Select(u => u.FullName)));
            foreach (string skill in Had)
            {
                Assert.That(s2.TierOf(skill), Is.EqualTo(s.TierOf(skill)), skill + "'s tier");
                Assert.That(s2.CountFor(skill), Is.EqualTo(s.CountFor(skill)), skill + "'s pips");
                Assert.That(s2.NamesFor(skill), Is.EqualTo(s.NamesFor(skill)), skill + "'s upgrades");
            }
            Assert.That(f2.Mode, Is.EqualTo(f.Mode));
            Assert.That(f2.Offered, Is.EqualTo(f.Offered));
            Assert.That(f2.Taken, Is.EqualTo(f.Taken));
            Assert.That(m2.Meta.ToString(), Is.EqualTo(m.Meta.ToString()));
            Assert.That(m2.Weapon.ToString(), Is.EqualTo(m.Weapon.ToString()));
        }

        [Test]
        public void AnEmptyBuild_PutOnARun_LeavesItAsAFreshRunHasIt()
        {
            var m = new RunModifiers();
            var s = new RunSkillUpgrades();
            var f = new RunFocusChoice();
            var fresh = new RunModifiers();
            new RehearsalBuild().ApplyTo(m, s, f, new BasicShotUpgradeValues(), 5, 5);
            Assert.That(RehearsalRules.Differences(RehearsalBuild.Capture(fresh, new RunSkillUpgrades(), new RunFocusChoice(), null, null),
                                                   RehearsalBuild.Capture(m, s, f, null, null)), Is.Empty);
            Assert.That(m.BasicShot.Values, Is.Not.Null);
            Assert.That(m.Count, Is.EqualTo(0));
            Assert.That(f.Offered || f.Taken, Is.False);
        }

        [Test]
        public void ADifferentBuild_IsSaidField_ByField()
        {
            ARichRun(out RunModifiers m, out RunSkillUpgrades s, out RunFocusChoice f);
            RehearsalBuild a = RehearsalBuild.Capture(m, s, f, Keys, Had);
            RehearsalBuild b = RehearsalJson.Copy(a);
            Assert.That(RehearsalRules.Differences(a, b), Is.Empty);
            b.Meta.DamageShare += 0.01f;
            b.Weapon.Parts.Add("Long Barrel");
            b.Upgrades[1].Amount = 0.06f;
            b.Run = "another run"; // not compared: which run it is from
            List<string> d = RehearsalRules.Differences(a, b);
            Assert.That(d.Count, Is.EqualTo(3), string.Join("\n", d));
            Assert.That(d.Any(x => x.StartsWith("Meta.DamageShare", StringComparison.Ordinal)), Is.True);
            Assert.That(d.Any(x => x.StartsWith("Weapon.Parts", StringComparison.Ordinal)), Is.True);
            Assert.That(d.Any(x => x.StartsWith("Upgrades[1].Amount", StringComparison.Ordinal)), Is.True);
        }

        [Test]
        public void NumbersComeBack_Exactly()
        {
            var b = new RehearsalBuild();
            float[] numbers = { 0.1f, 1f / 3f, 1e-7f, -0.35f, 123456.79f, float.MaxValue, float.Epsilon, 2.5f };
            foreach (float x in numbers) b.Upgrades.Add(new Upgrade { Amount = x });
            b.Meta.DashRechargeScale = float.PositiveInfinity;
            RehearsalBuild back = RehearsalFile.Parse(RehearsalJson.Write(b));
            Assert.That(back.Upgrades.Select(u => u.Amount), Is.EqualTo(numbers));
            Assert.That(float.IsPositiveInfinity(back.Meta.DashRechargeScale), Is.True);
        }

        [Test]
        public void AMissingFile_IsNoBuild_AndNoError()
        {
            Assert.That(File.Exists(FilePath), Is.False);
            Assert.That(RehearsalFile.Load(FilePath), Is.Null);
            Assert.That(RehearsalFile.Load(null), Is.Null);
            Assert.That(RehearsalFile.Load(""), Is.Null);
            Assert.That(Directory.Exists(_folder), Is.False, "nothing is made by a read");
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("not json at all")]
        [TestCase("{\"Version\": 1, \"Keys\": [\"Quick Shot\"")]
        [TestCase("{\"Version\": 1, \"Keys\": \"Quick Shot\"}")]
        [TestCase("{\"Version\": 1, \"MaxHealth\": 1.5}")]
        [TestCase("{\"Version\": 99}")]
        [TestCase("{\"Version\": 0}")]
        [TestCase("{}")]
        [TestCase("[1, 2, 3]")]
        [TestCase("{\"Version\": 1} and more")]
        public void ABrokenFile_IsNoBuild_NoError_AndIsLeftAsItWas(string text)
        {
            Directory.CreateDirectory(_folder);
            File.WriteAllText(FilePath, text);
            byte[] before = File.ReadAllBytes(FilePath);
            RehearsalBuild b = null;
            Assert.DoesNotThrow(() => b = RehearsalFile.Load(FilePath));
            Assert.That(b, Is.Null, text);
            Assert.That(File.ReadAllBytes(FilePath), Is.EqualTo(before), "never renamed, emptied or written over by a read");
            Assert.That(Directory.GetFiles(_folder).Length, Is.EqualTo(1), "nothing kept beside it");
        }

        [Test]
        public void AFileFromAnOlderGame_DropsWhatThisGameNoLongerHas_AndKeepsTheRest()
        {
            ARichRun(out RunModifiers m, out RunSkillUpgrades s, out RunFocusChoice f);
            string text = RehearsalJson.Write(RehearsalBuild.Capture(m, s, f, Keys, Had));
            // What an older game had and this one does not: an upgrade's kind, a basic-shot upgrade, a tier, a focus way, a patch's op.
            text = text.Replace("\"Kind\": \"CritChance\"", "\"Kind\": \"OldKindGone\"")
                       .Replace("\"Kind\": \"PiercingRound\"", "\"Kind\": \"OldShotGone\"")
                       .Replace("\"Tier\": \"Epic\" }]", "\"Tier\": \"Legendary\" }]")
                       .Replace("\"Focus\": \"SteadyHand\"", "\"Focus\": \"OldFocusGone\"")
                       .Replace("\"Op\": \"Add\"", "\"Op\": \"OldOpGone\"");
            Assert.That(text, Does.Contain("OldKindGone").And.Contain("OldShotGone").And.Contain("Legendary").And.Contain("OldFocusGone").And.Contain("OldOpGone"), "the test's text was changed");
            RehearsalBuild b = null;
            Assert.DoesNotThrow(() => b = RehearsalFile.Parse(text));
            Assert.That(b, Is.Not.Null, "read, with what is gone dropped");
            Assert.That(b.Upgrades.Count, Is.EqualTo(m.Taken.Count - 1), "the upgrade of a kind that is gone dropped, the other four kept");
            Assert.That(b.Upgrades.Any(u => u.Kind == UpgradeKind.CritChance), Is.False);
            Assert.That(b.BasicShot.Select(x => x.Kind), Is.EqualTo(new[] { BasicShotUpgradeKind.HotBarrel }), "the basic-shot upgrade that is gone dropped");
            Assert.That(b.SkillTiers, Is.Empty, "a tier that is gone dropped");
            Assert.That(b.Focus, Is.EqualTo(FocusMode.Today), "a focus way that is gone: today's");
            Assert.That(b.SkillUpgrades.Select(u => u.Name), Is.EqualTo(new[] { "Double Tap", "epic step" }), "the upgrade whose patch is gone dropped whole, never half");
            Assert.That(b.Keys, Is.EqualTo(Keys), "the rest kept");
            Assert.That(b.Meta.Pieces, Is.EqualTo(m.Meta.Pieces));
            Assert.That(b.Weapon.Parts, Is.EqualTo(m.Weapon.Parts));

            // A skill that is gone stays in the lists (the rehearsal leaves it out as it starts); put on a run, nothing throws.
            var m2 = new RunModifiers();
            Assert.DoesNotThrow(() => b.ApplyTo(m2, new RunSkillUpgrades(), new RunFocusChoice(), new BasicShotUpgradeValues(), 5, 5));
            Assert.That(m2.Taken.Count, Is.EqualTo(m.Taken.Count - 1));
        }

        [Test]
        public void AGoodFileWithFieldsItDoesNotKnow_OrWithoutSome_IsRead()
        {
            RehearsalBuild b = RehearsalFile.Parse("{\"Version\": 1, \"Keys\": [\"Quick Shot\"], \"SomethingNew\": {\"a\": [1, 2]}, \"MaxHealth\": 130}");
            Assert.That(b, Is.Not.Null);
            Assert.That(b.Keys, Is.EqualTo(new[] { "Quick Shot" }));
            Assert.That(b.MaxHealth, Is.EqualTo(130));
            Assert.That(b.Meta.DashCharges, Is.EqualTo(1), "a part the file lacks: as a fresh run has it");
            Assert.That(b.Weapon.DamageScale, Is.EqualTo(1f));
        }

        [Test]
        public void ARunThatEndedBeforeFloor2_TheRoomLabsRun_ARehearsal_AndTheSwitchOff_KeepNothing()
        {
            Assert.That(RehearsalValues.KeepsFromFloor, Is.EqualTo(2), "the order: only a run that reached at least floor 2");
            Assert.That(RehearsalRules.Keeps(true, false, false, 0), Is.False, "outside a run");
            Assert.That(RehearsalRules.Keeps(true, false, false, 1), Is.False, "a death in the first room does not wipe a good build");
            Assert.That(RehearsalRules.Keeps(true, false, false, 2), Is.True);
            Assert.That(RehearsalRules.Keeps(true, false, false, 5), Is.True, "the act cleared, or a death at the boss");
            Assert.That(RehearsalRules.Keeps(true, true, false, 5), Is.False, "the room lab's run");
            Assert.That(RehearsalRules.Keeps(true, false, true, 5), Is.False, "a rehearsal never replaces the build");
            Assert.That(RehearsalRules.Keeps(false, false, false, 5), Is.False, "the switch off");
        }

        [Test]
        public void TheSwitch_IsOnAsItShips()
        {
            Assert.That(RehearsalValues.On, Is.True, "his tick of 2026-11-27");
            Assert.That(RehearsalValues.FileName, Is.Not.EqualTo("between-runs.json"), "a file of its own");
            Assert.That(RehearsalValues.MenuPlace, Is.EqualTo(1), "right under START RUN");
        }

        [Test]
        public void Saving_WritesOverTheLastBuild_AndLeavesNoTemporaryFile()
        {
            var a = new RehearsalBuild { Run = "first" };
            var b = new RehearsalBuild { Run = "second" };
            Assert.That(RehearsalFile.Save(FilePath, a, out string e1), Is.True, e1);
            Assert.That(RehearsalFile.Save(FilePath, b, out string e2), Is.True, e2);
            Assert.That(RehearsalFile.Load(FilePath).Run, Is.EqualTo("second"), "one build only: the newest run's");
            Assert.That(Directory.GetFiles(_folder).Select(Path.GetFileName), Is.EqualTo(new[] { RehearsalValues.FileName }));
        }
    }
}
