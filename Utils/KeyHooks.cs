using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SharpHook;
using SharpHook.Data;

namespace Jido.Utils
{
    public class HooksManager : IHooksManager
    {
        private TaskPoolGlobalHook _hook = new();
        private readonly Dictionary<KeyCombo, EventHandler> _comboEvents = new();
        private readonly HashSet<KeyCode> _pressedKeys = new();
        public Dictionary<MouseButton, List<EventHandler>> _mouseClickedEvents = new();

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
            if (_comboEvents.ContainsKey(combo))
                throw new InvalidOperationException("Key combo already registered");
            _comboEvents.Add(combo, pressed);
        }

        public void UnregisterCombo(KeyCombo combo)
        {
            if (_comboEvents.ContainsKey(combo))
                _comboEvents.Remove(combo);
            else
                throw new InvalidOperationException("Key combo not registered");
        }

        public void OnKeyPressed(object? sender, KeyboardHookEventArgs args)
        {
            var keyCode = args.RawEvent.Keyboard.KeyCode;
            _pressedKeys.Add(keyCode);

            // If the key is not a modifier one, this is probably the end of a combo
            if (!ModifierKeyCodes.Contains(keyCode))
            {
                var combo = BuildCombo(keyCode);
                if (_comboEvents.TryGetValue(combo, out var handler))
                    handler?.Invoke(sender, args);
            }
        }

        public void OnKeyReleased(object? sender, KeyboardHookEventArgs args)
        {
            _pressedKeys.Remove(args.RawEvent.Keyboard.KeyCode);
        }

        public void OnMouseClicked(object? sender, MouseHookEventArgs args)
        {
            if (_mouseClickedEvents.ContainsKey(args.RawEvent.Mouse.Button))
                _mouseClickedEvents[args.RawEvent.Mouse.Button]?.ForEach(e => e.Invoke(sender, args));
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

                tcs.SetResult(BuildCombo(keyCode));
                _hook.KeyPressed -= handler;
            };
            _hook.KeyPressed += handler;
            return tcs.Task;
        }

        public void Dispose()
        {
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

        private KeyCombo BuildCombo(KeyCode key) =>
            new(
                key,
                ctrl: _pressedKeys.Contains(KeyCode.VcLeftControl) || _pressedKeys.Contains(KeyCode.VcRightControl),
                alt: _pressedKeys.Contains(KeyCode.VcLeftAlt) || _pressedKeys.Contains(KeyCode.VcRightAlt),
                shift: _pressedKeys.Contains(KeyCode.VcLeftShift) || _pressedKeys.Contains(KeyCode.VcRightShift)
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
