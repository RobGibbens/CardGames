using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace CardGames.Poker.Web.Data;

// Add profile data for application users by adding properties to the ApplicationUser class
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

