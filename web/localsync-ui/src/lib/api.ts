export interface Peer {
  id: string;
  alias: string;
  displayId: string;
  mnemonic: string;
  address: string;
  port: number;
  deviceType: string;
  /** "LocalSync" or "LocalSendCompat" — drives the trust badge. */
  protocol: string;
  lastSeen: string;
}

export type TransferState =
  | 'Pending'
  | 'Active'
  | 'Paused'
  | 'Verifying'
  | 'Completed'
  | 'Failed'
  | 'Cancelled';

export interface Transfer {
  id: string;
  fileName: string;
  totalSize: number;
  transferredSize: number;
  /** A name, never an ordinal: adding a state must not silently shift meaning. */
  state: TransferState;
  targetDeviceId: string | null;
  createdAt: string;
  completedAt: string | null;
  errorMessage: string | null;
}

export interface DaemonInfo {
  deviceId: string;
  displayId: string;
  mnemonic: string;
  alias: string;
  version: string;
  receiveDirectory: string;
}

const BASE = '/api/localsync/v1';

export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

async function json<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${BASE}${path}`, init);
  if (!response.ok) {
    throw new ApiError(`${init?.method ?? 'GET'} ${path} failed`, response.status);
  }
  return (await response.json()) as T;
}

export const api = {
  peers: () => json<Peer[]>('/peers'),
  transfers: () => json<Transfer[]>('/transfers'),
  info: () => json<DaemonInfo>('/info'),
};

export const CHUNK_SIZE = 1024 * 1024;

export interface UploadHandlers {
  onProgress?: (sent: number, total: number) => void;
  signal?: AbortSignal;
}

/**
 * Uploads a file to the local daemon, which relays it to the peer.
 *
 * Chunks are addressed by absolute offset, so a retry re-sends the same range
 * rather than appending — the receiver treats redelivery as a no-op.
 */
export async function uploadFile(
  peerId: string,
  file: File,
  handlers: UploadHandlers = {},
): Promise<void> {
  const session = await json<{ id: string }>('/session', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      fileName: file.name,
      totalSize: file.size,
      targetDeviceId: peerId,
      lastModified: new Date(file.lastModified).toISOString(),
    }),
  });

  let offset = 0;
  while (offset < file.size) {
    const end = Math.min(offset + CHUNK_SIZE, file.size);
    const response = await fetch(`${BASE}/session/${session.id}/chunk?offset=${offset}`, {
      method: 'PUT',
      body: file.slice(offset, end),
      ...(handlers.signal ? { signal: handlers.signal } : {}),
    });

    // Checking response.ok is what turns a silent server-side rejection into a
    // visible failure; the previous uploader ignored the status entirely.
    if (!response.ok) {
      throw new ApiError(`Chunk at offset ${offset} rejected`, response.status);
    }

    offset = end;
    handlers.onProgress?.(offset, file.size);
  }
}
