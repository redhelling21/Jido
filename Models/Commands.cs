using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Threading;
using System.Timers;
using SharpHook.Data;

namespace Jido.Models
{
    [JsonDerivedType(typeof(PressCommand), typeDiscriminator: "press")]
    [JsonDerivedType(typeof(WaitCommand), typeDiscriminator: "wait")]
    public abstract class LowLevelCommand
    { }

    public class PressCommand : LowLevelCommand
    {
        public KeyCode KeyToPress { get; set; }
        public int PressDurationInMs { get; set; } = 100;
    }

    public class WaitCommand : LowLevelCommand
    {
        public int WaitTimeInMs { get; set; }
    }

    [JsonDerivedType(typeof(HighLevelCommand), typeDiscriminator: "base")]
    [JsonDerivedType(typeof(CompositeHighLevelCommand), typeDiscriminator: "composite")]
    [JsonDerivedType(typeof(BasicHighLevelCommand), typeDiscriminator: "basic")]
    public class HighLevelCommand : IDisposable
    {
        public int IntervalInMs { get; set; }
        protected System.Timers.Timer Timer { get; set; } = new System.Timers.Timer();
        protected ConcurrentQueue<LowLevelCommand> CommandQueue { get; set; } = new ConcurrentQueue<LowLevelCommand>();

        public HighLevelCommand(int intervalInMs)
        {
            if (intervalInMs == 0)
            {
                throw new ArgumentException("Interval cannot be 0");
            }
            IntervalInMs = intervalInMs;
        }

        private double _randomizationRatio;

        public void Start(ConcurrentQueue<LowLevelCommand> queue, double randomizationRatio)
        {
            CommandQueue = queue;
            _randomizationRatio = randomizationRatio;
            Enqueue();
            Timer.Start();
        }

        public void Stop()
        {
            Timer.Stop();
        }

        public virtual void Dispose()
        {
            Timer.Stop();
            Timer.Dispose();
        }

        protected virtual void Enqueue()
        { }

        protected void RandomizeInterval()
        {
            // Produces a multiplier uniformly distributed in [1 - ratio, 1 + ratio].
            Timer.Interval = IntervalInMs * (1.0 - _randomizationRatio + Random.Shared.NextDouble() * 2 * _randomizationRatio);
        }
    }

    // Command that contains a sequence of low-level commands
    public class CompositeHighLevelCommand : HighLevelCommand
    {
        public List<LowLevelCommand> Commands { get; set; } = new List<LowLevelCommand>();

        protected override void Enqueue()
        {
            if (CommandQueue == null)
                return;
            foreach (var command in Commands)
                CommandQueue.Enqueue(command);
        }

        private void TimerCallback(Object? source, ElapsedEventArgs e)
        {
            Enqueue();
            RandomizeInterval();
        }

        public CompositeHighLevelCommand(List<LowLevelCommand> commands, int intervalInMs)
            : base(intervalInMs)
        {
            Commands = commands;
            // Dispose the placeholder created by the base field initializer before replacing it.
            Timer.Dispose();
            Timer = new System.Timers.Timer(IntervalInMs);
            Timer.Elapsed += TimerCallback;
            Timer.AutoReset = true;
        }
    }

    public class BasicHighLevelCommand : HighLevelCommand
    {
        public PressCommand Command { get; set; }

        protected override void Enqueue()
        {
            if (CommandQueue == null)
                return;
            CommandQueue.Enqueue(Command);
        }

        private void TimerCallback(Object? source, ElapsedEventArgs e)
        {
            Enqueue();
            RandomizeInterval();
        }

        public BasicHighLevelCommand(PressCommand command, int intervalInMs)
            : base(intervalInMs)
        {
            Command = command;

            // Dispose the placeholder created by the base field initializer before replacing it.
            Timer.Dispose();
            Timer = new System.Timers.Timer(IntervalInMs);
            Timer.Elapsed += TimerCallback;
            Timer.AutoReset = true;
        }
    }

    public class ConstantCommand
    {
        public KeyCode KeyToPress { get; set; }
    }
}
