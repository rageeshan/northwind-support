import type { TriageResult } from '../../../types/api';

interface TriageResultBannerProps {
  result: TriageResult;
  onDismiss: () => void;
}

/**
 * A dismissible banner shown immediately after ticket creation, explaining the triage decisions
 * the system made (priority, SLA window, agent assignment).
 */
export function TriageResultBanner({ result, onDismiss }: TriageResultBannerProps) {
  return (
    <div className="triage-banner" role="status" aria-live="polite">
      <div className="triage-banner__header">
        <span className="triage-banner__icon">🎯</span>
        <strong>Triage complete</strong>
        <button
          type="button"
          className="triage-banner__dismiss"
          aria-label="Dismiss triage result"
          onClick={onDismiss}
        >
          ✕
        </button>
      </div>

      <dl className="triage-banner__decisions">
        <div>
          <dt>Priority</dt>
          <dd>
            <span className={`priority-chip priority-chip--${result.priority.toLowerCase()}`}>
              {result.priority}
            </span>
            <span className="triage-banner__reason">{result.priorityReason}</span>
          </dd>
        </div>

        <div>
          <dt>SLA window</dt>
          <dd>
            <strong>{result.slaWindowHours.toFixed(1)} hours</strong>
            <span className="triage-banner__reason">{result.slaWindowReason}</span>
          </dd>
        </div>

        <div>
          <dt>Agent</dt>
          <dd>
            <span className="triage-banner__reason">{result.assignmentReason}</span>
          </dd>
        </div>
      </dl>
    </div>
  );
}
