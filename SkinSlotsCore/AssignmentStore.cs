using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SkinSlotsCore;

public sealed class AssignmentStore
{
	public const int LastPawnSuffix = 99;

	private const int FirstSkinId = 60000;

	public int NextSkinId { get; set; } = 60000;

	public Dictionary<string, List<int>> Planes { get; set; } = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);

	public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "since_SkinSlots", "assignments.json");

	public static AssignmentStore Load(string path)
	{
		if (!File.Exists(path))
		{
			return new AssignmentStore();
		}
		AssignmentStore assignmentStore = JsonSerializer.Deserialize<AssignmentStore>(File.ReadAllText(path)) ?? new AssignmentStore();
		assignmentStore.Planes = new Dictionary<string, List<int>>(assignmentStore.Planes, StringComparer.OrdinalIgnoreCase);
		if (assignmentStore.NextSkinId < 60000)
		{
			assignmentStore.NextSkinId = 60000;
		}
		return assignmentStore;
	}

	public void Save(string path)
	{
		string directoryName = Path.GetDirectoryName(path);
		if (!string.IsNullOrEmpty(directoryName))
		{
			Directory.CreateDirectory(directoryName);
		}
		File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions
		{
			WriteIndented = true
		}));
	}

	public int? PeekNextSlot(string plane, int firstSuffix)
	{
		Planes.TryGetValue(plane, out List<int> value);
		for (int i = firstSuffix; i <= 99; i++)
		{
			if (value == null || !value.Contains(i))
			{
				return i;
			}
		}
		return null;
	}

	public IReadOnlyList<int> UsedSlots(string plane)
	{
		if (!Planes.TryGetValue(plane, out List<int> value))
		{
			return Array.Empty<int>();
		}
		return value.OrderBy((int slot) => slot).ToArray();
	}

	public int AllocateSlot(string plane, int firstSuffix, int? requested)
	{
		if (!Planes.TryGetValue(plane, out List<int> value))
		{
			value = new List<int>();
			Planes[plane] = value;
		}
		if (requested.HasValue)
		{
			if (requested.Value < firstSuffix || requested.Value > 99)
			{
				throw new InvalidOperationException($"Slot {requested.Value + 1} is outside the free range {firstSuffix + 1}-{100}. Original skins use the slots before {firstSuffix + 1}.");
			}
			if (value.Contains(requested.Value))
			{
				throw new InvalidOperationException($"Slot {requested.Value + 1} is already assigned to {plane}.");
			}
			value.Add(requested.Value);
			return requested.Value;
		}
		for (int i = firstSuffix; i <= 99; i++)
		{
			if (!value.Contains(i))
			{
				value.Add(i);
				return i;
			}
		}
		throw new InvalidOperationException($"No slot left for {plane}. Slots {firstSuffix + 1}-{100} are all assigned.");
	}

	public void Clear()
	{
		Planes.Clear();
		NextSkinId = 60000;
	}

	public int AllocateSkinId()
	{
		int nextSkinId = NextSkinId;
		NextSkinId++;
		return nextSkinId;
	}
}
