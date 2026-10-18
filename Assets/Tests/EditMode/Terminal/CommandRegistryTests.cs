using NUnit.Framework;
using PkillGame.Terminal;

namespace PkillGame.Tests.Terminal
{
    public class CommandRegistryTests
    {
        CommandRegistry m_Registry;

        [SetUp]
        public void SetUp() => m_Registry = new CommandRegistry();

        // ── Unknown command ───────────────────────────────────────────────────────

        [Test]
        public void Execute_UnknownCommand_ReturnsCommandNotFound()
        {
            var result = m_Registry.Execute("foobar", System.Array.Empty<string>());
            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Lines[0].Contains("command not found"));
        }

        [Test]
        public void Execute_UnknownCommand_SecondLinePromptsHelp()
        {
            var result = m_Registry.Execute("foobar", System.Array.Empty<string>());
            Assert.IsTrue(result.Lines[1].Contains("help"));
        }

        [Test]
        public void Execute_CommandNameIsCaseInsensitive()
        {
            // "HELP" should resolve the same as "help"
            var result = m_Registry.Execute("HELP", System.Array.Empty<string>());
            Assert.IsFalse(result.IsError);
        }

        // ── help ──────────────────────────────────────────────────────────────────

        [Test]
        public void Help_NoArgs_IsNotError()
        {
            var result = m_Registry.Execute("help", System.Array.Empty<string>());
            Assert.IsFalse(result.IsError);
        }

        [Test]
        public void Help_NoArgs_ListsAllSixCommands()
        {
            var result = m_Registry.Execute("help", System.Array.Empty<string>());
            var joined = string.Join("\n", result.Lines);
            Assert.IsTrue(joined.Contains("spawn"), "missing: spawn");
            Assert.IsTrue(joined.Contains("del"),   "missing: del");
            Assert.IsTrue(joined.Contains("give"),  "missing: give");
            Assert.IsTrue(joined.Contains("tp"),    "missing: tp");
            Assert.IsTrue(joined.Contains("say"),   "missing: say");
            Assert.IsTrue(joined.Contains("help"),  "missing: help itself");
        }

        [Test]
        public void Help_KnownCommand_ShowsItsUsage()
        {
            var result = m_Registry.Execute("help", new[] { "tp" });
            Assert.IsFalse(result.IsError);
            Assert.IsTrue(string.Join("\n", result.Lines).Contains("tp <x> <y> <z>"));
        }

