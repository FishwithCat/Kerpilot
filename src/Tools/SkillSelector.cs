using System.Text;

namespace Kerpilot
{
    public static class SkillSelector
    {
        /// <summary>
        /// Composes the final system prompt by appending all skill content.
        /// The LLM decides which skills are relevant based on their descriptions.
        /// </summary>
        public static string ComposeSystemPrompt(string basePrompt)
        {
            var skills = SkillDefinitions.GetAllSkills();
            if (skills.Length == 0)
                return basePrompt;

            var sb = new StringBuilder(basePrompt);
            sb.Append("\n\n## Reference Knowledge\n");
            sb.Append("The following domain knowledge is available. Use the sections relevant to the player's question.\n");

            foreach (var skill in skills)
            {
                sb.Append("\n### ");
                sb.Append(skill.Title);
                if (!string.IsNullOrEmpty(skill.Description))
                {
                    sb.Append(" — ");
                    sb.Append(skill.Description);
                }
                sb.Append('\n');
                sb.Append(skill.Content);
                sb.Append('\n');
            }

            return sb.ToString();
        }
    }
}
