using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpHook;
using SharpHook.Data;

namespace Jido.Utils
{
    public static class SimulationUtils
    {
        private static EventSimulator _simulator = new EventSimulator();

        static SimulationUtils()
        {
            // This class is meant to be used as a static class
        }

        public static async Task MouseMoveAndClickAsync(short x, short y)
        {
            _simulator.SimulateMouseMovement(x, y);
            await Task.Delay(50);
            _simulator.SimulateMousePress(MouseButton.Button1);
            await Task.Delay(50);
            _simulator.SimulateMouseRelease(MouseButton.Button1);
        }
    }
}
