import { NavLink } from 'react-router';
import { useAuth } from '../../auth/useAuth';
import { BrandMark } from '../common/BrandMark';
import { BUILT_MODULE, NAV_SECTIONS } from './navigation';

interface SidebarProps {
  open: boolean;
  onNavigate: () => void;
}

export function Sidebar({ open, onNavigate }: SidebarProps) {
  const { hasAnyPermission, user } = useAuth();

  const sections = NAV_SECTIONS.map((section) => ({
    ...section,
    items: section.items.filter((item) => item.anyOf.length === 0 || hasAnyPermission(item.anyOf)),
  })).filter((section) => section.items.length > 0);

  return (
    <aside className={`nxt-sidebar ${open ? 'is-open' : ''}`} aria-label="Main navigation">
      <div className="nxt-sidebar-brand">
        <BrandMark />
        <div className="lh-sm">
          <div className="fw-bold text-white">NXT Hotspot</div>
          <div className="nxt-sidebar-subtitle">{user?.isPlatformAdmin ? 'Platform console' : 'Organization console'}</div>
        </div>
      </div>

      <nav className="nxt-sidebar-nav">
        {sections.map((section) => (
          <div key={section.title} className="nxt-nav-section">
            <div className="nxt-nav-section-title">{section.title}</div>
            <ul className="list-unstyled mb-0">
              {section.items.map((item) => {
                const Icon = item.icon;
                if (item.module > BUILT_MODULE) {
                  return (
                    <li key={item.path}>
                      <span className="nxt-nav-link is-disabled" aria-disabled="true" title={`Arrives in Phase 3 Module ${item.module}`}>
                        <Icon className="nxt-nav-icon" aria-hidden="true" />
                        <span className="flex-grow-1">{item.label}</span>
                        <span className="nxt-nav-soon">Soon</span>
                      </span>
                    </li>
                  );
                }
                return (
                  <li key={item.path}>
                    <NavLink
                      to={item.path}
                      end={item.path === '/'}
                      className={({ isActive }) => `nxt-nav-link ${isActive ? 'is-active' : ''}`}
                      onClick={onNavigate}
                    >
                      <Icon className="nxt-nav-icon" aria-hidden="true" />
                      <span className="flex-grow-1">{item.label}</span>
                    </NavLink>
                  </li>
                );
              })}
            </ul>
          </div>
        ))}
      </nav>

      <div className="nxt-sidebar-footer">RADIUS · AAA · Captive portal</div>
    </aside>
  );
}
