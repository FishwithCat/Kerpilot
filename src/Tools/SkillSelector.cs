using System.Text;

namespace Kerpilot
{
    public static class SkillSelector
    {
        /// <summary>
        /// Composes the final system prompt: base + optional live game state +
        /// all skill content. The game state snapshot is auto-attached each turn
        /// so the LLM has situational awareness for top-level facts (scene,
        /// vessel, orbit, career resources) without round-tripping through tools.
        /// The LLM decides which skills are relevant based on their descriptions.
        /// </summary>
        public static string ComposeSystemPrompt(string basePrompt, string gameStateSnapshot = null)
        {
            var skills = SkillDefinitions.GetAllSkills();
            bool hasSnapshot = !string.IsNullOrEmpty(gameStateSnapshot);
            if (skills.Length == 0 && !hasSnapshot)
                return basePrompt;

            var sb = new StringBuilder(basePrompt);

            if (hasSnapshot)
            {
                sb.Append("\n\n## Current Game State\n");
                sb.Append("This snapshot is auto-attached each turn. Use it for situational awareness — you do not need to call tools for facts already shown here. Call tools only when you need detail beyond this summary.\n");
                sb.Append("<game_state>\n");
                sb.Append(gameStateSnapshot);
                sb.Append("\n</game_state>");
            }

            if (skills.Length > 0)
            {
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
            }

            return sb.ToString();
        }
    }
}
