import { scoreToCondition } from "@/lib/certification-summary";

function conditionClass(condition: string): string {
  switch (condition) {
    case "Excellent":
      return "condition-badge condition-badge--excellent";
    case "Very Good":
      return "condition-badge condition-badge--very-good";
    case "Good":
      return "condition-badge condition-badge--good";
    case "Fair":
      return "condition-badge condition-badge--fair";
    default:
      return "condition-badge condition-badge--attention";
  }
}

export function ConditionBadge({ score, label }: { score: number; label?: string }) {
  const condition = label ?? scoreToCondition(score);
  return <span className={conditionClass(condition)}>{condition}</span>;
}

export function ScoreDisplay({ score }: { score: number }) {
  return (
    <div className="score-display">
      <p className="score-display__value">{score}/100</p>
      <p className="score-display__label">Overall score</p>
    </div>
  );
}
