using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using SharpHook;
using SharpHook.Data;

namespace Jido.Utils
{
    public static class SimulationUtils
    {
        // TODO : change when  handling linux
        public static EventSimulator Simulator { get; } = new EventSimulator();

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        private static async Task MoveMouse(short x, short y, int moveDurationMs, CancellationToken cancellationToken)
        {
            if (moveDurationMs > 0)
            {
                GetCursorPos(out POINT start);

                int steps = Math.Max(10, moveDurationMs / 10);
                int stepDelay = moveDurationMs / steps;
                for (int i = 1; i <= steps; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    double t = (double)i / steps;
                    double eased = t * t * (3 - 2 * t);

                    var cx = start.X + (x - start.X) * eased;
                    var cy = start.Y + (y - start.Y) * eased;

                    double jitter = (1 - eased) * 1.5;
                    cx += (Random.Shared.NextDouble() - 0.5) * jitter;
                    cy += (Random.Shared.NextDouble() - 0.5) * jitter;

                    Simulator.SimulateMouseMovement((short)cx, (short)cy);
                    await Task.Delay(stepDelay, cancellationToken);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            Simulator.SimulateMouseMovement(x, y);
            await Task.Delay(50, cancellationToken);
        }

        public static async Task MouseMoveAndClickAsync(
            short x,
            short y,
            bool left = true,
            int moveDurationMs = 30,
            CancellationToken cancellationToken = default
        )
        {
            await MoveMouse(x, y, moveDurationMs, cancellationToken);
            var button = left ? MouseButton.Button1 : MouseButton.Button2;
            Simulator.SimulateMousePress(button);
            try
            {
                await Task.Delay(50, cancellationToken);
            }
            finally
            {
                // Always release, even if the routine is cancelled mid-hold
                Simulator.SimulateMouseRelease(button);
            }
        }
    }
}
