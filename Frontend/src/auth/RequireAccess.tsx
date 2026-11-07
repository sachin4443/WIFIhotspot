import type { ReactNode } from 'react';
import { Link } from 'react-router';
import { FiLock } from 'react-icons/fi';
import { Card } from '../components/ui/Card';
import { EmptyState } from '../components/ui/States';
import { useAuth } from './useAuth';

interface Props {
  children: ReactNode;
  /** At least one of these permissions is required. */
  anyOf?: readonly string[];
  platformOnly?: boolean;
  organizationOnly?: boolean;
}

/** Route-level guard: renders an access-denied state instead of a page the user cannot use. The API enforces the same rules. */
export function RequireAccess({ children, anyOf = [], platformOnly, organizationOnly }: Props) {
  const { user, hasAnyPermission } = useAuth();
  const allowed = !!user
    && (!platformOnly || user.isPlatformAdmin)
    && (!organizationOnly || !user.isPlatformAdmin)
    && (anyOf.length === 0 || hasAnyPermission(anyOf));
  if (allowed) return <>{children}</>;
  return (
    <Card>
      <EmptyState icon={<FiLock />} title="You don’t have access to this page" text="Ask an administrator to grant the required role, then sign in again." action={<Link to="/" className="btn btn-primary btn-sm">Back to dashboard</Link>} />
    </Card>
  );
}
