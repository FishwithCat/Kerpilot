using NUnit.Framework;

namespace Kerpilot.Tests
{
    [TestFixture]
    public class SkillTests
    {
        private static readonly SkillDefinitions.Skill[] TestSkills = new[]
        {
            new SkillDefinitions.Skill
            {
                Id = "orbital_mechanics",
                Title = "Orbital Mechanics",
                Description = "Patched conics, burn directions, Hohmann transfers, gravity turns, inclination changes, rendezvous, and aerobraking",
                Content = "KSP uses patched conics."
            },
            new SkillDefinitions.Skill
            {
                Id = "rocket_design",
                Title = "Rocket Design",
                Description = "Staging strategy, TWR guidelines per stage, Tsiolkovsky equation, aerodynamic stability, and engine selection",
                Content = "Staging and TWR principles."
            },
            new SkillDefinitions.Skill
            {
                Id = "delta_v_budget",
                Title = "Delta-v Budget",
                Description = "Delta-v estimation, budgeting tips, transfer planning, and fuel requirements for reaching destinations",
                Content = "Delta-v budgeting principles."
            },
            new SkillDefinitions.Skill
            {
                Id = "contracts_guide",
                Title = "Contracts Guide",
                Description = "Contract types, parameter requirements, cost-effective mission planning, and career economy advice",
                Content = "Contract types and strategies."
            },
            new SkillDefinitions.Skill
            {
                Id = "basic_game_control",
                Title = "Basic Game Control",
                Description = "Keyboard controls, SAS/RCS modes, time warp, map view, camera, EVA, editor controls, and action groups",
                Content = "KSP keyboard controls and game mechanics."
            }
        };

        [SetUp]
        public void SetUp()
        {
            SkillDefinitions.SetSkillsForTesting(TestSkills);
        }

        [TearDown]
        public void TearDown()
        {
            SkillDefinitions.ReloadSkills();
        }

        // ── ComposeSystemPrompt ──

        [Test]
        public void ComposeSystemPrompt_NoSkills_ReturnsBasePrompt()
        {
            SkillDefinitions.SetSkillsForTesting(new SkillDefinitions.Skill[0]);
            string basePrompt = "You are a test assistant.";
            var result = SkillSelector.ComposeSystemPrompt(basePrompt);
            Assert.AreEqual(basePrompt, result);
        }

        [Test]
        public void ComposeSystemPrompt_IncludesAllSkills()
        {
            var result = SkillSelector.ComposeSystemPrompt("Base prompt.");
            Assert.IsTrue(result.StartsWith("Base prompt."));
            Assert.IsTrue(result.Contains("## Reference Knowledge"));
            Assert.IsTrue(result.Contains("### Orbital Mechanics"));
            Assert.IsTrue(result.Contains("### Rocket Design"));
            Assert.IsTrue(result.Contains("### Delta-v Budget"));
            Assert.IsTrue(result.Contains("### Contracts Guide"));
            Assert.IsTrue(result.Contains("### Basic Game Control"));
        }

        [Test]
        public void ComposeSystemPrompt_IncludesDescriptions()
        {
            var result = SkillSelector.ComposeSystemPrompt("Base.");
            Assert.IsTrue(result.Contains("Patched conics, burn directions"));
            Assert.IsTrue(result.Contains("Keyboard controls, SAS/RCS modes"));
        }

        [Test]
        public void ComposeSystemPrompt_IncludesContent()
        {
            var result = SkillSelector.ComposeSystemPrompt("Base.");
            Assert.IsTrue(result.Contains("KSP uses patched conics."));
            Assert.IsTrue(result.Contains("KSP keyboard controls and game mechanics."));
        }

        // ── ParseSkillFile ──

        [Test]
        public void ParseSkillFile_ValidFrontmatter_ExtractsAllFields()
        {
            string md = "---\nid: test_skill\ntitle: Test Skill\ndescription: A test skill for testing\n---\nBody content here.";
            var skill = SkillDefinitions.ParseSkillFile(md);
            Assert.AreEqual("test_skill", skill.Id);
            Assert.AreEqual("Test Skill", skill.Title);
            Assert.AreEqual("A test skill for testing", skill.Description);
            Assert.AreEqual("Body content here.", skill.Content);
        }

        [Test]
        public void ParseSkillFile_MissingFrontmatter_ReturnsEmptyId()
        {
            var skill = SkillDefinitions.ParseSkillFile("Just plain text, no frontmatter.");
            Assert.IsTrue(string.IsNullOrEmpty(skill.Id));
        }

        [Test]
        public void ParseSkillFile_WindowsLineEndings_WorksCorrectly()
        {
            string md = "---\r\nid: win\r\ntitle: Win\r\ndescription: Windows test\r\n---\r\nContent.";
            var skill = SkillDefinitions.ParseSkillFile(md);
            Assert.AreEqual("win", skill.Id);
            Assert.AreEqual("Win", skill.Title);
            Assert.AreEqual("Windows test", skill.Description);
            Assert.AreEqual("Content.", skill.Content);
        }

        [Test]
        public void ParseSkillFile_MultilineContent_PreservesNewlines()
        {
            string md = "---\nid: multi\ntitle: Multi\ndescription: Multi test\n---\nLine 1\n\nLine 3";
            var skill = SkillDefinitions.ParseSkillFile(md);
            Assert.IsTrue(skill.Content.Contains("\n"));
            Assert.IsTrue(skill.Content.Contains("Line 1"));
            Assert.IsTrue(skill.Content.Contains("Line 3"));
        }

        [Test]
        public void ParseSkillFile_EmptyInput_ReturnsEmptySkill()
        {
            var skill = SkillDefinitions.ParseSkillFile("");
            Assert.IsTrue(string.IsNullOrEmpty(skill.Id));
        }

        // ── Integration with LlmClient ──

        [Test]
        public void BaseSystemPrompt_IsAccessible()
        {
            Assert.IsNotEmpty(LlmClient.BaseSystemPrompt);
            Assert.IsTrue(LlmClient.BaseSystemPrompt.Contains("Kerpilot"));
        }
    }
}
