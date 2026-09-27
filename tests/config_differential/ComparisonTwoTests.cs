using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CameraUnlock.Core.Config;
using CameraUnlock.Core.Input;
using ValheimHeadTracking.Config;
using ValheimHeadTracking.Legacy;
using Xunit;

namespace ValheimHeadTracking.Tests.Differential
{
    /// <summary>
    /// Comparison 2, the import against the migration, and what the import does with each value
    /// v0.3.0 ran on. Every input of comparison 1 is migrated into a new CameraUnlock.ini, from a
    /// writable and from a read-only legacy file, once over the built-in Defaults.ini and once over
    /// a Defaults.ini that differs on every row this game takes from it.
    /// </summary>
    public class ComparisonTwoTests
    {
        /// <summary>Every global row the table binds, each away from its built-in value.</summary>
        private const string OtherDefaults =
            "[CameraUnlock]\r\nConfigFormat=1\r\n\r\n" +
            "[Network]\r\nUdpPort=4343\r\n\r\n" +
            "[General]\r\nEnableOnStartup=false\r\nWorldSpaceYaw=false\r\nRotationEnabled=true\r\n\r\n" +
            "[Smoothing]\r\nLocalSmoothing=0.25\r\nRemoteSmoothing=0.35\r\n\r\n" +
            "[Position]\r\nPositionEnabled=false\r\nPositionLimitY=0.16\r\nPositionLimitYDown=0.17\r\n\r\n" +
            "[Hotkeys]\r\nToggleKey=F8\r\nCycleTrackingModeKey=F7\r\nYawModeKey=F6\r\n";

        // A v0.3.0 .cfg can hold a number for a key, which BepInEx's enum parse accepts and Unity
        // names no key for. No hotkey list can hold it and no approved rule drops it, so the config
        // owner defers these imports: the player keeps what v0.3.0 ran on, nothing is written, and
        // the import runs again at the next start. The reticle key is dropped whatever it holds.
        // These are unresolved, not accepted: core's config-format.json has no rule for them yet
        // (N1 covers native virtual-key codes only). Once it records one, the map applies it and
        // this list is deleted. An input outside it that the codecs cannot hold still fails here.
        private static readonly string[] DeferredValues = { "value 010", "value -1", "value +1", "value space then 1", "value 1 then space", "value 2" };

        private static IEnumerable<string> Deferred()
        {
            return new[] { "[Hotkeys] ToggleKey", "[Hotkeys] PositionToggleKey", "[Hotkeys] YawModeKey" }
                .SelectMany(key => DeferredValues.Select(v => "corpus " + key + ": " + v))
                .OrderBy(n => n, StringComparer.Ordinal);
        }

