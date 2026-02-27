using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpHook.Data;

namespace Jido.Services
{
    public interface IToggleableService
    {
        public Task<KeyCode> ChangeToggleKey();

        public KeyCode ToggleKey { get; }
    }
}
