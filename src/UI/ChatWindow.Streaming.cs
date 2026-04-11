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
            _inputField.interactable = false;

            var thinkingObj = CreateStatusLabel("Thinking...");
            _coroutineHost.StartCoroutine(ScrollToBottom());

            int round = 0;
            const int maxRounds = 5;
            bool needsMoreRounds = true;

            _scrollPending = true;
            string latestText = null;
            GameObject lineRow = null;
            Text messageText = null;

            // Single UI loop coroutine shared across all tool-call rounds
            _coroutineHost.StartCoroutine(StreamingUiLoop(() => messageText, () => latestText, () => _scrollPending));

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
                        if (lineRow == null)
                        {
                            if (accumulated.Trim().Length == 0)
                                return;
                            Object.Destroy(thinkingObj);
                            thinkingObj = null;
                            var msg = new ChatMessage(MessageSender.AI, accumulated.TrimStart());
                            lineRow = ChatBubbleFactory.CreateMessageLine(msg, _contentTransform);
                            InsertMessageLine(lineRow);
                            messageText = ChatBubbleFactory.GetMessageText(lineRow);
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
                        if (lineRow == null)
                        {
                            Object.Destroy(thinkingObj);
                            thinkingObj = null;
                            var msg = new ChatMessage(MessageSender.AI, error);
                            lineRow = ChatBubbleFactory.CreateMessageLine(msg, _contentTransform);
                            InsertMessageLine(lineRow);
                            messageText = ChatBubbleFactory.GetMessageText(lineRow);
                        }
                        latestText = error;
                    }
                );

                if (pendingToolCalls != null)
                {
                    string contentBeforeTools = latestText != null ? latestText.Trim() : null;
                    bool hasVisibleContent = !string.IsNullOrEmpty(contentBeforeTools);

                    _conversationHistory.Add(ChatMessage.CreateAssistantToolCall(
                        pendingToolCalls, hasVisibleContent ? contentBeforeTools : null));

                    if (lineRow != null)
                    {
                        if (hasVisibleContent)
                        {
                            messageText.text = contentBeforeTools;
                            LayoutRebuilder.MarkLayoutForRebuild(_contentRectTransform);
                            lineRow = null;
                            messageText = null;
                            latestText = null;
                        }
                        else
                        {
                            Object.Destroy(lineRow);
                            lineRow = null;
                            messageText = null;
                            latestText = null;
                        }
                    }

                    if (thinkingObj != null)
                    {
                        Object.Destroy(thinkingObj);
                        thinkingObj = null;
                    }

                    foreach (var tc in pendingToolCalls)
                    {
                        var statusObj = CreateStatusLabel(ToolDefinitions.GetToolStatusLabel(tc.FunctionName));
                        _coroutineHost.StartCoroutine(ScrollToBottom());

                        string result = ToolDefinitions.ExecuteTool(tc.FunctionName, tc.Arguments);
                        _conversationHistory.Add(ChatMessage.CreateToolResult(tc.Id, result));

                        Object.Destroy(statusObj);
                    }

                    yield return null;

                    thinkingObj = CreateStatusLabel("Thinking...");
                    needsMoreRounds = true;
                }
            }

            _scrollPending = false;
            if (thinkingObj != null)
                Object.Destroy(thinkingObj);

            if (lineRow == null && latestText != null)
            {
                var msg = new ChatMessage(MessageSender.AI, latestText);
                lineRow = ChatBubbleFactory.CreateMessageLine(msg, _contentTransform);
                InsertMessageLine(lineRow);
                messageText = ChatBubbleFactory.GetMessageText(lineRow);
            }
            else if (messageText != null && latestText != null)
            {
                messageText.text = latestText;
                LayoutRebuilder.MarkLayoutForRebuild(_contentRectTransform);
            }

            _isStreaming = false;
            _inputField.interactable = true;
            _coroutineHost.StartCoroutine(ScrollToBottom());
        }

        /// <summary>
        /// Typewriter animation loop: reveals characters one by one with a blinking cursor.
        /// Accelerates when the LLM produces text faster than the base typing speed.
        /// </summary>
        private IEnumerator StreamingUiLoop(System.Func<Text> getMessageText, System.Func<string> getLatest, System.Func<bool> isActive)
        {
            int revealedCount = 0;
            float charAccum = 0f;
            float cursorTimer = 0f;
            bool cursorVisible = true;
            const float cursorBlinkInterval = 0.5f;
            const string cursor = "\u2588"; // full block cursor

            while (isActive())
            {
                float dt = Time.unscaledDeltaTime;
                var mt = getMessageText();
                string target = getLatest();

                if (target != null && mt != null)
                {
                    int targetLen = target.Length;
                    int backlog = targetLen - revealedCount;

                    if (backlog > 0)
                    {
                        // Accelerate when backlog grows to keep up with fast LLM output
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

                        // Reset blink to visible while actively typing
                        cursorVisible = true;
                        cursorTimer = 0f;
                    }
                    else
                    {
                        // Blink cursor when caught up, waiting for more tokens
                        cursorTimer += dt;
                        if (cursorTimer >= cursorBlinkInterval)
                        {
                            cursorTimer -= cursorBlinkInterval;
                            cursorVisible = !cursorVisible;
                        }
                    }

                    string visible = target.Substring(0, revealedCount);
                    string display = cursorVisible ? visible + cursor : visible;
                    mt.text = display;

                    LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRectTransform);
                    if (_scrollRect != null)
                        _scrollRect.verticalNormalizedPosition = 0f;
                }

                yield return null;
            }

            // Streaming ended — flush remaining text and remove cursor
            FlushTypewriter(getMessageText(), getLatest());
        }

        /// <summary>
        /// Shows the full final text without the cursor character.
        /// </summary>
        private void FlushTypewriter(Text mt, string final)
        {
            if (mt != null && final != null)
            {
                mt.text = final;
                LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRectTransform);
                if (_scrollRect != null)
                    _scrollRect.verticalNormalizedPosition = 0f;
            }
        }
    }
}
