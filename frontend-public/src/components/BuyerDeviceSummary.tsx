import type { CertificationSummary } from "@/lib/certification-summary";

import { ConditionBadge, ScoreDisplay } from "@/components/ConditionBadge";
import { CopyableCode } from "@/components/CopyableCode";

export function BuyerDeviceSummary({ summary }: { summary: CertificationSummary }) {
  return (
    <section className="card card-body buyer-summary">
      <div className="buyer-summary__header">
        <div>
          <p className="text-sm font-medium text-secondary">Device Summary</p>
          <h2 className="mt-1 text-xl font-semibold">
            {summary.manufacturer} {summary.model}
          </h2>
        </div>
        <ConditionBadge score={summary.overallScore} label={summary.condition} />
      </div>

      <div className="buyer-summary__grid">
        <SummaryItem label="CPU" value={summary.cpu} />
        <SummaryItem label="RAM" value={summary.ramGb > 0 ? `${summary.ramGb} GB` : "See report"} />
        <SummaryItem
          label="Storage"
          value={
            summary.storageGb > 0
              ? `${summary.storageGb} GB ${summary.storageType}`
              : summary.storageType
          }
        />
        <SummaryItem
          label="Battery Health"
          value={
            summary.batteryHealthPercent != null
              ? `${Math.round(summary.batteryHealthPercent)}%`
              : "Not reported"
          }
        />
        <SummaryItem
          label="Certification Date"
          value={new Date(summary.certificationDate).toLocaleDateString()}
        />
        <SummaryItem label="Certificate ID" value={summary.certificateId} mono copyable />
      </div>

      <div className="buyer-summary__score">
        <ScoreDisplay score={summary.overallScore} />
      </div>
    </section>
  );
}

function SummaryItem({
  label,
  value,
  mono,
  copyable,
}: {
  label: string;
  value: string;
  mono?: boolean;
  copyable?: boolean;
}) {
  return (
    <div className="metric-cell">
      <p className="metric-cell__label">{label}</p>
      {copyable ? (
        <CopyableCode
          value={value}
          monoClassName={`metric-cell__value ${mono ? "metric-cell__value--mono" : ""}`}
          copyLabel={`Copy ${label.toLowerCase()}`}
        />
      ) : (
        <p className={`metric-cell__value ${mono ? "metric-cell__value--mono" : ""}`}>{value}</p>
      )}
    </div>
  );
}
