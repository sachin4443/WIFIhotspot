import { Link } from 'react-router';
import { FiArrowLeft } from 'react-icons/fi';

export function NotFoundPage() {
  return (
    <div className="text-center py-5">
      <div className="display-4 fw-bold text-primary mb-2">404</div>
      <h1 className="h4 fw-semibold mb-2">Page not found</h1>
      <p className="text-body-secondary mb-4">The page you are looking for does not exist or has not been built yet.</p>
      <Link to="/" className="btn btn-primary d-inline-flex align-items-center gap-2">
        <FiArrowLeft aria-hidden="true" /> Back to dashboard
      </Link>
    </div>
  );
}
