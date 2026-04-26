import { useEffect, useRef, useState } from 'react';
import * as signalR from '@microsoft/signalr';
import type { SearchStatus } from '../types';
import { getSearchStatus } from '../api';

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

    connection.on('SearchStatusChanged', (update: Partial<SearchStatus>) => {
      setStatus((prev) => ({ ...(prev ?? ({} as SearchStatus)), ...update }));

      if (update.status === 'Completed' && update.reportId) {
        onCompleted?.(update.reportId);
      }
      if (update.status === 'Failed') {
        onFailed?.(update.errorMessage);
      }
    });

    connection
      .start()
      .then(() => connection.invoke('JoinSearch', searchRequestId))
      .catch(() => setError('Failed to connect for real-time updates.'));

    return () => {
      connection.stop();
    };
  }, [searchRequestId, isPublic, onCompleted, onFailed]);

  return { status, error };
}
