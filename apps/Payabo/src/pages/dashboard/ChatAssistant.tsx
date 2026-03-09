import { type FormEvent, useMemo, useState } from "react";

import { MobileBottomNav } from "../../components/navigation/MobileBottomNav";

type ChatMessageVariant = "hero" | "coach" | "user" | "insight";

type ChatMessage = {
  id: string;
  variant: ChatMessageVariant;
  text: string;
  tag?: string;
};

const starterMessages: ChatMessage[] = [
  {
    id: "hero",
    variant: "hero",
    text: "Do I smell smoke? That is just your weekend budget overheating."
  },
  {
    id: "coach",
    variant: "coach",
    text: "The weekend is for rest, not for exhausting your bank account.",
    tag: "Spending alert"
  },
  {
    id: "user-1",
    variant: "user",
    text: "Got a bit carried away"
  },
  {
    id: "insight",
    variant: "insight",
    text: "GBP 120 on rides in two days? That is more than your weekly lunch budget.",
    tag: "Transport spike"
  }
];

const quickReplies = [
  "Show me this weekend's damage",
  "Build me a recovery plan",
  "How do I cut transport spend?"
];

const buildAssistantReply = (input: string): ChatMessage => {
  const normalizedInput = input.trim().toLowerCase();

  if (normalizedInput.includes("weekend") || normalizedInput.includes("damage")) {
    return {
      id: crypto.randomUUID(),
      variant: "insight",
      tag: "Weekend snapshot",
      text: "You are up 34 percent versus last weekend. Transport and takeout caused most of the wobble."
    };
  }

  if (normalizedInput.includes("transport") || normalizedInput.includes("ride") || normalizedInput.includes("uber")) {
    return {
      id: crypto.randomUUID(),
      variant: "insight",
      tag: "Transport fix",
      text: "Cap rides at GBP 45 for the week and switch two short trips to public transport. That saves about GBP 32."
    };
  }

  if (normalizedInput.includes("plan") || normalizedInput.includes("recovery")) {
    return {
      id: crypto.randomUUID(),
      variant: "insight",
      tag: "Recovery plan",
      text: "Freeze non-essential spend for 48 hours, move GBP 60 into bills, and set a daily limit of GBP 18 until Friday."
    };
  }

  return {
    id: crypto.randomUUID(),
    variant: "insight",
    tag: "Next best move",
    text: "Your essentials are covered, but your flex budget is slipping. Want me to rebalance food, rides, and fun for the week?"
  };
};

export const ChatAssistant = () => {
  const [messages, setMessages] = useState<ChatMessage[]>(starterMessages);
  const [draft, setDraft] = useState("");

  const suggestions = useMemo(() => quickReplies, []);

  const sendMessage = (value: string) => {
    const nextValue = value.trim();
    if (!nextValue) {
      return;
    }

    const userMessage: ChatMessage = {
      id: crypto.randomUUID(),
      variant: "user",
      text: nextValue
    };

    setMessages((current) => [...current, userMessage, buildAssistantReply(nextValue)]);
    setDraft("");
  };

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    sendMessage(draft);
  };

  return (
    <main className="payabo-chat-page">
      <section className="payabo-chat-shell">
        <header className="payabo-chat-header">
          <div>
            <p className="payabo-chat-header__eyebrow">Payabo chat</p>
            <h1>Money advice that talks back.</h1>
          </div>
          <div className="payabo-chat-header__actions" aria-label="Alerts and profile">
            <button type="button" className="payabo-chat-icon-button" aria-label="Notifications">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
                <path d="M12 4C8.69 4 6 6.69 6 10V13.2L4.29 16.06C4.1 16.37 4.09 16.76 4.26 17.08C4.44 17.4 4.77 17.6 5.13 17.6H18.87C19.23 17.6 19.56 17.4 19.74 17.08C19.91 16.76 19.9 16.37 19.71 16.06L18 13.2V10C18 6.69 15.31 4 12 4Z" stroke="currentColor" strokeWidth="1.7" strokeLinejoin="round" />
                <path d="M9.5 19C9.88 20.15 10.84 20.9 12 20.9C13.16 20.9 14.12 20.15 14.5 19" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" />
              </svg>
            </button>
            <button type="button" className="payabo-chat-icon-button" aria-label="Profile">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
                <circle cx="12" cy="8" r="3.25" stroke="currentColor" strokeWidth="1.7" />
                <path d="M5 19C6.54 16.56 9.07 15.1 12 15.1C14.93 15.1 17.46 16.56 19 19" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" />
              </svg>
            </button>
          </div>
        </header>

        <div className="payabo-chat-thread" aria-live="polite">
          {messages.map((message, index) => (
            <article
              key={message.id}
              className={`payabo-chat-message payabo-chat-message--${message.variant}`}
              style={{ animationDelay: `${index * 90}ms` }}
            >
              {message.tag ? <span className="payabo-chat-message__tag">{message.tag}</span> : null}
              <p>{message.text}</p>
            </article>
          ))}
        </div>

        <div className="payabo-chat-composer-wrap">
          <div className="payabo-chat-suggestions" aria-label="Suggested prompts">
            {suggestions.map((suggestion) => (
              <button key={suggestion} type="button" className="payabo-chat-suggestion" onClick={() => sendMessage(suggestion)}>
                {suggestion}
              </button>
            ))}
          </div>

          <form className="payabo-chat-composer" onSubmit={handleSubmit}>
            <input
              type="text"
              value={draft}
              onChange={(event) => setDraft(event.target.value)}
              placeholder="Ask me anything about your spending"
              aria-label="Chat message"
            />
            <button type="submit" aria-label="Send message">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
                <path d="M5 12H19" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
                <path d="M13 6L19 12L13 18" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
              </svg>
            </button>
          </form>

          <MobileBottomNav />
        </div>
      </section>
    </main>
  );
};
