namespace WarThunderTechTree.Native;

internal static class ResearchPlanner
{
    public static ResearchRoute Fastest(TreeData tree, VehicleRecord target)
    {
        var vehicles = tree.Vehicles
            .OrderBy(vehicle => vehicle.RankNumber)
            .ThenBy(vehicle => vehicle.TreeOrder)
            .ToList();
        var byId = vehicles.ToDictionary(vehicle => vehicle.UnitId, StringComparer.OrdinalIgnoreCase);
        var groups = vehicles
            .Where(vehicle => !string.IsNullOrWhiteSpace(vehicle.GroupId))
            .GroupBy(vehicle => vehicle.GroupId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        var chainCache = new Dictionary<string, List<VehicleRecord>>(StringComparer.OrdinalIgnoreCase);

        List<VehicleRecord> Chain(VehicleRecord vehicle, HashSet<string>? visiting = null)
        {
            if (chainCache.TryGetValue(vehicle.UnitId, out var cached)) return cached;
            visiting ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase) { vehicle.UnitId };
            VehicleRecord? prerequisite = null;
            if (!string.IsNullOrWhiteSpace(vehicle.RequirementId))
            {
                if (!byId.TryGetValue(vehicle.RequirementId, out prerequisite) && groups.TryGetValue(vehicle.RequirementId, out var members))
                    prerequisite = members.OrderBy(item => item.ResearchRp ?? 0).FirstOrDefault();
            }
            List<VehicleRecord> result;
            if (prerequisite is null || visiting.Contains(prerequisite.UnitId)) result = [vehicle];
            else
            {
                visiting.Add(prerequisite.UnitId);
                result = [.. Chain(prerequisite, visiting), vehicle];
                visiting.Remove(prerequisite.UnitId);
            }
            chainCache[vehicle.UnitId] = result;
            return result;
        }

        var selected = new Dictionary<string, VehicleRecord>(StringComparer.OrdinalIgnoreCase);
        foreach (var vehicle in Chain(target)) selected.TryAdd(vehicle.UnitId, vehicle);
        var extras = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rank in vehicles.Select(vehicle => vehicle.RankNumber).Distinct().Order())
        {
            if (rank >= target.RankNumber) break;
            var required = UnlockRequirement(tree.VehicleType, rank);
            while (selected.Values.Count(vehicle => vehicle.RankNumber == rank) < required)
            {
                var candidate = vehicles
                    .Where(vehicle => vehicle.RankNumber == rank && vehicle.TreeSection == "researchable" && !selected.ContainsKey(vehicle.UnitId))
                    .Select(vehicle => new
                    {
                        Vehicle = vehicle,
                        Fresh = Chain(vehicle).Where(item => !selected.ContainsKey(item.UnitId)).ToList()
                    })
                    .Where(item => item.Fresh.Count > 0)
                    .OrderBy(item => item.Fresh.Sum(vehicle => vehicle.ResearchRp ?? 0))
                    .ThenBy(item => item.Fresh.Count)
                    .ThenBy(item => item.Vehicle.TreeOrder)
                    .FirstOrDefault();
                if (candidate is null) break;
                foreach (var vehicle in candidate.Fresh)
                {
                    if (selected.TryAdd(vehicle.UnitId, vehicle)) extras.Add(vehicle.UnitId);
                }
            }
        }

        var ordered = selected.Values.OrderBy(vehicle => vehicle.RankNumber).ThenBy(vehicle => vehicle.TreeOrder).ToList();
        return new ResearchRoute(
            ordered,
            ordered.Sum(vehicle => vehicle.ResearchRp ?? 0),
            ordered.Sum(vehicle => vehicle.PurchaseSl ?? 0),
            extras.Count);
    }

    public static int UnlockRequirement(string type, int rankNumber)
    {
        if (type == "helicopter") return 1;
        if (type == "aviation" && rankNumber == 8) return 3;
        return rankNumber >= 5 ? 5 : 6;
    }
}
