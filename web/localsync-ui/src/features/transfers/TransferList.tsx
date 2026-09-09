import {
  Activity,
  AlertCircle,
  CheckCircle,
  File as FileIcon,
  Loader2,
  PauseCircle,
} from 'lucide-react';
import type { Transfer, TransferState } from '../../lib/api';
import { formatBytes } from '../../lib/events';

const TONE: Record<TransferState, { chip: string; bar: string; icon: typeof CheckCircle }> = {
  Pending: { chip: 'text-slate-400', bar: 'bg-slate-500', icon: PauseCircle },
  Active: { chip: 'text-indigo-400', bar: 'bg-indigo-500', icon: Loader2 },
  Paused: { chip: 'text-amber-400', bar: 'bg-amber-500', icon: PauseCircle },
  Verifying: { chip: 'text-cyan-400', bar: 'bg-cyan-500', icon: Loader2 },
  Completed: { chip: 'text-emerald-400', bar: 'bg-emerald-500', icon: CheckCircle },
  Failed: { chip: 'text-rose-400', bar: 'bg-rose-500', icon: AlertCircle },
  Cancelled: { chip: 'text-slate-400', bar: 'bg-slate-500', icon: AlertCircle },
};

export function TransferList({ transfers }: { transfers: Transfer[] }) {
  if (transfers.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center p-12 rounded-2xl bg-white/5 border border-white/10">
        <Activity className="w-8 h-8 text-indigo-400/50 mb-4" />
        <p className="text-slate-400 font-medium">No transfers yet</p>
        <p className="text-sm text-slate-500 mt-1">Send a file to a device above</p>
      </div>
    );
  }

  return (
    <section className="space-y-4">
      <h2 className="text-xl font-semibold font-outfit text-white">Transfers</h2>

      <div className="grid gap-4">
        {transfers.map((t) => {
          // Looked up by name. The previous UI indexed an array by the enum's
          // ordinal, so inserting a state silently relabelled every transfer.
          const tone = TONE[t.state] ?? TONE.Pending;
          const Icon = tone.icon;
          const percent = t.totalSize > 0 ? (t.transferredSize / t.totalSize) * 100 : 0;
          const spinning = t.state === 'Active' || t.state === 'Verifying';

          return (
            <article
              key={t.id}
              className="relative overflow-hidden bg-white/5 border border-white/10 p-5 rounded-2xl"
            >
              <div className="flex items-center justify-between gap-4">
                <div className="flex items-center gap-4 min-w-0">
                  <div className="p-3 rounded-xl bg-indigo-500/15 text-indigo-300 shrink-0">
                    <FileIcon className="w-6 h-6" />
                  </div>
                  <div className="min-w-0">
                    <p className="font-semibold text-slate-100 text-lg truncate">{t.fileName}</p>
                    <p className="text-xs text-slate-400 font-mono mt-1">
                      {formatBytes(t.transferredSize)} / {formatBytes(t.totalSize)}
                    </p>
                    {t.errorMessage && (
                      <p className="text-xs text-rose-400 mt-1">{t.errorMessage}</p>
                    )}
                  </div>
                </div>

                <div className="flex flex-col items-end gap-2 shrink-0">
                  <span className="flex items-center gap-2 px-3 py-1 rounded-full bg-black/40 border border-white/5">
                    <Icon
                      className={`w-3.5 h-3.5 ${tone.chip} ${spinning ? 'animate-spin' : ''}`}
                    />
                    <span className={`text-xs font-bold uppercase tracking-wide ${tone.chip}`}>
                      {t.state}
                    </span>
                  </span>
                  <span className="text-sm font-semibold text-slate-300">
                    {Math.round(percent)}%
                  </span>
                </div>
              </div>

              <div className="absolute bottom-0 left-0 h-1 w-full bg-slate-800/50">
                <div
                  className={`h-full transition-all duration-300 ease-out ${tone.bar}`}
                  style={{ width: `${percent}%` }}
                />
              </div>
            </article>
          );
        })}
      </div>
    </section>
  );
}
