import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { bootstrapService } from '@/services/bootstrapService';
import { useAuth } from '@/auth';

export function SetupRedirect() {
  const navigate = useNavigate();
  const { isLoading: authLoading } = useAuth();

  useEffect(() => {
    const checkSetup = async () => {
      if (authLoading) return;
      try {
        const status = await bootstrapService.status(true);
        if (status.tenantCount === 0) {
          navigate('/setup', { replace: true });
        }
      } catch {
        // Ignore errors and keep users on their current path
      }
    };

    checkSetup();
  }, [authLoading, navigate]);

  return null;
}