        private static readonly Lazy<string> MigratedDir = new Lazy<string>(() =>
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "migrated");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            return dir;
        });

        [Fact]
        public void TheMigrationHoldsWhatTheImportReadOverTheBuiltInDefaults()
        {
            Compare(null);
        }

        [Fact]
        public void TheMigrationHoldsWhatTheImportReadOverOtherDefaults()
        {
            Compare(OtherDefaults);
        }

        private static void Compare(string defaultsIni)
        {
            List<DifferentialInput> inputs = Inputs.All().ToList();
            var failures = new ConcurrentBag<string>();
            var deferred = new ConcurrentBag<string>();
            var refused = new ConcurrentBag<string>();
            var created = new ConcurrentDictionary<string, byte[]>(StringComparer.Ordinal);
            byte[] committed = File.ReadAllBytes(ConfigTests.Committed());
            string allDefault = MigrationOutcome.Describe(MigrationOutcome.Run(new DifferentialInput("no file", null), defaultsIni, false).Config);
            Parallel.ForEach(inputs, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, input =>
            {
                ImportOutcome import = ImportOutcome.Run(input);
                foreach (bool readOnly in input.Bytes == null ? new[] { false } : new[] { false, true })
                {
                    string name = input.Name + (readOnly ? " (read-only)" : "");
                    MigrationOutcome migration = MigrationOutcome.Run(input, defaultsIni, readOnly);

                    if (import.Error != null || migration.Error != null)
                    {
                        if (import.Error != migration.Error) failures.Add(name + ": import " + import.Error + ", migration " + migration.Error);
                        if (!readOnly) refused.Add(input.Name);
                        continue;
                    }

                    string imported = MigrationOutcome.Describe(import.Config);
                    string migrated = MigrationOutcome.Describe(migration.Config);
                    // A row the import leaves to Defaults.ini holds what default gives, not what
                    // the import read, where the two differ.
                    string expected = FollowDefaults(imported, allDefault, import.Result.FollowsDefaultsIni);
                    if (input.Bytes == null)
                    {
                        if (migration.Status != ConfigLoadStatus.Created) failures.Add(name + ": " + migration.Status);
                        if (!migration.Created.SequenceEqual(committed)) failures.Add(name + ": the created file is not config/CameraUnlock.ini");
                        if (expected != migrated) failures.Add(name + ":\n" + Diff(expected, migrated));
                        continue;
                    }

                    if (migration.Status == ConfigLoadStatus.Deferred)
                    {
                        if (!readOnly) deferred.Add(input.Name);
                        if (!migration.Reason.Contains("cannot be converted")) failures.Add(name + ": deferred: " + migration.Reason);
                    }
                    else if (migration.Status != ConfigLoadStatus.Migrated)
                    {
                        failures.Add(name + ": " + migration.Status + ": " + migration.Reason);
                        continue;
                    }
                    else
                    {
                        created[Sha256(migration.Created)] = migration.Created;
                    }
                    if (expected != migrated) failures.Add(name + ":\n" + Diff(expected, migrated));
                }
            });
            Assert.True(failures.IsEmpty, string.Join("\n", failures.OrderBy(f => f, StringComparer.Ordinal).Take(20)));
            // Handed to core's canonical config lint by tests/config_differential/lint-migrated.mjs,
            // which pixi run test runs next.
            foreach (KeyValuePair<string, byte[]> file in created)
            {
                File.WriteAllBytes(Path.Combine(MigratedDir.Value, file.Key + ".ini"), file.Value);
            }
            Assert.Equal(ComparisonOneTests.RefusedByBepInEx(), refused.OrderBy(n => n, StringComparer.Ordinal));
            Assert.Equal(Deferred(), deferred.OrderBy(n => n, StringComparer.Ordinal));
        }

        /// <summary>
        /// The map proof: on every input, what the converted plugin runs on from the import is what
        /// v0.3.0 ran on, apart from exactly the values the approved changes drop.
        /// </summary>
        [Fact]
        public void TheImportKeepsEverySettingButTheApprovedDrops()
        {
            var failures = new ConcurrentBag<string>();
            var changedAlone = new ConcurrentBag<string>();
            Parallel.ForEach(Inputs.All().ToList(), new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, input =>
            {
                LegacyOutcome oracle = Oracle.Run(input);
                ImportOutcome import = ImportOutcome.Run(input);
                if (oracle.Error != null)
                {
                    if (import.Error != oracle.Error) failures.Add(input.Name + ": " + import.Error);
                    return;
                }
                LegacyConfig old = oracle.Config;
                ImportResult result = import.Result;
                ImportStatus status = input.Bytes == null ? ImportStatus.Absent : ImportStatus.Imported;
                if (result.Status != status) failures.Add(input.Name + ": " + result.Status);

                SortedDictionary<string, string> before = LegacyStartup.Of(old);
                SortedDictionary<string, string> after = ConvertedStartup.Of(import.Config);
                // A group is compared unless one of its values was dropped: the folded values are
                // what the runtime applies now, so they must equal what v0.3.0 applied.
                bool sensitivityDropped = result.Dropped.Any(d => d.Rule == DropRule.PoseShaping && d.Section == "Sensitivity");
                bool inversionDropped = result.Dropped.Any(d => d.Rule == DropRule.PoseShaping && d.Section == "Inversion");
                bool crosshairDropped = result.Dropped.Any(d => d.Section == "Aim Decoupling");
                var unboundKeys = new HashSet<string>(result.Dropped.Where(d => d.Rule == DropRule.ModifierKey).Select(d => StartupKey(d.Key)));
                foreach (string key in before.Keys)
                {
                    if (key == "RotationSensitivity" && sensitivityDropped) continue;
                    if (key == "RotationInversion" && inversionDropped) continue;
                    if (key == "CrosshairFollowsAim" && crosshairDropped) continue;
                    if (key == "ReticleToggleKey") continue;
                    if (unboundKeys.Contains(key))
                    {
                        // N3: the Ctrl, Shift or Alt key is unbound and the chord stays.
                        string chord = before[key].Substring(before[key].IndexOf("Ctrl+Shift+", StringComparison.Ordinal));
                        if (after[key] != chord) failures.Add(input.Name + ": " + key + " " + before[key] + " -> " + after[key]);
                        continue;
                    }
                    if (before[key] != after[key]) failures.Add(input.Name + ": " + key + " " + before[key] + " -> " + after[key]);
                }
                if (after.ContainsKey("ReticleToggleKey")) failures.Add(input.Name + ": a reticle toggle");

                var expectedDrops = new List<string>();
                var expectedShaping = new List<string>();
                Action<string, string, float, float> shaping = (section, key, value, shipped) =>
                {
                    bool folded = value == shipped;
                    expectedShaping.Add(section + " " + key + " " + Codec(value) + " " + Codec(shipped) + " " + folded);
                    if (!folded) expectedDrops.Add("PoseShaping " + section + " " + key + " " + Codec(value));
                };
                Action<string, string, bool> inversion = (section, key, value) =>
                {
                    bool folded = !value;
                    expectedShaping.Add(section + " " + key + " " + LegacyStartup.Text(value) + " false " + folded);
                    if (!folded) expectedDrops.Add("PoseShaping " + section + " " + key + " true");
                };
                Action<string, UnityEngine.KeyCode> modifier = (key, code) =>
                {
                    if (IsModifierKey((int)code)) expectedDrops.Add("ModifierKey Hotkeys " + key + " " + code);
                };
                modifier("ToggleKey", old.ToggleKey);
                modifier("PositionToggleKey", old.PositionToggleKey);
                modifier("YawModeKey", old.YawModeKey);
                if (old.ReticleToggleKey != UnityEngine.KeyCode.None)
                    expectedDrops.Add("Reticle Hotkeys ReticleToggleKey " + LegacyStartup.KeyName((int)old.ReticleToggleKey));
                if (!old.EnableAimDecoupling) expectedDrops.Add("CoupledAim Aim Decoupling EnableAimDecoupling false");
                if (!old.ShowDecoupledCrosshair) expectedDrops.Add("Reticle Aim Decoupling ShowDecoupledCrosshair false");
                shaping("Sensitivity", "YawSensitivity", old.YawSensitivity, 1.0f);
                shaping("Sensitivity", "PitchSensitivity", old.PitchSensitivity, 1.0f);
                shaping("Sensitivity", "RollSensitivity", old.RollSensitivity, 1.0f);
                inversion("Inversion", "InvertYaw", old.InvertYaw);
                inversion("Inversion", "InvertPitch", old.InvertPitch);
                inversion("Inversion", "InvertRoll", old.InvertRoll);

                string[] drops = result.Dropped.Select(d => d.Rule + " " + d.Section + " " + d.Key + " " + d.Value).ToArray();
                string[] poses = result.PoseShaping.Select(p => p.Section + " " + p.Key + " " + p.Value + " " + p.Shipped + " " + p.Folded).ToArray();
                if (!drops.SequenceEqual(expectedDrops)) failures.Add(input.Name + ": dropped " + string.Join("; ", drops));
                if (!poses.SequenceEqual(expectedShaping)) failures.Add(input.Name + ": pose shaping " + string.Join("; ", poses));

                string[] follows = result.FollowsDefaultsIni.Select(c => c.Key).ToArray();
                string[] expectedFollows = Untouched(old);
                if (!follows.SequenceEqual(expectedFollows)) failures.Add(input.Name + ": follows Defaults.ini " + string.Join(", ", follows));
                if (expectedFollows.Length == AllRows.Length - 1) changedAlone.Add(AllRows.Except(expectedFollows).Single());
            });
            Assert.True(failures.IsEmpty, string.Join("\n", failures.OrderBy(f => f, StringComparer.Ordinal).Take(20)));
            // Every row the .cfg can set is changed alone by some corpus input, and left out there.
            Assert.Equal(AllRows.Where(r => r != "RotationEnabled" && r != "PositionEnabled").OrderBy(r => r, StringComparer.Ordinal),
                changedAlone.Distinct().OrderBy(r => r, StringComparer.Ordinal));
        }

        /// <summary>Every row of the table that follows Defaults.ini, in the order the map gives them.</summary>
        private static readonly string[] AllRows =
        {
            "UdpPort", "EnableOnStartup", "WorldSpaceYaw", "ToggleKey", "CycleTrackingModeKey", "YawModeKey",
            "RotationEnabled", "PositionEnabled", "LocalSmoothing", "RemoteSmoothing", "PositionLimitY", "PositionLimitYDown",
        };

        /// <summary>
        /// The rows a .cfg read as <paramref name="old"/> leaves to Defaults.ini: each whose value is
        /// what v0.3.0 shipped, and the tracking mode, which no build had a setting for.
        /// </summary>
        private static string[] Untouched(LegacyConfig old)
        {
            var shipped = new LegacyConfig();
            var rows = new List<string>();
            Action<string, bool> row = (name, unchanged) => { if (unchanged) rows.Add(name); };
            row("UdpPort", old.UdpPort == shipped.UdpPort);
            row("EnableOnStartup", old.EnableOnStartup == shipped.EnableOnStartup);
            row("WorldSpaceYaw", old.WorldSpaceYaw == shipped.WorldSpaceYaw);
            row("ToggleKey", old.ToggleKey == shipped.ToggleKey);
            row("CycleTrackingModeKey", old.PositionToggleKey == shipped.PositionToggleKey);
            row("YawModeKey", old.YawModeKey == shipped.YawModeKey);
            rows.Add("RotationEnabled");
            rows.Add("PositionEnabled");
            row("LocalSmoothing", old.LocalSmoothing.Equals(shipped.LocalSmoothing));
            row("RemoteSmoothing", old.RemoteSmoothing.Equals(shipped.RemoteSmoothing));
            row("PositionLimitY", old.PositionLimitY.Equals(shipped.PositionLimitY));
            row("PositionLimitYDown", old.PositionLimitYDown.Equals(shipped.PositionLimitYDown));
            return rows.ToArray();
        }

        private static string StartupKey(string legacyKey)
        {
            return legacyKey == "PositionToggleKey" ? "CycleTrackingModeKey" : legacyKey;
        }

        /// <summary>Unity's RightShift to LeftAlt, the keys N3 unbinds.</summary>
        private static bool IsModifierKey(int unityKeyCode)
        {
            return unityKeyCode >= (int)UnityEngine.KeyCode.RightShift && unityKeyCode <= (int)UnityEngine.KeyCode.LeftAlt;
        }

        /// <summary>
        /// <paramref name="imported"/> with the line of each row in <paramref name="follows"/> taken
        /// from <paramref name="allDefault"/>, a config holding what default gives on every row.
        /// </summary>
        private static string FollowDefaults(string imported, string allDefault, IEnumerable<ConceptDescriptor> follows)
        {
            Dictionary<string, string> defaults = allDefault.Split('\n').Where(l => l.Length > 0)
                .ToDictionary(l => l.Substring(0, l.IndexOf('=')), l => l, StringComparer.Ordinal);
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (ConceptDescriptor concept in follows)
            {
                names.Add(concept.Key);
                if (concept.Key == "RotationEnabled" || concept.Key == "PositionEnabled")
                {
                    names.Add("RotationEnabled");
                    names.Add("PositionEnabled");
                }
                if (concept.Key == "LocalSmoothing" || concept.Key == "RemoteSmoothing") names.Add("Position." + concept.Key);
            }
            return string.Join("\n", imported.Split('\n').Select(l =>
            {
                int eq = l.IndexOf('=');
                return eq > 0 && names.Contains(l.Substring(0, eq)) ? defaults[l.Substring(0, eq)] : l;
            }));
        }

        /// <summary>
        /// The owner rule of 2026-09-26: a setting the player never changed from what v0.3.0 shipped
        /// follows Defaults.ini. With no file, an empty file and the newest first-run file, every
        /// row is left there, the tracking mode pair included.
        /// </summary>
        [Fact]
        public void EveryUntouchedRowFollowsDefaultsIni()
        {
            var inputs = new[]
            {
                new DifferentialInput("no file", null),
                new DifferentialInput("empty file", new byte[0]),
                new DifferentialInput("first run v0.3.0", Inputs.NewestFirstRun()),
            };
            foreach (DifferentialInput input in inputs)
            {
                ImportOutcome import = ImportOutcome.Run(input);
                Assert.Equal(AllRows.OrderBy(r => r, StringComparer.Ordinal),
                    import.Result.FollowsDefaultsIni.Select(c => c.Key).OrderBy(r => r, StringComparer.Ordinal));
            }
        }

        /// <summary>
        /// Over a Defaults.ini holding another value on every row, the newest first-run file migrates
        /// into the committed file and takes every value from Defaults.ini, and a .cfg that changed
        /// one setting keeps that one and takes the rest from Defaults.ini.
        /// </summary>
        [Fact]
        public void UntouchedRowsTakeDefaultsIniAndAChangedRowKeepsThePlayers()
        {
            string committed = Encoding.ASCII.GetString(File.ReadAllBytes(ConfigTests.Committed()));
            MigrationOutcome shipped = MigrationOutcome.Run(new DifferentialInput("first run v0.3.0", Inputs.NewestFirstRun()), OtherDefaults, false);
            Assert.Equal(ConfigLoadStatus.Migrated, shipped.Status);
            Assert.Equal(committed, Encoding.ASCII.GetString(shipped.Created));
            ValheimConfig c = shipped.Config;
            Assert.Equal(4343, c.UdpPort);
            Assert.False(c.EnableOnStartup);
            Assert.False(c.WorldSpaceYaw);
            Assert.True(c.RotationEnabled);
            Assert.False(c.PositionEnabled);
            Assert.Equal(0.25f, c.LocalSmoothing);
            Assert.Equal(0.35f, c.RemoteSmoothing);
            Assert.Equal(0.16f, c.Position.LimitY);
            Assert.Equal(0.17f, c.Position.LimitYDown);
            Assert.Equal("F8", c.ToggleKeyName);
            Assert.Equal("F7", c.CycleTrackingModeKeyName);
            Assert.Equal("F6", c.YawModeKeyName);

            string shippedCfg = Encoding.ASCII.GetString(Inputs.NewestFirstRun());
            string changedCfg = shippedCfg.Replace("LocalSmoothing = 0\r\n", "LocalSmoothing = 0.5\r\n");
            Assert.NotEqual(shippedCfg, changedCfg);
            MigrationOutcome changed = MigrationOutcome.Run(new DifferentialInput("LocalSmoothing 0.5", Encoding.ASCII.GetBytes(changedCfg)), OtherDefaults, false);
            Assert.Equal(ConfigLoadStatus.Migrated, changed.Status);
            Assert.Equal(committed.Replace("LocalSmoothing=default", "LocalSmoothing=0.5"), Encoding.ASCII.GetString(changed.Created));
            Assert.Equal(0.5f, changed.Config.LocalSmoothing);
            Assert.Equal(0.35f, changed.Config.RemoteSmoothing);
            Assert.Equal(4343, changed.Config.UdpPort);
            Assert.Equal("F8", changed.Config.ToggleKeyName);
            Assert.False(changed.Config.PositionEnabled);
        }

        /// <summary>
        /// Every build's shipped defaults fold: the pose-shaping values of each first-run file are
        /// the ones the conversion moved into code, so nothing is dropped for a player who never
        /// changed them.
        /// </summary>
        [Fact]
        public void EveryFirstRunFileFoldsItsPoseShaping()
        {
            foreach (DifferentialInput input in Inputs.FirstRuns())
            {
                ImportOutcome import = ImportOutcome.Run(input);
                Assert.True(import.Result.PoseShaping.All(p => p.Folded), input.Name);
                Assert.DoesNotContain(import.Result.Dropped, d => d.Rule == DropRule.PoseShaping);
            }
        }

        /// <summary>
        /// Fresh equals upgrade: the newest published build's first-run file migrates, over the
        /// built-in Defaults.ini, into the committed file. Its vertical limits, 0.6 and 0.4, are
        /// what v0.3.0 shipped, so they follow Defaults.ini like every other untouched setting.
        /// </summary>
        [Fact]
        public void TheNewestFirstRunMigratesToTheCommittedFile()
        {
            MigrationOutcome migration = MigrationOutcome.Run(
                new DifferentialInput("first run v0.3.0", Inputs.NewestFirstRun()), null, false);

            Assert.Equal(ConfigLoadStatus.Migrated, migration.Status);
            string committed = Encoding.ASCII.GetString(File.ReadAllBytes(ConfigTests.Committed()));
            Assert.Equal(committed, Encoding.ASCII.GetString(migration.Created));
            Assert.Contains(migration.Log, l => l.Contains("not carried: [Hotkeys] ReticleToggleKey=Insert"));
            Assert.Contains(migration.Log, l => l.Contains("not carried: [Hotkeys] RecenterKey=Home"));
            Assert.DoesNotContain(migration.Log, l => l.Contains("not carried: [Aim Decoupling]"));
        }

        /// <summary>Every KeyCode a .cfg can name converts to the key name that reads back as it.</summary>
        [Fact]
        public void EveryUnityKeyCodeConvertsToItsName()
        {
            string keys = File.ReadAllText(Path.Combine(ConfigTests.RepoRoot(), "cameraunlock-core", "data", "keys.json"));
            var codes = new List<int>();
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(keys, "\"unity\":\\s*(\\d+)"))
            {
                codes.Add(int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
            }
            Assert.True(codes.Count > 300, "keys.json gave " + codes.Count + " Unity codes");
            foreach (int code in codes.Where(IsModifierKey))
            {
                var dropped = new List<DroppedValue>();
                Assert.Equal("Ctrl+Shift+Y", LegacyConfigImport.HotkeyList((UnityEngine.KeyCode)code, UnityEngine.KeyCode.Y, "ToggleKey", dropped));
                DroppedValue drop = Assert.Single(dropped);
                Assert.Equal(DropRule.ModifierKey, drop.Rule);
                Assert.Equal("Hotkeys ToggleKey " + (UnityEngine.KeyCode)code, drop.Section + " " + drop.Key + " " + drop.Value);
            }
            Assert.Equal(6, codes.Count(IsModifierKey));
            foreach (int code in codes.Where(c => c != 0 && !IsModifierKey(c)))
            {
                var dropped = new List<DroppedValue>();
                string list = LegacyConfigImport.HotkeyList((UnityEngine.KeyCode)code, UnityEngine.KeyCode.Y, "ToggleKey", dropped);
                Assert.Empty(dropped);
                KeyBinding[] bindings;
                string error;
                Assert.True(KeyBindings.TryParse(list, out bindings, out error), code + ": " + list + ": " + error);
                Assert.Equal(new KeyBinding(KeyModifiers.None, code), bindings[0]);
                Assert.Equal(new KeyBinding(KeyModifiers.Ctrl | KeyModifiers.Shift, (int)UnityEngine.KeyCode.Y), bindings[1]);
            }
            Assert.Equal("Ctrl+Shift+G", LegacyConfigImport.HotkeyList(UnityEngine.KeyCode.None, UnityEngine.KeyCode.G, "PositionToggleKey", new List<DroppedValue>()));
        }

        private static string Codec(float value)
        {
            return Encoding.ASCII.GetString(new FloatCodec().Render(value));
        }

        private static string Sha256(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                var text = new StringBuilder();
                foreach (byte b in sha.ComputeHash(bytes)) text.Append(b.ToString("x2"));
                return text.ToString();
            }
        }

        private static string Diff(string expected, string actual)
        {
            string[] e = expected.Split('\n');
            string[] a = actual.Split('\n');
            var lines = new List<string>();
            for (int i = 0; i < e.Length && i < a.Length; i++)
            {
                if (e[i] != a[i]) lines.Add("  expected " + e[i] + " | got " + a[i]);
            }
            return string.Join("\n", lines);
        }
    }
}
