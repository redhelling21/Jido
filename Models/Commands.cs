using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Threading;
using System.Timers;
using SharpHook.Native;

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
    public class HighLevelCommand
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

        public void Start(ConcurrentQueue<LowLevelCommand> queue)
        {
            CommandQueue = queue;
            Timer.Start();
        }

        public void Stop()
        {
            Timer.Stop();
        }

        protected void RandomizeInterval()
        {
            Timer.Interval = IntervalInMs * (Random.Shared.NextDouble() * 0.2 + 0.9);
        }
    }

    // Command that contains a sequence of low-level commands
    public class CompositeHighLevelCommand : HighLevelCommand
    {
        public List<LowLevelCommand> Commands { get; set; } = new List<LowLevelCommand>();

        private void TimerCallback(Object? source, ElapsedEventArgs e)
        {
            if (CommandQueue == null)
                return;
            foreach (var command in Commands)
                CommandQueue.Enqueue(command);
            RandomizeInterval();
        }

        public CompositeHighLevelCommand(List<LowLevelCommand> commands, int intervalInMs)
            : base(intervalInMs)
        {
            Commands = commands;
            Timer = new System.Timers.Timer(IntervalInMs);
            Timer.Elapsed += TimerCallback;
            Timer.AutoReset = true;
        }
    }

    public class BasicHighLevelCommand : HighLevelCommand
    {
        public PressCommand Command { get; set; }

        private void TimerCallback(Object? source, ElapsedEventArgs e)
        {
            if (CommandQueue == null)
                return;
            CommandQueue.Enqueue(Command);
            RandomizeInterval();
        }

        public BasicHighLevelCommand(PressCommand command, int intervalInMs)
            : base(intervalInMs)
        {
            Command = command;

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
