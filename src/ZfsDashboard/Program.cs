using Zfs.Core;
using Zfs.Core.Services;
using Zfs.Core.Services.TestData;
using ZfsDashboard;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddSingleton<SystemService>();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IZpoolService, TestDataZpoolService>();
    builder.Services.AddSingleton<IZfsService, TestDataZfsService>();
}
else
{
    builder.Services.AddSingleton<ICommandExecutor, CommandExecutor>();
    builder.Services.AddSingleton<IZpoolService, ZpoolService>();
    builder.Services.AddSingleton<IZfsService, ZfsService>();
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();
app.MapGet("/api/live", async (SystemService sys, IZfsService zfs, IZpoolService zpool) => await sys.GetDashboardDataAsync(zfs, zpool));
app.Run();
