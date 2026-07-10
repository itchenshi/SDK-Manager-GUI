using System.Collections.Generic;
using System.Threading.Tasks;
using SDK_Manager_GUI.Models;

namespace SDK_Manager_GUI.Services
{
    public interface IMirrorProvider
    {
        Task<IEnumerable<MirrorSource>> GetMirrorsAsync(SdkLanguage language);
        Task<MirrorSource> GetBestMirrorAsync(SdkLanguage language);
        Task TestMirrorLatencyAsync(MirrorSource mirror);
        Task AddMirrorAsync(MirrorSource mirror);
        Task RemoveMirrorAsync(string mirrorId);
        Task UpdateMirrorAsync(MirrorSource mirror);
        Task SetDefaultMirrorAsync(string mirrorId);
        Task ResetToDefaultAsync();
        Task RecordMirrorResultAsync(string mirrorId, bool success);
        Task TestAndDisableUnreachableMirrorsAsync();
    }
}
