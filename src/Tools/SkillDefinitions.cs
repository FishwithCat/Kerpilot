using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace Kerpilot
{
    public static class SkillDefinitions
    {
        public struct Skill
        {
            public string Id;
            public string Title;
            public string Content;
            public string[] Keywords;
        }

        private static Skill[] _cachedSkills;

        public static Skill[] GetAllSkills()
        {
            if (_cachedSkills == null)
                _cachedSkills = LoadSkillsFromDisk();
            return _cachedSkills;
        }

        public static void ReloadSkills()
        {
            _cachedSkills = null;
        }

        public static void SetSkillsForTesting(Skill[] skills)
        {
            _cachedSkills = skills;
        }

        public static Skill ParseSkillFile(string text)
        {
            var skill = new Skill();
            if (string.IsNullOrEmpty(text))
                return skill;

            // Normalize line endings
            text = text.Replace("\r\n", "\n");

            // Must start with ---
            if (!text.StartsWith("---"))
                return skill;

            int endIndex = text.IndexOf("\n---", 3);
            if (endIndex < 0)
                return skill;

            string frontmatter = text.Substring(3, endIndex - 3).Trim();
            string body = text.Substring(endIndex + 4).Trim();

            foreach (string line in frontmatter.Split('\n'))
            {
                int colonIndex = line.IndexOf(':');
                if (colonIndex < 0) continue;

                string key = line.Substring(0, colonIndex).Trim();
                string value = line.Substring(colonIndex + 1).Trim();

                switch (key)
                {
                    case "id":
                        skill.Id = value;
                        break;
                    case "title":
                        skill.Title = value;
                        break;
                    case "keywords":
                        var keywords = new List<string>();
                        foreach (string kw in value.Split(','))
                        {
                            string trimmed = kw.Trim();
                            if (trimmed.Length > 0)
                                keywords.Add(trimmed);
                        }
                        skill.Keywords = keywords.ToArray();
                        break;
                }
            }

            skill.Content = body;
            return skill;
        }

        private static Skill[] LoadSkillsFromDisk()
        {
            string skillsDir = Path.Combine(KSPUtil.ApplicationRootPath, "GameData/Kerpilot/Skills");
            if (!Directory.Exists(skillsDir))
            {
                Debug.LogWarning("[Kerpilot] Skills directory not found: " + skillsDir);
                return new Skill[0];
            }

            string[] files = Directory.GetFiles(skillsDir, "*.md");
            Array.Sort(files);

            var skills = new List<Skill>();
            foreach (string file in files)
            {
                try
                {
                    string text = File.ReadAllText(file);
                    var skill = ParseSkillFile(text);
                    if (!string.IsNullOrEmpty(skill.Id))
                        skills.Add(skill);
                    else
                        Debug.LogWarning("[Kerpilot] Failed to parse skill file: " + file);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[Kerpilot] Error reading skill file " + file + ": " + ex.Message);
                }
            }

            return skills.ToArray();
        }
    }
}
