namespace LeoLauncher;

// UI-thread contract. Each host/window retains its own enable policy and disposal
// responsibility; no implicit dependency on a business window's visibility.
interface IPeriodicTask:IDisposable
{
    bool Enabled{get;}
    bool Running{get;}
    void Configure(bool enabled);
    Task RunNowAsync();
    void Retry();
}
