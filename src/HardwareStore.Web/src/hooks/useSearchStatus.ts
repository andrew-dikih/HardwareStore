import { useEffect, useRef, useState } from 'react';
import * as signalR from '@microsoft/signalr';
import type { SearchStatus } from '../types';
import { getSearchStatus } from '../api';

const RECONNECT_DELAY_MS = 5000;
const CONNECTION_ERROR_MSG = 'Failed to connect for real-time updates.';

interface UseSearchStatusOptions {
  searchRequestId: string | undefined;
  isPublic?: boolean;
  onCompleted?: (reportId: string) => void;
  onFailed?: (errorMessage?: string) => void;
}

export function useSearchStatus({
  searchRequestId,
  isPublic = false,
  onCompleted,
  onFailed,
}: UseSearchStatusOptions) {
  const [status, setStatus] = useState<SearchStatus | null>(null);
  const [error, setError] = useState('');
  const connectionRef = useRef<signalR.HubConnection | null>(null);

  useEffect(() => {
    if (!searchRequestId || isPublic) return;

    // Seed the current status immediately via HTTP so the UI is not empty while
    // the SignalR connection is being established.
    getSearchStatus(searchRequestId)
      .then((res) => {
        setStatus(res.data);
        if (res.data.status === 'Completed' && res.data.reportId) {
          onCompleted?.(res.data.reportId);
        } else if (res.data.status === 'Failed') {
          onFailed?.(res.data.errorMessage);
        }
      })
      .catch((err) => console.warn('Initial status fetch failed, relying on SignalR:', err));

    const token = localStorage.getItem('token');

    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/search-status', {
        accessTokenFactory: () => token ?? '',
      })
      .withAutomaticReconnect()
      .build();

    connectionRef.current = connection;
    let cancelled = false;

    connection.on('SearchStatusChanged', (update: Partial<SearchStatus>) => {
      setStatus((prev) => ({ ...(prev ?? ({} as SearchStatus)), ...update }));

      if (update.status === 'Completed' && update.reportId) {
        onCompleted?.(update.reportId);
      }
      if (update.status === 'Failed') {
        onFailed?.(update.errorMessage);
      }
    });

    // After a successful reconnect, re-join the search group so we keep
    // receiving updates for this search request.
    connection.onreconnected(() => {
      connection
        .invoke('JoinSearch', searchRequestId)
        .catch((err) => {
          console.warn('Failed to rejoin search group after reconnect:', err);
          setError(CONNECTION_ERROR_MSG);
        });
    });

    // withAutomaticReconnect() gives up after its built-in retry schedule
    // (0 / 2 / 10 / 30 s). When it does, onclose fires. We schedule a manual
    // restart so that an API restart (e.g. after a deploy) is handled
    // transparently without requiring a page reload.
    connection.onclose((err) => {
      if (cancelled) return;
      if (err) console.warn('SignalR connection closed with error:', err);

      const attemptRestart = () => {
        if (cancelled) return;
        connection
          .start()
          .then(() => {
            setError('');
            return connection.invoke('JoinSearch', searchRequestId);
          })
          .catch((restartErr) => {
            console.warn('SignalR restart attempt failed:', restartErr);
            if (!cancelled) setTimeout(attemptRestart, RECONNECT_DELAY_MS);
          });
      };

      setTimeout(attemptRestart, RECONNECT_DELAY_MS);
    });

    connection
      .start()
      .then(() => connection.invoke('JoinSearch', searchRequestId))
      .catch(() => setError(CONNECTION_ERROR_MSG));

    return () => {
      cancelled = true;
      connection.stop();
    };
  }, [searchRequestId, isPublic, onCompleted, onFailed]);

  return { status, error };
}
