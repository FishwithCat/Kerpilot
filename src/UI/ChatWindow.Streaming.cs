using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kerpilot
{
    public partial class ChatWindow
    {
        private IEnumerator StreamLlmResponse()
        {
            _isStreaming = true;
            _streamingCancelled = false;
            _inputField.interactable = false;

            StartThinkingAnimation();

            int round = 0;
            const int maxRounds = 5;
            bool needsMoreRounds = true;

            _scrollPending = true;
            string latestText = null;
            bool hasStreamLine = false;
            // Length of _logBuilder before streaming (without the "Thinking..." line)
            int logSnapshotLength = -1;

            _coroutineHost.StartCoroutine(StreamingUiLoop(() => latestText, () => _scrollPending));

            while (needsMoreRounds && round < maxRounds)
            {
                round++;
                needsMoreRounds = false;
                List<ToolCall> pendingToolCalls = null;

                yield return LlmClient.SendChatRequest(
                    _conversationHistory,
                    _settings,
                    onToken: (accumulated) =>
                    {
                        if (!hasStreamLine)
                        {
                            if (accumulated.Trim().Length == 0)
                                return;
                            RemoveLastLogLine();
                            logSnapshotLength = _logBuilder.Length;
                            hasStreamLine = true;
                        }
                        latestText = accumulated;
                    },
                    onComplete: (text) =>
                    {
                        _conversationHistory.Add(new ChatMessage(MessageSender.AI, text));
                        latestText = text;
                    },
                    onToolCalls: (toolCalls) =>
                    {
                        pendingToolCalls = toolCalls;
                    },
                    onError: (error) =>
                    {
                        if (!hasStreamLine)
                        {
                            RemoveLastLogLine();
                            logSnapshotLength = _logBuilder.Length;
                            hasStreamLine = true;
                        }
                        latestText = error;
                    },
                    isCancelled: () => _streamingCancelled
                );

                if (pendingToolCalls != null)
                {
                    string contentBeforeTools = latestText != null ? latestText.Trim() : null;
                    bool hasVisibleContent = !string.IsNullOrEmpty(contentBeforeTools);

                    _conversationHistory.Add(ChatMessage.CreateAssistantToolCall(
                        pendingToolCalls, hasVisibleContent ? contentBeforeTools : null));

                    if (hasStreamLine)
                    {
                        _logBuilder.Length = logSnapshotLength;
                        if (hasVisibleContent)
                            AppendToLog(FormatAiLine(contentBeforeTools));
                        FlushLog();
                    }

                    hasStreamLine = false;
                    latestText = null;
                    logSnapshotLength = -1;

                    foreach (var tc in pendingToolCalls)
                    {
                        AppendToLog(FormatToolLine(ToolDefinitions.GetToolStatusLabel(tc.FunctionName)));
                        FlushLog();
                        _coroutineHost.StartCoroutine(ScrollToBottom());

                        string result = ToolDefinitions.ExecuteTool(tc.FunctionName, tc.Arguments);
                        _conversationHistory.Add(ChatMessage.CreateToolResult(tc.Id, result));

                        RemoveLastLogLine();
                    }

                    yield return null;

                    StartThinkingAnimation();
                    needsMoreRounds = true;
                }
            }

            _scrollPending = false;

            if (hasStreamLine && latestText != null)
            {
                _logBuilder.Length = logSnapshotLength;
                AppendToLog(FormatAiLine(latestText));
            }
            else if (!hasStreamLine)
            {
                RemoveLastLogLine();
                if (latestText != null)
                    AppendToLog(FormatAiLine(latestText));
            }

            FlushLog();
            _isStreaming = false;
            _streamingCoroutine = null;
            _inputField.interactable = true;
            _coroutineHost.StartCoroutine(ScrollToBottom());
        }

        private void RemoveLastLogLine()
        {
            StopThinkingAnimation();
            int len = _logBuilder.Length;
            for (int i = len - 1; i >= 0; i--)
            {
                if (_logBuilder[i] == '\n')
                {
                    _logBuilder.Length = i;
                    return;
                }
            }
            _logBuilder.Clear();
        }

        private void StartThinkingAnimation()
        {
            StopThinkingAnimation();
            string logBase = _logBuilder.ToString();
            AppendToLog(FormatToolLine("Thinking..."));
            FlushLog();
            _coroutineHost.StartCoroutine(ScrollToBottom());
            _thinkingAnim = _coroutineHost.StartCoroutine(AnimateThinking(logBase));
        }

        private void StopThinkingAnimation()
        {
            if (_thinkingAnim != null)
            {
                _coroutineHost.StopCoroutine(_thinkingAnim);
                _thinkingAnim = null;
            }
        }

        private IEnumerator AnimateThinking(string logBase)
        {
            string[] frames = new[]
            {
                FormatToolLine("Thinking."),
                FormatToolLine("Thinking.."),
                FormatToolLine("Thinking...")
            };
            int index = 0;
            var wait = new WaitForSecondsRealtime(0.4f);
            string prefix = logBase.Length > 0 ? logBase + "\n" : "";

            while (true)
            {
                yield return wait;
                index = (index + 1) % frames.Length;
                if (_logText != null)
                    _logText.text = prefix + frames[index];
            }
        }

        private IEnumerator StreamingUiLoop(System.Func<string> getLatest, System.Func<bool> isActive)
        {
            int revealedCount = 0;
            float charAccum = 0f;
            float cursorTimer = 0f;
            bool cursorVisible = true;
            const float cursorBlinkInterval = 0.5f;
            const string cursor = "\u2588";
            const float uiUpdateInterval = 0.1f; // ~10fps throttle
            float timeSinceUpdate = uiUpdateInterval; // force first update immediately
            string cachedLogBase = null;
            int cachedLogLength = -1;
            int lastRevealedCount = -1;
            bool lastCursorVisible = true;

            while (isActive())
            {
                float dt = Time.unscaledDeltaTime;
                timeSinceUpdate += dt;
                string target = getLatest();

                if (target != null && _logText != null)
                {
                    int targetLen = target.Length;
                    int backlog = targetLen - revealedCount;

                    if (backlog > 0)
                    {
                        float speed = UIStyleConstants.TypingCharsPerSecond;
                        if (backlog > UIStyleConstants.TypingCatchUpThreshold)
                            speed *= UIStyleConstants.TypingCatchUpMultiplier;

                        charAccum += speed * dt;
                        int chars = (int)charAccum;
                        if (chars > 0)
                        {
                            charAccum -= chars;
                            revealedCount = Mathf.Min(revealedCount + chars, targetLen);
                        }

                        cursorVisible = true;
                        cursorTimer = 0f;
                    }
                    else
                    {
                        cursorTimer += dt;
                        if (cursorTimer >= cursorBlinkInterval)
                        {
                            cursorTimer -= cursorBlinkInterval;
                            cursorVisible = !cursorVisible;
                        }
                    }

                    bool needsUpdate = timeSinceUpdate >= uiUpdateInterval
                        && (revealedCount != lastRevealedCount || cursorVisible != lastCursorVisible);

                    if (needsUpdate)
                    {
                        timeSinceUpdate = 0f;
                        lastRevealedCount = revealedCount;
                        lastCursorVisible = cursorVisible;

                        // Cache logBase — only rebuild when _logBuilder changes
                        int currentLogLen = _logBuilder.Length;
                        if (cachedLogLength != currentLogLen)
                        {
                            cachedLogBase = currentLogLen > 0
                                ? _logBuilder.ToString() + "\n"
                                : "";
                            cachedLogLength = currentLogLen;
                        }

                        string visible = target.Substring(0, revealedCount);
                        string cursorStr = cursorVisible ? cursor : "";
                        _logText.text = cachedLogBase + FormatAiLine(visible + cursorStr);

                        LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRectTransform);
                        if (_scrollRect != null)
                            _scrollRect.verticalNormalizedPosition = 0f;
                    }
                }

                yield return null;
            }

            if (_logText != null)
            {
                FlushLog();
                LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRectTransform);
                if (_scrollRect != null)
                    _scrollRect.verticalNormalizedPosition = 0f;
            }
        }
    }
}
