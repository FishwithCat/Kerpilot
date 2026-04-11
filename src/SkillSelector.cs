using System;
using System.Collections.Generic;
using System.Text;

namespace Kerpilot
{
    public static class SkillSelector
    {
        /// <summary>
        /// Scores user message against skill keywords and returns the top matching skills.
        /// </summary>
        public static List<SkillDefinitions.Skill> SelectSkills(string userMessage, int maxSkills = 2)
        {
            var result = new List<SkillDefinitions.Skill>();
            if (string.IsNullOrEmpty(userMessage))
                return result;

            string lower = userMessage.ToLowerInvariant();
            var scored = new List<KeyValuePair<int, SkillDefinitions.Skill>>();

            foreach (var skill in SkillDefinitions.GetAllSkills())
            {
                int score = 0;
                foreach (string keyword in skill.Keywords)
                {
                    if (keyword.Contains(" "))
                    {
                        // Multi-word phrase: use substring match
                        if (lower.Contains(keyword))
                            score++;
                    }
                    else
                    {
                        // Single word: check word boundaries to reduce false positives
                        if (ContainsWord(lower, keyword))
                            score++;
                    }
                }

                if (score > 0)
                    scored.Add(new KeyValuePair<int, SkillDefinitions.Skill>(score, skill));
            }

            // Sort by score descending
            scored.Sort((a, b) => b.Key.CompareTo(a.Key));

            int count = Math.Min(maxSkills, scored.Count);
            for (int i = 0; i < count; i++)
                result.Add(scored[i].Value);

            return result;
        }

        /// <summary>
        /// Composes the final system prompt by appending matched skill content to the base prompt.
        /// </summary>
        public static string ComposeSystemPrompt(string basePrompt, List<SkillDefinitions.Skill> skills)
        {
            if (skills == null || skills.Count == 0)
                return basePrompt;

            var sb = new StringBuilder(basePrompt);
            sb.Append("\n\n## Reference Knowledge\n");

            foreach (var skill in skills)
            {
                sb.Append("\n### ");
                sb.Append(skill.Title);
                sb.Append('\n');
                sb.Append(skill.Content);
                sb.Append('\n');
            }

            return sb.ToString();
        }

        /// <summary>
        /// Checks if a word appears in text with word boundary awareness.
        /// A character is considered a word boundary if it's not a letter or digit.
        /// </summary>
        private static bool ContainsWord(string text, string word)
        {
            int index = 0;
            while (index <= text.Length - word.Length)
            {
                int found = text.IndexOf(word, index, StringComparison.Ordinal);
                if (found < 0)
                    return false;

                bool startOk = found == 0 || !char.IsLetterOrDigit(text[found - 1]);
                int endPos = found + word.Length;
                bool endOk = endPos >= text.Length || !char.IsLetterOrDigit(text[endPos]);

                if (startOk && endOk)
                    return true;

                index = found + 1;
            }

            return false;
        }
    }
}
