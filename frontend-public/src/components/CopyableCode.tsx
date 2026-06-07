"use client";

import { CopyButton } from "@/components/CopyButton";

type Props = {
  value: string;
  className?: string;
  monoClassName?: string;
  copyLabel?: string;
};

export function CopyableCode({
  value,
  className = "",
  monoClassName = "font-mono",
  copyLabel = "Copy code",
}: Props) {
  return (
    <span className={`copyable-code ${className}`.trim()}>
      <span className={monoClassName}>{value}</span>
      <CopyButton text={value} label={copyLabel} />
    </span>
  );
}
