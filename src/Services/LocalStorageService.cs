using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace FundsManager.Services;

public class LocalStorageService
{
    private ProtectedLocalStorage ProtectedLocalStorage;

    public LocalStorageService(ProtectedLocalStorage protectedLocalStorage)
    {
        ProtectedLocalStorage = protectedLocalStorage;
    }

    public async Task<T> LoadStorage<T>(string name, T? defaultValue = default)
    {
        var result = await ProtectedLocalStorage.GetAsync<T>(name);
        if (result.Success)
        {
            return result.Value;
        }
        if (defaultValue != null)
        {
            await ProtectedLocalStorage.SetAsync(name, defaultValue);
        }
        return defaultValue;
    }

    public async Task SetStorage<T>(string name, T value)
    {
        await ProtectedLocalStorage.SetAsync(name, value);
    }
}