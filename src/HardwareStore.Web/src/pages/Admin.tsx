import { useState, useEffect } from 'react';
import {
  adminGetUsers,
  adminGetPendingUsers,
  adminApproveUser,
  adminRejectUser,
  adminUpdateUserSettings,
  adminDeleteUser,
  adminGetRetailers,
  adminCreateRetailer,
  adminUpdateRetailer,
  adminDeleteRetailer,
} from '../api';
import type { UserDto, Retailer } from '../types';
import type { AxiosError } from 'axios';

type Tab = 'pending' | 'users' | 'retailers';

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString();
}

// ---- User Settings Modal ----
function UserSettingsModal({
  user,
  retailers,
  onClose,
  onSave,
}: {
  user: UserDto;
  retailers: Retailer[];
  onClose: () => void;
  onSave: (id: string, settings: { dailySearchLimit: number; allowedRetailerIds: string[]; status?: string }) => Promise<void>;
}) {
  const [limit, setLimit] = useState(user.dailySearchLimit);
  const [allowed, setAllowed] = useState<string[]>(user.allowedRetailerIds);
  const [status, setStatus] = useState(user.status);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');

  const toggleRetailer = (id: string) =>
    setAllowed((prev) => prev.includes(id) ? prev.filter((r) => r !== id) : [...prev, id]);

  const handleSave = async () => {
    setSaving(true);
    setError('');
    try {
      await onSave(user.id, { dailySearchLimit: limit, allowedRetailerIds: allowed, status });
      onClose();
    } catch (err) {
      const e = err as AxiosError<{ message: string }>;
      setError(e.response?.data?.message ?? 'Failed to save.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-2xl shadow-xl w-full max-w-md p-6 space-y-4">
        <div className="flex justify-between items-center">
          <h3 className="font-bold text-gray-900">{user.displayName}</h3>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 text-xl">×</button>
        </div>
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">
            Daily Search Limit
          </label>
          <input
            type="number"
            min={0}
            className="w-full border border-gray-300 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-orange-400"
            value={limit}
            onChange={(e) => setLimit(Number(e.target.value))}
          />
        </div>
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">Status</label>
          <select
            className="w-full border border-gray-300 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-orange-400"
            value={status}
            onChange={(e) => setStatus(e.target.value)}
          >
            <option>Active</option>
            <option>Suspended</option>
            <option>PendingApproval</option>
          </select>
        </div>
        {retailers.length > 0 && (
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Allowed Retailers
            </label>
            <div className="space-y-1">
              {retailers.map((r) => (
                <label key={r.id} className="flex items-center gap-2 text-sm cursor-pointer">
                  <input
                    type="checkbox"
                    checked={allowed.includes(r.id)}
                    onChange={() => toggleRetailer(r.id)}
                    className="h-4 w-4 text-orange-600 rounded"
                  />
                  {r.name}
                </label>
              ))}
            </div>
          </div>
        )}
        {error && <p className="text-sm text-red-600">{error}</p>}
        <div className="flex gap-2">
          <button
            onClick={onClose}
            className="flex-1 border border-gray-300 text-gray-700 font-semibold py-2.5 rounded-xl hover:bg-gray-50 transition text-sm"
          >
            Cancel
          </button>
          <button
            onClick={handleSave}
            disabled={saving}
            className="flex-1 bg-orange-600 text-white font-semibold py-2.5 rounded-xl hover:bg-orange-700 disabled:opacity-50 transition text-sm"
          >
            {saving ? 'Saving…' : 'Save'}
          </button>
        </div>
      </div>
    </div>
  );
}

// ---- Retailer Form Modal ----
function RetailerModal({
  initial,
  onClose,
  onSave,
}: {
  initial?: Retailer;
  onClose: () => void;
  onSave: (data: Omit<Retailer, 'id'>, id?: string) => Promise<void>;
}) {
  const [form, setForm] = useState<Omit<Retailer, 'id'>>({
    name: initial?.name ?? '',
    baseUrl: initial?.baseUrl ?? '',
    logoUrl: initial?.logoUrl ?? '',
    isEnabled: initial?.isEnabled ?? true,
    isAvailableToAll: initial?.isAvailableToAll ?? false,
    scraperType: initial?.scraperType ?? '',
  });
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');

  const handleSave = async () => {
    if (!form.name || !form.baseUrl) { setError('Name and base URL are required.'); return; }
    setSaving(true);
    setError('');
    try {
      await onSave(form, initial?.id);
      onClose();
    } catch (err) {
      const e = err as AxiosError<{ message: string }>;
      setError(e.response?.data?.message ?? 'Failed to save.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-2xl shadow-xl w-full max-w-md p-6 space-y-4">
        <div className="flex justify-between items-center">
          <h3 className="font-bold text-gray-900">{initial ? 'Edit Retailer' : 'Add Retailer'}</h3>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 text-xl">×</button>
        </div>
        {(['name', 'baseUrl', 'logoUrl', 'scraperType'] as const).map((field) => (
          <div key={field}>
            <label className="block text-sm font-medium text-gray-700 mb-1 capitalize">
              {field === 'baseUrl' ? 'Base URL' : field === 'logoUrl' ? 'Logo URL' : field === 'scraperType' ? 'Scraper Type' : 'Name'}
            </label>
            <input
              type="text"
              className="w-full border border-gray-300 rounded-xl px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-orange-400"
              value={form[field] ?? ''}
              onChange={(e) => setForm({ ...form, [field]: e.target.value })}
            />
          </div>
        ))}
        <div className="flex gap-4">
          <label className="flex items-center gap-2 text-sm cursor-pointer">
            <input
              type="checkbox"
              checked={form.isEnabled}
              onChange={(e) => setForm({ ...form, isEnabled: e.target.checked })}
              className="h-4 w-4 text-orange-600 rounded"
            />
            Enabled
          </label>
          <label className="flex items-center gap-2 text-sm cursor-pointer">
            <input
              type="checkbox"
              checked={form.isAvailableToAll}
              onChange={(e) => setForm({ ...form, isAvailableToAll: e.target.checked })}
              className="h-4 w-4 text-orange-600 rounded"
            />
            Available to All
          </label>
        </div>
        {error && <p className="text-sm text-red-600">{error}</p>}
        <div className="flex gap-2">
          <button
            onClick={onClose}
            className="flex-1 border border-gray-300 text-gray-700 font-semibold py-2.5 rounded-xl hover:bg-gray-50 transition text-sm"
          >
            Cancel
          </button>
          <button
            onClick={handleSave}
            disabled={saving}
            className="flex-1 bg-orange-600 text-white font-semibold py-2.5 rounded-xl hover:bg-orange-700 disabled:opacity-50 transition text-sm"
          >
            {saving ? 'Saving…' : 'Save'}
          </button>
        </div>
      </div>
    </div>
  );
}

// ---- Main Admin Page ----
export default function Admin() {
  const [tab, setTab] = useState<Tab>('pending');
  const [users, setUsers] = useState<UserDto[]>([]);
  const [pending, setPending] = useState<UserDto[]>([]);
  const [retailers, setRetailers] = useState<Retailer[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [editingUser, setEditingUser] = useState<UserDto | null>(null);
  const [editingRetailer, setEditingRetailer] = useState<Retailer | 'new' | null>(null);

  const loadAll = async () => {
    setLoading(true);
    try {
      const [u, p, r] = await Promise.all([
        adminGetUsers(),
        adminGetPendingUsers(),
        adminGetRetailers(),
      ]);
      setUsers(u.data);
      setPending(p.data);
      setRetailers(r.data);
    } catch {
      setError('Failed to load admin data.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { loadAll(); }, []);

  const handleApprove = async (id: string) => {
    await adminApproveUser(id);
    await loadAll();
  };

  const handleReject = async (id: string) => {
    await adminRejectUser(id);
    await loadAll();
  };

  const handleSaveUserSettings = async (
    id: string,
    settings: { dailySearchLimit: number; allowedRetailerIds: string[]; status?: string }
  ) => {
    await adminUpdateUserSettings(id, settings);
    await loadAll();
  };

  const handleDeleteUser = async (id: string) => {
    if (!confirm('Delete this user?')) return;
    await adminDeleteUser(id);
    await loadAll();
  };

  const handleSaveRetailer = async (data: Omit<Retailer, 'id'>, id?: string) => {
    if (id) await adminUpdateRetailer(id, data);
    else await adminCreateRetailer(data);
    await loadAll();
  };

  const handleDeleteRetailer = async (id: string) => {
    if (!confirm('Delete this retailer?')) return;
    await adminDeleteRetailer(id);
    await loadAll();
  };

  const statusBadge = (status: string) => {
    const map: Record<string, string> = {
      Active: 'bg-green-100 text-green-800',
      PendingApproval: 'bg-yellow-100 text-yellow-800',
      Suspended: 'bg-red-100 text-red-800',
    };
    return `inline-block text-xs font-semibold px-2 py-0.5 rounded-full ${map[status] ?? 'bg-gray-100 text-gray-700'}`;
  };

  const tabs: { key: Tab; label: string; count?: number }[] = [
    { key: 'pending', label: 'Pending', count: pending.length },
    { key: 'users', label: 'All Users', count: users.length },
    { key: 'retailers', label: 'Retailers', count: retailers.length },
  ];

  return (
    <div className="max-w-3xl mx-auto space-y-6">
      <h1 className="text-2xl font-bold text-gray-900">Admin Panel</h1>

      {error && (
        <p className="text-sm text-red-600 bg-red-50 border border-red-200 rounded-lg px-3 py-2">{error}</p>
      )}

      {/* Tabs */}
      <div className="flex gap-1 bg-gray-100 p-1 rounded-xl">
        {tabs.map((t) => (
          <button
            key={t.key}
            onClick={() => setTab(t.key)}
            className={`flex-1 py-2 text-sm font-semibold rounded-lg transition ${
              tab === t.key ? 'bg-white shadow text-orange-600' : 'text-gray-600 hover:text-gray-800'
            }`}
          >
            {t.label}
            {t.count !== undefined && t.count > 0 && (
              <span className="ml-1.5 text-xs bg-orange-100 text-orange-700 px-1.5 py-0.5 rounded-full">
                {t.count}
              </span>
            )}
          </button>
        ))}
      </div>

      {loading ? (
        <div className="flex justify-center py-10">
          <div className="w-8 h-8 border-4 border-orange-200 border-t-orange-600 rounded-full animate-spin" />
        </div>
      ) : (
        <>
          {/* Pending Tab */}
          {tab === 'pending' && (
            <div className="space-y-3">
              {pending.length === 0 && (
                <div className="bg-white rounded-2xl shadow-sm p-8 text-center text-gray-400">
                  No pending approvals 🎉
                </div>
              )}
              {pending.map((u) => (
                <div key={u.id} className="bg-white rounded-2xl shadow-sm border border-gray-100 p-5">
                  <div className="flex items-start justify-between gap-3 flex-wrap">
                    <div>
                      <p className="font-semibold text-gray-900">{u.displayName}</p>
                      <p className="text-sm text-gray-500">{u.email}</p>
                      <p className="text-xs text-gray-400">Registered {formatDate(u.createdAt)}</p>
                    </div>
                    <div className="flex gap-2">
                      <button
                        onClick={() => handleApprove(u.id)}
                        className="px-4 py-2 bg-green-600 text-white rounded-xl text-sm font-semibold hover:bg-green-700 transition"
                      >
                        Approve
                      </button>
                      <button
                        onClick={() => handleReject(u.id)}
                        className="px-4 py-2 bg-red-100 text-red-700 rounded-xl text-sm font-semibold hover:bg-red-200 transition"
                      >
                        Reject
                      </button>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}

          {/* All Users Tab */}
          {tab === 'users' && (
            <div className="space-y-3">
              {users.map((u) => (
                <div key={u.id} className="bg-white rounded-2xl shadow-sm border border-gray-100 p-5">
                  <div className="flex items-start justify-between gap-3 flex-wrap">
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2 flex-wrap">
                        <p className="font-semibold text-gray-900">{u.displayName}</p>
                        <span className={statusBadge(u.status)}>{u.status}</span>
                        {u.role === 'Admin' && (
                          <span className="text-xs bg-purple-100 text-purple-700 font-semibold px-2 py-0.5 rounded-full">
                            Admin
                          </span>
                        )}
                      </div>
                      <p className="text-sm text-gray-500">{u.email}</p>
                      <p className="text-xs text-gray-400">
                        {u.searchesUsedToday}/{u.dailySearchLimit} searches today · Joined {formatDate(u.createdAt)}
                      </p>
                    </div>
                    <div className="flex gap-2">
                      <button
                        onClick={() => setEditingUser(u)}
                        className="px-3 py-1.5 border border-gray-300 text-gray-700 rounded-xl text-xs font-semibold hover:bg-gray-50 transition"
                      >
                        Edit
                      </button>
                      <button
                        onClick={() => handleDeleteUser(u.id)}
                        className="px-3 py-1.5 bg-red-100 text-red-700 rounded-xl text-xs font-semibold hover:bg-red-200 transition"
                      >
                        Delete
                      </button>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}

          {/* Retailers Tab */}
          {tab === 'retailers' && (
            <div className="space-y-3">
              <div className="flex justify-end">
                <button
                  onClick={() => setEditingRetailer('new')}
                  className="px-4 py-2 bg-orange-600 text-white rounded-xl text-sm font-semibold hover:bg-orange-700 transition"
                >
                  + Add Retailer
                </button>
              </div>
              {retailers.map((r) => (
                <div key={r.id} className="bg-white rounded-2xl shadow-sm border border-gray-100 p-5">
                  <div className="flex items-start justify-between gap-3 flex-wrap">
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2">
                        <p className="font-semibold text-gray-900">{r.name}</p>
                        {r.isEnabled ? (
                          <span className="text-xs bg-green-100 text-green-700 font-semibold px-2 py-0.5 rounded-full">Enabled</span>
                        ) : (
                          <span className="text-xs bg-gray-100 text-gray-500 font-semibold px-2 py-0.5 rounded-full">Disabled</span>
                        )}
                        {r.isAvailableToAll && (
                          <span className="text-xs bg-blue-100 text-blue-700 font-semibold px-2 py-0.5 rounded-full">Public</span>
                        )}
                      </div>
                      <p className="text-sm text-gray-500 truncate">{r.baseUrl}</p>
                      {r.scraperType && (
                        <p className="text-xs text-gray-400">Scraper: {r.scraperType}</p>
                      )}
                    </div>
                    <div className="flex gap-2">
                      <button
                        onClick={() => setEditingRetailer(r)}
                        className="px-3 py-1.5 border border-gray-300 text-gray-700 rounded-xl text-xs font-semibold hover:bg-gray-50 transition"
                      >
                        Edit
                      </button>
                      <button
                        onClick={() => handleDeleteRetailer(r.id)}
                        className="px-3 py-1.5 bg-red-100 text-red-700 rounded-xl text-xs font-semibold hover:bg-red-200 transition"
                      >
                        Delete
                      </button>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </>
      )}

      {/* User settings modal */}
      {editingUser && (
        <UserSettingsModal
          user={editingUser}
          retailers={retailers}
          onClose={() => setEditingUser(null)}
          onSave={handleSaveUserSettings}
        />
      )}

      {/* Retailer modal */}
      {editingRetailer !== null && (
        <RetailerModal
          initial={editingRetailer === 'new' ? undefined : editingRetailer}
          onClose={() => setEditingRetailer(null)}
          onSave={handleSaveRetailer}
        />
      )}
    </div>
  );
}
