using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace CardGames.Poker.Api.Data.Entities;

public class ApplicationUser : IdentityUser
{
	[PersonalData]
	[MaxLength(100)]
	public string? FirstName { get; set; }

	[PersonalData]
	[MaxLength(100)]
	public string? LastName { get; set; }

	[PersonalData]
	[MaxLength(512)]
	public string? AvatarUrl { get; set; }
}