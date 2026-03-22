using Zfs.Core;
using Zfs.Core.Services;
using ZfsDashboard;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddSingleton<ICommandExecutor, CommandExecutor>();
builder.Services.AddSingleton<ZpoolService>();
builder.Services.AddSingleton<ZfsService>();
builder.Services.AddSingleton<SystemService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();
app.MapGet("/api/live", async (SystemService sys, ZfsService zfs, ZpoolService zpool) => await sys.GetDashboardDataAsync(zfs, zpool));
app.Run();
