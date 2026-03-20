using MediatR;
using GitHub.Copilot.SDK;

namespace CardGames.Poker.Api.Features.Games.Generic.v1.Queries.GetGeneratedTableName;

public class GetGeneratedTableNameQueryHandler : IRequestHandler<GetGeneratedTableNameQuery, GetGeneratedTableNameResponse>
{
    public async Task<GetGeneratedTableNameResponse> Handle(GetGeneratedTableNameQuery request, CancellationToken cancellationToken)
    {
        try
        {
            string gameType = request.GameType ?? string.Empty;

            await using var client = new CopilotClient();

            await using var session = await client.CreateSessionAsync(new SessionConfig
            {
                Model = "gpt-5.1-mini",
                OnPermissionRequest = PermissionHandler.ApproveAll
            });

            var prompt =
                $"""
                **Prompt: Generate a Unique Poker Table Name**

                You are generating a creative, unique, and randomized name for an online poker table.

                **Requirements:**
                - The name must be composed of **2 to 5 English words**.
                - Total length must be **between 15 and 60 characters (including spaces)**.
                - The name should feel **distinct, memorable, and slightly thematic**, but does not need to be serious.
                - Avoid generic names like “Poker Table 1” or “High Stakes Table.”
                - Do not include punctuation except for apostrophes if needed.
                - Do not include emojis.
                - Ensure the name is **safe for general audiences** (no explicit or offensive content).
                - The name must be **unique each time** (use randomness in word selection and structure).

                **Optional Input:**
                - Game type: `{gameType}`  
                - If provided, subtly incorporate or reflect the game type in the name.
                - If not provided, ignore this.

                **Style Guidance:**
                - Combine evocative adjectives, nouns, and occasional verbs.
                - Possible themes include: luck, risk, money, western, casinos, animals, night, danger, humor.
                - Vary structure (e.g., 'Adjective Noun', 'Noun of Noun', 'Verb the Noun', etc.).

                **Output Format:**
                - Return **only the table name**, with no explanation or extra text.

                **Examples (do not reuse):**
                - Velvet River Bluff
                - Midnight Ace Syndicate
                - Rusty Chip Showdown
                - Lucky Coyote Holdem Run
            """;

            var response = await session.SendAndWaitAsync(
            new MessageOptions
            {
                Prompt = prompt
            }
            );

            var text = response?.Data.Content ?? "New Table";

            return new GetGeneratedTableNameResponse(text);
        }
        catch (Exception ex)
        {
            return new GetGeneratedTableNameResponse("New Table");
        }
    }
}