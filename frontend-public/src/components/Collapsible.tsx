"use client";

import { useState } from "react";

interface Props {
  children: React.ReactNode;
  title: string;
  defaultOpen?: boolean;
}

export function Collapsible({ children, title, defaultOpen = false }: Props) {
  const [open, setOpen] = useState(defaultOpen);

  return (
    <div className="collapsible">
      <button
        type="button"
        className="collapsible__trigger"
        onClick={() => setOpen(!open)}
        aria-expanded={open}
      >
        <span>{title}</span>
        <span className="text-muted text-xs">{open ? "Hide" : "Show"}</span>
      </button>
      {open && <div className="collapsible__content">{children}</div>}
    </div>
  );
}
