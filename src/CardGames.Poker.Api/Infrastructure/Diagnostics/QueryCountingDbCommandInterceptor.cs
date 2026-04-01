using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CardGames.Poker.Api.Infrastructure.Diagnostics;

/// <summary>
/// EF Core interceptor that increments a request-scoped query counter on every SQL command execution.
/// </summary>
public sealed class QueryCountingDbCommandInterceptor(QueryCountContext queryCountContext)
	: DbCommandInterceptor
{
	public override InterceptionResult<DbDataReader> ReaderExecuting(
		DbCommand command,
		CommandEventData eventData,
		InterceptionResult<DbDataReader> result)
	{
		queryCountContext.Increment();
		return base.ReaderExecuting(command, eventData, result);
	}

	public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
		DbCommand command,
		CommandEventData eventData,
		InterceptionResult<DbDataReader> result,
		CancellationToken cancellationToken = default)
	{
		queryCountContext.Increment();
		return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
	}

	public override InterceptionResult<object> ScalarExecuting(
		DbCommand command,
		CommandEventData eventData,
		InterceptionResult<object> result)
	{
		queryCountContext.Increment();
		return base.ScalarExecuting(command, eventData, result);
	}

	public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
		DbCommand command,
		CommandEventData eventData,
		InterceptionResult<object> result,
		CancellationToken cancellationToken = default)
	{
		queryCountContext.Increment();
		return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
	}

	public override InterceptionResult<int> NonQueryExecuting(
		DbCommand command,
		CommandEventData eventData,
		InterceptionResult<int> result)
	{
		queryCountContext.Increment();
		return base.NonQueryExecuting(command, eventData, result);
	}

	public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
		DbCommand command,
		CommandEventData eventData,
		InterceptionResult<int> result,
		CancellationToken cancellationToken = default)
	{
		queryCountContext.Increment();
		return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
	}
}
