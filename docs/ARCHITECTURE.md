# CardGames Architecture Notes

## Validation convention

`CardGames.Poker.Api` uses DataAnnotations-based request validation on contract DTOs together with the .NET minimal-API `AddValidation()` pipeline in `Program.cs`.

Do not add FluentValidation registrations unless the API also introduces concrete `AbstractValidator<T>` implementations and intentionally changes the project-wide validation strategy.
