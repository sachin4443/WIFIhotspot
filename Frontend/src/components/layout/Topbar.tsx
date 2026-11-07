import { useEffect, useRef, useState } from 'react';
import { FiChevronDown, FiClock, FiLogOut, FiMenu } from 'react-icons/fi';
import { useAuth } from '../../auth/useAuth';
import { useNow } from '../../hooks/useNow';
import { formatDuration, initials } from '../../utils/format';

interface TopbarProps {
  onOpenSidebar: () => void;
  title: string;
}

export function Topbar({ onOpenSidebar, title }: TopbarProps) {
  const { user, session, logout } = useAuth();
  const [menuOpen, setMenuOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);
  const now = useNow(1000);

  useEffect(() => {
    if (!menuOpen) return;
    const close = (event: MouseEvent | KeyboardEvent) => {
      if (event instanceof KeyboardEvent ? event.key === 'Escape' : !menuRef.current?.contains(event.target as Node)) setMenuOpen(false);
    };
    document.addEventListener('mousedown', close);
    document.addEventListener('keydown', close);
    return () => {
      document.removeEventListener('mousedown', close);
      document.removeEventListener('keydown', close);
    };
  }, [menuOpen]);

  if (!user || !session) return null;

  const remaining = Math.max(0, session.expiresAt - now);
  const displayName = user.fullName || user.username;
  const roleLabel = user.roles.length > 0 ? user.roles.join(', ') : 'Administrator';

  return (
    <header className="nxt-topbar">
      <button type="button" className="nxt-icon-btn d-lg-none" onClick={onOpenSidebar} aria-label="Open navigation">
        <FiMenu />
      </button>
      <div className="nxt-topbar-title min-w-0 flex-grow-1">{title}</div>

      <span className={`nxt-session-badge ${remaining <= 5 * 60 * 1000 ? 'is-warning' : ''}`} title={`Session expires at ${new Date(session.expiresAt).toLocaleTimeString()}`}>
        <FiClock aria-hidden="true" />
        <span className="d-none d-md-inline">Session</span>
        <span>{formatDuration(remaining)}</span>
      </span>

      <div className="nxt-dropdown" ref={menuRef}>
        <button type="button" className="btn nxt-user-btn d-flex align-items-center gap-2" aria-expanded={menuOpen} aria-haspopup="menu" onClick={() => setMenuOpen((v) => !v)}>
          <span className="nxt-avatar" aria-hidden="true">{initials(displayName)}</span>
          <span className="d-none d-md-flex flex-column align-items-start lh-sm text-start">
            <span className="fw-semibold fs-sm text-truncate nxt-user-name">{displayName}</span>
            <span className="text-muted-2 nxt-user-role text-truncate">{roleLabel}</span>
          </span>
          <FiChevronDown className="text-muted-2 d-none d-sm-inline" aria-hidden="true" />
        </button>
        {menuOpen && (
          <div className="nxt-dropdown-menu" role="menu">
            <div className="px-2 py-2">
              <div className="fw-semibold fs-sm text-truncate">{displayName}</div>
              <div className="text-muted-2 fs-xs text-truncate">@{user.username} · {roleLabel}</div>
            </div>
            <hr className="my-1" />
            <button type="button" className="nxt-dropdown-item is-danger" role="menuitem" onClick={() => logout('manual')}>
              <FiLogOut aria-hidden="true" /> Sign out
            </button>
          </div>
        )}
      </div>
    </header>
  );
}
