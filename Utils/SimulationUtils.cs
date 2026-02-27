using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpHook;
using SharpHook.Data;

namespace Jido.Utils
{
    public static class SimulationUtils
    {
        private static readonly EventSimulator _simulator = new EventSimulator();
        private static readonly Random _rng = new Random();

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        public static async Task MouseMoveAndClickAsync(
            short x,
            short y,
            int moveDurationMs = 30,
            CancellationToken cancellationToken = default
        )
        {
            if (moveDurationMs > 0)
            {
                GetCursorPos(out POINT start);

                int steps = Math.Max(10, moveDurationMs / 10);
                int stepDelay = moveDurationMs / steps;
                // Split the movement into small steps
                for (int i = 1; i <= steps; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    double t = (double)i / steps;
                    // Smoothstep easing
                    double eased = t * t * (3 - 2 * t);

                    var cx = (start.X + (x - start.X) * eased);
                    var cy = (start.Y + (y - start.Y) * eased);

                    // Tiny pixel-level noise that fades out near the target
                    double jitter = (1 - eased) * 1.5;
                    cx += (_rng.NextDouble() - 0.5) * jitter;
                    cy += (_rng.NextDouble() - 0.5) * jitter;

                    _simulator.SimulateMouseMovement((short)cx, (short)cy);
                    await Task.Delay(stepDelay, cancellationToken);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            _simulator.SimulateMouseMovement(x, y);
            await Task.Delay(50, cancellationToken);
            _simulator.SimulateMousePress(MouseButton.Button1);
            await Task.Delay(50, cancellationToken);
            _simulator.SimulateMouseRelease(MouseButton.Button1);
        }
    }
}
