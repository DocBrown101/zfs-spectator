using Zfs.Core.Models;
using ZfsDashboard.Presentation;

namespace ZfsDashboard.ViewModels.Dashboard;

public sealed record NetworkRateViewModel
{
    private readonly NetworkRateInfo rate;

    public NetworkRateViewModel(NetworkRateInfo rate)
    {
        this.rate = rate;
    }

    public string Name => this.rate.Name;
    public double RxBytesPerSecond => this.rate.RxBytesPerSec;
    public double TxBytesPerSecond => this.rate.TxBytesPerSec;
    public string DownloadRate => this.rate.RxBytesPerSec.FormatRate();
    public string UploadRate => this.rate.TxBytesPerSec.FormatRate();
}
