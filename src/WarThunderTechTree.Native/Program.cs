namespace WarThunderTechTree.Native;

internal static class Program
{
    [STAThread]
    private static async Task<int> Main(string[] args)
    {
        if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase)) return await SelfTest.RunAsync();
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var preview = args.FirstOrDefault(argument => argument.StartsWith("--preview-", StringComparison.OrdinalIgnoreCase));
        if (preview is not null)
        {
            using var repository = new DataRepository();
            await repository.InitializeAsync();
            var isLoadout = preview.Equals("--preview-loadout", StringComparison.OrdinalIgnoreCase);
            var country = isLoadout ? "ussr" : "usa";
            var type = isLoadout ? "aviation" : "ground";
            var unitId = isLoadout ? "su_30sm2" : "us_m1a2_sep3_abrams";
            var tree = await repository.GetTreeAsync(country, type);
            var vehicle = tree.Vehicles.Single(item => item.UnitId == unitId);
            var details = await repository.GetDetailsAsync(country, type, vehicle);
            var (section, data) = preview.ToLowerInvariant() switch
            {
                "--preview-spec" => ("车辆性能", details.Specifications),
                "--preview-ammo" => ("弹药数据", details.Ammunition),
                "--preview-loadout" => ("挂载数据", details.Loadouts),
                "--preview-mods" => ("改装件", details.Modifications),
                _ => ("辅助设备", details.Auxiliary)
            };
            Application.Run(new JsonDetailForm(section, data!.Value, vehicle.NameChinese, "rb"));
            return 0;
        }
        Application.Run(new MainForm());
        return 0;
    }
}
