using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx.Configuration;
using CameraUnlock.Core.Config.Testing;
using CameraUnlock.Core.Input;
using ValheimHeadTracking.Legacy;
using UnityEngine;

namespace ValheimHeadTracking.Tests.Differential
{
    /// <summary>One differential input: a legacy file's bytes, or no file at all.</summary>
    internal sealed class DifferentialInput
    {
        public DifferentialInput(string name, byte[] bytes)
        {
            Name = name;
            Bytes = bytes;
        }

        public string Name { get; }

        /// <summary>Null for no file.</summary>
        public byte[] Bytes { get; }
    }

    internal static class Inputs
    {
        public const string LegacyName = "com.cameraunlock.valheim.headtracking.cfg";

        public static string RepoRoot()
        {
            DirectoryInfo dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "pixi.toml"))) dir = dir.Parent;
            if (dir == null) throw new InvalidOperationException("no pixi.toml above " + AppDomain.CurrentDomain.BaseDirectory);
            return dir.FullName;
        }

        public static string FirstRunDir()
        {
            return Path.Combine(RepoRoot(), "tests", "config_differential", "data", "first-run");
        }

        /// <summary>The newest published build's first-run file, the base the corpus mutates.</summary>
        public static byte[] NewestFirstRun()
        {
            return File.ReadAllBytes(Path.Combine(FirstRunDir(), "v0.3.0.cfg"));
        }

        /// <summary>
        /// Every published build's first-run file. v0.1.0, v0.1.1 and v0.1.2 shipped the same DLL,
        /// which writes the same bytes, so one file stands for the three.
        /// </summary>
        public static IEnumerable<DifferentialInput> FirstRuns()
        {
            string[] files = Directory.GetFiles(FirstRunDir(), "*.cfg");
            Array.Sort(files, StringComparer.Ordinal);
            if (files.Length != 8) throw new InvalidOperationException(FirstRunDir() + " holds " + files.Length + " first-run files, not 8");
            foreach (string file in files)
            {
                yield return new DifferentialInput("first run " + Path.GetFileNameWithoutExtension(file), File.ReadAllBytes(file));
            }
        }

        public static IEnumerable<DifferentialInput> Corpus()
        {
            foreach (IniMutation m in IniMutations.Generate(NewestFirstRun(), LegacyConfigKeys.All(), Descriptors()))
            {
                yield return new DifferentialInput("corpus " + m.Name, m.Bytes);
            }
        }

        public static IEnumerable<DifferentialInput> All()
        {
            yield return new DifferentialInput("no file", null);
            yield return new DifferentialInput("empty file", new byte[0]);
            foreach (DifferentialInput input in FirstRuns()) yield return input;
            foreach (DifferentialInput input in Corpus()) yield return input;
        }

        /// <summary>
        /// A descriptor per key the reader reads, in its order: another valid value, and a value
        /// beyond each end of every AcceptableValueRange, which BepInEx clamps.
        /// </summary>
        public static List<MutationKey> Descriptors()
        {
            var none = new string[0];
            var noChords = new ChordSwitch[0];
            Func<string, string, string, string[], MutationKey> plain =
                (section, key, alternate, outOfRange) => new MutationKey(section, key, alternate, outOfRange, false, noChords);
            Func<string, string, string, MutationKey> hotkey =
                (section, key, alternate) => new MutationKey(section, key, alternate, none, true, noChords);
            return new List<MutationKey>
            {
                plain("Network", "UdpPort", "4343", new[] { "80", "70000" }),
                plain("General", "EnableOnStartup", "false", none),
                plain("Sensitivity", "YawSensitivity", "1.5", new[] { "0.05", "3.5" }),
                plain("Sensitivity", "PitchSensitivity", "1.5", new[] { "0.05", "3.5" }),
                plain("Sensitivity", "RollSensitivity", "0.5", new[] { "0.05", "3.5" }),
                plain("Inversion", "InvertYaw", "true", none),
                plain("Inversion", "InvertPitch", "true", none),
                plain("Inversion", "InvertRoll", "true", none),
                hotkey("Hotkeys", "ToggleKey", "F9"),
                hotkey("Hotkeys", "PositionToggleKey", "F11"),
                hotkey("Hotkeys", "ReticleToggleKey", "F10"),
                plain("Aim Decoupling", "EnableAimDecoupling", "false", none),
                plain("Aim Decoupling", "ShowDecoupledCrosshair", "false", none),
                plain("Position", "PositionLimitY", "0.3", new[] { "-0.1", "1.6" }),
                plain("Position", "PositionLimitYDown", "0.25", new[] { "-0.1", "1.6" }),
                plain("Smoothing", "LocalSmoothing", "0.3", new[] { "-0.1", "1.5" }),
                plain("Smoothing", "RemoteSmoothing", "0.5", new[] { "-0.1", "1.5" }),
                plain("General", "WorldSpaceYaw", "false", none),
                hotkey("Hotkeys", "YawModeKey", "F12"),
            };
        }
    }

    /// <summary>A scratch folder holding at most the legacy file, deleted on dispose.</summary>
    internal sealed class LegacyFolder : IDisposable
    {
        public LegacyFolder(DifferentialInput input)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "vh-diff-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
            LegacyPath = System.IO.Path.Combine(Path, Inputs.LegacyName);
            if (input.Bytes != null) File.WriteAllBytes(LegacyPath, input.Bytes);
        }

        public string Path { get; }

        public string LegacyPath { get; }

        public string[] Entries()
        {
            string[] names = Directory.GetFileSystemEntries(Path).Select(System.IO.Path.GetFileName).ToArray();
            Array.Sort(names, StringComparer.Ordinal);
            return names;
        }

        public void Dispose()
        {
            foreach (string file in Directory.GetFiles(Path)) File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(Path, true);
        }
    }

    /// <summary>
    /// What one run of a legacy reader gave: the settings, or the exception BepInEx threw reading
    /// the file, which in game stops the plugin loading at all.
    /// </summary>
    internal sealed class LegacyOutcome
    {
        public LegacyConfig Config;
        public bool Found;
        public string Error;

        /// <summary>
        /// The oracle only: the RecenterKey v0.3.0 bound and then applied nowhere. The frozen
        /// reader does not read it (see <see cref="ComparisonOneTests"/>).
        /// </summary>
        public KeyCode? RecenterKey;

        public string Describe()
        {
            if (Error != null) return "throws " + Error;
            return "found=" + LegacyStartup.Text(Found) + "\n" + LegacyStartup.Fields(Config);
        }

        public static string ErrorOf(Exception e)
        {
            return e.GetType().FullName + ": " + e.Message;
        }
    }

    /// <summary>
    /// What v0.3.0 ran on for one input, read by its own HeadTrackingConfig and core's
    /// HeadTrackingConfigBase at v0.3.0's pin (oracle/, both byte for byte) on a ConfigFile built
    /// as BepInEx's BaseUnityPlugin builds the plugin's, which reads an existing file at once.
    /// </summary>
    internal static class Oracle
    {
        // HeadTrackingConfig keeps its instance in a static, as the plugin did, so one input at a time.
        private static readonly object Gate = new object();

        public static LegacyOutcome Run(DifferentialInput input)
        {
            using (var folder = new LegacyFolder(input))
            {
                lock (Gate)
                {
                    ConfigFile file;
                    try
                    {
                        file = new ConfigFile(folder.LegacyPath, false);
                        HeadTrackingConfig.Initialize(file);
                    }
                    catch (ArgumentException e)
                    {
                        return new LegacyOutcome { Error = LegacyOutcome.ErrorOf(e) };
                    }
                    return new LegacyOutcome
                    {
                        Found = input.Bytes != null,
                        RecenterKey = (KeyCode)file[new ConfigDefinition("Hotkeys", "RecenterKey")].BoxedValue,
                        Config = new LegacyConfig
                        {
                            UdpPort = HeadTrackingConfig.UdpPort.Value,
                            EnableOnStartup = HeadTrackingConfig.EnableOnStartup.Value,
                            YawSensitivity = HeadTrackingConfig.YawSensitivity.Value,
                            PitchSensitivity = HeadTrackingConfig.PitchSensitivity.Value,
                            RollSensitivity = HeadTrackingConfig.RollSensitivity.Value,
                            InvertYaw = HeadTrackingConfig.InvertYaw.Value,
                            InvertPitch = HeadTrackingConfig.InvertPitch.Value,
                            InvertRoll = HeadTrackingConfig.InvertRoll.Value,
                            ToggleKey = HeadTrackingConfig.ToggleKey.Value,
                            PositionToggleKey = HeadTrackingConfig.PositionToggleKey.Value,
                            ReticleToggleKey = HeadTrackingConfig.ReticleToggleKey.Value,
                            EnableAimDecoupling = HeadTrackingConfig.EnableAimDecoupling.Value,
                            ShowDecoupledCrosshair = HeadTrackingConfig.ShowDecoupledCrosshair.Value,
                            PositionLimitY = HeadTrackingConfig.PositionLimitY.Value,
                            PositionLimitYDown = HeadTrackingConfig.PositionLimitYDown.Value,
                            LocalSmoothing = HeadTrackingConfig.LocalSmoothing.Value,
                            RemoteSmoothing = HeadTrackingConfig.RemoteSmoothing.Value,
                            WorldSpaceYaw = HeadTrackingConfig.WorldSpaceYaw.Value,
                            YawModeKey = HeadTrackingConfig.YawModeKey.Value,
                        },
                    };
                }
            }
        }
    }

    /// <summary>
    /// The frozen reader on one input, on a ConfigFile built as the plugin's is. It must leave the
    /// file and its folder as they were.
    /// </summary>
    internal static class FrozenReader
    {
        public static LegacyOutcome Run(DifferentialInput input)
        {
            using (var folder = new LegacyFolder(input))
            {
                string[] before = folder.Entries();
                DateTime written = input.Bytes == null ? DateTime.MinValue : File.GetLastWriteTimeUtc(folder.LegacyPath);

                var outcome = new LegacyOutcome();
                try
                {
                    outcome.Config = LegacyConfigReader.Read(new ConfigFile(folder.LegacyPath, false), out outcome.Found);
                }
                catch (ArgumentException e)
                {
                    outcome.Error = LegacyOutcome.ErrorOf(e);
                }

                if (!before.SequenceEqual(folder.Entries()))
                    throw new InvalidOperationException(input.Name + ": the frozen reader changed the folder: " + string.Join(", ", folder.Entries()));
                if (input.Bytes != null)
                {
                    if (!File.ReadAllBytes(folder.LegacyPath).SequenceEqual(input.Bytes))
                        throw new InvalidOperationException(input.Name + ": the frozen reader rewrote the legacy file");
                    if (File.GetLastWriteTimeUtc(folder.LegacyPath) != written)
                        throw new InvalidOperationException(input.Name + ": the frozen reader touched the legacy file");
                }
                return outcome;
            }
        }
    }

    /// <summary>
    /// What v0.3.0's plugin Awake, OpenTrackReceiver.UpdateProcessorSettings, CrosshairOffsetHook
    /// and HotkeyHandler set up from its settings, one line per item, floats with their bits.
    /// </summary>
    internal static class LegacyStartup
    {
        /// <summary>The position multiplier v0.3.0 applied on every axis, a constant in its code.</summary>
        public const float PositionSensitivity = 2.0f;

        public static SortedDictionary<string, string> Of(LegacyConfig c)
        {
            var s = new SortedDictionary<string, string>(StringComparer.Ordinal);
            s["TrackingEnabled"] = Text(c.EnableOnStartup);
            // The session started every launch in rotation and position; the cycle key moved it.
            s["RotationEnabled"] = "true";
            s["PositionEnabled"] = "true";
            s["WorldSpaceYaw"] = Text(c.WorldSpaceYaw);
            s["UdpPort"] = c.UdpPort.ToString(CultureInfo.InvariantCulture);
            s["LocalSmoothing"] = Text(c.LocalSmoothing);
            s["RemoteSmoothing"] = Text(c.RemoteSmoothing);
            s["RotationSensitivity"] = Text(c.YawSensitivity) + " " + Text(c.PitchSensitivity) + " " + Text(c.RollSensitivity);
            // Pitch is negated unless InvertPitch is set: OpenTrack's up is Unity's down.
            s["RotationInversion"] = Text(c.InvertYaw) + " " + Text(!c.InvertPitch) + " " + Text(c.InvertRoll);
            s["PositionSensitivity"] = Text(PositionSensitivity) + " " + Text(PositionSensitivity) + " " + Text(PositionSensitivity);
            s["PositionLimits"] = Text(0.60f) + " " + Text(c.PositionLimitY) + " " + Text(c.PositionLimitYDown)
                                  + " " + Text(0.80f) + " " + Text(0.60f);
            s["PositionInversion"] = "true false false";
            s["CrosshairFollowsAim"] = Text(c.EnableAimDecoupling && c.ShowDecoupledCrosshair);
            s["ToggleKey"] = Hotkey(c.ToggleKey, KeyCode.Y);
            s["CycleTrackingModeKey"] = Hotkey(c.PositionToggleKey, KeyCode.G);
            s["YawModeKey"] = Hotkey(c.YawModeKey, KeyCode.H);
            s["ReticleToggleKey"] = Hotkey(c.ReticleToggleKey, KeyCode.U);
            return s;
        }

        /// <summary>Every field, floats with their bits.</summary>
        public static string Fields(LegacyConfig c)
        {
            var text = new StringBuilder();
            foreach (FieldInfo field in typeof(LegacyConfig).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object value = field.GetValue(c);
                string shown = value is float ? Text((float)value)
                    : value is KeyCode ? ((int)(KeyCode)value).ToString(CultureInfo.InvariantCulture) + " " + value
                    : Convert.ToString(value, CultureInfo.InvariantCulture);
                text.Append(field.Name).Append('=').Append(shown).Append('\n');
            }
            return text.ToString();
        }

        public static string Text(bool value)
        {
            return value ? "true" : "false";
        }

        public static string Text(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture) + "/0x"
                   + BitConverter.ToInt32(BitConverter.GetBytes(value), 0).ToString("X8", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The bindings v0.3.0's HotkeyHandler fired an action on: the key, on its down-edge whatever
        /// else was held, unless it was None, and Ctrl+Shift+letter from ChordHotkeys.
        /// </summary>
        public static string Hotkey(KeyCode primary, KeyCode chordLetter)
        {
            string chord = KeyBindings.Format(new[] { new KeyBinding(KeyModifiers.Ctrl | KeyModifiers.Shift, (int)chordLetter) });
            if (primary == KeyCode.None) return chord;
            return KeyName((int)primary) + ", " + chord;
        }

        /// <summary>A Unity key code's name, or the number for one that names no key.</summary>
        public static string KeyName(int unityKeyCode)
        {
            try
            {
                return KeyBindings.Format(new[] { new KeyBinding(KeyModifiers.None, unityKeyCode) });
            }
            catch (ArgumentException)
            {
                return unityKeyCode.ToString(CultureInfo.InvariantCulture);
            }
        }
    }
}
