using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpHook;
using SharpHook.Data;

namespace Jido.Utils
{
    public class HooksManager : IHooksManager
    {
        private TaskPoolGlobalHook _hook = new();
        private readonly ConcurrentDictionary<KeyCombo, EventHandler> _comboEvents = new();
        private readonly ConcurrentDictionary<KeyCode, byte> _pressedKeys = new();
        // Guarded by _mouseLock: registration happens at startup and removal at shutdown, while
        // OnMouseClicked reads on the SharpHook dispatch thread.
        private readonly Dictionary<MouseButton, List<EventHandler>> _mouseClickedEvents = new();
        private readonly object _mouseLock = new();

        // Set to true while ListenNextCombo is waiting; suppresses normal combo dispatch.
        private volatile bool _isListening;

        // 0 = idle, 1 = a listen is in flight. Guards against overlapping ListenNextCombo calls.
        private int _listenInProgress;

        private static readonly HashSet<KeyCode> ModifierKeyCodes =
            new()
            {
                KeyCode.VcLeftControl,
                KeyCode.VcRightControl,
                KeyCode.VcLeftAlt,
                KeyCode.VcRightAlt,
                KeyCode.VcLeftShift,
                KeyCode.VcRightShift,
            };

        public HooksManager()
        {
            _hook.KeyPressed += OnKeyPressed;
            _hook.KeyReleased += OnKeyReleased;
            _hook.MouseClicked += OnMouseClicked;
            _hook.RunAsync();
        }

        public void RegisterCombo(KeyCombo combo, EventHandler pressed)
        {
            if (!_comboEvents.TryAdd(combo, pressed))
                throw new InvalidOperationException("Key combo already registered");
        }

        public void UnregisterCombo(KeyCombo combo)
        {
            if (!_comboEvents.TryRemove(combo, out _))
                throw new InvalidOperationException("Key combo not registered");
        }

        public bool IsComboRegistered(KeyCombo combo) => _comboEvents.ContainsKey(combo);

        public Task<KeyCombo> ListenNextCombo()
        {
            // Block overlapping listens
            if (Interlocked.CompareExchange(ref _listenInProgress, 1, 0) != 0)
                throw new InvalidOperationException("A combo listen is already in progress");

            // So the await of the caller won't run on the hook thread
            var tcs = new TaskCompletionSource<KeyCombo>(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler<KeyboardHookEventArgs> handler = null!;
            handler = (sender, e) =>
            {
                var keyCode = e.RawEvent.Keyboard.KeyCode;
                if (ModifierKeyCodes.Contains(keyCode))
                    return; // wait for the non-modifier key
                _hook.KeyPressed -= handler;
                _isListening = false;
                Interlocked.Exchange(ref _listenInProgress, 0);
                tcs.SetResult(BuildCombo(keyCode));
            };
            _isListening = true;
            _hook.KeyPressed += handler;
            return tcs.Task;
        }

        public void Dispose()
        {
            _pressedKeys.Clear();
            _hook.Dispose();
        }

        public void RegisterMouseClick(MouseButton button, EventHandler clicked)
        {
            lock (_mouseLock)
            {
                if (!_mouseClickedEvents.TryGetValue(button, out var handlers))
                {
                    handlers = new List<EventHandler>();
                    _mouseClickedEvents[button] = handlers;
                }
                handlers.Add(clicked);
            }
        }

        public void UnRegisterMouseClick(MouseButton button, EventHandler clicked)
        {
            lock (_mouseLock)
            {
                if (!_mouseClickedEvents.TryGetValue(button, out var handlers))
                    throw new InvalidOperationException("Button not registered");
                handlers.Remove(clicked);
            }
        }

        private void OnKeyPressed(object? sender, KeyboardHookEventArgs args)
        {
            var keyCode = args.RawEvent.Keyboard.KeyCode;
            _pressedKeys.TryAdd(keyCode, 0);

            if (!_isListening && !ModifierKeyCodes.Contains(keyCode))
            {
                var combo = BuildCombo(keyCode);
                if (_comboEvents.TryGetValue(combo, out var handler))
                    handler?.Invoke(sender, args);
            }
        }

        private void OnKeyReleased(object? sender, KeyboardHookEventArgs args)
        {
            _pressedKeys.TryRemove(args.RawEvent.Keyboard.KeyCode, out _);
        }

        private void OnMouseClicked(object? sender, MouseHookEventArgs args)
        {
            EventHandler[] snapshot;
            lock (_mouseLock)
            {
                if (!_mouseClickedEvents.TryGetValue(args.RawEvent.Mouse.Button, out var handlers))
                    return;
                snapshot = handlers.ToArray();
            }
            // Invoke outside the lock: handlers run arbitrary service code.
            foreach (var handler in snapshot)
                handler.Invoke(sender, args);
        }

        private KeyCombo BuildCombo(KeyCode key) =>
            new(
                key,
                ctrl: _pressedKeys.ContainsKey(KeyCode.VcLeftControl)
                    || _pressedKeys.ContainsKey(KeyCode.VcRightControl),
                alt: _pressedKeys.ContainsKey(KeyCode.VcLeftAlt) || _pressedKeys.ContainsKey(KeyCode.VcRightAlt),
                shift: _pressedKeys.ContainsKey(KeyCode.VcLeftShift) || _pressedKeys.ContainsKey(KeyCode.VcRightShift)
            );
    }

    public interface IHooksManager : IDisposable
    {
        void RegisterCombo(KeyCombo combo, EventHandler pressed);

        void UnregisterCombo(KeyCombo combo);

        bool IsComboRegistered(KeyCombo combo);

        void RegisterMouseClick(MouseButton button, EventHandler clicked);

        void UnRegisterMouseClick(MouseButton button, EventHandler clicked);

        Task<KeyCombo> ListenNextCombo();
    }
}
