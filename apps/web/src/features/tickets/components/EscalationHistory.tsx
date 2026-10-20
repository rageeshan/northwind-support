import { useEffect, useState } from 'react';
import { ticketsApi } from '../../../api/tickets';
import type { Escalation } from '../../../types/api';
import { formatDateTime, formatRelativeToNow } from '../../../lib/format';

interface EscalationHistoryProps {
  ticketId: number;
  /** Pass a new value to force a re-fetch after a successful escalation. */
  refreshKey: number;
}

/** Lists all escalation history rows for a ticket, newest first. */
export function EscalationHistory({ ticketId, refreshKey }: EscalationHistoryProps) {
  const [rows, setRows] = useState<Escalation[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string>();

  const [prevKey, setPrevKey] = useState({ ticketId, refreshKey });
  if (prevKey.ticketId !== ticketId || prevKey.refreshKey !== refreshKey) {
    setPrevKey({ ticketId, refreshKey });
    setIsLoading(true);
  }

  useEffect(() => {
    let ignore = false;
    ticketsApi
      .getEscalations(ticketId)
      .then((data) => {
        if (!ignore) {
          setRows(data);
          setError(undefined);
        }
      })
      .catch(() => {
        if (!ignore) {
          setError('Could not load escalation history.');
        }
      })
      .finally(() => {
        if (!ignore) {
          setIsLoading(false);
        }
      });

    return () => {
      ignore = true;
    };
  }, [ticketId, refreshKey]);

  if (isLoading) {
    return (
      <div className="card escalation-history">
        <h2>Escalation History</h2>
        <p className="escalation-history__empty">Loading…</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="card escalation-history">
        <h2>Escalation History</h2>
        <p className="field__error">{error}</p>
      </div>
    );
  }

  return (
    <div className="card escalation-history">
      <h2>Escalation History</h2>

      {rows.length === 0 ? (
        <p className="escalation-history__empty">No escalations yet.</p>
      ) : (
        <ol className="escalation-history__list">
          {rows.map((row) => (
            <li key={row.id} className="escalation-history__item">
              <div className="escalation-history__header">
                <span className="escalation-history__priorities">
                  <span className={`priority-chip priority-chip--${row.fromPriority.toLowerCase()}`}>
                    {row.fromPriority}
                  </span>
                  {' → '}
                  <span className={`priority-chip priority-chip--${row.toPriority.toLowerCase()}`}>
                    {row.toPriority}
                  </span>
                </span>
                <span className="escalation-history__when" title={formatDateTime(row.escalatedAtUtc)}>
                  {formatRelativeToNow(row.escalatedAtUtc)}
                </span>
              </div>

              <p className="escalation-history__reason">"{row.reason}"</p>

              <p className="escalation-history__meta">
                Escalated by <strong>{row.escalatedBy}</strong>
                {row.toAgent && (
                  <>
                    {' · '}Assigned to <strong>{row.toAgent.fullName}</strong>
                  </>
                )}
                {row.toDueAtUtc && (
                  <>
                    {' · '}New due date: <strong>{formatDateTime(row.toDueAtUtc)}</strong>
                  </>
                )}
              </p>
            </li>
          ))}
        </ol>
      )}
    </div>
  );
}
