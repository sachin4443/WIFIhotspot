import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import '@fontsource-variable/inter';
import 'bootstrap/dist/css/bootstrap.min.css';
import './styles/app.css';
import { App } from './App';
import { AuthProvider } from './auth/AuthProvider';
import { ConfirmProvider } from './components/ui/ConfirmProvider';
import { ToastProvider } from './toast/ToastProvider';
import { WorkspaceProvider } from './workspace/WorkspaceProvider';

const container = document.getElementById('root');
if (!container) throw new Error('Root element #root not found.');

createRoot(container).render(
  <StrictMode>
    <ToastProvider>
      <AuthProvider>
        <WorkspaceProvider>
          <ConfirmProvider>
            <App />
          </ConfirmProvider>
        </WorkspaceProvider>
      </AuthProvider>
    </ToastProvider>
  </StrictMode>,
);
