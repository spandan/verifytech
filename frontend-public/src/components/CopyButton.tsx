"use client";

import { useState } from "react";

type Props = {
  text: string;
  label?: string;
  className?: string;
  onCopied?: () => void;
};

export function CopyButton({ text, label = "Copy", className = "", onCopied }: Props) {
  const [copied, setCopied] = useState(false);

  async function handleCopy() {
    if (!text) return;
    await navigator.clipboard.writeText(text);
    setCopied(true);
    onCopied?.();
    setTimeout(() => setCopied(false), 2000);
  }

  return (
    <button
      type="button"
      onClick={() => void handleCopy()}
      className={`copy-btn ${className}`.trim()}
      aria-label={copied ? "Copied" : label}
      title={copied ? "Copied" : label}
      disabled={!text}
    >
      {copied ? (
        <svg width="16" height="16" viewBox="0 0 16 16" fill="none" aria-hidden="true">
          <path
            d="M3 8.5L6.5 12L13 4"
            stroke="currentColor"
            strokeWidth="1.75"
            strokeLinecap="round"
            strokeLinejoin="round"
          />
        </svg>
      ) : (
        <svg width="16" height="16" viewBox="0 0 16 16" fill="none" aria-hidden="true">
          <path
            d="M5.5 2.5H11.5C12.0523 2.5 12.5 2.94772 12.5 3.5V4.5H13.5C14.0523 4.5 14.5 4.94772 14.5 5.5V12.5C14.5 13.0523 14.0523 13.5 13.5 13.5H7.5C6.94772 13.5 6.5 13.0523 6.5 12.5V11.5H5.5C4.94772 11.5 4.5 11.0523 4.5 10.5V3.5C4.5 2.94772 4.94772 2.5 5.5 2.5Z"
            stroke="currentColor"
            strokeWidth="1.25"
            strokeLinejoin="round"
          />
          <path
            d="M6.5 5.5H13.5V12.5H6.5V5.5Z"
            stroke="currentColor"
            strokeWidth="1.25"
            strokeLinejoin="round"
          />
        </svg>
      )}
    </button>
  );
}
