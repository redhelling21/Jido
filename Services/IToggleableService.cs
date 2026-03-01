using System.Threading.Tasks;
using Jido.Utils;

namespace Jido.Services
{
    public interface IToggleableService
    {
        public Task<KeyCombo> ChangeToggleKey();

        public KeyCombo ToggleKey { get; }
    }
}
