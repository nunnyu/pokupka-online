using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace PkillGame.Terminal
{
    // Attach to any GameObject in the game scene that has a UIDocument component
    // pointed at TerminalView.uxml.
    [RequireComponent(typeof(UIDocument))]
    public class TerminalController : MonoBehaviour
    {
        // ── Public API ────────────────────────────────────────────────────────────
        public CommandRegistry Commands { get; private set; }
        public bool IsOpen => m_Overlay?.style.display == DisplayStyle.Flex;

        // ─────────────────────────────────────────────────────────────────────────

        UIDocument   m_Document;
        VisualElement m_Overlay;
        ScrollView   m_OutputScroll;
        TextField    m_InputField;

        readonly List<string> m_History      = new();
        int                   m_HistoryIndex = -1;
        bool                  m_SplashPrinted;

        const string DIVIDER  = "────────────────────────────────────────────────────────────";
        const string VERSION  = "pkill -9 v0.1";
        const string HINT     = "Escape to close  |  'help' for commands";

        void Awake()
        {
            Commands  = new CommandRegistry();
            m_Document = GetComponent<UIDocument>();
        }

        void OnEnable()
        {
            var root       = m_Document.rootVisualElement;
            m_Overlay      = root.Q("terminal-overlay");
            m_OutputScroll = root.Q<ScrollView>("output-scroll");
            m_InputField   = root.Q<TextField>("input-field");

            m_InputField.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
        }

        void OnDisable()
        {
            m_InputField?.UnregisterCallback<KeyDownEvent>(OnKeyDown);
        }

        void Update()
        {
            if (Keyboard.current[Key.T].wasPressedThisFrame && !IsOpen)
                Open();
        }

        // ── Open / Close ──────────────────────────────────────────────────────────

        public void Open()
        {
            m_Overlay.style.display = DisplayStyle.Flex;

            if (!m_SplashPrinted)
            {
                PrintSplash();
                m_SplashPrinted = true;
            }

            m_InputField.value = string.Empty;
            m_HistoryIndex     = -1;
            m_InputField.Focus();
            // Defer SetValueWithoutNotify so the focus event fires first
            m_InputField.schedule.Execute(() => m_InputField.SelectAll()).StartingIn(0);
        }

        public void Close()
        {
            m_Overlay.style.display = DisplayStyle.None;
        }

        // ── Key handling ──────────────────────────────────────────────────────────

        void OnKeyDown(KeyDownEvent evt)
        {
            switch (evt.keyCode)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    Submit();
                    evt.StopPropagation();
                    break;

                case KeyCode.Escape:
                    Close();
                    evt.StopPropagation();
                    break;

                case KeyCode.UpArrow:
                    NavigateHistory(+1);
                    evt.StopPropagation();
                    break;

                case KeyCode.DownArrow:
                    NavigateHistory(-1);
                    evt.StopPropagation();
                    break;

                case KeyCode.Tab:
                    Autocomplete();
                    evt.StopPropagation();
                    break;
            }
        }

        // ── Submit ────────────────────────────────────────────────────────────────

        void Submit()
        {
            var raw = m_InputField.value.Trim();
            m_InputField.value = string.Empty;

            if (string.IsNullOrEmpty(raw)) return;

            // Store in history (most-recent first, cap at 100 entries)
            m_History.Insert(0, raw);
            if (m_History.Count > 100) m_History.RemoveAt(m_History.Count - 1);
            m_HistoryIndex = -1;

            AppendLine($"$ {raw}");

            var tokens  = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var result  = Commands.Execute(tokens[0], tokens[1..]);

            foreach (var line in result.Lines)
                AppendLine(line, result.IsError);

            ScrollToBottom();
            m_InputField.Focus();
        }

        // ── History navigation ────────────────────────────────────────────────────

        void NavigateHistory(int dir)
        {
            if (m_History.Count == 0) return;
            m_HistoryIndex = Mathf.Clamp(m_HistoryIndex + dir, -1, m_History.Count - 1);
            m_InputField.SetValueWithoutNotify(m_HistoryIndex >= 0 ? m_History[m_HistoryIndex] : string.Empty);
            MoveCaretToEnd();
        }

        // ── Autocomplete ──────────────────────────────────────────────────────────

        void Autocomplete()
        {
            var input = m_InputField.value.Trim();
            if (string.IsNullOrEmpty(input)) return;

            var match = Commands.Autocomplete(input);
            if (match != null)
            {
                m_InputField.value = match + " ";
                MoveCaretToEnd();
            }
        }

        // ── Output helpers ────────────────────────────────────────────────────────

        public void AppendLine(string text, bool isError = false)
        {
            var label = new Label(text);
            label.AddToClassList("terminal-line");
            if (isError) label.AddToClassList("error");
            m_OutputScroll.Add(label);
        }

        void ScrollToBottom()
        {
            // Schedule for after layout so the scroll position is current
            m_OutputScroll.schedule.Execute(() =>
                m_OutputScroll.scrollOffset = new Vector2(0, float.MaxValue)).StartingIn(0);
        }

        void MoveCaretToEnd()
        {
            int len = m_InputField.value.Length;
            m_InputField.cursorIndex  = len;
            m_InputField.selectIndex  = len;
        }

        void PrintSplash()
        {
            AppendLine($"{VERSION}  |  {HINT}");
            AppendLine(DIVIDER);
        }
    }
}
