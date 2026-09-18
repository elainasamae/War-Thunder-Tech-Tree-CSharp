using System.Collections.Concurrent;
using System.Text.Json;

namespace WarThunderTechTree.Native;

internal sealed class DataRepository : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _dataDirectory;
    private readonly ConcurrentDictionary<string, Task<TreeData>> _trees = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Task<Dictionary<string, JsonElement>>> _detailIndexes = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Task<Dictionary<string, JsonElement>>> _modsIndexes = new(StringComparer.OrdinalIgnoreCase);

    public CountriesIndex Countries { get; private set; } = new();
    public IReadOnlyDictionary<string, string> Names { get; private set; } = new Dictionary<string, string>();

    public DataRepository(string? dataDirectory = null)
    {
        _dataDirectory = dataDirectory ?? Path.Combine(AppContext.BaseDirectory, "Data");
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Countries = await ReadAsync<CountriesIndex>("countries.json", cancellationToken);
        var names = await ReadAsync<VehicleNamesIndex>("vehicle-names-zh.json", cancellationToken);
        Names = new Dictionary<string, string>(names.Vehicles, StringComparer.OrdinalIgnoreCase);
    }

    public Task<TreeData> GetTreeAsync(string country, string type, CancellationToken cancellationToken = default)
    {
        var key = $"{country}-{type}";
        return _trees.GetOrAdd(key, _ => LoadTreeAsync(key, cancellationToken));
    }

    private async Task<TreeData> LoadTreeAsync(string key, CancellationToken cancellationToken)
    {
        var tree = await ReadAsync<TreeData>($"{key}.json", cancellationToken);
        foreach (var vehicle in tree.Vehicles)
        {
            vehicle.NameChinese = Names.TryGetValue(vehicle.UnitId, out var name) ? name : vehicle.NameEnglish;
        }
        return tree;
    }

    public async Task<VehicleDetails> GetDetailsAsync(
        string country,
        string type,
        VehicleRecord vehicle,
        CancellationToken cancellationToken = default)
    {
        var id = vehicle.UnitId;
        var auxiliaryTask = GetDetailAsync("vehicle-auxiliary-equipment.json", id, cancellationToken);
        var specificationsTask = GetDetailAsync("vehicle-specifications.json", id, cancellationToken);
        var ammunitionTask = GetDetailAsync("vehicle-ammunition.json", id, cancellationToken);
        var loadoutsTask = GetDetailAsync("aircraft-loadouts.json", id, cancellationToken);
        var modsTask = GetModificationAsync(country, type, vehicle, cancellationToken);
        await Task.WhenAll(auxiliaryTask, specificationsTask, ammunitionTask, loadoutsTask, modsTask);
        return new VehicleDetails(
            await auxiliaryTask,
            await specificationsTask,
            await ammunitionTask,
            await loadoutsTask,
            await modsTask);
    }

    public Task<JsonElement?> GetDetailAsync(string fileName, string unitId, CancellationToken cancellationToken = default)
    {
        var indexTask = _detailIndexes.GetOrAdd(fileName, _ => LoadVehicleIndexAsync(fileName, cancellationToken));
        return FindAsync(indexTask, unitId);
    }

    private async Task<JsonElement?> GetModificationAsync(
        string country,
        string type,
        VehicleRecord vehicle,
        CancellationToken cancellationToken)
    {
        var key = $"{country}-{type}-mods.json";
        var index = await _modsIndexes.GetOrAdd(key, _ => LoadVehicleIndexAsync(key, cancellationToken));
        if (index.TryGetValue(vehicle.UnitId, out var exact)) return exact;
        return vehicle.GroupId is not null && index.TryGetValue(vehicle.GroupId, out var group) ? group : null;
    }

    private static async Task<JsonElement?> FindAsync(Task<Dictionary<string, JsonElement>> indexTask, string unitId)
    {
        var index = await indexTask;
        return index.TryGetValue(unitId, out var value) ? value : null;
    }

    private async Task<Dictionary<string, JsonElement>> LoadVehicleIndexAsync(string fileName, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(Path.Combine(_dataDirectory, fileName));
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var result = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (!document.RootElement.TryGetProperty("vehicles", out var vehicles)) return result;
        foreach (var property in vehicles.EnumerateObject()) result[property.Name] = property.Value.Clone();
        return result;
    }

    private async Task<T> ReadAsync<T>(string fileName, CancellationToken cancellationToken)
    {
        var path = Path.Combine(_dataDirectory, fileName);
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidDataException($"无法读取数据文件：{path}");
    }

    public void Dispose()
    {
        _trees.Clear();
        _detailIndexes.Clear();
        _modsIndexes.Clear();
    }
}
