namespace CardGames.Poker.Api.Infrastructure;

public class PagedResponse<T> where T : class
{
	public IEnumerable<T> Data { get; }
	public int PageNumber { get; }
	public int PageSize { get; }
	public int TotalCount { get; }
	public int TotalPages { get; }
	public bool HasPreviousPage { get; }
	public bool HasNextPage { get; }

	public PagedResponse(
		IEnumerable<T>? data,
		int pageNumber,
		int pageSize,
		int totalCount)
	{
		if (pageSize <= 0)
		{
			throw new ArgumentException("Page size must be greater than 0.", nameof(pageSize));
		}

		Data = data ?? [];
		PageNumber = pageNumber;
		PageSize = pageSize;
		TotalCount = totalCount;
		TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
		HasPreviousPage = PageNumber > 1;
		HasNextPage = PageNumber < TotalPages;
	}
}