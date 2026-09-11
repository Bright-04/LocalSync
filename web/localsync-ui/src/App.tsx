import { FolderSync, Wifi, WifiOff } from 'lucide-react';
import { useCallback, useEffect, useState } from 'react';
import { PeerList } from './features/peers/PeerList';
import { TransferList } from './features/transfers/TransferList';
import type { DaemonInfo, Peer, Transfer } from './lib/api';
import { api } from './lib/api';
import { subscribe } from './lib/events';

export default function App() {
  const [peers, setPeers] = useState<Peer[]>([]);
  const [transfers, setTransfers] = useState<Transfer[]>([]);
  const [info, setInfo] = useState<DaemonInfo | null>(null);
  const [connected, setConnected] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      const [p, t, i] = await Promise.all([api.peers(), api.transfers(), api.info()]);
      setPeers(p);
      setTransfers(t);
      setInfo(i);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unknown error');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    // One event stream drives every live update; there is no polling loop.
    return subscribe({
      onConnectionChange: setConnected,
      onPeerAppeared: (peer) =>
        setPeers((current) =>
          current.some((p) => p.id === peer.id) ? current : [...current, peer],
        ),
      onPeerDisappeared: (id) => setPeers((current) => current.filter((p) => p.id !== id)),
      onTransferCreated: (transfer) =>
        setTransfers((current) => [transfer, ...current.filter((t) => t.id !== transfer.id)]),
      onProgress: (updates) =>
        setTransfers((current) =>
          current.map((t) => {
            const update = updates.find((u) => u.id === t.id);
            return update
              ? { ...t, transferredSize: update.transferredSize, state: update.state }
              : t;
          }),
        ),
      onStateChange: (change) =>
        setTransfers((current) =>
          current.map((t) => (t.id === change.id ? { ...t, state: change.to } : t)),
        ),
    });
  }, []);

  return (
    <div className="min-h-screen bg-[#0B0F19] text-slate-200 font-inter overflow-x-hidden">
      <div className="fixed inset-0 pointer-events-none">
        <div className="absolute top-[-20%] left-[-10%] w-[50%] h-[50%] bg-indigo-600/20 blur-[120px] rounded-full" />
        <div className="absolute bottom-[-20%] right-[-10%] w-[50%] h-[50%] bg-cyan-600/10 blur-[120px] rounded-full" />
      </div>

      <nav className="sticky top-0 z-50 border-b border-white/10 bg-[#0B0F19]/80 backdrop-blur-xl">
        <div className="max-w-7xl mx-auto px-6 h-20 flex items-center justify-between gap-4">
          <div className="flex items-center gap-3 min-w-0">
            <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-indigo-500 to-cyan-400 flex items-center justify-center shrink-0">
              <FolderSync className="w-6 h-6 text-white" />
            </div>
            <div className="min-w-0">
              <h1 className="text-xl font-extrabold font-outfit tracking-tight text-white">
                LocalSync
              </h1>
              {info && (
                <p className="text-xs text-slate-500 font-mono truncate">
                  {info.alias} · {info.mnemonic}
                </p>
              )}
            </div>
          </div>

          <span
            className={`flex items-center gap-2 px-3 py-1.5 rounded-full text-xs font-semibold shrink-0 ${
              connected ? 'bg-emerald-500/15 text-emerald-300' : 'bg-slate-500/15 text-slate-400'
            }`}
          >
            {connected ? <Wifi className="w-3.5 h-3.5" /> : <WifiOff className="w-3.5 h-3.5" />}
            {connected ? 'Live' : 'Reconnecting'}
          </span>
        </div>
      </nav>

      <main className="relative max-w-7xl mx-auto px-6 py-12 space-y-14">
        <PeerList peers={peers} loading={loading} error={error} onRefresh={() => void load()} />
        <div className="h-px w-full bg-gradient-to-r from-transparent via-white/10 to-transparent" />
        <TransferList transfers={transfers} />
      </main>
    </div>
  );
}
