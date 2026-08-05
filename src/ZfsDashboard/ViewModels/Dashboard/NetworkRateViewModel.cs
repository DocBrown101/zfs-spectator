using Zfs.Core.Models;
using ZfsDashboard.Presentation;

namespace ZfsDashboard.ViewModels.Dashboard;

public sealed record NetworkRateViewModel
{
    public NetworkRateViewModel(NetworkRateInfo rate)
    {
        this.Name = rate.Name;
        this.RxBytesPerSecond = rate.RxBytesPerSec;
        this.TxBytesPerSecond = rate.TxBytesPerSec;
        this.DownloadRate = rate.RxBytesPerSec.FormatRate();
        this.UploadRate = rate.TxBytesPerSec.FormatRate();
    }

    public string Name { get; }
    public double RxBytesPerSecond { get; }
    public double TxBytesPerSecond { get; }
    public string DownloadRate { get; }
    public string UploadRate { get; }
}
