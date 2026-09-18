using System.Text.Json;

namespace WarThunderTechTree.Native;

internal static class SelfTest
{
    public static async Task<int> RunAsync()
    {
        try
        {
            var dataDirectory = Path.Combine(AppContext.BaseDirectory, "Data");
            using var repository = new DataRepository(dataDirectory);
            await repository.InitializeAsync();
            if (repository.Countries.Countries.Count != 10) throw new InvalidDataException("国家数量不是 10");

            var treeCount = 0;
            var vehicleCount = 0;
            var missingRatings = 0;
            foreach (var country in repository.Countries.Countries)
            {
                foreach (var type in new[] { "ground", "aviation", "helicopter" })
                {
                    var tree = await repository.GetTreeAsync(country.Code, type);
                    treeCount++;
                    vehicleCount += tree.Vehicles.Count;
                    missingRatings += tree.Vehicles.Count(vehicle =>
                        string.IsNullOrWhiteSpace(vehicle.BattleRatingAb) || vehicle.BattleRatingAb == "—" ||
                        string.IsNullOrWhiteSpace(vehicle.BattleRatingRb) || vehicle.BattleRatingRb == "—" ||
                        string.IsNullOrWhiteSpace(vehicle.BattleRatingSb) || vehicle.BattleRatingSb == "—");
                }
            }
            if (treeCount != 30 || vehicleCount != 2660 || missingRatings != 0)
                throw new InvalidDataException($"数据统计异常：{treeCount} 棵树，{vehicleCount} 辆，{missingRatings} 辆缺权重");

            var usa = await repository.GetTreeAsync("usa", "ground");
            var m1a2 = usa.Vehicles.Single(vehicle => vehicle.UnitId == "us_m1a2_sep3_abrams");
            var m1Details = await repository.GetDetailsAsync("usa", "ground", m1a2);
            if (m1Details.Auxiliary is null || m1Details.Specifications is null || m1Details.Ammunition is null)
                throw new InvalidDataException("M1A2 SEPv3 资料不完整");

            var ussr = await repository.GetTreeAsync("ussr", "aviation");
            var su30 = ussr.Vehicles.Single(vehicle => vehicle.UnitId == "su_30sm2");
            var su30Details = await repository.GetDetailsAsync("ussr", "aviation", su30);
            if (su30Details.Loadouts is null || !su30Details.Loadouts.Value.TryGetProperty("slot_count", out var slots) || slots.GetInt32() != 13)
                throw new InvalidDataException("Su-30SM2 挂载数据不完整");

            var previews = new[]
            {
                new JsonDetailForm("辅助设备", m1Details.Auxiliary.Value, m1a2.NameChinese, "rb"),
                new JsonDetailForm("车辆性能", m1Details.Specifications.Value, m1a2.NameChinese, "rb"),
                new JsonDetailForm("弹药数据", m1Details.Ammunition.Value, m1a2.NameChinese, "rb"),
                new JsonDetailForm("改装件", m1Details.Modifications!.Value, m1a2.NameChinese, "rb"),
                new JsonDetailForm("挂载数据", su30Details.Loadouts.Value, su30.NameChinese, "rb")
            };
            foreach (var preview in previews)
            {
                preview.CreateControl();
                preview.Dispose();
            }

            Console.WriteLine($"SELF-TEST OK | countries=10 trees={treeCount} vehicles={vehicleCount} missingRatings={missingRatings}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"SELF-TEST FAILED | {exception}");
            return 1;
        }
    }
}
