using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ValheimHeadTracking.Legacy;
using Xunit;

namespace ValheimHeadTracking.Tests.Differential
{
    /// <summary>
    /// Comparison 1: v0.3.0's own reader (oracle/HeadTrackingConfig.cs and core's
    /// HeadTrackingConfigBase at v0.3.0's pin, both the published files byte for byte) against the
    /// frozen reader in src/ValheimHeadTracking/Legacy, on every input. A difference here is
    /// something players would see change that the conversion did not cause, and each one is
    /// listed with the commit that made it.
    /// <para>
    /// There is one. v0.3.0 bound [Hotkeys] RecenterKey through core's base class and applied it
    /// nowhere: the mod stopped keeping a centre of its own in 66f40ba, before v0.3.0. Core's
    /// e92f4bf stopped binding it, and this repo took that in 479b1f5, so the frozen reader does not
    /// read it and a new start leaves it out of the .cfg it saves. Every setting v0.3.0 ran on, and
    /// everything it set up from them, is the same.
    /// </para>
    /// </summary>
    public class ComparisonOneTests
    {
        [Fact]
        public void TheFrozenReaderReadsEveryInputAsV030Did()
        {
            List<DifferentialInput> inputs = Inputs.All().ToList();
            Assert.True(inputs.Count > 1000, "the corpus gave only " + inputs.Count + " inputs");
            var failures = new ConcurrentBag<string>();
            var throwing = new ConcurrentBag<string>();
            Parallel.ForEach(inputs, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, input =>
            {
                LegacyOutcome oracle = Oracle.Run(input);
                LegacyOutcome frozen = FrozenReader.Run(input);
                if (oracle.Error != null) throwing.Add(input.Name);
                if (oracle.Describe() != frozen.Describe())
                {
                    failures.Add(input.Name + ":\n" + Diff(oracle.Describe(), frozen.Describe()));
                }
                else if (oracle.Config != null && !LegacyStartup.Of(oracle.Config).SequenceEqual(LegacyStartup.Of(frozen.Config)))
                {
                    failures.Add(input.Name + ": startup differs");
                }
            });
            Assert.True(failures.IsEmpty, string.Join("\n", failures.OrderBy(f => f, StringComparer.Ordinal).Take(20)));

            // BepInEx refuses a section header with a space inside its brackets, in the constructor
            // BaseUnityPlugin runs, so v0.3.0 never loaded on such a file, and nothing after it
            // does either: the plugin's own code never runs. These are the only such inputs.
            Assert.Equal(RefusedByBepInEx(), throwing.OrderBy(n => n, StringComparer.Ordinal));
        }

        /// <summary>The one listed difference: v0.3.0 reads RecenterKey, the frozen reader does not.</summary>
        [Fact]
        public void OnlyV030ReadsRecenterKey()
        {
            Assert.DoesNotContain(LegacyConfigKeys.All(), k => k.Key == "RecenterKey");
            Assert.DoesNotContain(typeof(LegacyConfig).GetFields(), f => f.Name == "RecenterKey");

            LegacyOutcome shipped = Oracle.Run(new DifferentialInput("first run v0.3.0", Inputs.NewestFirstRun()));
            Assert.Equal(UnityEngine.KeyCode.Home, shipped.RecenterKey);
        }

        internal static IEnumerable<string> RefusedByBepInEx()
        {
            return new[] { "Aim Decoupling", "General", "Hotkeys", "Inversion", "Network", "Position", "Sensitivity", "Smoothing" }
                .Select(s => "corpus [" + s + "]: header with spaces")
                .OrderBy(n => n, StringComparer.Ordinal);
        }

        [Fact]
        public void EveryRecordedFileHoldsTheBytesItsProvenanceNames()
        {
            string root = Inputs.RepoRoot();
            string provenance = Path.Combine(root, "tests", "config_differential", "provenance.tsv");
            int checkedFiles = 0;
            foreach (string line in File.ReadAllLines(provenance))
            {
                if (line.Length == 0 || line[0] == '#') continue;
                string[] fields = line.Split('\t');
                Assert.Equal(3, fields.Length);
                string path = fields[1];
                if (path.Contains(":") || path.Contains(" ")) continue;
                string full = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
                Assert.True(File.Exists(full), path + " is missing");
                Assert.Equal(fields[2], Sha256(File.ReadAllBytes(full)));
                checkedFiles++;
            }
            Assert.True(checkedFiles >= 13, "provenance.tsv names only " + checkedFiles + " repo files");
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
                if (e[i] != a[i]) lines.Add("  oracle " + e[i] + " | frozen " + a[i]);
            }
            return string.Join("\n", lines);
        }
    }
}
