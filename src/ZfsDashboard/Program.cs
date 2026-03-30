using Zfs.Core;
using Zfs.Core.Services;
using Zfs.Core.Services.TestData;
using ZfsDashboard;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IZpoolService, DemoDataZpoolService>();
    builder.Services.AddSingleton<IZfsService, DemoDataZfsService>();
    builder.Services.AddSingleton<ISystemService, DemoDataSystemService>();
}
else
{
    builder.Services.AddSingleton<ICommandExecutor, CommandExecutor>();
    builder.Services.AddSingleton<IZpoolService, ZpoolService>();
    builder.Services.AddSingleton<IZfsService, ZfsService>();
    builder.Services.AddSingleton<ISystemService, SystemService>();
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();
app.MapGet("/api/live", async (ISystemService sys, IZfsService zfs, IZpoolService zpool) => await sys.GetDashboardDataAsync(zfs, zpool));
app.Run();
