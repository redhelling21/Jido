using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        public Dictionary<MouseButton, List<EventHandler>> _mouseClickedEvents = new();

        // Set to true while ListenNextCombo is waiting; suppresses normal combo dispatch.
        private volatile bool _isListening;

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

        public Task<KeyCombo> ListenNextCombo()
        {
            var tcs = new TaskCompletionSource<KeyCombo>();
            EventHandler<KeyboardHookEventArgs> handler = null!;
            handler = (sender, e) =>
            {
                var keyCode = e.RawEvent.Keyboard.KeyCode;
                if (ModifierKeyCodes.Contains(keyCode))
                    return; // wait for the non-modifier key
                _isListening = false;
                _hook.KeyPressed -= handler;
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
            if (!_mouseClickedEvents.ContainsKey(button))
                _mouseClickedEvents.Add(button, new List<EventHandler>());
            _mouseClickedEvents[button].Add(clicked);
        }

        public void UnRegisterMouseClick(MouseButton button, EventHandler clicked)
        {
            if (_mouseClickedEvents.ContainsKey(button))
                _mouseClickedEvents[button].Remove(clicked);
            else
                throw new InvalidOperationException("Button not registered");
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
            if (_mouseClickedEvents.ContainsKey(args.RawEvent.Mouse.Button))
                _mouseClickedEvents[args.RawEvent.Mouse.Button]?.ForEach(e => e.Invoke(sender, args));
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

        void RegisterMouseClick(MouseButton button, EventHandler clicked);

        void UnRegisterMouseClick(MouseButton button, EventHandler clicked);

        Task<KeyCombo> ListenNextCombo();
    }
}
