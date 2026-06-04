"use client";

import { useState } from "react";

export function ShareButton({ url }: { url: string }) {
  const [copied, setCopied] = useState(false);

  async function copy() {
    await navigator.clipboard.writeText(url);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }

  return (
    <button type="button" onClick={copy} className="btn btn-secondary">
      {copied ? "Link copied" : "Copy link"}
    </button>
  );
}
