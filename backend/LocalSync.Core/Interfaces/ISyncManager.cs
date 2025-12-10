namespace LocalSync.Core.Interfaces;

public interface ISyncManager
{
    void StartSync(string folderPath, Guid targetDeviceId);
    void StopSync(string folderPath);
    IEnumerable<string> GetActiveSyncs();
}
