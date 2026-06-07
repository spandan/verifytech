"use client";

import { CopyableCode } from "@/components/CopyableCode";

export function CopyableVerificationUrl({ url }: { url: string }) {
  return (
    <CopyableCode value={url} monoClassName="break-all font-mono text-sm" copyLabel="Copy verification link" />
  );
}
