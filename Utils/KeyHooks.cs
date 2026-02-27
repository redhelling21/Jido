using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using SharpHook;
using SharpHook.Data;

namespace Jido.Utils
{
    public class HooksManager : IHooksManager
    {
        private TaskPoolGlobalHook _hook = new();
        public Dictionary<KeyCode, EventHandler> _keyPressedEvents = new();
        public Dictionary<MouseButton, List<EventHandler>> _mouseClickedEvents = new();

        public HooksManager()
        {
            _hook.KeyPressed += OnKeyPressed;
            _hook.MouseClicked += OnMouseClicked;
            _hook.RunAsync();
        }

        public void RegisterKey(KeyCode key, EventHandler pressed)
        {
            if (_keyPressedEvents.ContainsKey(key))
            {
                throw new Exception("Key already registered");
            }
            _keyPressedEvents.Add(key, pressed);
        }

        public void UnregisterKey(KeyCode key)
        {
            if (_keyPressedEvents.ContainsKey(key))
            {
                _keyPressedEvents.Remove(key);
            }
            else
            {
                throw new Exception("Key not registered");
            }
        }

        public void OnKeyPressed(object? sender, KeyboardHookEventArgs args)
        {
            if (_keyPressedEvents.ContainsKey(args.RawEvent.Keyboard.KeyCode))
            {
                _keyPressedEvents[args.RawEvent.Keyboard.KeyCode]?.Invoke(sender, args);
            }
        }

        public void OnMouseClicked(object? sender, MouseHookEventArgs args)
        {
            if (_mouseClickedEvents.ContainsKey(args.RawEvent.Mouse.Button))
            {
                _mouseClickedEvents[args.RawEvent.Mouse.Button]?.ForEach(e => e.Invoke(sender, args));
            }
        }

        public Task<KeyCode> ListenNextKey()
        {
            var tcs = new TaskCompletionSource<KeyCode>();
            EventHandler<KeyboardHookEventArgs> handler = null;
            handler = (sender, e) =>
            {
                tcs.SetResult(e.RawEvent.Keyboard.KeyCode);
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
            {
                _mouseClickedEvents.Add(button, new List<EventHandler>());
            }
            _mouseClickedEvents[button].Add(clicked);
        }

        public void UnRegisterMouseClick(MouseButton button, EventHandler clicked)
        {
            if (_mouseClickedEvents.ContainsKey(button))
            {
                _mouseClickedEvents[button].Remove(clicked);
            }
            else
            {
                throw new Exception("Button not registered");
            }
        }
    }

    public interface IHooksManager : IDisposable
    {
        void RegisterKey(KeyCode key, EventHandler pressed);

        void UnregisterKey(KeyCode key);

        void RegisterMouseClick(MouseButton button, EventHandler clicked);

        void UnRegisterMouseClick(MouseButton button, EventHandler clicked);

        Task<KeyCode> ListenNextKey();
    }
}
