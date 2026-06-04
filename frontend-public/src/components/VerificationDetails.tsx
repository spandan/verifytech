"use client";

import { Collapsible } from "@/components/Collapsible";

interface Change {
  field: string;
  certified_value: unknown;
  live_value: unknown;
}

interface Props {
  identityScore: number;
  valueScore: number;
  changes: Change[];
}

export function VerificationDetails({ identityScore, valueScore, changes }: Props) {
  return (
    <Collapsible title="Technical details">
      <div className="space-y-4">
        {(identityScore > 0 || valueScore > 0) && (
          <div className="grid grid-cols-2 gap-3">
            <div className="metric-cell text-center">
              <p className="metric-cell__label">Identity match</p>
              <p className="stat-card__value text-xl">{Math.round(identityScore * 100)}%</p>
            </div>
            <div className="metric-cell text-center">
              <p className="metric-cell__label">Condition match</p>
              <p className="stat-card__value text-xl">{Math.round(valueScore * 100)}%</p>
            </div>
          </div>
        )}

        {changes.length > 0 && (
          <div>
            <p className="mb-2 text-sm font-medium text-[var(--color-text-primary)]">
              Detected differences
            </p>
            <ul className="space-y-2">
              {changes.map((c, i) => (
                <li key={i} className="text-sm">
                  <span className="font-medium">{c.field}</span>
                  <span className="text-muted"> — </span>
                  {String(c.certified_value)} → {String(c.live_value)}
                </li>
              ))}
            </ul>
          </div>
        )}
      </div>
    </Collapsible>
  );
}
