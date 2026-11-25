using Zfs.Core.Models;
using Zfs.Core.Services;

namespace ZfsDashboard.ViewModels.Shared;

public sealed record EncryptionDetailsViewModel(string Label, string Algorithm, bool KeyLocked, KeySettings? Key, string? EncryptionRoot)
{
    public string? LoadKeyCommand => this.KeyLocked && this.EncryptionRoot is not null
        ? CommandSuggestionsService.LoadKeyCommand(this.EncryptionRoot)
        : null;
}
