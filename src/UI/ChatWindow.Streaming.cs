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
            _sendButton.interactable = false;

            var thinkingObj = CreateStatusLabel("Thinking...");
            _coroutineHost.StartCoroutine(ScrollToBottom());

            int round = 0;
            const int maxRounds = 5;
            bool needsMoreRounds = true;

            _scrollPending = true;
            string latestText = null;
            GameObject bubbleRow = null;
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
                        // On first visible token, replace "Thinking..." with a real bubble.
                        // Skip whitespace-only content (some models stream leading newlines).
                        if (bubbleRow == null)
                        {
                            if (accumulated.Trim().Length == 0)
                                return;
                            Object.Destroy(thinkingObj);
                            thinkingObj = null;
                            var msg = new ChatMessage(MessageSender.AI, accumulated.TrimStart());
                            bubbleRow = ChatBubbleFactory.CreateBubble(msg, _contentTransform);
                            messageText = ChatBubbleFactory.GetMessageText(bubbleRow);
                            if (messageText != null)
                            {
                                var textLayout = messageText.GetComponent<LayoutElement>();
                                if (textLayout != null)
                                    textLayout.preferredWidth = UIStyleConstants.Scaled(
                                        UIStyleConstants.WindowWidth * UIStyleConstants.BubbleMaxWidthRatio)
                                        - UIStyleConstants.ScaledInt(UIStyleConstants.BubblePadding) * 2;
                            }
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
                        // On error with no tokens yet, replace "Thinking..." with error in bubble
                        if (bubbleRow == null)
                        {
                            Object.Destroy(thinkingObj);
                            thinkingObj = null;
                            var msg = new ChatMessage(MessageSender.AI, error);
                            bubbleRow = ChatBubbleFactory.CreateBubble(msg, _contentTransform);
                            messageText = ChatBubbleFactory.GetMessageText(bubbleRow);
                        }
                        latestText = error;
                    }
                );

                if (pendingToolCalls != null)
                {
                    // Check if LLM streamed visible content before the tool calls
                    string contentBeforeTools = latestText != null ? latestText.Trim() : null;
                    bool hasVisibleContent = !string.IsNullOrEmpty(contentBeforeTools);

                    // Add assistant tool-call message to history (with content if any)
                    _conversationHistory.Add(ChatMessage.CreateAssistantToolCall(
                        pendingToolCalls, hasVisibleContent ? contentBeforeTools : null));

                    if (bubbleRow != null)
                    {
                        if (hasVisibleContent)
                        {
                            // Keep the bubble — update with trimmed text and reset for next round
                            messageText.text = contentBeforeTools;
                            LayoutRebuilder.MarkLayoutForRebuild(_contentRectTransform);
                            bubbleRow = null;
                            messageText = null;
                            latestText = null;
                        }
                        else
                        {
                            // Whitespace-only — destroy the premature bubble
                            Object.Destroy(bubbleRow);
                            bubbleRow = null;
                            messageText = null;
                            latestText = null;
                        }
                    }

                    // Destroy thinking label — we'll show per-tool status instead
                    if (thinkingObj != null)
                    {
                        Object.Destroy(thinkingObj);
                        thinkingObj = null;
                    }

                    // Execute each tool: show per-tool status label
                    foreach (var tc in pendingToolCalls)
                    {
                        var statusObj = CreateStatusLabel(ToolDefinitions.GetToolStatusLabel(tc.FunctionName));
                        _coroutineHost.StartCoroutine(ScrollToBottom());

                        string result = ToolDefinitions.ExecuteTool(tc.FunctionName, tc.Arguments);
                        _conversationHistory.Add(ChatMessage.CreateToolResult(tc.Id, result));

                        Object.Destroy(statusObj);
                    }

                    // Wait one frame for cleanup
                    yield return null;

                    // Show thinking label for next LLM round
                    thinkingObj = CreateStatusLabel("Thinking...");
                    needsMoreRounds = true;
                }
            }

            // Final UI update with complete text
            _scrollPending = false;
            // Clean up thinking label if still present
            if (thinkingObj != null)
                Object.Destroy(thinkingObj);

            // If no bubble was created yet (e.g. non-streamed response after tool calls), create one now
            if (bubbleRow == null && latestText != null)
            {
                var msg = new ChatMessage(MessageSender.AI, latestText);
                bubbleRow = ChatBubbleFactory.CreateBubble(msg, _contentTransform);
                messageText = ChatBubbleFactory.GetMessageText(bubbleRow);
            }
            else if (messageText != null && latestText != null)
            {
                messageText.text = latestText;
                LayoutRebuilder.MarkLayoutForRebuild(_contentRectTransform);
            }

            _isStreaming = false;
            _sendButton.interactable = true;
            _coroutineHost.StartCoroutine(ScrollToBottom());
        }

        /// <summary>
        /// Throttled UI update loop during streaming: updates bubble text and scrolls at ~10fps.
        /// </summary>
        private IEnumerator StreamingUiLoop(System.Func<Text> getMessageText, System.Func<string> getLatest, System.Func<bool> isActive)
        {
            string displayed = null;
            var wait = new WaitForSeconds(0.1f);
            while (isActive())
            {
                var mt = getMessageText();
                string latest = getLatest();
                if (latest != null && latest != displayed && mt != null)
                {
                    mt.text = latest;
                    displayed = latest;
                    LayoutRebuilder.MarkLayoutForRebuild(_contentRectTransform);
                }
                if (_autoScroll && _scrollRect != null)
                    _scrollRect.verticalNormalizedPosition = 0f;
                yield return wait;
            }
        }
    }
}
