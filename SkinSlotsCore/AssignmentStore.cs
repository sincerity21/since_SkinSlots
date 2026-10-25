using System.Text.Json;

namespace SkinSlotsCore;

public sealed class AssignmentStore
{
    private const int FirstPrivateSlot = 50;
    private const int LastPrivateSlot = 99;
    private const int FirstSkinId = 60000;

    public int NextSkinId { get; set; } = FirstSkinId;
    public Dictionary<string, List<int>> Planes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "since_SkinSlots", "assignments.json");

    public static AssignmentStore Load(string path)
    {
        if (!File.Exists(path))
            return new AssignmentStore();

        var store = JsonSerializer.Deserialize<AssignmentStore>(File.ReadAllText(path)) ?? new AssignmentStore();
        store.Planes = new Dictionary<string, List<int>>(store.Planes, StringComparer.OrdinalIgnoreCase);
        if (store.NextSkinId < FirstSkinId)
            store.NextSkinId = FirstSkinId;
        return store;
    }

    public void Save(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    public int AllocateSlot(string plane, int? requested)
    {
        if (!Planes.TryGetValue(plane, out var used))
        {
            used = [];
            Planes[plane] = used;
        }

        if (requested.HasValue)
        {
            if (requested.Value is < FirstPrivateSlot or > LastPrivateSlot)
                throw new InvalidOperationException($"Private slot {requested.Value} is outside {FirstPrivateSlot}-{LastPrivateSlot}.");
            if (used.Contains(requested.Value))
                throw new InvalidOperationException($"Slot {requested.Value:D2} is already assigned to {plane}.");
            used.Add(requested.Value);
            return requested.Value;
        }

        for (var slot = FirstPrivateSlot; slot <= LastPrivateSlot; slot++)
        {
            if (used.Contains(slot))
                continue;
            used.Add(slot);
            return slot;
        }

        throw new InvalidOperationException($"No private slot left for {plane}. Slots {FirstPrivateSlot}-{LastPrivateSlot} are all assigned.");
    }

    public int AllocateSkinId()
    {
        var id = NextSkinId;
        NextSkinId++;
        return id;
    }
}
