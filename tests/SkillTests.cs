using System.Collections.Generic;
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
                Content = "KSP uses patched conics.",
                Keywords = new[]
                {
                    "orbit", "orbital", "apoapsis", "periapsis", "hohmann", "transfer",
                    "maneuver", "node", "inclination", "prograde", "retrograde",
                    "normal", "antinormal", "radial", "gravity turn", "circularize",
                    "rendezvous", "dock", "docking", "aerobrake", "aerobraking",
                    "soi", "sphere of influence", "encounter", "intercept", "burn",
                    "phase angle", "ascending node", "descending node"
                }
            },
            new SkillDefinitions.Skill
            {
                Id = "rocket_design",
                Title = "Rocket Design",
                Content = "Staging and TWR principles.",
                Keywords = new[]
                {
                    "design", "build", "stage", "staging", "twr", "thrust",
                    "mass ratio", "engine", "isp", "specific impulse",
                    "fairing", "aerodynamic", "drag", "stability",
                    "center of mass", "center of lift", "center of pressure",
                    "strut", "fuel tank", "booster", "rocket", "asparagus"
                }
            },
            new SkillDefinitions.Skill
            {
                Id = "delta_v_budget",
                Title = "Delta-v Budget",
                Content = "Delta-v budgeting principles.",
                Keywords = new[]
                {
                    "delta-v", "deltav", "delta v", "dv", "budget",
                    "fuel", "enough fuel", "how much fuel",
                    "reach", "get to", "travel to", "fly to",
                    "mun", "minmus", "duna", "eve", "jool", "moho", "eeloo",
                    "dres", "laythe", "tylo", "vall", "pol", "bop",
                    "map", "mission plan", "round trip"
                }
            },
            new SkillDefinitions.Skill
            {
                Id = "contracts_guide",
                Title = "Contracts Guide",
                Content = "Contract types and strategies.",
                Keywords = new[]
                {
                    "contract", "contracts", "mission", "missions", "objective", "objectives",
                    "gather scientific data", "gather science", "science data",
                    "explore", "world first", "world-first",
                    "test part", "test a part", "part test",
                    "rescue", "rescue kerbal", "stranded",
                    "satellite", "position a satellite", "specific orbit",
                    "survey", "waypoint",
                    "tourism", "tourist", "tourists",
                    "accept", "deadline", "reward", "reputation"
                }
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

        // ── SkillSelector.SelectSkills ──

        [Test]
        public void SelectSkills_UnrelatedInput_ReturnsEmpty()
        {
            var result = SkillSelector.SelectSkills("hello how are you");
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void SelectSkills_NullInput_ReturnsEmpty()
        {
            var result = SkillSelector.SelectSkills(null);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void SelectSkills_EmptyInput_ReturnsEmpty()
        {
            var result = SkillSelector.SelectSkills("");
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void SelectSkills_HohmannTransfer_MatchesOrbitalMechanics()
        {
            var result = SkillSelector.SelectSkills("How do I do a Hohmann transfer?");
            Assert.IsTrue(result.Count >= 1);
            Assert.AreEqual("orbital_mechanics", result[0].Id);
        }

        [Test]
        public void SelectSkills_MunDeltaV_MatchesDeltaVBudget()
        {
            var result = SkillSelector.SelectSkills("Do I have enough delta-v to reach the Mun?");
            Assert.IsTrue(result.Count >= 1);
            bool found = false;
            foreach (var s in result)
                if (s.Id == "delta_v_budget") found = true;
            Assert.IsTrue(found, "delta_v_budget should match for Mun delta-v query");
        }

        [Test]
        public void SelectSkills_RocketDesign_MatchesRocketDesign()
        {
            var result = SkillSelector.SelectSkills("What TWR should my first stage have?");
            Assert.IsTrue(result.Count >= 1);
            bool found = false;
            foreach (var s in result)
                if (s.Id == "rocket_design") found = true;
            Assert.IsTrue(found, "rocket_design should match for TWR query");
        }

        [Test]
        public void SelectSkills_ContractQuery_MatchesContractsGuide()
        {
            var result = SkillSelector.SelectSkills("How do I complete this contract?");
            Assert.IsTrue(result.Count >= 1);
            bool found = false;
            foreach (var s in result)
                if (s.Id == "contracts_guide") found = true;
            Assert.IsTrue(found, "contracts_guide should match for contract query");
        }

        [Test]
        public void SelectSkills_GatherScience_MatchesContractsGuide()
        {
            var result = SkillSelector.SelectSkills("How do I gather scientific data from Kerbin?");
            Assert.IsTrue(result.Count >= 1);
            bool found = false;
            foreach (var s in result)
                if (s.Id == "contracts_guide") found = true;
            Assert.IsTrue(found, "contracts_guide should match for gather science query");
        }

        [Test]
        public void SelectSkills_ReturnsAtMostTwo()
        {
            var result = SkillSelector.SelectSkills(
                "I need to design a rocket with enough delta-v for a Hohmann transfer to Duna");
            Assert.IsTrue(result.Count <= 2);
        }

        [Test]
        public void SelectSkills_MaxSkillsOne_ReturnsAtMostOne()
        {
            var result = SkillSelector.SelectSkills("Hohmann transfer orbit burn", maxSkills: 1);
            Assert.IsTrue(result.Count <= 1);
        }

        // ── SkillSelector.ComposeSystemPrompt ──

        [Test]
        public void ComposeSystemPrompt_NoSkills_ReturnsBasePrompt()
        {
            string basePrompt = "You are a test assistant.";
            var result = SkillSelector.ComposeSystemPrompt(basePrompt, new List<SkillDefinitions.Skill>());
            Assert.AreEqual(basePrompt, result);
        }

        [Test]
        public void ComposeSystemPrompt_NullSkills_ReturnsBasePrompt()
        {
            string basePrompt = "You are a test assistant.";
            var result = SkillSelector.ComposeSystemPrompt(basePrompt, null);
            Assert.AreEqual(basePrompt, result);
        }

        [Test]
        public void ComposeSystemPrompt_WithSkills_AppendsContent()
        {
            string basePrompt = "Base prompt.";
            var skills = SkillSelector.SelectSkills("Hohmann transfer to the Mun");
            var result = SkillSelector.ComposeSystemPrompt(basePrompt, skills);

            Assert.IsTrue(result.StartsWith("Base prompt."));
            Assert.IsTrue(result.Contains("## Reference Knowledge"));
            Assert.IsTrue(result.Length > basePrompt.Length);
        }

        [Test]
        public void ComposeSystemPrompt_ContainsSkillTitles()
        {
            var skills = SkillSelector.SelectSkills("How do I do a Hohmann transfer?");
            var result = SkillSelector.ComposeSystemPrompt("Base.", skills);
            Assert.IsTrue(result.Contains("### Orbital Mechanics"));
        }

        // ── ParseSkillFile ──

        [Test]
        public void ParseSkillFile_ValidFrontmatter_ExtractsAllFields()
        {
            string md = "---\nid: test_skill\ntitle: Test Skill\nkeywords: alpha, beta, gamma\n---\nBody content here.";
            var skill = SkillDefinitions.ParseSkillFile(md);
            Assert.AreEqual("test_skill", skill.Id);
            Assert.AreEqual("Test Skill", skill.Title);
            Assert.AreEqual(3, skill.Keywords.Length);
            Assert.AreEqual("alpha", skill.Keywords[0]);
            Assert.AreEqual("beta", skill.Keywords[1]);
            Assert.AreEqual("gamma", skill.Keywords[2]);
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
            string md = "---\r\nid: win\r\ntitle: Win\r\nkeywords: a, b\r\n---\r\nContent.";
            var skill = SkillDefinitions.ParseSkillFile(md);
            Assert.AreEqual("win", skill.Id);
            Assert.AreEqual("Win", skill.Title);
            Assert.AreEqual(2, skill.Keywords.Length);
            Assert.AreEqual("Content.", skill.Content);
        }

        [Test]
        public void ParseSkillFile_MultilineContent_PreservesNewlines()
        {
            string md = "---\nid: multi\ntitle: Multi\nkeywords: x\n---\nLine 1\n\nLine 3";
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
