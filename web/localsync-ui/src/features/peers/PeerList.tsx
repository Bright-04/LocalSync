import {
  AlertCircle,
  Loader2,
  Network,
  RefreshCcw,
  Server,
  ShieldAlert,
  ShieldCheck,
  UploadCloud,
} from 'lucide-react';
import { useRef, useState } from 'react';
import type { Peer } from '../../lib/api';
import { uploadFile } from '../../lib/api';

interface Props {
  peers: Peer[];
  loading: boolean;
  error: string | null;
  onRefresh: () => void;
}

export function PeerList({ peers, loading, error, onRefresh }: Props) {
  const [sending, setSending] = useState<Record<string, boolean>>({});
  const [failures, setFailures] = useState<Record<string, string>>({});
  const inputs = useRef<Record<string, HTMLInputElement | null>>({});

  const send = async (peer: Peer, file: File) => {
    setSending((s) => ({ ...s, [peer.id]: true }));
    setFailures((f) => ({ ...f, [peer.id]: '' }));
    try {
      await uploadFile(peer.id, file);
    } catch (err) {
      // Surfaced in the card rather than swallowed into console.error, which
      // is what the previous uploader did.
      setFailures((f) => ({
        ...f,
        [peer.id]: err instanceof Error ? err.message : 'Upload failed',
      }));
    } finally {
      setSending((s) => ({ ...s, [peer.id]: false }));
    }
  };

  if (loading) {
    return (
      <div className="flex flex-col items-center justify-center p-16 space-y-4">
        <Loader2 className="w-10 h-10 text-indigo-500 animate-spin" />
        <p className="text-slate-400 font-medium">Scanning the local network…</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="p-6 bg-rose-500/10 border border-rose-500/20 rounded-2xl flex items-center gap-4 text-rose-400">
        <AlertCircle className="w-8 h-8 shrink-0" />
        <div>
          <h3 className="font-semibold text-lg">Cannot reach the daemon</h3>
          <p className="text-sm opacity-80">{error}</p>
        </div>
      </div>
    );
  }

  return (
    <section className="space-y-6">
      <header className="flex justify-between items-end">
        <div>
          <h2 className="text-2xl font-bold font-outfit text-white flex items-center gap-3 mb-1">
            <Network className="w-6 h-6 text-indigo-400" />
            Nearby devices
          </h2>
          <p className="text-sm text-slate-400">{peers.length} found on your network</p>
        </div>
        <button
          type="button"
          onClick={onRefresh}
          className="p-3 bg-white/5 hover:bg-white/10 border border-white/10 rounded-xl transition-all"
          aria-label="Refresh peer list"
        >
          <RefreshCcw className="w-5 h-5 text-slate-300" />
        </button>
      </header>

      {peers.length === 0 ? (
        <div className="flex flex-col items-center justify-center p-16 border border-white/10 rounded-3xl bg-gradient-to-b from-white/5 to-transparent">
          <Server className="w-14 h-14 text-slate-600 mb-5" />
          <p className="text-lg font-medium text-slate-300">Nothing here yet</p>
          <p className="text-sm text-slate-500 mt-2 text-center max-w-sm">
            Start LocalSync on another device on the same network. Discovery does not cross subnets.
          </p>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-5">
          {peers.map((peer) => {
            const compat = peer.protocol !== 'LocalSync';
            const busy = sending[peer.id] ?? false;
            const failure = failures[peer.id];

            return (
              <article
                key={peer.id}
                className={`relative border p-6 rounded-3xl transition-all overflow-hidden ${
                  compat
                    ? 'bg-amber-500/5 border-amber-500/30'
                    : 'bg-white/5 border-white/10 hover:bg-white/10'
                }`}
              >
                <div className="flex justify-between items-start mb-5">
                  <div className="p-3 rounded-2xl bg-indigo-500/10 border border-indigo-500/20 text-indigo-300">
                    <Server className="w-6 h-6" />
                  </div>
                  {/* The alias is attacker-supplied, so the trust chip is
                      always rendered adjacent to it, never on its own. */}
                  <span
                    className={`flex items-center gap-1.5 px-3 py-1.5 rounded-full text-xs font-semibold ${
                      compat
                        ? 'bg-amber-500/15 text-amber-300 border border-amber-500/30'
                        : 'bg-emerald-500/15 text-emerald-300 border border-emerald-500/30'
                    }`}
                  >
                    {compat ? (
                      <ShieldAlert className="w-3.5 h-3.5" />
                    ) : (
                      <ShieldCheck className="w-3.5 h-3.5" />
                    )}
                    {compat ? 'COMPAT' : 'LocalSync'}
                  </span>
                </div>

                <h3 className="font-bold text-xl text-white font-outfit mb-1 break-all">
                  {peer.alias}
                </h3>
                <p className="text-sm text-slate-400 font-mono mb-1">
                  {peer.address}:{peer.port}
                </p>
                <p className="text-xs text-slate-500 font-mono mb-5 break-all">{peer.mnemonic}</p>

                {compat && (
                  <p className="text-xs text-amber-300/90 mb-4 leading-relaxed">
                    This device uses LocalSend compatibility mode. Its identity cannot be verified
                    and could be impersonated by anyone on this network.
                  </p>
                )}

                <input
                  ref={(el) => {
                    inputs.current[peer.id] = el;
                  }}
                  type="file"
                  className="hidden"
                  onChange={(e) => {
                    const file = e.target.files?.[0];
                    if (file) void send(peer, file);
                    e.target.value = '';
                  }}
                />
                <button
                  type="button"
                  disabled={busy}
                  onClick={() => inputs.current[peer.id]?.click()}
                  className="w-full py-3 px-4 bg-indigo-600 hover:bg-indigo-500 disabled:opacity-50 text-white rounded-xl text-sm font-semibold transition-all flex items-center justify-center gap-2"
                >
                  {busy ? (
                    <Loader2 className="w-4 h-4 animate-spin" />
                  ) : (
                    <UploadCloud className="w-4 h-4" />
                  )}
                  {busy ? 'Sending…' : 'Send a file'}
                </button>

                {failure && <p className="text-xs text-rose-400 mt-3">{failure}</p>}
              </article>
            );
          })}
        </div>
      )}
    </section>
  );
}
