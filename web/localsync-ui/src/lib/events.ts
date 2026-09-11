import type { Peer, Transfer, TransferState } from './api';

export interface ProgressUpdate {
  id: string;
  state: TransferState;
  transferredSize: number;
  totalSize: number;
}

export interface StateChange {
  id: string;
  from: TransferState;
  to: TransferState;
}

export interface EventHandlers {
  onTransferCreated?: (transfer: Transfer) => void;
  onProgress?: (updates: ProgressUpdate[]) => void;
  onStateChange?: (change: StateChange) => void;
  onPeerAppeared?: (peer: Peer) => void;
  onPeerDisappeared?: (id: string) => void;
  onConnectionChange?: (connected: boolean) => void;
}

/**
 * Subscribes to the daemon's Server-Sent Events stream.
 *
 * EventSource is native to the browser and reconnects on its own, so this
 * replaces the SignalR client and its bundle entirely.
 */
export function subscribe(handlers: EventHandlers): () => void {
  const source = new EventSource('/api/localsync/v1/events');

  const on = <T>(name: string, handle: (payload: T) => void) => {
    source.addEventListener(name, (event) => {
      try {
        handle(JSON.parse((event as MessageEvent<string>).data) as T);
      } catch {
        // A malformed frame must not tear down the whole stream.
      }
    });
  };

  source.onopen = () => handlers.onConnectionChange?.(true);
  source.onerror = () => handlers.onConnectionChange?.(false);

  on<Transfer>('transfer-created', (t) => handlers.onTransferCreated?.(t));
  on<ProgressUpdate[]>('transfer-progress', (u) => handlers.onProgress?.(u));
  on<StateChange>('transfer-state', (c) => handlers.onStateChange?.(c));
  on<Peer>('peer-appeared', (p) => handlers.onPeerAppeared?.(p));
  on<{ id: string }>('peer-disappeared', (p) => handlers.onPeerDisappeared?.(p.id));

  return () => source.close();
}

export function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  const units = ['KB', 'MB', 'GB', 'TB'];
  let value = bytes / 1024;
  let unit = 0;
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024;
    unit += 1;
  }
  return `${value.toFixed(value >= 100 ? 0 : 1)} ${units[unit]}`;
}
