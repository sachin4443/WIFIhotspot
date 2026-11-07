import { useEffect, useRef, useState } from 'react';
import { FiChevronDown, FiClock, FiLogOut, FiMenu } from 'react-icons/fi';
import { useAuth } from '../../auth/useAuth';
import { useNow } from '../../hooks/useNow';
import { formatDuration, initials } from '../../utils/format';

interface TopbarProps {
  onToggleSidebar: () => void;
}

export function Topbar({ onToggleSidebar }: TopbarProps) {
  const { user, session, logout } = useAuth();
  const [menuOpen, setMenuOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);
  const now = useNow(1000);

  useEffect(() => {
    if (!menuOpen) return;
    const close = (event: MouseEvent | KeyboardEvent) => {
      if (event instanceof KeyboardEvent ? event.key === 'Escape' : !menuRef.current?.contains(event.target as Node)) {
        setMenuOpen(false);
      }
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
  const expiringSoon = remaining <= 5 * 60 * 1000;
  const displayName = user.fullName || user.username;
  const roleLabel = user.roles.length > 0 ? user.roles.join(', ') : user.isPlatformAdmin ? 'Platform admin' : 'Admin';

  return (
    <header className="nxt-topbar">
      <button type="button" className="btn nxt-icon-btn" onClick={onToggleSidebar} aria-label="Toggle navigation">
        <FiMenu />
      </button>

      <div className="d-none d-md-block text-body-secondary small">
        {user.isPlatformAdmin ? 'Platform administration' : 'Organization administration'}
      </div>

      <div className="ms-auto d-flex align-items-center gap-2 gap-sm-3">
        <span
          className={`badge rounded-pill d-inline-flex align-items-center gap-1 nxt-session-badge ${expiringSoon ? 'is-warning' : ''}`}
          title={`Session expires at ${new Date(session.expiresAt).toLocaleTimeString()}`}
        >
          <FiClock aria-hidden="true" />
          <span className="d-none d-sm-inline">Session</span> {formatDuration(remaining)}
        </span>

        <div className="dropdown" ref={menuRef}>
          <button
            type="button"
            className="btn nxt-user-btn d-flex align-items-center gap-2"
            aria-expanded={menuOpen}
            aria-haspopup="menu"
            onClick={() => setMenuOpen((v) => !v)}
          >
            <span className="nxt-avatar" aria-hidden="true">{initials(displayName)}</span>
            <span className="d-none d-sm-flex flex-column align-items-start lh-sm text-start">
              <span className="fw-semibold small text-truncate nxt-user-name">{displayName}</span>
              <span className="text-body-secondary nxt-user-role text-truncate">{roleLabel}</span>
            </span>
            <FiChevronDown className="text-body-secondary" aria-hidden="true" />
          </button>
          <div className={`dropdown-menu dropdown-menu-end shadow-sm ${menuOpen ? 'show' : ''}`} role="menu">
            <div className="px-3 py-2">
              <div className="fw-semibold small">{displayName}</div>
              <div className="text-body-secondary small">@{user.username}</div>
            </div>
            <div className="dropdown-divider" />
            <button type="button" className="dropdown-item d-flex align-items-center gap-2 text-danger" role="menuitem" onClick={() => logout('manual')}>
              <FiLogOut aria-hidden="true" /> Sign out
            </button>
          </div>
        </div>
      </div>
    </header>
  );
}
