using Zfs.Core;
using Zfs.Core.Services;
using Zfs.Core.Services.TestData;
using ZfsDashboard;
using ZfsDashboard.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<ICommandExecutor, EmbeddedJsonCommandExecutor>();
    builder.Services.AddSingleton<IZpoolService, ZpoolService>();
    builder.Services.AddSingleton<IZfsService, ZfsService>();
    builder.Services.AddSingleton<ISystemService, DemoDataSystemService>();
    builder.Services.AddSingleton<IDiskTemperatureProvider, DemoDataDiskTemperatureProvider>();
}
else
{
    builder.Services.AddSingleton<ICommandExecutor, CommandExecutor>();
    builder.Services.AddSingleton<IZpoolService, ZpoolService>();
    builder.Services.AddSingleton<IZfsService, ZfsService>();
    builder.Services.AddSingleton<ISystemService, SystemService>();
    builder.Services.AddSingleton<DiskTemperatureBackgroundService>();
    builder.Services.AddSingleton<IDiskTemperatureProvider>(sp => sp.GetRequiredService<DiskTemperatureBackgroundService>());
    builder.Services.AddHostedService(sp => sp.GetRequiredService<DiskTemperatureBackgroundService>());
}

builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<DashboardSnapshotBackgroundService>();
builder.Services.AddSingleton<IDashboardSnapshotProvider>(sp => sp.GetRequiredService<DashboardSnapshotBackgroundService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<DashboardSnapshotBackgroundService>());

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();
app.Run();
