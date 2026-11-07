import { useEffect, useState } from 'react';
import { Outlet, useLocation } from 'react-router';
import { Sidebar } from './Sidebar';
import { Topbar } from './Topbar';

const DESKTOP_QUERY = '(min-width: 992px)';

/** Authenticated application shell: sidebar + top bar + routed page. */
export function AppLayout() {
  const location = useLocation();
  // Desktop: sidebar can be collapsed. Mobile: sidebar is an overlay that starts closed.
  const [collapsed, setCollapsed] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);

  useEffect(() => setMobileOpen(false), [location.pathname]);

  const toggle = () => {
    if (window.matchMedia(DESKTOP_QUERY).matches) setCollapsed((v) => !v);
    else setMobileOpen((v) => !v);
  };

  return (
    <div className={`nxt-shell ${collapsed ? 'is-collapsed' : ''}`}>
      <Sidebar open={mobileOpen} onNavigate={() => setMobileOpen(false)} />
      {mobileOpen && <div className="nxt-backdrop" onClick={() => setMobileOpen(false)} aria-hidden="true" />}
      <div className="nxt-main">
        <Topbar onToggleSidebar={toggle} />
        <main className="nxt-content" id="main-content">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