        [Test]
        public void Help_UnknownCommand_ReturnsError()
        {
            var result = m_Registry.Execute("help", new[] { "nonexistent" });
            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Lines[0].Contains("no manual entry"));
        }

        // ── spawn ─────────────────────────────────────────────────────────────────

        [Test]
        public void Spawn_NoArgs_ReturnsUsageError()
        {
            var result = m_Registry.Execute("spawn", System.Array.Empty<string>());
            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Lines[0].Contains("usage:"));
        }

        [Test]
        public void Spawn_NoDelegateWired_ReturnsError()
        {
            var result = m_Registry.Execute("spawn", new[] { "box" });
            Assert.IsTrue(result.IsError);
        }

        [Test]
        public void Spawn_DelegateWired_IsNotError()
        {
            m_Registry.OnSpawn = _ => CommandResult.Ok("spawned");
            var result = m_Registry.Execute("spawn", new[] { "box" });
            Assert.IsFalse(result.IsError);
        }

        [Test]
        public void Spawn_DelegateWired_ReceivesPrefabName()
        {
            string captured = null;
            m_Registry.OnSpawn = name => { captured = name; return CommandResult.Ok(); };

            m_Registry.Execute("spawn", new[] { "box" });
            Assert.AreEqual("box", captured);
        }

        [Test]
        public void Spawn_DelegateReturnsError_PropagatesError()
        {
            m_Registry.OnSpawn = _ => CommandResult.Error("prop limit reached (20/20)");
            var result = m_Registry.Execute("spawn", new[] { "box" });
            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Lines[0].Contains("prop limit"));
        }

        // ── del ───────────────────────────────────────────────────────────────────

        [Test]
        public void Del_NoDelegateWired_ReturnsError()
        {
            var result = m_Registry.Execute("del", System.Array.Empty<string>());
            Assert.IsTrue(result.IsError);
        }

        [Test]
        public void Del_DelegateWired_IsInvoked()
        {
            bool called = false;
            m_Registry.OnDel = () => { called = true; return CommandResult.Ok("deleted"); };

            var result = m_Registry.Execute("del", System.Array.Empty<string>());
            Assert.IsFalse(result.IsError);
            Assert.IsTrue(called);
        }

        // ── give ──────────────────────────────────────────────────────────────────

        [Test]
        public void Give_NoArgs_ReturnsUsageError()
        {
            var result = m_Registry.Execute("give", System.Array.Empty<string>());
            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Lines[0].Contains("usage:"));
        }

        [Test]
        public void Give_NoDelegateWired_ReturnsError()
        {
            var result = m_Registry.Execute("give", new[] { "shotgun" });
            Assert.IsTrue(result.IsError);
        }

        [Test]
        public void Give_DelegateWired_ReceivesItemName()
        {
            string captured = null;
            m_Registry.OnGive = item => { captured = item; return CommandResult.Ok(); };

            m_Registry.Execute("give", new[] { "shotgun" });
            Assert.AreEqual("shotgun", captured);
        }

        // ── tp ────────────────────────────────────────────────────────────────────

        [Test]
        public void Tp_NoArgs_ReturnsUsageError()
        {
            var result = m_Registry.Execute("tp", System.Array.Empty<string>());
            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Lines[0].Contains("usage:"));
        }

        [Test]
        public void Tp_TooFewArgs_ReturnsUsageError()
        {
            var result = m_Registry.Execute("tp", new[] { "10", "5" });
            Assert.IsTrue(result.IsError);
        }

        [Test]
        public void Tp_NonNumericArgs_ReturnsUsageError()
        {
            var result = m_Registry.Execute("tp", new[] { "abc", "0", "0" });
            Assert.IsTrue(result.IsError);
        }

        [Test]
        public void Tp_ValidIntegerArgs_InvokedWithCorrectCoords()
        {
            float rx = 0, ry = 0, rz = 0;
            m_Registry.OnTeleport = (x, y, z) => { rx = x; ry = y; rz = z; return CommandResult.Ok(); };

            m_Registry.Execute("tp", new[] { "10", "0", "7" });
            Assert.AreEqual(10f, rx, 0.001f);
            Assert.AreEqual(0f,  ry, 0.001f);
            Assert.AreEqual(7f,  rz, 0.001f);
        }

        [Test]
        public void Tp_NoDelegateWired_ReturnsError()
        {
            var result = m_Registry.Execute("tp", new[] { "1", "2", "3" });
            Assert.IsTrue(result.IsError);
        }

        // ── say ───────────────────────────────────────────────────────────────────

        [Test]
        public void Say_NoArgs_ReturnsUsageError()
        {
            var result = m_Registry.Execute("say", System.Array.Empty<string>());
            Assert.IsTrue(result.IsError);
            Assert.IsTrue(result.Lines[0].Contains("usage:"));
        }

        [Test]
        public void Say_NoDelegateWired_ReturnsError()
        {
            var result = m_Registry.Execute("say", new[] { "hello" });
            Assert.IsTrue(result.IsError);
        }

        [Test]
        public void Say_SingleWord_PassedThrough()
        {
            string captured = null;
            m_Registry.OnSay = msg => { captured = msg; return CommandResult.Ok(); };

            m_Registry.Execute("say", new[] { "gg" });
            Assert.AreEqual("gg", captured);
        }

        [Test]
        public void Say_MultipleWords_JoinedWithSpaces()
        {
            string captured = null;
            m_Registry.OnSay = msg => { captured = msg; return CommandResult.Ok(); };

            m_Registry.Execute("say", new[] { "hello", "world" });
            Assert.AreEqual("hello world", captured);
        }

        // ── Autocomplete ──────────────────────────────────────────────────────────

        [Test]
        public void Autocomplete_Spawn_PartialMatch()
        {
            Assert.AreEqual("spawn", m_Registry.Autocomplete("sp"));
        }

        [Test]
        public void Autocomplete_Give_PartialMatch()
        {
            Assert.AreEqual("give", m_Registry.Autocomplete("gi"));
        }

        [Test]
        public void Autocomplete_Help_PartialMatch()
        {
            Assert.AreEqual("help", m_Registry.Autocomplete("he"));
        }

        [Test]
        public void Autocomplete_ExactMatch_ReturnsNull()
        {
            // Already a complete command — nothing to tab-complete to
            Assert.IsNull(m_Registry.Autocomplete("spawn"));
        }

        [Test]
        public void Autocomplete_NoMatch_ReturnsNull()
        {
            Assert.IsNull(m_Registry.Autocomplete("zzz"));
        }

        [Test]
        public void Autocomplete_UpperCase_IsCaseInsensitive()
        {
            Assert.AreEqual("spawn", m_Registry.Autocomplete("SP"));
        }

        [Test]
        public void Autocomplete_EmptyString_ReturnsACommand()
        {
            // Any empty input should still find some match
            Assert.IsNotNull(m_Registry.Autocomplete(""));
        }

        // ── CommandResult helpers ─────────────────────────────────────────────────

        [Test]
        public void CommandResult_Ok_IsNotError()
        {
            var r = CommandResult.Ok("line1", "line2");
            Assert.IsFalse(r.IsError);
            Assert.AreEqual(2, r.Lines.Length);
        }

        [Test]
        public void CommandResult_Ok_NoArgs_EmptyLines()
        {
            var r = CommandResult.Ok();
            Assert.IsFalse(r.IsError);
            Assert.AreEqual(0, r.Lines.Length);
        }

        [Test]
        public void CommandResult_Error_IsError()
        {
            var r = CommandResult.Error("something went wrong");
            Assert.IsTrue(r.IsError);
            Assert.AreEqual("something went wrong", r.Lines[0]);
        }

        [Test]
        public void CommandResult_Error_MultiLine()
        {
            var r = CommandResult.Error("line1", "line2");
            Assert.IsTrue(r.IsError);
            Assert.AreEqual(2, r.Lines.Length);
        }
    }
}
