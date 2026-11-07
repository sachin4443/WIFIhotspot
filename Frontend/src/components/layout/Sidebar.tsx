import { NavLink } from 'react-router';
import { FiX } from 'react-icons/fi';
import { useAuth } from '../../auth/useAuth';
import { BrandMark } from '../common/BrandMark';
import { visibleSections } from './navigation';

interface SidebarProps {
  open: boolean;
  onClose: () => void;
  contextLabel: string;
  contextValue: string;
}

export function Sidebar({ open, onClose, contextLabel, contextValue }: SidebarProps) {
  const { user, hasAnyPermission } = useAuth();
  if (!user) return null;
  const sections = visibleSections(user.isPlatformAdmin, hasAnyPermission);

  return (
    <aside className={`nxt-sidebar ${open ? 'is-open' : ''}`} aria-label="Main navigation">
      <div className="nxt-sidebar-brand">
        <BrandMark size={34} />
        <div className="lh-sm min-w-0">
          <div className="nxt-sidebar-brand-name">NXT Hotspot</div>
          <div className="nxt-sidebar-brand-sub">{user.isPlatformAdmin ? 'Platform console' : 'Admin console'}</div>
        </div>
        <button type="button" className="nxt-icon-btn nxt-sidebar-close" onClick={onClose} aria-label="Close navigation">
          <FiX />
        </button>
      </div>

      <div className="nxt-sidebar-context">
        <div className="nxt-sidebar-context-label">{contextLabel}</div>
        <div className="nxt-sidebar-context-value" title={contextValue}>{contextValue}</div>
      </div>

      <nav className="nxt-sidebar-nav">
        {sections.map((section) => (
          <div key={section.title} className="nxt-nav-section">
            <div className="nxt-nav-section-title">{section.title}</div>
            <ul className="list-unstyled mb-0 d-grid gap-1">
              {section.items.map((item) => {
                const Icon = item.icon;
                return (
                  <li key={item.path}>
                    <NavLink to={item.path} end={item.path === '/'} className={({ isActive }) => `nxt-nav-link ${isActive ? 'is-active' : ''}`} onClick={onClose}>
                      <Icon className="nxt-nav-icon" aria-hidden="true" />
                      <span className="text-truncate">{item.label}</span>
                    </NavLink>
                  </li>
                );
              })}
            </ul>
          </div>
        ))}
      </nav>

      <div className="nxt-sidebar-footer">NXT Hotspot · RADIUS &amp; hotspot management</div>
    </aside>
  );
}
