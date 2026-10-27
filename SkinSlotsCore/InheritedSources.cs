using System;

namespace SkinSlotsCore;

public sealed class InheritedSources : IDisposable
{
	public required string TablePath { get; init; }

	public required string LocalizationDirectory { get; init; }

	public required bool TableFromMods { get; init; }

	public required bool LocalizationFromMods { get; init; }

	public string? TablePak { get; init; }

	public string? LocalizationPak { get; init; }

	public string? DataPakPath { get; init; }

	public void Dispose()
	{
	}
}
