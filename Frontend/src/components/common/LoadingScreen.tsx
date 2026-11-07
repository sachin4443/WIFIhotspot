/** Full-page loading state used while the session is being restored. */
export function LoadingScreen({ message = 'Loading…' }: { message?: string }) {
  return (
    <div className="d-flex flex-column align-items-center justify-content-center min-vh-100 gap-3 text-body-secondary" role="status">
      <div className="spinner-border text-primary" aria-hidden="true" />
      <span>{message}</span>
    </div>
  );
}
