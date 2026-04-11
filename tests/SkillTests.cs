using System.Collections.Generic;
using NUnit.Framework;

namespace Kerpilot.Tests
{
    [TestFixture]
    public class SkillTests
    {
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
            // A message that could match all three skills
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

        // ── Integration with LlmClient ──

        [Test]
        public void BaseSystemPrompt_IsAccessible()
        {
            Assert.IsNotEmpty(LlmClient.BaseSystemPrompt);
            Assert.IsTrue(LlmClient.BaseSystemPrompt.Contains("Kerpilot"));
        }
    }
}
