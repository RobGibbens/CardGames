using System.Buffers.Binary;

namespace CardGames.Contracts.SignalR;

public static class GameStateVersion
{
	public static ulong FromRowVersion(byte[]? rowVersion)
	{
		if (rowVersion is not { Length: 8 })
			return 0;
		return BinaryPrimitives.ReadUInt64BigEndian(rowVersion);
	}
}
