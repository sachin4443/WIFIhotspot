import { useEffect, useState } from 'react';
import { Outlet, useLocation } from 'react-router';
import { useAuth } from '../../auth/useAuth';
import { useWorkspace } from '../../workspace/useWorkspace';
import { Sidebar } from './Sidebar';
import { Topbar } from './Topbar';
import { titleForPath } from './navigation';

/** Authenticated shell: light sidebar (drawer below 992px) + top bar + routed page. */
export function AppLayout() {
  const location = useLocation();
  const { user } = useAuth();
  const { organization } = useWorkspace();
  const [drawerOpen, setDrawerOpen] = useState(false);

  useEffect(() => setDrawerOpen(false), [location.pathname]);

  useEffect(() => {
    if (!drawerOpen) return;
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') setDrawerOpen(false); };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [drawerOpen]);

  const title = titleForPath(location.pathname);
  useEffect(() => { document.title = `${title} · NXT Hotspot`; }, [title]);

  const contextLabel = user?.isPlatformAdmin ? 'Workspace' : 'Organization';
  const contextValue = user?.isPlatformAdmin ? 'Platform administration' : organization?.name ?? 'Loading…';

  return (
    <div className="nxt-shell">
      <Sidebar open={drawerOpen} onClose={() => setDrawerOpen(false)} contextLabel={contextLabel} contextValue={contextValue} />
      {drawerOpen && <div className="nxt-backdrop d-lg-none" onClick={() => setDrawerOpen(false)} aria-hidden="true" />}
      <div className="nxt-main">
        <Topbar onOpenSidebar={() => setDrawerOpen(true)} title={title} />
        <main className="nxt-content" id="main-content">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
