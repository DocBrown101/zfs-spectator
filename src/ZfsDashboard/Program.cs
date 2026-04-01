using Zfs.Core;
using Zfs.Core.Services;
using Zfs.Core.Services.TestData;
using ZfsDashboard;
using ZfsDashboard.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddSingleton<IPartialRenderer, PartialRenderer>();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IZpoolService, DemoDataZpoolService>();
    builder.Services.AddSingleton<IZfsService, DemoDataZfsService>();
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

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();
app.Run();
