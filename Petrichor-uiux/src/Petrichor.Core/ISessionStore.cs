using System.Threading.Tasks;
namespace Petrichor.Core {
    public interface ISessionStore { Task<string> LoadAsync(); Task SaveAsync(string cookie); Task ClearAsync(); }
    public sealed class MemorySessionStore : ISessionStore {
        private string value;
        public Task<string> LoadAsync() => Task.FromResult(value);
        public Task SaveAsync(string cookie) { value = cookie; return Task.FromResult(0); }
        public Task ClearAsync() { value = null; return Task.FromResult(0); }
    }
}
