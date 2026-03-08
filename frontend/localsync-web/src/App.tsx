import { QueryClient, QueryClientProvider, useQuery } from '@tanstack/react-query';
import { 
  Network, Laptop, Globe, RefreshCcw, UploadCloud, 
  File as FileIcon, CheckCircle, AlertCircle, PlayCircle, Loader2,
  FolderSync, Activity, Server, Settings, Zap
} from 'lucide-react';
import { useState, useEffect } from 'react';
import { HubConnectionBuilder } from '@microsoft/signalr';

const queryClient = new QueryClient();

interface Device {
  id: string;
  name: string;
  ipAddress: string;
  port: number;
  isOnline: boolean;
  lastSeen: string;
}

const CHUNK_SIZE = 1024 * 1024; // 1MB chunks
const TRANSFER_STATES = ['Pending', 'Active', 'Paused', 'Completed', 'Failed', 'Cancelled'];

function TransferDashboard() {
  const [sessions, setSessions] = useState<any[]>([]);

  useEffect(() => {
    fetch('/api/transfer/sessions').then(r => r.json()).then(setSessions);

    const connection = new HubConnectionBuilder()
      .withUrl('/transferHub')
      .withAutomaticReconnect()
      .build();

    connection.on('SessionCreated', (session) => {
      setSessions(prev => [session, ...prev]);
    });

    connection.on('ProgressUpdated', (sessionId, transferred) => {
      setSessions(prev => prev.map(s => s.id === sessionId ? { ...s, transferredSize: transferred } : s));
    });

    connection.on('StateChanged', (sessionId, state) => {
      setSessions(prev => prev.map(s => s.id === sessionId ? { ...s, state } : s));
    });

    connection.start().catch(err => console.error('SignalR error:', err));

    return () => {
      connection.stop();
    };
  }, []);

  if (sessions.length === 0) return (
    <div className="flex flex-col items-center justify-center p-12 mt-8 rounded-2xl bg-white/5 border border-white/10 backdrop-blur-md">
      <div className="p-4 bg-indigo-500/10 rounded-full mb-4">
        <Activity className="w-8 h-8 text-indigo-400 opacity-50" />
      </div>
      <p className="text-slate-400 font-medium">No active transfers</p>
      <p className="text-sm text-slate-500 mt-1">Select a file to send to a device</p>
    </div>
  );

  return (
    <div className="mt-8 space-y-4">
      <div className="flex items-center gap-2 mb-6">
        <Zap className="w-5 h-5 text-indigo-400" />
        <h2 className="text-xl font-semibold font-outfit text-white">Live Transfers</h2>
      </div>
      
      <div className="grid gap-4">
        {sessions.map(s => {
          const progress = s.totalSize > 0 ? (s.transferredSize / s.totalSize) * 100 : 0;
          const stateName = TRANSFER_STATES[s.state];
          const isCompleted = s.state === 3;
          const isFailed = s.state === 4;
          const isActive = s.state === 1;

          return (
            <div key={s.id} className="relative overflow-hidden bg-white/5 backdrop-blur-xl border border-white/10 p-5 rounded-2xl transition-all duration-300 hover:bg-white/10 group">
              <div className="flex items-center justify-between z-10 relative">
                <div className="flex items-center gap-4">
                  <div className={`p-3 rounded-xl flex items-center justify-center transition-colors ${isCompleted ? 'bg-emerald-500/20 text-emerald-400' : isFailed ? 'bg-rose-500/20 text-rose-400' : 'bg-indigo-500/20 text-indigo-400'}`}>
                    <FileIcon className="w-6 h-6" />
                  </div>
                  <div>
                    <p className="font-semibold text-slate-100 text-lg tracking-tight">{s.fileName}</p>
                    <div className="flex items-center gap-2 mt-1">
                      <span className="text-xs font-medium px-2 py-0.5 rounded-full bg-white/10 text-slate-300">
                        To: {s.targetDeviceId.split('-')[0]}
                      </span>
                      <span className="text-xs text-slate-400 font-mono">
                        {(s.transferredSize / 1024 / 1024).toFixed(1)} / {(s.totalSize / 1024 / 1024).toFixed(1)} MB
                      </span>
                    </div>
                  </div>
                </div>
                
                <div className="flex flex-col items-end gap-2">
                  <div className="flex items-center gap-2 px-3 py-1 rounded-full bg-black/40 border border-white/5">
                    {isCompleted ? <CheckCircle className="w-3.5 h-3.5 text-emerald-400" /> : 
                     isFailed ? <AlertCircle className="w-3.5 h-3.5 text-rose-400" /> : 
                     isActive ? <Loader2 className="w-3.5 h-3.5 text-indigo-400 animate-spin" /> :
                     <PlayCircle className="w-3.5 h-3.5 text-slate-400" />}
                    <span className={`text-xs font-bold tracking-wide uppercase ${isCompleted ? 'text-emerald-400' : isFailed ? 'text-rose-400' : isActive ? 'text-indigo-400' : 'text-slate-400'}`}>
                      {stateName}
                    </span>
                  </div>
                  <span className="text-sm font-semibold text-slate-300">{Math.round(progress)}%</span>
                </div>
              </div>
              
              {/* Progress Bar Background */}
              <div className="absolute bottom-0 left-0 h-1 w-full bg-slate-800/50">
                <div 
                  className={`h-full transition-all duration-500 ease-out ${isCompleted ? 'bg-emerald-500' : isFailed ? 'bg-rose-500' : 'bg-indigo-500'}`} 
                  style={{ width: `${progress}%` }}
                />
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}

function DeviceList() {
  const { data: devices, isLoading, isError, refetch, isFetching } = useQuery<Device[]>({
    queryKey: ['devices'],
    queryFn: async () => {
      const res = await fetch('/api/devices');
      if (!res.ok) throw new Error('Network response was not ok');
      return res.json();
    },
    refetchInterval: 5000,
  });

  const uploadFile = async (device: Device, file: File) => {
    try {
      const sessionRes = await fetch('/api/transfer/session', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ targetDeviceId: device.id, fileName: file.name, totalSize: file.size })
      });
      const session = await sessionRes.json();

      let offset = 0;
      while (offset < file.size) {
        const chunk = file.slice(offset, offset + CHUNK_SIZE);
        const isLastChunk = offset + CHUNK_SIZE >= file.size;

        const formData = new FormData();
        formData.append('sessionId', session.id);
        formData.append('offset', offset.toString());
        formData.append('isLastChunk', isLastChunk.toString());
        formData.append('chunk', chunk);

        await fetch('/api/transfer/chunk', {
          method: 'POST',
          body: formData
        });

        offset += chunk.size;
      }
    } catch (err) {
      console.error("Upload failed", err);
    }
  };

  if (isLoading) return (
    <div className="flex flex-col items-center justify-center p-16 space-y-4">
      <Loader2 className="w-10 h-10 text-indigo-500 animate-spin" />
      <p className="text-slate-400 font-medium animate-pulse">Scanning the local network...</p>
    </div>
  );

  if (isError) return (
    <div className="p-6 bg-rose-500/10 border border-rose-500/20 rounded-2xl flex items-center gap-4 text-rose-400">
      <AlertCircle className="w-8 h-8" />
      <div>
        <h3 className="font-semibold text-lg">Connection Failed</h3>
        <p className="text-sm opacity-80">Could not reach the LocalSync background service.</p>
      </div>
    </div>
  );

  return (
    <div className="space-y-6">
      <div className="flex justify-between items-end">
        <div>
          <h2 className="text-2xl font-bold font-outfit text-white flex items-center gap-3 mb-1">
            <Network className="w-6 h-6 text-indigo-400" />
            Network Radar
          </h2>
          <p className="text-sm text-slate-400">Discovering devices on your LAN</p>
        </div>
        <button 
          onClick={() => refetch()} 
          className="p-3 bg-white/5 hover:bg-white/10 border border-white/10 rounded-xl transition-all hover:scale-105 active:scale-95 group"
        >
          <RefreshCcw className={`w-5 h-5 text-slate-300 group-hover:text-white ${isFetching ? 'animate-spin text-indigo-400' : ''}`} />
        </button>
      </div>

      {!devices || devices.length === 0 ? (
        <div className="flex flex-col items-center justify-center p-16 border border-white/10 rounded-3xl bg-gradient-to-b from-white/5 to-transparent">
          <div className="relative">
            <div className="absolute inset-0 bg-indigo-500/20 blur-xl rounded-full animate-pulse" />
            <Globe className="w-16 h-16 text-slate-500 relative z-10 mb-6" />
          </div>
          <p className="text-lg font-medium text-slate-300">Space looks empty...</p>
          <p className="text-sm text-slate-500 mt-2 text-center max-w-sm">No other LocalSync instances found. Make sure they are running on the same Wi-Fi network.</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-5">
          {devices.map(device => (
            <div key={device.id} className="group relative bg-white/5 hover:bg-white/10 border border-white/10 p-6 rounded-3xl transition-all duration-300 hover:-translate-y-1 hover:shadow-2xl hover:shadow-indigo-500/10 overflow-hidden">
              <div className="absolute top-0 right-0 p-6 opacity-10 group-hover:opacity-20 transition-opacity">
                <Laptop className="w-24 h-24" />
              </div>
              
              <div className="relative z-10">
                <div className="flex justify-between items-start mb-6">
                  <div className={`p-3 rounded-2xl inline-flex items-center justify-center backdrop-blur-md border ${device.isOnline ? 'bg-emerald-500/10 border-emerald-500/20 text-emerald-400' : 'bg-rose-500/10 border-rose-500/20 text-rose-400'}`}>
                    <Server className="w-6 h-6" />
                  </div>
                  <div className="flex items-center gap-2 bg-black/40 px-3 py-1.5 rounded-full border border-white/5">
                    <span className={`w-2 h-2 rounded-full ${device.isOnline ? 'bg-emerald-500 shadow-[0_0_8px_rgba(16,185,129,0.8)]' : 'bg-rose-500'}`}></span>
                    <span className="text-xs font-semibold uppercase tracking-wider text-slate-300">{device.isOnline ? 'Online' : 'Offline'}</span>
                  </div>
                </div>
                
                <h3 className="font-bold text-xl text-white font-outfit mb-1">{device.name}</h3>
                <p className="text-sm text-slate-400 font-mono mb-6">{device.ipAddress}:{device.port}</p>
                
                <label className="w-full py-3 px-4 bg-indigo-600 hover:bg-indigo-500 shadow-lg shadow-indigo-500/25 text-white rounded-xl text-sm font-semibold transition-all cursor-pointer flex items-center justify-center gap-2 group-hover:shadow-indigo-500/40">
                  <UploadCloud className="w-4 h-4 transition-transform group-hover:-translate-y-0.5" />
                  Beam File
                  <input 
                    type="file" 
                    className="hidden" 
                    onChange={(e) => {
                      if (e.target.files && e.target.files[0]) {
                        uploadFile(device, e.target.files[0]);
                      }
                    }} 
                  />
                </label>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <div className="min-h-screen bg-[#0B0F19] text-slate-200 font-inter selection:bg-indigo-500/30 overflow-x-hidden">
        {/* Background Gradients */}
        <div className="fixed inset-0 pointer-events-none">
          <div className="absolute top-[-20%] left-[-10%] w-[50%] h-[50%] bg-indigo-600/20 blur-[120px] rounded-full" />
          <div className="absolute bottom-[-20%] right-[-10%] w-[50%] h-[50%] bg-cyan-600/10 blur-[120px] rounded-full" />
        </div>

        {/* Navbar */}
        <nav className="sticky top-0 z-50 border-b border-white/10 bg-[#0B0F19]/80 backdrop-blur-xl">
          <div className="max-w-7xl mx-auto px-6 h-20 flex items-center justify-between">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-indigo-500 to-cyan-400 flex items-center justify-center shadow-lg shadow-indigo-500/20">
                <FolderSync className="w-6 h-6 text-white" />
              </div>
              <h1 className="text-2xl font-extrabold font-outfit tracking-tight text-transparent bg-clip-text bg-gradient-to-r from-white to-slate-400">
                LocalSync
              </h1>
            </div>
            <div className="flex items-center gap-4">
              <button className="p-2.5 text-slate-400 hover:text-white hover:bg-white/5 rounded-xl transition-colors">
                <Settings className="w-5 h-5" />
              </button>
            </div>
          </div>
        </nav>

        <main className="relative max-w-7xl mx-auto px-6 py-12 space-y-16">
          <DeviceList />
          <div className="h-px w-full bg-gradient-to-r from-transparent via-white/10 to-transparent" />
          <TransferDashboard />
        </main>
      </div>
    </QueryClientProvider>
  );
}

export default App;
